using Unity.Netcode;
using UnityEngine;
using _Project.Core.Stats;

namespace _Project.Features.StageSystem.Scripts
{
    /// <summary>
    /// Placed on the player capsule prefab. Listens for death server-side and
    /// delegates to RunNetworkController for match-wide death resolution.
    /// </summary>
    [RequireComponent(typeof(CharacterStats))]
    public class PlayerDeathHandler : NetworkBehaviour
    {
        private CharacterStats _stats;

        public override void OnNetworkSpawn()
        {
            _stats = GetComponent<CharacterStats>();
            if (_stats != null)
                _stats.OnDeath += HandleDeath;
        }

        public override void OnNetworkDespawn()
        {
            if (_stats != null)
                _stats.OnDeath -= HandleDeath;
        }

        private void HandleDeath()
        {
            if (!IsServer) return;
            RunNetworkController.Singleton?.HandlePlayerDeath(OwnerClientId, _stats.Team);
        }
    }
}
