using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Explosive Rounds", fileName = "ExplosiveRounds")]
    public class ExplosiveRoundsItem : ItemDefinition, IOnHitItem
    {
        [Tooltip("Chance to trigger delayed explosion (0-1)")]
        public float procChance = 0.2f;
        public float delay = 1f;
        public float baseDamage = 10f;

        [Header("VFX")]
        public GameObject stickyVfxPrefab;
        public GameObject explosionVfxPrefab;

        public void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            // Proc convention: don't re-proc off our own delayed explosion (Effect→Secondary path)
            // or off DOT ticks — otherwise the explosion's OnHit re-rolls this and DOTs rain bombs.
            if (ctx.AttackType == _Project.Core.Enums.AttackType.Secondary) return;
            if (ctx.AttackType == _Project.Core.Enums.AttackType.DamageOverTime) return;

            float bonusChance = 0f;
            float multiplier = 1f;

            if (owner.TryGetComponent(out _Project.Core.Stats.CharacterStats ownerStats))
            {
                bonusChance = ownerStats.GetStat(_Project.Core.Enums.StatType.ProcChanceFlatAdd);
                multiplier = ownerStats.GetStat(_Project.Core.Enums.StatType.ProcChanceMultiplier);
                if (multiplier == 0f) multiplier = 1f;
            }

            float finalChance = (procChance * multiplier) + bonusChance;

            if (Random.value <= finalChance)
            {
                Debug.Log($"[Explosive Rounds] Attached sticky explosive to the target! Will deal {baseDamage * stacks} damage in {delay}s.");
                if (owner.TryGetComponent(out MonoBehaviour runner))
                {
                    runner.StartCoroutine(ExplosionRoutine(ctx.Target, Mathf.RoundToInt(baseDamage * stacks), ctx));
                }
            }
        }

        private System.Collections.IEnumerator ExplosionRoutine(GameObject target, int damage, DamageContext originalCtx)
        {
            GameObject stickyVisual = null;
            if (target != null && stickyVfxPrefab != null)
            {
                stickyVisual = Instantiate(stickyVfxPrefab, target.transform.position, Quaternion.identity, target.transform);
            }

            yield return new WaitForSeconds(delay);

            if (stickyVisual != null) Destroy(stickyVisual);

            if (target != null && target.TryGetComponent(out _Project.Core.Stats.CharacterStats targetStats) && !targetStats.IsDead)
            {
                var explosionCtx = new DamageContext
                {
                    Source = originalCtx.Source,
                    Target = target,
                    Damage = damage,
                    DamageType = _Project.Core.Enums.DamageType.Explosive,
                    // Secondary (was Effect): item follow-up damage per convention, so the top-of-
                    // OnHitEnemy guard catches it and it won't chain into other proc items.
                    AttackType = _Project.Core.Enums.AttackType.Secondary,
                    IsCritical = false
                };
                targetStats.TakeDamage(explosionCtx);
                Debug.Log("[Explosive Rounds] Exploded!");

                if (explosionVfxPrefab != null)
                {
                    Vector3 hitPos = target.transform.position;
                    if (target.TryGetComponent(out Collider col)) hitPos = col.bounds.center;

                    GameObject boom = Instantiate(explosionVfxPrefab, hitPos, Quaternion.identity);
                    Destroy(boom, 2f);
                }
            }
        }
    }
}