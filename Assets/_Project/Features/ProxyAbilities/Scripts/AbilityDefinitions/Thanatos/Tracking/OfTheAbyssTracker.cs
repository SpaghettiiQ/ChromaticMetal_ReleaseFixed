using System.Collections;
using Unity.Netcode;
using UnityEngine;
using _Project.Features.Player.Scripts;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Features.BurdenSystem.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Tracking
{
    public class OfTheAbyssTracker : MonoBehaviour
    {
        private Coroutine _jumpBoostRoutine;

        public void ExecuteAbility(OfTheAbyssAbility ability)
        {
            if (ability == null) return;
            if (!TryGetComponent(out PlayerMovement movement)) return;

            // Aim primarily along where the player is looking, but clamp the vertical so the dash
            // doesn't punt them straight up/down. Keeps the trail viable along the ground.
            Camera cam = GetComponentInChildren<Camera>();
            Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
            dir.y = Mathf.Clamp(dir.y, -0.25f, 0.35f);
            Vector3 dashDir = dir.normalized;

            // Instant impulse — ApplySustainedForce gets eaten by friction frame-by-frame, but
            // a one-shot velocity injection survives long enough to read as a real dash.
            movement.ApplyForce(dashDir * ability.dashImpulse, ability.resetVelocityOnDash);

            // Temporarily boost jump height. Routed through the new SetJumpHeight RPC so it
            // works for non-host clients too.
            if (Mathf.Abs(ability.jumpHeightMultiplier - 1f) > 0.001f && ability.dashDuration > 0f)
            {
                float originalJumpHeight = movement.jumpHeight;
                movement.SetJumpHeight(originalJumpHeight * ability.jumpHeightMultiplier);

                if (_jumpBoostRoutine != null) StopCoroutine(_jumpBoostRoutine);
                _jumpBoostRoutine = StartCoroutine(RestoreJumpHeightRoutine(movement, originalJumpHeight, ability.dashDuration));
            }

            // Residue trail rotates to follow the horizontal component of the dash so the
            // segments visually face the direction Thanatos was moving.
            Vector3 trailFacing = new Vector3(dashDir.x, 0f, dashDir.z);
            Quaternion trailRot = trailFacing.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(trailFacing.normalized)
                : Quaternion.identity;

            // Pull the caster's team so the spawned residue can shield same-team members
            // regardless of how the prefab is configured.
            TeamAffiliation casterTeam = TeamAffiliation.None;
            if (TryGetComponent(out CharacterStats casterStats)) casterTeam = casterStats.Team;

            StartCoroutine(SpawnTrailRoutine(ability.residueTrailPrefab, ability.dashDuration,
                ability.trailSegments, ability.trailLifetime, trailRot, casterTeam));
        }

        private IEnumerator RestoreJumpHeightRoutine(PlayerMovement movement, float originalJumpHeight, float duration)
        {
            yield return new WaitForSeconds(duration);
            if (movement != null) movement.SetJumpHeight(originalJumpHeight);
            _jumpBoostRoutine = null;
        }

        private IEnumerator SpawnTrailRoutine(GameObject residuePrefab, float duration, int segments, float lifetime, Quaternion rotation, TeamAffiliation casterTeam)
        {
            if (residuePrefab == null || segments <= 0) yield break;

            float timePerSegment = duration / segments;

            for (int i = 0; i < segments; i++)
            {
                // Raycast down to find ground so the segment sits flush instead of floating at
                // hip height.
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 20f))
                {
                    GameObject trailPiece = Instantiate(residuePrefab, hit.point + Vector3.up * 0.1f, rotation);

                    // Force the ability-origin shield BEFORE NetworkObject.Spawn — once spawned,
                    // the zone's Update starts ticking and we want the team-skip applied from
                    // the very first tick. Belt-and-suspenders: even if the prefab's checkbox
                    // wasn't ticked, this guarantees the caster's team is protected. Use
                    // GetComponentInChildren because the ResiduePool/ResidueTrail prefabs put
                    // the actual ResidueZone on a Cube/visual child of the NetworkObject root.
                    var zone = trailPiece.GetComponentInChildren<ResidueZone>(true);
                    if (zone != null)
                    {
                        zone.MarkAsAbilityOriginated(casterTeam);
                    }

                    NetworkObject netObj = trailPiece.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        netObj.Spawn();
                    }
                    Destroy(trailPiece, lifetime);
                }

                yield return new WaitForSeconds(timePerSegment);
            }
        }
    }
}
