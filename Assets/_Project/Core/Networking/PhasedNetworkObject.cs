using System;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Events;
using _Project.Core.Interfaces;

namespace _Project.Core.Networking
{
    /// <summary>
    /// Minimal team-tag for networked dynamic entities. With the spatial-offset
    /// isolation model (Thrive's scenes physically translated by a large world offset),
    /// cross-team visibility/collision is handled by distance — no per-client visibility
    /// callbacks or physics layers are needed. This component now exists purely so
    /// other systems (damage filter, AI targeting, interactable guards) can still ask
    /// "what phase does this entity belong to?" as defense-in-depth.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PhasedNetworkObject : NetworkBehaviour, ITeamPhased
    {
        // Kept for compatibility — LobbyNetworkController still wires it.
        public static Func<ulong, TeamAffiliation> ClientTeamResolver;

        public NetworkVariable<TeamPhase> NetworkedPhase = new NetworkVariable<TeamPhase>(
            TeamPhase.Cleansers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>Set BEFORE NetworkObject.Spawn so the initial value is correct on first sync.</summary>
        [System.NonSerialized] public TeamPhase InitialPhase = TeamPhase.Cleansers;

        public TeamPhase Phase => NetworkedPhase.Value;
        public event Action<TeamPhase> PhaseChanged;

        public override void OnNetworkSpawn()
        {
            if (IsServer && NetworkedPhase.Value != InitialPhase)
                NetworkedPhase.Value = InitialPhase;

            NetworkedPhase.OnValueChanged += HandlePhaseChanged;

            if (IsLocalPlayerObject())
                PhaseEvents.RaiseLocalPlayerPhaseChanged(NetworkedPhase.Value);
        }

        public override void OnNetworkDespawn()
        {
            NetworkedPhase.OnValueChanged -= HandlePhaseChanged;
        }

        /// <summary>Server only: assign the phase.</summary>
        public void SetPhase(TeamPhase phase)
        {
            if (!IsServer) return;
            if (NetworkedPhase.Value == phase) return;
            NetworkedPhase.Value = phase;
        }

        public void SetPhaseFromTeam(TeamAffiliation team) => SetPhase(team.ToPhase());

        private bool IsLocalPlayerObject()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.LocalClient != null && nm.LocalClient.PlayerObject == NetworkObject;
        }

        private void HandlePhaseChanged(TeamPhase oldPhase, TeamPhase newPhase)
        {
            PhaseChanged?.Invoke(newPhase);
            if (IsLocalPlayerObject())
                PhaseEvents.RaiseLocalPlayerPhaseChanged(newPhase);
        }
    }
}
