using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Guided Nanomissiles", fileName = "GuidedNanomissiles")]
    public class GuidedNanomissilesItem : ItemDefinition, IOnHitItem
    {
        [Tooltip("Base chance to fire a missile (0-1)")]
        public float baseProcChance = 0.15f;
        [Tooltip("Additional chance per stack")]
        public float procChancePerStack = 0.05f;
        public float searchRadius = 20f;
        public float missileDamage = 25f;
        public float missileSpeed = 30f;

        [Header("VFX")]
        public GameObject missilePrefab;
        public GameObject explosionVfxPrefab;

        public void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            // Don't self-trigger off the missile's own impact, and don't proc off burn/bleed
            // ticks (those are passive DOTs, not real player attacks). Chain (Synaptic) and
            // ricochet (Splitter) hits use AttackType.Secondary which we intentionally still
            // allow through, so chain/ricochet → missile cross-procs continue to work.
            if (ctx.AttackType == _Project.Core.Enums.AttackType.MissileImpact) return;
            if (ctx.AttackType == _Project.Core.Enums.AttackType.DamageOverTime) return;

            float totalChance = baseProcChance + (procChancePerStack * (stacks - 1));

            // Try to get owner stats for proc chance bonuses and team
            _Project.Core.Enums.TeamAffiliation ownerTeam = _Project.Core.Enums.TeamAffiliation.None;
            if (owner.TryGetComponent(out _Project.Core.Stats.CharacterStats ownerStats))
            {
                ownerTeam = ownerStats.Team;
                float bonusChance = ownerStats.GetStat(_Project.Core.Enums.StatType.ProcChanceFlatAdd);
                float multiplier = ownerStats.GetStat(_Project.Core.Enums.StatType.ProcChanceMultiplier);
                if (multiplier == 0f) multiplier = 1f;

                totalChance = (totalChance * multiplier) + bonusChance;
            }

            if (Random.value > totalChance) return;

            Collider[] hits = Physics.OverlapSphere(owner.transform.position, searchRadius);
            GameObject nearestTarget = null;
            _Project.Core.Stats.CharacterStats nearestStats = null;
            float nearestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.gameObject == owner || hit.gameObject == ctx.Target) continue;

                // Walk up to the character root so we don't end up with a hitbox child as the target.
                var targetStats = hit.GetComponentInParent<_Project.Core.Stats.CharacterStats>();
                if (targetStats == null || targetStats.IsDead) continue;

                if (ownerTeam != _Project.Core.Enums.TeamAffiliation.None &&
                    targetStats.Team != _Project.Core.Enums.TeamAffiliation.None &&
                    ownerTeam == targetStats.Team) continue;

                float dist = Vector3.Distance(owner.transform.position, targetStats.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestTarget = targetStats.gameObject;
                    nearestStats = targetStats;
                }
            }

            // Fall back to the original target if nothing else is in range. Without this the
            // missile silently no-ops whenever the player engages a lone enemy or a sparse
            // group — which made the item feel broken even at 100% proc.
            if (nearestTarget == null && ctx.Target != null)
            {
                var fallbackStats = ctx.Target.GetComponentInParent<_Project.Core.Stats.CharacterStats>();
                if (fallbackStats != null && !fallbackStats.IsDead)
                {
                    nearestTarget = fallbackStats.gameObject;
                    nearestStats = fallbackStats;
                }
            }

            if (nearestTarget == null || nearestStats == null)
            {
                Debug.Log($"[Guided Nanomissiles] proc fired but no valid target found within {searchRadius}m.");
                return;
            }

            var missileCtx = new DamageContext
            {
                Source = owner,
                Target = nearestTarget,
                Damage = Mathf.RoundToInt(missileDamage),
                DamageType = _Project.Core.Enums.DamageType.Explosive,
                // MissileImpact (not Projectile) so a missile-killed crit chain can route back
                // through other procs without re-triggering missiles themselves.
                AttackType = _Project.Core.Enums.AttackType.MissileImpact,
                IsCritical = false
            };

            if (owner.TryGetComponent(out MonoBehaviour runner))
            {
                runner.StartCoroutine(MissileVisualRoutine(owner.transform.position, nearestTarget, nearestStats, missileCtx));
            }
            else
            {
                Debug.LogWarning("[Guided Nanomissiles] proc fired but owner has no MonoBehaviour to host the missile coroutine.");
            }
        }

        private System.Collections.IEnumerator MissileVisualRoutine(Vector3 startPos, GameObject targetObj, _Project.Core.Stats.CharacterStats targetStats, DamageContext ctx)
        {
            GameObject visualMissile = null;
            if (missilePrefab != null)
            {
                visualMissile = Instantiate(missilePrefab, startPos, Quaternion.identity);
            }

            Vector3 currentPos = startPos;
            while (targetObj != null && !targetStats.IsDead)
            {
                Vector3 targetPos = targetObj.transform.position;
                // Aim for the center mass slightly
                if (targetObj.TryGetComponent(out Collider col)) targetPos = col.bounds.center;

                float dist = Vector3.Distance(currentPos, targetPos);
                if (dist < 0.5f) break; // Reached target

                Vector3 dir = (targetPos - currentPos).normalized;
                currentPos += dir * missileSpeed * Time.deltaTime;
                
                if (visualMissile != null)
                {
                    visualMissile.transform.position = currentPos;
                    visualMissile.transform.rotation = Quaternion.LookRotation(dir);
                }

                yield return null;
            }

            if (visualMissile != null) Destroy(visualMissile);

            // Apply explosion/damage if we actually hit them before they died
            if (targetObj != null && !targetStats.IsDead)
            {
                targetStats.TakeDamage(ctx);
                Debug.Log($"[Guided Nanomissiles] Hit {targetObj.name}!");

                if (explosionVfxPrefab != null)
                {
                    // Spawn explosion at target's location
                    Vector3 hitPos = targetObj.transform.position;
                    if (targetObj.TryGetComponent(out Collider col)) hitPos = col.bounds.center;
                    
                    GameObject explosion = Instantiate(explosionVfxPrefab, hitPos, Quaternion.identity);
                    Destroy(explosion, 2f); // cleanup after 2 sec
                }
            }
        }
    }
}