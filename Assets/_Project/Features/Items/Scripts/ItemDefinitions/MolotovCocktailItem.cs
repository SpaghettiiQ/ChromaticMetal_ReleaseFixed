using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Molotov Cocktail", fileName = "MolotovCocktail")]
    public class MolotovCocktailItem : ItemDefinition, IOnHitItem
    {
        [Tooltip("Chance to ignite (0-1)")]
        public float procChance = 0.5f;
        public float burnDamagePerSecond = 5f;
        public float duration = 3f;

        [Header("VFX")]
        public GameObject fireVfxPrefab;

        public void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            // Proc convention (was previously missing): don't re-ignite off our own burn ticks
            // (DamageOverTime) — without this the burn DOT raises OnHit each tick and Molotov
            // re-rolls ignite → self-cascading fires — nor off item-proc follow-ups (Secondary).
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
                Debug.Log($"[Molotov Cocktail] Ignited target! Doing {burnDamagePerSecond * stacks} DPS for {duration} seconds.");
                
                if (owner.TryGetComponent(out MonoBehaviour runner))
                {
                    runner.StartCoroutine(BurnRoutine(ctx.Target, stacks, ctx));
                }
            }
        }

        private System.Collections.IEnumerator BurnRoutine(GameObject target, int stacks, DamageContext originalCtx)
        {
            GameObject activeVfx = null;
            if (target != null && fireVfxPrefab != null)
            {
                // Attach visual fire to the target
                activeVfx = Instantiate(fireVfxPrefab, target.transform.position, Quaternion.identity, target.transform);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return new WaitForSeconds(1f);
                elapsed += 1f;

                if (target != null && target.TryGetComponent(out _Project.Core.Stats.CharacterStats targetStats) && !targetStats.IsDead)
                {
                    var burnCtx = new DamageContext
                    {
                        Source = originalCtx.Source,
                        Target = target,
                        Damage = Mathf.RoundToInt(burnDamagePerSecond * stacks),
                        DamageType = _Project.Core.Enums.DamageType.Fire,
                        AttackType = _Project.Core.Enums.AttackType.DamageOverTime,
                        IsCritical = false
                    };
                    targetStats.TakeDamage(burnCtx);
                }
                else
                {
                    break;
                }
            }

            if (activeVfx != null)
            {
                Destroy(activeVfx);
            }
        }
    }
}