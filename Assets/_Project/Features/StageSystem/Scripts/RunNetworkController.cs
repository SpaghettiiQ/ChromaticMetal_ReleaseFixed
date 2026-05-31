using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Networking;
using _Project.Features.LobbySystem.Scripts;
using _Project.Core.Audio;
using _Project.Core.Stats;
using _Project.Features.BurdenSystem.Scripts;
using _Project.Features.Enemies.Scripts.Spawning;
using _Project.Features.Player.Scripts;

namespace _Project.Features.StageSystem.Scripts
{
    public class RunNetworkController : NetworkBehaviour, IRunManager
    {
        public static RunNetworkController Singleton { get; private set; }

        [Header("Databases")]
        public Data.MapDatabase mapDatabase;

        [Header("Run State (Server Only)")]
        public NetworkVariable<GameMode> activeRunMode = new NetworkVariable<GameMode>(GameMode.Coop);
        public NetworkVariable<int> currentLoop = new NetworkVariable<int>(1);
        
        // INDEPENDENT PROGRESSION TRACKING
        public NetworkVariable<int> team1StageIndex = new NetworkVariable<int>(0);
        public NetworkVariable<int> team2StageIndex = new NetworkVariable<int>(0);

        [Header("PvE Loop Choice")]
        private bool _pveWantsToEndRun = false; // Toggled by in-game environment object

        [Header("PvP Score")]
        public NetworkVariable<int> teamCleansersWins = new NetworkVariable<int>(0);
        public NetworkVariable<int> teamThriveWins = new NetworkVariable<int>(0);
        private const int PvpWinsRequired = 3;

        // Single PvP itinerary — both teams play the same sequence of scenes (each scene
        // is authored with two physically-separated regions). Per-team progress is tracked
        // by team1StageIndex / team2StageIndex above, so teams can advance asymmetrically.
        private List<string> _team1Itinerary = new List<string>();

        private Dictionary<string, string> _pendingUnloads = new Dictionary<string, string>();
        private Dictionary<string, string> _pendingReloads = new Dictionary<string, string>();

        // Per-scene team ownership. Rebuilt on every load/unload.
        private Dictionary<string, HashSet<TeamAffiliation>> _sceneOwners = new Dictionary<string, HashSet<TeamAffiliation>>();

        // Replicated registry of which team(s) are currently in each loaded scene.
        // Clients use this to SetActive each scene's TeamRegionRoot — the unused team's
        // region of a dual-region scene is disabled so its geometry doesn't bleed into
        // the other team's view when two scenes are loaded simultaneously.
        public NetworkList<SceneOwnerEntry> SceneOwnership;

        // Death tracking (server-only)
        private readonly HashSet<ulong> _deadPlayers = new HashSet<ulong>();
        // deadClientId → clientId they are currently spectating
        private readonly Dictionary<ulong, ulong> _spectatingMap = new Dictionary<ulong, ulong>();

        private void Awake()
        {
            if (Singleton != null && Singleton != this) Destroy(gameObject);
            else Singleton = this;

            DontDestroyOnLoad(gameObject);

            SceneOwnership = new NetworkList<SceneOwnerEntry>();

            // Ensure SoundtrackManager exists
            if (SoundtrackManager.Singleton == null)
            {
                var soundtrackObj = new GameObject("SoundtrackManager");
                soundtrackObj.AddComponent<SoundtrackManager>();
            }
        }

        // --- 1. SETUP ---
        public void GenerateNewLoopItinerary(GameMode mode)
        {
            if (!IsServer) return;

            // The end-screen sets Time.timeScale = 0 and never restores it.
            // Resetting here ensures spawners / timers don't stall in the next match.
            ResetTimeScaleClientRpc();

            activeRunMode.Value = mode;
            team1StageIndex.Value = 0;
            team2StageIndex.Value = 0;
            _pveWantsToEndRun = false; // Reset vote at start of new loop

            if (currentLoop.Value == 1 && DifficultySystem.Scripts.DifficultyNetworkController.Singleton != null)
            {
                DifficultySystem.Scripts.DifficultyNetworkController.Singleton.ResetProgress();
                DifficultySystem.Scripts.DifficultyNetworkController.Singleton.StartMatchTimer();
            }

            _team1Itinerary.Clear();
            _deadPlayers.Clear();
            _spectatingMap.Clear();

            // Both teams play the same sequence of scenes. Each scene is authored with
            // two physically-separated regions (Cleansers + Thrive). Stage progress is
            // independently tracked per team via team1StageIndex / team2StageIndex.

            _team1Itinerary.AddRange(GetRandomStandardMaps(3));

            _team1Itinerary.Add(activeRunMode.Value == GameMode.PvP
                ? mapDatabase.finalPvPMapScene
                : mapDatabase.finalPvEMapScene);

            Debug.Log($"[StageSystem] Loop {currentLoop.Value} Generated.");
        }

