using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Features.ProxyCharacters.Data;
using _Project.Features.ProxyCharacters.Scripts;
using _Project.Features.LobbySystem.Structs; // unfortunate dependency between features but acceptable

namespace _Project.Features.LobbySystem.Scripts
{
    public class MatchNetworkController : NetworkBehaviour
    {
        public static MatchNetworkController Singleton { get; private set; }

        [Header("State")]
        public NetworkVariable<MatchState> CurrentState = new NetworkVariable<MatchState>(MatchState.Lobby);
        public NetworkVariable<float> CharacterSelectTimer = new NetworkVariable<float>(180f); // 3 minutes

        [Header("Databases")]
        [Tooltip("Assign all available Proxy Characters here so we can look them up by Index")]
        public ProxyCharacterData[] CharacterDatabase;

        [Header("Prefabs")]
        [Tooltip("The raw generic Player Capsule with NO stats or weapons yet")]
        public GameObject PlayerCapsulePrefab;

        private void Awake()
        {
            if (Singleton != null && Singleton != this) Destroy(gameObject);
            else Singleton = this;
            
            DontDestroyOnLoad(gameObject); // This must survive scene transitions
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnectedServerOnly;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnectedServerOnly;
            }
        }

        /// <summary>
        /// Server-side gate: once we've left the Lobby state (Host clicked Start), reject any
        /// further joins by immediately disconnecting them with a reason. The client side picks
        /// this up via NetworkSessionWatcher and shows the "Match in progress" modal.
        /// </summary>
        private void HandleClientConnectedServerOnly(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId) return; // host itself
            if (CurrentState.Value == MatchState.Lobby) return;    // join allowed pre-start
            Debug.Log($"[MatchController] Rejecting late-join client {clientId} (state={CurrentState.Value}).");
            NetworkManager.Singleton.DisconnectClient(clientId, "Match already in progress.");
        }

        private void Update()
        {
            if (!IsServer) return;

            if (CurrentState.Value == MatchState.CharacterSelect)
            {
                CharacterSelectTimer.Value -= Time.deltaTime;

                if (CharacterSelectTimer.Value <= 0f || AreAllPlayersLockedIn())
                {
                    StartLoadingStage();
                }
            }
        }

        // Called by the UI Controller when the Host clicks "Start"
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void StartCharacterSelectRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            
            CurrentState.Value = MatchState.CharacterSelect;
            CharacterSelectTimer.Value = 180f; // Reset to 3 minutes
        }

        // Called by UI Controller when a client locks in
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void LockInCharacterRpc(int characterIndex, RpcParams rpcParams = default)
        {
            if (CurrentState.Value != MatchState.CharacterSelect) return;

            ulong clientId = rpcParams.Receive.SenderClientId;
            LobbyNetworkController.Singleton.UpdatePlayerLockIn(clientId, characterIndex, true);

            // Snap the player's lobby team to whichever team the picked character belongs to.
            // Why: Singleplayer/Coop default-seeds team=Cleansers before character pick, so a
            // Thrive lock-in would otherwise be overridden by the team-mismatch fallback in
            // SpawnPlayersForScene and the player would silently spawn as Ensign instead.
            if (CharacterDatabase != null && characterIndex >= 0 && characterIndex < CharacterDatabase.Length)
            {
                var picked = CharacterDatabase[characterIndex];
                if (picked != null && picked.teamDefinition != null)
                {
                    LobbyNetworkController.Singleton.SetPlayerTeam(clientId, picked.teamDefinition.teamAffiliation);
                }
            }
        }

        private bool AreAllPlayersLockedIn()
        {
            var players = LobbyNetworkController.Singleton.LobbyPlayers;
            
            if (players.Count == 0) return false;

            foreach (var p in players)
            {
                if (!p.IsLockedIn) return false;
            }
            return true;
        }

        private void StartLoadingStage()
        {
            CurrentState.Value = MatchState.LoadingMap;

            var mode = LobbyNetworkController.Singleton.CurrentSettings.Value.Mode;

            var runManager = GetComponent<IRunManager>();
            if (runManager == null)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb is IRunManager rm) { runManager = rm; break; }
                }
            }
            if (runManager == null)
            {
                Debug.LogError("[MatchController] No IRunManager found in the scene! Cannot load maps.");
                return;
            }

            runManager.GenerateNewLoopItinerary(mode);

            // Single itinerary: both PvP teams play the same scene at the same stage index.
            // Each scene is authored with two physically-separated TeamRegionRoots; the
            // unused team's region is disabled at runtime when only one team is in the scene.
            string firstMap = runManager.GetCurrentMapForTeam(TeamAffiliation.Cleansers);
            NetworkManager.Singleton.SceneManager.LoadScene(firstMap, LoadSceneMode.Additive);
        }

        // Triggers once the server confirms EVERY client has successfully loaded the new scene
        // Triggers once the server confirms EVERY client has successfully loaded a specific scene
        private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (!IsServer || sceneName == "MainMenu") return;

            CurrentState.Value = MatchState.InProgress;

            // SpawnPlayersForScene is invoked by RunNetworkController.HandleSceneLoaded
            // AFTER it has registered any per-team world-offset and translated the scene.
            // That ordering matters: spawn points must already be at their offset world
            // positions before players are placed. So we do NOT spawn here.

            UnloadMainMenuClientRpc();
        }

        [ClientRpc]
        private void UnloadMainMenuClientRpc()
        {
            Scene mainMenu = UnityEngine.SceneManagement.SceneManager.GetSceneByName("MainMenu");
            if (mainMenu.isLoaded)
            {
                UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(mainMenu);
                Debug.Log("[MatchController] MainMenu unloaded.");
            }
        }

        public void SpawnPlayersForScene(string sceneName, TeamAffiliation targetTeam = TeamAffiliation.None, bool forceRespawn = false)
        {
            var runManager = GetComponent<IRunManager>();
            if (runManager == null)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb is IRunManager rm) { runManager = rm; break; }
                }
            }
            if (runManager == null) return;

            // 1. Get the actual Scene object that just finished loading
            Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            if (!targetScene.isLoaded) return;

            // 2. Identify which players belong in THIS specific scene
            var playersInScene = new List<LobbyPlayerState>();
            foreach (var player in LobbyNetworkController.Singleton.LobbyPlayers)
            {
                // Assign a fallback team logic here, just to guarantee spawn if they somehow skip team select.
                var effectiveTeam = player.Team == _Project.Core.Enums.TeamAffiliation.None ? _Project.Core.Enums.TeamAffiliation.Cleansers : player.Team;

                // If we specified a target team (e.g. PvP final stage merge), skip everyone else
                if (targetTeam != TeamAffiliation.None && effectiveTeam != targetTeam) continue;

                string targetMap = runManager.GetCurrentMapForTeam(effectiveTeam);
                if (targetMap == sceneName)
                {
                    // CRITICAL: If the player is ALREADY in this specific scene (e.g. the other team got to the final stage first), do NOT teleport them!
                    if (!forceRespawn && NetworkManager.Singleton.ConnectedClients.TryGetValue(player.ClientId, out var client) && client.PlayerObject != null)
                    {
                        if (client.PlayerObject.gameObject.scene.name == sceneName) continue;
                    }

                    playersInScene.Add(player);
                }
            }

            if (playersInScene.Count == 0) return; // No players belong here, do nothing

            // 3. Scan the new Scene's Root GameObjects to find all Spawn Points by Tag
            Dictionary<string, List<Transform>> sceneSpawnPoints = new Dictionary<string, List<Transform>>();
            Dictionary<string, int> spawnPointIndices = new Dictionary<string, int>();

            foreach (var rootObj in targetScene.GetRootGameObjects())
            {
                foreach (Transform t in rootObj.GetComponentsInChildren<Transform>(true))
                {
                    if (!string.IsNullOrEmpty(t.tag) && t.tag != "Untagged")
                    {
                        if (!sceneSpawnPoints.ContainsKey(t.tag))
                        {
                            sceneSpawnPoints[t.tag] = new List<Transform>();
                            spawnPointIndices[t.tag] = 0; 
                        }
                        sceneSpawnPoints[t.tag].Add(t);
                    }
                }
            }

            // 4. Iterate through the designated players and spawn/teleport them
            foreach (var playerData in playersInScene)
            {
                int index = playerData.IsLockedIn ? playerData.CharacterIndex : 0;
                ProxyCharacterData selectedCharacter = CharacterDatabase[index];

                // If the (possibly default index-0) character's team doesn't match the
                // player's actual team, find a team-appropriate fallback. Otherwise a
                // Thrive player who never locked in tries to spawn at SpawnPoint_Alpha
                // and falls into the void (Thrive scenes only contain SpawnPoint_Beta).
                var spawnEffectiveTeamForChar = playerData.Team == TeamAffiliation.None ? TeamAffiliation.Cleansers : playerData.Team;
                if (selectedCharacter == null || selectedCharacter.teamDefinition == null ||
                    selectedCharacter.teamDefinition.teamAffiliation != spawnEffectiveTeamForChar)
                {
                    for (int ci = 0; ci < CharacterDatabase.Length; ci++)
                    {
                        var candidate = CharacterDatabase[ci];
                        if (candidate != null && candidate.teamDefinition != null &&
                            candidate.teamDefinition.teamAffiliation == spawnEffectiveTeamForChar)
                        {
                            selectedCharacter = candidate;
                            break;
                        }
                    }
                }

                Vector3 spawnPos = Vector3.up * 2f;
                Quaternion spawnRot = Quaternion.identity;
                string targetTag = selectedCharacter != null && selectedCharacter.teamDefinition != null
                    ? selectedCharacter.teamDefinition.spawnPointTag
                    : "Untagged";

                Debug.Log($"[spawn] client={playerData.ClientId} lobbyTeam={playerData.Team} " +
                          $"locked={playerData.IsLockedIn} charIdx={playerData.CharacterIndex} " +
                          $"resolvedChar='{(selectedCharacter != null ? selectedCharacter.proxyName : "<null>")}' " +
                          $"resolvedTeamDef='{(selectedCharacter != null && selectedCharacter.teamDefinition != null ? selectedCharacter.teamDefinition.teamName : "<null>")}' " +
                          $"targetTag='{targetTag}' scene='{sceneName}'");

                if (sceneSpawnPoints.ContainsKey(targetTag) && sceneSpawnPoints[targetTag].Count > 0)
                {
                    var points = sceneSpawnPoints[targetTag];
                    int currentSpawnIndex = spawnPointIndices[targetTag];

                    Transform spawnTransform = points[currentSpawnIndex % points.Count];
                    spawnPos = spawnTransform.position;
                    spawnRot = spawnTransform.rotation;

                    // If multiple players share the same tagged spawn point (common in Coop
                    // where there's typically only one SpawnPoint_Alpha), scatter subsequent
                    // spawns in a small ring so overlapping CharacterControllers don't shove
                    // each other through the floor on spawn.
                    int seatInsideThisPoint = currentSpawnIndex / points.Count;
                    if (seatInsideThisPoint > 0)
                    {
                        const float ringRadius = 1.5f;
                        float angle = seatInsideThisPoint * (Mathf.PI * 2f / 6f);
                        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
                        spawnPos += offset;
                    }

                    // Lift 1m above the spawn point so the CharacterController has a moment
                    // of airtime to settle onto the floor cleanly instead of immediately
                    // clipping into it. Combined with PlayerMovement.Teleport zeroing velocity.
                    spawnPos += Vector3.up * 1f;

                    spawnPointIndices[targetTag]++;
                }
                else
                {
                    Debug.LogWarning($"[MatchController] No spawn points tagged '{targetTag}' found in '{sceneName}'. Using default (0,2,0).");
                }

                // --- THE CRITICAL FIX: CHECK FOR EXISTING PLAYER ---
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerData.ClientId, out var client) && client.PlayerObject != null)
                {
                    // PLAYER EXISTS (Stage Transition)
                    NetworkObject existingPlayerObj = client.PlayerObject;
                    
                    // 1. Teleport them to the new spawn point via the Networked PlayerMovement
                    if (existingPlayerObj.TryGetComponent<_Project.Features.Player.Scripts.PlayerMovement>(out var pm))
                    {
                        pm.Teleport(spawnPos, spawnRot);
                    }
                    else
                    {
                        var cc = existingPlayerObj.GetComponent<CharacterController>();
                        if (cc != null) cc.enabled = false;
                        
                        existingPlayerObj.transform.position = spawnPos;
                        existingPlayerObj.transform.rotation = spawnRot;
                        
                        if (cc != null) cc.enabled = true;
                    }
                    
                    // 2. Move their persistent body into the new scene
                    DontDestroyOnLoad(existingPlayerObj.gameObject);
                    
                    Debug.Log($"[MatchController] Teleported existing player {playerData.ClientId} to new stage.");
                }
                else
                {
                    // PLAYER DOES NOT EXIST (Start of Run)
                    GameObject playerInstance = Instantiate(PlayerCapsulePrefab, spawnPos, spawnRot);

                    // CRUCIAL: Place the player object into DontDestroyOnLoad so it survives scene unloads natively across the network
                    DontDestroyOnLoad(playerInstance);

                    if (playerInstance.TryGetComponent<NetworkObject>(out var netObj))
                    {
                        netObj.DestroyWithScene = false;

                        var spawnEffectiveTeam = playerData.Team == TeamAffiliation.None ? TeamAffiliation.Cleansers : playerData.Team;

                        // Set Team + phase BEFORE Spawn so the synced initial NetworkVariable values are correct.
                        if (playerInstance.TryGetComponent<_Project.Core.Stats.CharacterStats>(out var stats))
                        {
                            stats.Team = spawnEffectiveTeam;
                        }
                        if (playerInstance.TryGetComponent<_Project.Core.Networking.PhasedNetworkObject>(out var phased))
                        {
                            bool isPvP = _Project.Features.StageSystem.Scripts.RunNetworkController.Singleton != null &&
                                         _Project.Features.StageSystem.Scripts.RunNetworkController.Singleton.activeRunMode.Value == GameMode.PvP;
                            phased.InitialPhase = isPvP
                                ? spawnEffectiveTeam.ToPhase()
                                : _Project.Core.Enums.TeamPhase.Both;
                        }

                        netObj.SpawnAsPlayerObject(playerData.ClientId, true);

                        if (playerInstance.TryGetComponent<ProxyCharacterInitializer>(out var initializer))
                        {
                            initializer.InitializeProxyCharacter(selectedCharacter);
                        }
                    }
                }
            }
        }
    }
}
