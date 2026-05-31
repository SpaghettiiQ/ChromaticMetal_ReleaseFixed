using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _Project.Features.Player.Scripts;
using _Project.Features.Weapons.Scripts;
using _Project.Core.Stats;
using _Project.Core.Enums;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Tracking
{
    public class InfernumTracker : MonoBehaviour
    {
        public void ExecuteAbility(InfernumAbility ability)
        {
            if (ability == null) return;
            StartCoroutine(AbilityRoutine(ability));
        }

        private IEnumerator AbilityRoutine(InfernumAbility ability)
        {
            Camera cam = GetComponentInChildren<Camera>();
            Vector3 lookDir = cam != null ? cam.transform.forward : transform.forward;
            Vector3 lookOrigin = cam != null ? cam.transform.position : transform.position;

            CharacterStats myStats = GetComponent<CharacterStats>();
            TeamAffiliation myTeam = myStats != null ? myStats.Team : TeamAffiliation.None;

            // 1. Pick destination — full 3D look direction up to teleportDistance, or in front
            //    of the nearest enemy if one is closer than the cap.
            Vector3 destination;
            Quaternion destRotation = transform.rotation;

            // Pre-teleport target picker uses an FOV cone so we land in front of an enemy the
            // player was actually looking at (not one behind them).
            List<Transform> targetsBefore = FindEnemiesInView(lookOrigin, lookDir, ability.viewRadius, myTeam, useConeCheck: true);
            Transform nearest = targetsBefore.Count > 0 ? targetsBefore[0] : null;

            if (nearest != null && Vector3.Distance(transform.position, nearest.position) < ability.teleportDistance)
            {
                // Stop short of the target so pellets fly forward.
                Vector3 dirToNearest = (nearest.position - transform.position).normalized;
                destination = nearest.position - dirToNearest * ability.stoppingDistance;

                // Face the enemy on the horizontal plane (don't snap pitch — the camera owns that).
                Vector3 horizFace = new Vector3(dirToNearest.x, 0f, dirToNearest.z);
                if (horizFace.sqrMagnitude > 0.0001f)
                    destRotation = Quaternion.LookRotation(horizFace.normalized, Vector3.up);
            }
            else
            {
                // Full 3D blink along the camera's forward, including vertical.
                destination = transform.position + lookDir.normalized * ability.teleportDistance;
            }

            // 2. Wall + ground safety. CapsuleCast (not Raycast) uses the player's actual body
            //    width so we can't slip a body through a too-narrow gap, and an OverlapCapsule
            //    backoff loop after that handles the case where the cast somehow lands inside
            //    geometry (sharp corners, vertical look into terrain, etc.).
            destination = ResolveSafeTeleport(transform.position, destination);

            // 3. Teleport.
            PlayerMovement movement = GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.Teleport(destination, destRotation);
            }

            // 4. Reset weapon heat to 0 so the upcoming auto-fire fits cleanly inside one heat
            //    cycle. After N shots heat naturally hits max and triggers the eject + cool-off.
            TheEverblackWeapon weapon = GetComponentInChildren<TheEverblackWeapon>();
            if (weapon != null) weapon.ResetHeat();

            yield return new WaitForSeconds(Mathf.Max(0f, ability.teleportToFireDelay));

            if (weapon == null) yield break;

            // 5. Re-acquire targets from the new position. Proximity-only (no FOV gate) so
            //    enemies behind / to the sides also get hit — otherwise landing in front of
            //    a single target dumps all 8 shots into that one enemy. Uses targetingRadius
            //    (typically tighter than viewRadius) as the hard cap on engagement range.
            Vector3 postOrigin = cam != null ? cam.transform.position : transform.position;
            Vector3 postLook = cam != null ? cam.transform.forward : transform.forward;
            List<Transform> postTargets = FindEnemiesInView(postOrigin, postLook, ability.targetingRadius, myTeam, useConeCheck: false);

            // No targets in range / LOS → no shots fire, no heat accumulates, no eject. Thanatos
            // lands at the teleport destination and nothing else happens. The player still ate
            // the cooldown, but the ability gracefully no-ops on the shotgun side.
            if (postTargets.Count == 0)
            {
                Debug.Log("[Infernum] No targets within targetingRadius/LOS — skipping auto-fire and eject.");
                yield break;
            }

            int pelletCount = Mathf.Max(1, weapon.pelletsPerShot);
            int shotsLeft = ability.postTeleportShots;
            float interval = Mathf.Max(0f, ability.shotInterval);

            // The camera lives on the owning client. Routed through PlayerCamera.SnapLookAt
            // which RPCs to the owner when called from the server.
            PlayerCamera playerCam = GetComponentInChildren<PlayerCamera>();

            // Distribute round-robin: 2 enemies + 8 shots = 4 each, 4 + 8 = 2 each, etc.
            int idx = 0;
            int safety = ability.postTeleportShots * 4; // bail if every target dies mid-rotation
            while (shotsLeft > 0 && safety-- > 0)
            {
                Transform t = postTargets[idx % postTargets.Count];
                idx++;
                if (t == null) continue;

                // Re-read postOrigin each iteration because the camera may have moved during
                // the previous interval (other movement systems, recoil). Aim slightly above
                // the foot pivot so we look at the torso, not the floor.
                Vector3 currentOrigin = cam != null ? cam.transform.position : transform.position;
                Vector3 aimPoint = t.position + Vector3.up * 1f;
                Vector3 dir = (aimPoint - currentOrigin).normalized;

                // Snap the owner's view to face this target before firing so the player can
                // see what they're shooting at when round-robining across multiple enemies.
                if (playerCam != null) playerCam.SnapLookAt(dir);

                weapon.AutoTargetFire(dir, pelletCount);
                shotsLeft--;

                if (interval > 0f) yield return new WaitForSeconds(interval);
            }

            // 6. Guarantee the eject runs even if the configured heatPerShot doesn't perfectly
            //    sum to maxHeat across postTeleportShots. Without this the player needs one
            //    extra manual shot to tip the bar over, which feels weird mid-rotation.
            weapon.ForceHeatEject();
        }

        private Vector3 ResolveSafeTeleport(Vector3 origin, Vector3 requestedDestination)
        {
            // Read the player's actual capsule. Fallback values handle weird prefab states so
            // the ability never throws on a missing CharacterController.
            CharacterController cc = GetComponent<CharacterController>();
            float radius = cc != null ? cc.radius : 0.5f;
            float height = cc != null ? cc.height : 2f;
            Vector3 ccCenter = cc != null ? cc.center : new Vector3(0f, height * 0.5f, 0f);

            // Shrink the cast radius slightly so resting against a wall doesn't always read as
            // overlap (CharacterController itself has a small skinWidth that physics queries
            // don't account for).
            float castRadius = Mathf.Max(0.01f, radius - 0.02f);

            float halfBody = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 bottomOffset = ccCenter + Vector3.down * halfBody;
            Vector3 topOffset = ccCenter + Vector3.up * halfBody;

            Vector3 travel = requestedDestination - origin;
            float travelDist = travel.magnitude;
            Vector3 destination = requestedDestination;

            // Phase 1: sweep the capsule along the travel vector and stop where it first hits.
            if (travelDist > 0.01f)
            {
                Vector3 dir = travel / travelDist;
                Vector3 startBottom = origin + bottomOffset;
                Vector3 startTop = origin + topOffset;
                if (Physics.CapsuleCast(startBottom, startTop, castRadius, dir, out RaycastHit sweepHit, travelDist, ~0, QueryTriggerInteraction.Ignore))
                {
                    // Stop short of the impact so the body doesn't embed in the geometry.
                    float safeDist = Mathf.Max(0f, sweepHit.distance - 0.05f);
                    destination = origin + dir * safeDist;
                }
            }

            // Phase 2: if the destination still overlaps something (corner cases the sweep
            // missed — pinching against a sloped floor when looking down hard, etc.), walk back
            // toward the origin in fixed steps until the capsule is clear.
            if (!IsCapsuleClearAt(destination, bottomOffset, topOffset, castRadius))
            {
                Vector3 backDir = travelDist > 0.01f ? -(travel / travelDist) : Vector3.zero;
                const int backoffSteps = 8;
                const float stepSize = 0.4f;
                for (int i = 1; i <= backoffSteps; i++)
                {
                    Vector3 candidate = destination + backDir * (i * stepSize);
                    if (IsCapsuleClearAt(candidate, bottomOffset, topOffset, castRadius))
                    {
                        destination = candidate;
                        break;
                    }
                }

                // Last-ditch: if no backoff step found a clear pose, fall back to the origin
                // (the only position we know is safe — the player was there a moment ago).
                if (!IsCapsuleClearAt(destination, bottomOffset, topOffset, castRadius))
                {
                    destination = origin;
                }
            }

            return destination;
        }

        private bool IsCapsuleClearAt(Vector3 pos, Vector3 bottomOffset, Vector3 topOffset, float castRadius)
        {
            Collider[] overlaps = Physics.OverlapCapsule(pos + bottomOffset, pos + topOffset, castRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in overlaps)
            {
                // Ignore the player's own colliders (CharacterController, any child hitboxes).
                if (col.transform.root == transform) continue;
                return false;
            }
            return true;
        }

        private List<Transform> FindEnemiesInView(Vector3 origin, Vector3 forward, float viewRadius, TeamAffiliation myTeam, bool useConeCheck)
        {
            List<Transform> targets = new List<Transform>();
            Collider[] possible = Physics.OverlapSphere(transform.position, viewRadius);

            foreach (Collider col in possible)
            {
                if (col.gameObject == gameObject) continue;

                CharacterStats stats = col.GetComponentInParent<CharacterStats>();
                if (stats == null) continue;
                if (stats.gameObject == gameObject) continue; // never self-target
                if (stats.IsDead) continue;
                if (stats.Team == myTeam) continue; // skip allies (and the firer themselves under None==None)

                if (useConeCheck)
                {
                    Vector3 dirToTarget = (col.transform.position - origin).normalized;
                    float dot = Vector3.Dot(forward, dirToTarget);
                    if (dot <= 0.5f) continue; // roughly 120° FOV
                }

                // Line-of-sight gate: if a wall, floor, or other piece of geometry sits between
                // Thanatos and the target's torso, skip it. Without this, the proximity-only
                // post-teleport pool happily auto-fires through walls at enemies in an adjacent
                // room.
                if (!HasLineOfSight(origin, stats.transform)) continue;

                targets.Add(col.transform);
            }

            // Dedupe per character root and sort nearest-first.
            return targets
                .GroupBy(t => t.root)
                .Select(g => g.First())
                .OrderBy(t => Vector3.Distance(transform.position, t.position))
                .ToList();
        }

        // Raycast from origin to the target's torso. If anything blocks the ray before it
        // reaches the target's own colliders, we don't have LOS.
        private bool HasLineOfSight(Vector3 origin, Transform target)
        {
            Vector3 aimPoint = target.position + Vector3.up * 1f;
            Vector3 toTarget = aimPoint - origin;
            float dist = toTarget.magnitude;
            if (dist < 0.01f) return true;

            Vector3 dir = toTarget / dist;
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                // Nothing in the way at all — clear line.
                return true;
            }

            // The ray hit something. If it's the target itself (or any of its colliders / hitboxes
            // sharing the same root), the target is visible. Otherwise geometry is blocking.
            return hit.transform.root == target.root;
        }
    }
}