        public string GetCurrentMapForTeam(TeamAffiliation team)
        {
            int idx = (team == TeamAffiliation.Thrive && activeRunMode.Value == GameMode.PvP)
                ? team2StageIndex.Value
                : team1StageIndex.Value;
            if (idx >= _team1Itinerary.Count) return "MainMenu";
            return _team1Itinerary[idx];
        }

        // --- 2. INDEPENDENT PROGRESSION ---
        // Called when a team finishes their current standard stage (e.g., they step on the extraction pad)
        public void AdvanceTeamToNextStage(TeamAffiliation team)
        {
            if (!IsServer) return;

            // Fully heal and revive the whole team before they enter the next stage.
            HealAndReviveTeam(team);

            string oldMap = GetCurrentMapForTeam(team);
            Debug.Log($"[StageSystem] Advancing team {team} from map {oldMap}");

            if (activeRunMode.Value == GameMode.PvP)
            {
                // PvP keeps the two teams on independent stage indices.
                if (team == TeamAffiliation.Cleansers)
                    team1StageIndex.Value++;
                else if (team == TeamAffiliation.Thrive)
                    team2StageIndex.Value++;
            }
            else
            {
                // Coop / singleplayer is a single-team progression — team1StageIndex is the
                // canonical index regardless of which team the lone player belongs to. The old
                // code only advanced for Cleansers, so a Thrive-team singleplayer (e.g. Thanatos)
                // never moved past stage 1 and the loop/end-of-run check never fired either.
                team1StageIndex.Value++;
            }

            bool loopAdvancing = CheckStageState();
            Debug.Log($"[StageSystem] loopAdvancing={loopAdvancing} for team {team}");

            if (!loopAdvancing)
            {
                string newMap = GetCurrentMapForTeam(team);
                Debug.Log($"[StageSystem] Transitioning team {team} from {oldMap} to {newMap}");
                TransitionTeamToMap(team, oldMap, newMap);
            }
        }

        private bool CheckStageState()
        {
            // If PvE, check if we finished the final stage (Index 3)
            if (activeRunMode.Value == GameMode.Coop)
            {
                if (team1StageIndex.Value > 3)
                {
                    EndFullRun();
                    return true;
                }
            }
            // If PvP, check if BOTH teams are waiting in the Final Stage (Index 3)
            else if (activeRunMode.Value == GameMode.PvP)
            {
                if (team1StageIndex.Value == 3 && team2StageIndex.Value == 3)
                {
                    Debug.Log("[StageSystem] Both teams arrived at Final PvP Arena! Promoting all players to Both phase.");
                    PromoteAllPlayersToBoth();
                }
            }

            return false;
        }

        // Server-only. Called when both teams have arrived at the final PvP arena.
        // All player NetworkObjects (and their child weapons) are flipped to TeamPhase.Both
        // so they become mutually visible/collidable in the shared arena.
        private void PromoteAllPlayersToBoth()
        {
            if (!IsServer || NetworkManager.Singleton == null) return;
            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (client.PlayerObject == null) continue;
                foreach (var phased in client.PlayerObject.GetComponentsInChildren<PhasedNetworkObject>(true))
                {
                    phased.SetPhase(TeamPhase.Both);
                }
            }
        }

        private void TransitionTeamToMap(TeamAffiliation team, string oldMap, string newMap)
        {
            if (oldMap == newMap)
            {
                Debug.Log($"[StageSystem] oldMap == newMap ({newMap}). Triggering complete scene reload.");

                // PROTECT PLAYERS: We must evacuate them from the scene before destroying/unloading it!
                foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
                {
                    if (client.PlayerObject != null && client.PlayerObject.gameObject.scene.name == oldMap)
                    {
                        client.PlayerObject.gameObject.transform.parent = null;
                        DontDestroyOnLoad(client.PlayerObject.gameObject);
                    }
                }

                int validSceneCount = 0;
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name != "DontDestroyOnLoad") validSceneCount++;
                }

