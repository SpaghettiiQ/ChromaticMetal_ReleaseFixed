using UnityEngine;
using Unity.Netcode;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Features.Player.Scripts;
using _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Projectiles;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking
{
    /// <summary>
    /// Spawns a PresenceRift at the Conduit's aim point, launches the Conduit backwards, and
    /// removes a fixed portion of their current Burden. Rift handles its own lifetime,
    /// pull/slow/tick, and final collapse explosion.
    /// </summary>
    public class OppressiveControlTracker : MonoBehaviour
    {
        public void ExecuteAbility(PresenceRift riftPrefab, float knockback, float burdenRemoved, float range)
        {
            Camera cam = GetComponentInChildren<Camera>();
            Vector3 aimOrigin = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.5f;
            Vector3 aimDir = cam != null ? cam.transform.forward : transform.forward;

            // Default: max range. Raycast to find the first surface so the rift sits on
            // geometry rather than mid-air past a wall.
            Vector3 spawnPoint = aimOrigin + aimDir * range;
            if (Physics.Raycast(aimOrigin, aimDir, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPoint = hit.point;
            }

            // Launch the Conduit backwards along the aim vector with a slight upward arc
            // so they pop off the ground instead of getting dragged across it.
            if (TryGetComponent(out PlayerMovement movement))
            {
                Vector3 knockbackDir = -aimDir;
                knockbackDir.y = Mathf.Max(0.2f, knockbackDir.y);
                movement.ApplyForce(knockbackDir.normalized * knockback, true);
            }

            // Server-only: remove burden + spawn the networked rift.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (riftPrefab == null) return;

            if (TryGetComponent(out IBurdenable burdenable))
            {
                burdenable.RemoveBurden(burdenRemoved);
            }

            // Pass our team to the rift so its pull/slow/tick can spare same-team players
            // (the FINAL collapse still hits everyone — that's the design's "high-risk
            // big-reward" payoff).
            CharacterStats stats = GetComponent<CharacterStats>();
            _Project.Core.Enums.TeamAffiliation myTeam = stats != null
                ? stats.Team
                : _Project.Core.Enums.TeamAffiliation.None;

            var rift = Instantiate(riftPrefab, spawnPoint, Quaternion.identity);
            if (rift.TryGetComponent(out NetworkObject nm))
            {
                nm.Spawn();
            }
            rift.Initialize(gameObject, myTeam);
        }
    }
}
