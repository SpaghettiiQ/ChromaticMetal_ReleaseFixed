using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Networking;
using _Project.Features.LobbySystem.Structs;

namespace _Project.Features.LobbySystem.Scripts
{
    public class LobbyNetworkController : NetworkBehaviour
    {
        public static LobbyNetworkController Singleton { get; private set; }

        // The authoritative state of the lobby settings and players
        public NetworkVariable<LobbySettings> CurrentSettings = new NetworkVariable<LobbySettings>();
        public NetworkList<LobbyPlayerState> LobbyPlayers;

        private void Awake()
        {
            if (Singleton != null && Singleton != this) Destroy(gameObject);
            else Singleton = this;

            LobbyPlayers = new NetworkList<LobbyPlayerState>();

            // Wire the Core-side resolver so PhasedNetworkObject can map clientId → team.
            PhasedNetworkObject.ClientTeamResolver = clientId =>
            {
                if (Singleton == null || Singleton.LobbyPlayers == null) return TeamAffiliation.None;
                for (int i = 0; i < Singleton.LobbyPlayers.Count; i++)
                {
                    var p = Singleton.LobbyPlayers[i];
                    if (p.ClientId == clientId)
                    {
                        return p.Team == TeamAffiliation.None ? TeamAffiliation.Cleansers : p.Team;
                    }
                }
                return TeamAffiliation.None;
            };
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;

                // Add the Host to the lobby immediately
                AddPlayerToList(NetworkManager.ServerClientId);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            }
        }

        #region Server-Side Connection Handling
        private void HandleClientConnected(ulong clientId)
        {
            AddPlayerToList(clientId);
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            for (int i = 0; i < LobbyPlayers.Count; i++)
            {
                if (LobbyPlayers[i].ClientId == clientId)
                {
                    LobbyPlayers.RemoveAt(i);
                    break;
                }
            }
        }

        private void AddPlayerToList(ulong clientId)
        {
            // Prevent duplicate entries for the Host (who triggers both OnNetworkSpawn and OnClientConnectedCallback)
            if (GetPlayerIndex(clientId) != -1) return;

            LobbyPlayers.Add(new LobbyPlayerState
            {
                ClientId = clientId,
                PlayerName = $"Player {clientId}",
                Team = TeamAffiliation.Cleansers, // Default to a valid team so they spawn correctly
                CharacterIndex = -1,
                IsReady = false,
                IsLockedIn = false
            });
        }
        #endregion

        #region Client RPC Requests
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void UpdateSettingsRpc(LobbySettings newSettings, RpcParams rpcParams = default)
        {
            // Only allow the Host (Client 0) to change settings
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            CurrentSettings.Value = newSettings;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestChangeTeamRpc(TeamAffiliation newTeam, RpcParams rpcParams = default)
        {
            int index = GetPlayerIndex(rpcParams.Receive.SenderClientId);
            if (index == -1) return;

            var state = LobbyPlayers[index];
            state.Team = newTeam;
            LobbyPlayers[index] = state; // Triggers the network update
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ToggleReadyRpc(RpcParams rpcParams = default)
        {
            int index = GetPlayerIndex(rpcParams.Receive.SenderClientId);
            if (index == -1) return;

            var state = LobbyPlayers[index];
            state.IsReady = !state.IsReady;
            LobbyPlayers[index] = state;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void KickPlayerRpc(ulong targetClientId, RpcParams rpcParams = default)
        {
            // Only the Host can kick
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;

            if (targetClientId != NetworkManager.ServerClientId) // Don't kick yourself
            {
                NetworkManager.Singleton.DisconnectClient(targetClientId);
            }
        }
        #endregion

        // Helper to find a player in the NetworkList
        private int GetPlayerIndex(ulong clientId)
        {
            for (int i = 0; i < LobbyPlayers.Count; i++)
            {
                if (LobbyPlayers[i].ClientId == clientId) return i;
            }
            return -1;
        }
        
        // Inside LobbyNetworkController.cs
        public void UpdatePlayerLockIn(ulong clientId, int charIndex, bool isLockedIn)
        {
            int index = GetPlayerIndex(clientId);
            if (index == -1) return;

            var state = LobbyPlayers[index];
            state.CharacterIndex = charIndex;
            state.IsLockedIn = isLockedIn;
            LobbyPlayers[index] = state;
        }

        public void SetPlayerTeam(ulong clientId, TeamAffiliation team)
        {
            int index = GetPlayerIndex(clientId);
            if (index == -1) return;

            var state = LobbyPlayers[index];
            if (state.Team == team) return;
            state.Team = team;
            LobbyPlayers[index] = state;
        }
    }
}