                if (validSceneCount <= 1)
                {
                    // Unity CRASHES if we explicitly use UnloadScene on the ONLY loaded scene. 
                    // Switching to LoadSceneMode.Single natively unloads and reloads it cleanly!
                    NetworkManager.Singleton.SceneManager.LoadScene(newMap, LoadSceneMode.Single);
                }
                else
                {
                    // In PvP, other maps are loaded. We can safely Unload Additive since it's not the last scene.
                    Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(oldMap);
                    if (scene.isLoaded)
                    {
                        _pendingReloads[oldMap] = newMap;
                        NetworkManager.Singleton.SceneManager.UnloadScene(scene);
                    }
                }
                return;
            }

            Scene loadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(newMap);
            if (loadedScene.isLoaded)
            {
                // The map is already loaded (e.g. PvP final stage, and the other team is already there)
                MatchNetworkController.Singleton.SpawnPlayersForScene(newMap, team, true);
                UnloadMapIfEmpty(oldMap);
            }
            else
            {
                // Register the old map to be unloaded once the new one finishes loading
                _pendingUnloads[newMap] = oldMap;
                NetworkManager.Singleton.SceneManager.LoadScene(newMap, LoadSceneMode.Additive);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
                NetworkManager.Singleton.SceneManager.OnUnloadEventCompleted += HandleSceneUnloaded;
            }

            // All sides (server + clients): react to ownership replication + local scene
            // loads. When ownership changes for a loaded scene, toggle its TeamRegionRoots
            // so the inactive team's region of a dual-region map is disabled.
            SceneOwnership.OnListChanged += HandleSceneOwnershipChanged;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleUnitySceneLoaded;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
                NetworkManager.Singleton.SceneManager.OnUnloadEventCompleted -= HandleSceneUnloaded;
            }

            if (SceneOwnership != null) SceneOwnership.OnListChanged -= HandleSceneOwnershipChanged;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleUnitySceneLoaded;
        }

        private void HandleSceneOwnershipChanged(NetworkListEvent<SceneOwnerEntry> evt)
        {
            if (evt.Type != NetworkListEvent<SceneOwnerEntry>.EventType.Add &&
                evt.Type != NetworkListEvent<SceneOwnerEntry>.EventType.Value) return;
            string sceneName = evt.Value.SceneName.ToString();
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
                TeamRegionRoot.ApplyOwnership(scene, evt.Value);
        }

        private void HandleUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            for (int i = 0; i < SceneOwnership.Count; i++)
            {
                if (SceneOwnership[i].SceneName == scene.name)
                {
                    TeamRegionRoot.ApplyOwnership(scene, SceneOwnership[i]);
                    return;
                }
            }
        }

        private void UpsertSceneOwnership(string sceneName, HashSet<TeamAffiliation> owners)
        {
            if (!IsServer || string.IsNullOrEmpty(sceneName)) return;
            var entry = new SceneOwnerEntry
            {
                SceneName = sceneName,
                HasCleansers = owners.Contains(TeamAffiliation.Cleansers),
                HasThrive = owners.Contains(TeamAffiliation.Thrive)
            };
            for (int i = 0; i < SceneOwnership.Count; i++)
            {
                if (SceneOwnership[i].SceneName == sceneName)
                {
                    SceneOwnership[i] = entry;
                    return;
                }
            }
            SceneOwnership.Add(entry);
        }

        private void RemoveSceneOwnership(string sceneName)
        {
            if (!IsServer) return;
            for (int i = 0; i < SceneOwnership.Count; i++)
            {
                if (SceneOwnership[i].SceneName == sceneName) { SceneOwnership.RemoveAt(i); return; }
            }
        }

        [Tooltip("Delay (seconds) between NGO finishing a scene load and the server placing players. " +
                 "Gives navmesh, physics colliders, and TeamRegionRoot activations a moment to settle " +
                 "so the teleport-to-spawn-point lands cleanly.")]
        [SerializeField] private float postSceneLoadSpawnDelay = 0.5f;

        private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (!IsServer) return;

            // Replicate ownership + toggle TeamRegionRoots on the freshly-loaded scene.
            RefreshSceneOwnership(sceneName);

            // Defer player placement so NavMeshSurface bakes / region SetActives have a chance
            // to settle on this frame. Without this, teleports landing on freshly-activated
            // geometry can clip through the floor or miss the navmesh entirely.
            StartCoroutine(DeferredSpawnAndUnload(sceneName));
        }

        private System.Collections.IEnumerator DeferredSpawnAndUnload(string sceneName)
        {
            if (postSceneLoadSpawnDelay > 0f)
                yield return new WaitForSeconds(postSceneLoadSpawnDelay);
            else
                yield return null;

            if (!IsServer) yield break;
            if (MatchNetworkController.Singleton == null) yield break;

            MatchNetworkController.Singleton.SpawnPlayersForScene(sceneName, TeamAffiliation.None, true);

            if (_pendingUnloads.TryGetValue(sceneName, out string oldMap))
            {
                UnloadMapIfEmpty(oldMap);
                _pendingUnloads.Remove(sceneName);
            }
        }

        /// <summary>
        /// Server-only. Determines which team(s) currently consider `sceneName` their
        /// active map by scanning LobbyPlayers, and replicates the result so all clients
        /// can toggle the scene's TeamRegionRoot activations.
        /// </summary>
        private void RefreshSceneOwnership(string sceneName)
        {
            if (!IsServer || string.IsNullOrEmpty(sceneName) || sceneName == "MainMenu") return;

            var owners = new HashSet<TeamAffiliation>();
            if (LobbyNetworkController.Singleton != null)
            {
                foreach (var p in LobbyNetworkController.Singleton.LobbyPlayers)
                {
                    var team = p.Team == TeamAffiliation.None ? TeamAffiliation.Cleansers : p.Team;
                    if (GetCurrentMapForTeam(team) == sceneName) owners.Add(team);
                }
            }

            _sceneOwners[sceneName] = owners;
            UpsertSceneOwnership(sceneName, owners);

            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                // Apply locally on the server too (clients react via NetworkList propagation).
                var entry = new SceneOwnerEntry
                {
                    SceneName = sceneName,
                    HasCleansers = owners.Contains(TeamAffiliation.Cleansers),
                    HasThrive = owners.Contains(TeamAffiliation.Thrive)
                };
                TeamRegionRoot.ApplyOwnership(scene, entry);
            }
        }

        private void HandleSceneUnloaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (!IsServer) return;

            if (_pendingReloads.TryGetValue(sceneName, out string nextMap))
            {
                _pendingReloads.Remove(sceneName);
                Debug.Log($"[StageSystem] Finished unloading {sceneName} for reload. Now loading {nextMap}.");
                NetworkManager.Singleton.SceneManager.LoadScene(nextMap, LoadSceneMode.Additive);
            }
        }

        private void UnloadMapIfEmpty(string mapName)
        {
            if (mapName == "MainMenu") return; // Handled by MatchNetworkController

            // If another team is still taking their time in this map for PvP, DO NOT UNLOAD IT YET!
            if (GetCurrentMapForTeam(TeamAffiliation.Cleansers) == mapName ||
                GetCurrentMapForTeam(TeamAffiliation.Thrive) == mapName)
            {
                // Ownership shrank from 2 → 1 (or stayed 1). Re-evaluate so geometry layer
                // and spawner roster reflect the remaining team only.
                RefreshSceneOwnership(mapName);
                return;
            }

            _sceneOwners.Remove(mapName);
            RemoveSceneOwnership(mapName);

            Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(mapName);
            if (scene.isLoaded)
            {
                NetworkManager.Singleton.SceneManager.UnloadScene(scene);
                Debug.Log($"[StageSystem] Unloaded previous stage map: {mapName}");
            }
        }

        // --- 3. LOOP MANAGEMENT ---
        public void SetPvERunEndVote(bool wantsToEnd)
        {
            if (!IsServer || activeRunMode.Value != GameMode.Coop) return;
            _pveWantsToEndRun = wantsToEnd;
            Debug.Log($"[StageSystem] PvE End Run Vote logged: {_pveWantsToEndRun}");
        }

        public void ResolvePvPMatch(TeamAffiliation winningTeam)
        {
            if (!IsServer || activeRunMode.Value != GameMode.PvP) return;

            if (winningTeam == TeamAffiliation.Cleansers) teamCleansersWins.Value++;
            else if (winningTeam == TeamAffiliation.Thrive) teamThriveWins.Value++;

            Debug.Log($"[StageSystem] PvP Match Concluded. Score - Cleansers: {teamCleansersWins.Value} | THRIVE: {teamThriveWins.Value}");

            if (teamCleansersWins.Value >= PvpWinsRequired || teamThriveWins.Value >= PvpWinsRequired)
            {
                EndFullRun();
            }
            else
            {
                AdvanceToNextLoop();
            }
        }

        private void AdvanceToNextLoop()
        {
            string oldMap1 = GetCurrentMapForTeam(TeamAffiliation.Cleansers);
            string oldMap2 = GetCurrentMapForTeam(TeamAffiliation.Thrive);

            currentLoop.Value++;
            Debug.Log($"[StageSystem] Advancing to Loop {currentLoop.Value}...");
            
            // Re-roll maps and reset stage indices to 0!
            GenerateNewLoopItinerary(activeRunMode.Value);

            string newMap1 = GetCurrentMapForTeam(TeamAffiliation.Cleansers);
            TransitionTeamToMap(TeamAffiliation.Cleansers, oldMap1, newMap1);

            if (activeRunMode.Value == GameMode.PvP)
            {
                string newMap2 = GetCurrentMapForTeam(TeamAffiliation.Thrive);
                TransitionTeamToMap(TeamAffiliation.Thrive, oldMap2, newMap2);
            }
        }

        private void EndFullRun()
        {
            Debug.Log("[StageSystem] RUN COMPLETED! Returning to Main Menu.");

            // defeatedTeam.None = PvE victory (everyone succeeded).
            // Any other value means that team was defeated.
            TeamAffiliation defeatedTeam = TeamAffiliation.None;
            if (activeRunMode.Value == GameMode.PvP)
            {
                if (teamCleansersWins.Value >= PvpWinsRequired) defeatedTeam = TeamAffiliation.Thrive;
                else if (teamThriveWins.Value >= PvpWinsRequired) defeatedTeam = TeamAffiliation.Cleansers;
            }

            ShowEndScreenClientRpc(defeatedTeam);
        }

        [ClientRpc]
        private void ResetTimeScaleClientRpc()
        {
            Time.timeScale = 1f;
        }

        [ClientRpc]
        private void ShowEndScreenClientRpc(TeamAffiliation defeatedTeam)
        {
            if (Features.InGameUI.InGameUIController.Instance == null) return;

            // Freeze the game and release mouse
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Lock input
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                if (NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent(out UnityEngine.InputSystem.PlayerInput playerInput))
                    playerInput.enabled = false;
            }

            // defeatedTeam.None = successful PvE extraction (victory).
            // Otherwise, local team loses if their team matches defeatedTeam.
            bool isVictory;
            if (defeatedTeam == TeamAffiliation.None)
            {
                isVictory = true;
            }
            else
            {
                TeamAffiliation myTeam = GetLocalPlayerTeam();
                isVictory = (myTeam != defeatedTeam);
            }

            string matchTime = "0:00";
            if (DifficultySystem.Scripts.DifficultyNetworkController.Singleton != null)
            {
                float time = DifficultySystem.Scripts.DifficultyNetworkController.Singleton.MatchTime;
                int minutes = Mathf.FloorToInt(time / 60f);
                int seconds = Mathf.FloorToInt(time % 60f);
                matchTime = $"{minutes:00}:{seconds:00}";
            }
            
            int stage = (GetLocalPlayerTeam() == TeamAffiliation.Thrive && activeRunMode.Value == GameMode.PvP) ? team2StageIndex.Value : team1StageIndex.Value;
            stage = Mathf.Clamp(stage + 1, 1, 4);

            string statsText = $"Match Time: {matchTime}\nLevel Reached: 1\nStage: {stage}\nLoop: {currentLoop.Value}\nCharacter Played: Agent\nEnemies Killed: ?\nDamage Dealt: ?\nDamage Taken: ?\nMoney: $0\nItem Count: 0";
            List<(Sprite icon, int count)> items = new List<(Sprite, int)>();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                
                int levelReached = DifficultySystem.Scripts.DifficultyNetworkController.Singleton != null ? DifficultySystem.Scripts.DifficultyNetworkController.Singleton.CurrentLevel : 1;
                int currentMoney = 0;
                int itemCount = 0;
                string characterName = "Agent";
                if (playerObj.TryGetComponent(out _Project.Features.ProxyCharacters.Scripts.ProxyCharacterInitializer proxy))
                {
                    characterName = proxy.CharacterName;
                }

                if (playerObj.TryGetComponent(out Core.Stats.CharacterStats stats))
                {
                    currentMoney = stats.CurrentMoney.Value;
                }
                
                if (playerObj.TryGetComponent(out Features.Items.Scripts.CharacterInventory inventory))
                {
                    foreach (var item in inventory.NetworkedItems)
                    {
                        itemCount += item.Stacks;
                        if (inventory.Database != null)
                        {
                            var def = inventory.Database.GetItem(item.ItemID);
                            if (def != null) items.Add((def.icon, item.Stacks));
                        }
                    }
                }

                statsText = $"Match Time: {matchTime}\nLevel Reached: {levelReached}\nStage: {stage}\nLoop: {currentLoop.Value}\nCharacter Played: {characterName}\nMoney: ${currentMoney}\nItem Count: {itemCount}";
            }

            Features.InGameUI.InGameUIController.Instance.ShowEndScreen(isVictory, items, statsText);
        }

        private TeamAffiliation GetLocalPlayerTeam()
        {
            if (NetworkManager.Singleton == null || LobbyNetworkController.Singleton == null)
                return TeamAffiliation.None;

            ulong localId = NetworkManager.Singleton.LocalClientId;
            foreach (var p in LobbyNetworkController.Singleton.LobbyPlayers)
            {
                if (p.ClientId == localId)
                    return p.Team == TeamAffiliation.None ? TeamAffiliation.Cleansers : p.Team;
            }
            return TeamAffiliation.None;
        }

        // ==========================================
        // DEATH HANDLING
        // ==========================================

        /// <summary>
        /// Called server-side by PlayerDeathHandler when a player's health hits zero.
        /// </summary>
        public void HandlePlayerDeath(ulong deadClientId, TeamAffiliation team)
        {
            if (!IsServer) return;

            _deadPlayers.Add(deadClientId);

            // Count how many players on this team are still alive.
            int total = 0, dead = 0;
            foreach (var player in LobbyNetworkController.Singleton.LobbyPlayers)
            {
                TeamAffiliation effectiveTeam = player.Team == TeamAffiliation.None ? TeamAffiliation.Cleansers : player.Team;
                if (effectiveTeam != team) continue;
                total++;
                if (_deadPlayers.Contains(player.ClientId)) dead++;
            }

            if (total == 0)
            {
                Debug.LogWarning($"[StageSystem] HandlePlayerDeath: No lobby players found for team {team}. Skipping end-screen trigger.");
                return;
            }

            if (dead >= total)
            {
                // Whole team wiped — end the match.
                Debug.Log($"[StageSystem] Team {team} fully wiped. Ending match.");
                ShowEndScreenClientRpc(team); // that team is the defeated team
            }
            else
            {
                // Teammates still alive — switch dead player to spectate.
                ulong spectateTargetClientId = FindLivingTeammate(deadClientId, team);
                if (spectateTargetClientId == ulong.MaxValue) return;

                // Resolve the spectate target's NetworkObjectId so the client can look it up.
                ulong spectateTargetNetObjId = 0;
                string spectateTargetName = "Teammate";

                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(spectateTargetClientId, out var targetClient)
                    && targetClient.PlayerObject != null)
                {
                    spectateTargetNetObjId = targetClient.PlayerObject.NetworkObjectId;
                }

                foreach (var p in LobbyNetworkController.Singleton.LobbyPlayers)
                {
                    if (p.ClientId == spectateTargetClientId)
                    {
                        spectateTargetName = p.PlayerName.ToString();
                        break;
                    }
                }

                _spectatingMap[deadClientId] = spectateTargetClientId;

                var rpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { deadClientId } }
                };
                ActivateSpectateClientRpc(spectateTargetNetObjId, spectateTargetName, rpcParams);
            }
        }

        private ulong FindLivingTeammate(ulong excludeClientId, TeamAffiliation team)
        {
            foreach (var player in LobbyNetworkController.Singleton.LobbyPlayers)
            {
                TeamAffiliation effectiveTeam = player.Team == TeamAffiliation.None ? TeamAffiliation.Cleansers : player.Team;
                if (effectiveTeam != team) continue;
                if (player.ClientId == excludeClientId) continue;
                if (_deadPlayers.Contains(player.ClientId)) continue;
                return player.ClientId;
            }
            return ulong.MaxValue;
        }

        // ==========================================
        // STAGE HEALING & REVIVE
        // ==========================================

        private void HealAndReviveTeam(TeamAffiliation team)
        {
            if (!IsServer) return;

            // Collect which clients on this team were dead (need deactivate spectate RPC).
            List<ulong> wasDeadList = new List<ulong>();

            foreach (var player in LobbyNetworkController.Singleton.LobbyPlayers)
            {
                TeamAffiliation effectiveTeam = player.Team == TeamAffiliation.None ? TeamAffiliation.Cleansers : player.Team;
                if (effectiveTeam != team) continue;

                if (_deadPlayers.Contains(player.ClientId))
                    wasDeadList.Add(player.ClientId);

                // Fully restore health (bypasses IsDead guard) and reset burden.
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(player.ClientId, out var client)
                    && client.PlayerObject != null)
                {
                    if (client.PlayerObject.TryGetComponent<CharacterStats>(out var stats))
                        stats.ReviveAndHeal();

                    if (client.PlayerObject.TryGetComponent<BurdenController>(out var burden))
                        burden.RemoveBurden(burden.GetCurrentBurden());
                }
            }

            // Remove from death tracking.
            foreach (ulong id in wasDeadList)
            {
                _deadPlayers.Remove(id);
                _spectatingMap.Remove(id);

                // Tell the previously-dead client to deactivate spectate mode.
                var rpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { id } }
                };
                DeactivateSpectateClientRpc(rpcParams);
            }
        }

        // ==========================================
        // SPECTATE RPCs (Client-side)
        // ==========================================

        [ClientRpc]
        private void ActivateSpectateClientRpc(ulong spectateTargetNetObjId, string targetName, ClientRpcParams rpcParams = default)
        {
            var localPlayerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (localPlayerObj == null) return;

            // Disable movement input.
            if (localPlayerObj.TryGetComponent<PlayerInputHandler>(out var input))
                input.enabled = false;

            // Find the target NetworkObject and point the spectate camera at their PlayerCamera.
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(spectateTargetNetObjId, out var targetNetObj))
            {
                var targetCamera = targetNetObj.GetComponentInChildren<PlayerCamera>();
                var localCamera  = localPlayerObj.GetComponentInChildren<PlayerCamera>();

                if (localCamera != null && targetCamera != null)
                    localCamera.SetSpectateTarget(targetCamera.transform);

                // Point UI binder at the spectated player's stats.
                var binder = Features.InGameUI.InGameUIController.Instance?.GetComponent<Features.InGameUI.InGameUIBinder>();
                binder?.SetSpectateTarget(targetNetObj.gameObject);
            }

            Features.InGameUI.InGameUIController.Instance?.ShowSpectatingOverlay(targetName);
        }

        [ClientRpc]
        private void DeactivateSpectateClientRpc(ClientRpcParams rpcParams = default)
        {
            var localPlayerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (localPlayerObj == null) return;

            // Re-enable input.
            if (localPlayerObj.TryGetComponent<PlayerInputHandler>(out var input))
                input.enabled = true;

            // Restore own camera.
            var localCamera = localPlayerObj.GetComponentInChildren<PlayerCamera>();
            localCamera?.ClearSpectateTarget();

            // Restore UI binder to local player.
            var binder = Features.InGameUI.InGameUIController.Instance?.GetComponent<Features.InGameUI.InGameUIBinder>();
            binder?.ClearSpectateTarget();

            Features.InGameUI.InGameUIController.Instance?.HideSpectatingOverlay();
        }

        // --- Internal Helpers ---
        private List<string> GetRandomStandardMaps(int count)
        {
            List<string> pool = new List<string>(mapDatabase.standardMapScenes);
            List<string> result = new List<string>();
            if (pool.Count == 0) return result;

            for (int i = 0; i < count; i++)
            {
                if (pool.Count == 0) pool = new List<string>(mapDatabase.standardMapScenes);
                int randomIndex = Random.Range(0, pool.Count);
                result.Add(pool[randomIndex]);
                pool.RemoveAt(randomIndex);
            }
            return result;
        }
    }
}

