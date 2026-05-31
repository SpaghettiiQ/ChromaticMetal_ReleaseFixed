using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace _Project.Core.Managers
{
    /// <summary>
    /// Shared, collision-aware knockback for NavMeshAgent-driven enemies.
    ///
    /// Enemies live on the NavMesh and can't take a physics impulse, so a knockback detaches the
    /// agent, slides the transform in a short gravity arc, then re-anchors it onto the NavMesh.
    /// Unlike a naive "transform += velocity" slide (which is what both Bulwark Advance and the
    /// Purifying Maul used to do independently), this:
    ///   • clamps HORIZONTAL motion to the NavMesh via <see cref="NavMesh.Raycast(Vector3,Vector3,out NavMeshHit,int)"/>,
    ///     so an enemy can never be shoved THROUGH a wall — a wall is a NavMesh edge, and the slide
    ///     stops dead at it (no physics layer mask required); and
    ///   • clamps VERTICAL motion to the sampled floor height every frame, so even a weak knockback
    ///     can't sink the enemy through the ground mid-arc before the final re-anchor. (The old code
    ///     only re-anchored at the END of the arc, so a light shove visibly clipped into the floor
    ///     for the whole duration; a strong shove displaced far enough that the snap hid it.)
    ///
    /// Callers tune distance/feel via <paramref name="duration"/>, <paramref name="gravityMultiplier"/>
    /// and <paramref name="horizontalDrag"/>.
    /// </summary>
    public static class EnemyKnockback
    {
        /// <param name="agent">The enemy's NavMeshAgent. Disabled during the slide, restored after.</param>
        /// <param name="dir">Knockback direction (may include an upward component for a small hop).</param>
        /// <param name="force">Initial speed of the slide, in m/s.</param>
        /// <param name="duration">How long the slide lasts before re-anchoring, in seconds.</param>
        /// <param name="gravityMultiplier">Scales <see cref="Physics.gravity"/> for the arc.</param>
        /// <param name="horizontalDrag">Higher = the horizontal shove bleeds off faster (shorter push).</param>
        /// <param name="reanchorSampleRadius">Radius for the final NavMesh re-anchor sample.</param>
        public static IEnumerator Run(NavMeshAgent agent, Vector3 dir, float force,
                                      float duration = 0.5f, float gravityMultiplier = 2f,
                                      float horizontalDrag = 4f, float reanchorSampleRadius = 6f)
        {
            if (agent == null) yield break;

            Transform t = agent.transform;
            bool wasEnabled = agent.enabled;
            // Detach from the grid so it stops fighting the displacement.
            agent.enabled = false;

            Vector3 velocity = dir * force;

            // Seed the tracked floor height from the start position. We always query the floor near
            // this height (not the airborne transform height) so a small sample radius reliably finds
            // the ground even while the enemy is at the top of the arc.
            float floorY = t.position.y;
            if (NavMesh.SamplePosition(t.position, out NavMeshHit startHit, reanchorSampleRadius, NavMesh.AllAreas))
            {
                floorY = startHit.position.y;
            }

            float timer = 0f;
            while (timer < duration && t != null)
            {
                float dt = Time.deltaTime;
                Vector3 pos = t.position;

                // --- Horizontal: clamp to the NavMesh so we never slide through a wall. ---
                // Project start/target down to the tracked floor height so NavMesh.Raycast traces
                // along the mesh surface even when the enemy is mid-hop above it.
                Vector3 horizStep = new Vector3(velocity.x, 0f, velocity.z) * dt;
                Vector3 rayStart = new Vector3(pos.x, floorY, pos.z);
                Vector3 rayEnd = rayStart + horizStep;
                if (NavMesh.Raycast(rayStart, rayEnd, out NavMeshHit wallHit, NavMesh.AllAreas))
                {
                    // Hit a NavMesh edge (wall/obstacle boundary). Stop at it and kill the push so
                    // the rest of the arc doesn't keep grinding into the wall.
                    pos.x = wallHit.position.x;
                    pos.z = wallHit.position.z;
                    velocity.x = 0f;
                    velocity.z = 0f;
                }
                else
                {
                    pos.x = rayEnd.x;
                    pos.z = rayEnd.z;
                }

                // Re-track the floor height under the new horizontal position (terrain can slope).
                if (NavMesh.SamplePosition(new Vector3(pos.x, floorY, pos.z), out NavMeshHit floorHit, 2f, NavMesh.AllAreas))
                {
                    floorY = floorHit.position.y;
                }

                // --- Vertical: gravity arc, clamped so it can never sink below the floor. ---
                velocity.y += Physics.gravity.y * gravityMultiplier * dt;
                pos.y += velocity.y * dt;
                if (pos.y < floorY)
                {
                    pos.y = floorY;
                    if (velocity.y < 0f) velocity.y = 0f; // landed — stop falling
                }

                t.position = pos;

                // Horizontal drag bleeds the shove off over time.
                velocity.x = Mathf.Lerp(velocity.x, 0f, dt * horizontalDrag);
                velocity.z = Mathf.Lerp(velocity.z, 0f, dt * horizontalDrag);

                timer += dt;
                yield return null;
            }

            if (t != null && agent != null)
            {
                // Final re-anchor onto the playable NavMesh.
                if (NavMesh.SamplePosition(t.position, out NavMeshHit hit, reanchorSampleRadius, NavMesh.AllAreas))
                {
                    t.position = hit.position;
                }

                agent.enabled = wasEnabled;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
            }
        }
    }
}
