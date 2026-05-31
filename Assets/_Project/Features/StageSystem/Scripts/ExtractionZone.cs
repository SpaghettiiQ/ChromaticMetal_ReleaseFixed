using Unity.Netcode;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Features.LobbySystem.Scripts;

namespace _Project.Features.StageSystem.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class ExtractionZone : NetworkBehaviour
    {
        [Tooltip("The terminal that must be fully charged for this zone to work.")]
        public ExtractionTerminal linkedTerminal;

        private Collider _triggerCollider;

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || !_triggerCollider.enabled) return;

            if (linkedTerminal == null || !linkedTerminal.IsDoorOpen.Value) return;

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsPlayerObject)
            {
                ExecuteExtraction(netObj);
            }
        }

        private void FixedUpdate()
        {
            // Fallback for physics engine issues explicitly checking overlaps on the server
            if (!IsServer || !_triggerCollider.enabled) return;

            if (linkedTerminal == null || !linkedTerminal.IsDoorOpen.Value) return;

            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
                {
                    Vector3 basePos = client.PlayerObject.transform.position;
                    Vector3 centerPos = basePos + Vector3.up * 1f;

                    // Since CharacterControllers are disabled on the server for non-hosts (thus turning off their colliders),
                    // we manually check if their coordinate position is simply inside the Extraction Zone bounds.
                    if (_triggerCollider.bounds.Contains(basePos) || _triggerCollider.bounds.Contains(centerPos))
                    {
                        ExecuteExtraction(client.PlayerObject);
                        break;
                    }
                }
            }
        }

        private void ExecuteExtraction(NetworkObject netObj)
        {
            // 3. Look up the player's team using the Lobby State
            TeamAffiliation extractingTeam = TeamAffiliation.None;

            if (LobbyNetworkController.Singleton != null)
            {
                foreach (var playerState in LobbyNetworkController.Singleton.LobbyPlayers)
                {
                    if (playerState.ClientId == netObj.OwnerClientId)
                    {
                        extractingTeam = playerState.Team;
                        break;
                    }
                }
            }

            if (extractingTeam == TeamAffiliation.None)
            {
                extractingTeam = TeamAffiliation.Cleansers;
            }

            Debug.Log($"[StageSystem] Player from {extractingTeam} stepped on extraction (FixedUpdate)! Advancing stage...");

            // 4. Disable the collider to prevent multiple triggers from the same team
            // in the fraction of a second before the scene unloads.
            _triggerCollider.enabled = false;

            // 5. Tell the Run Manager to advance the loop!
            if (RunNetworkController.Singleton != null)
            {
                RunNetworkController.Singleton.AdvanceTeamToNextStage(extractingTeam);
            }
        }
    }
}