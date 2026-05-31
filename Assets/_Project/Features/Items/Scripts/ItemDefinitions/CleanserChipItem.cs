using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.BurdenSystem.Scripts;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Cleanser Chip", fileName = "CleanserChip")]
    public class CleanserChipItem : ItemDefinition, IPassiveItem, IOnHitItem
    {
        [Tooltip("Reduction applied to burden AccumulationMultiplier per stack")]
        public float burdenResistanceIncrease = 0.2f;
        [Tooltip("EnemyData.enemyName of the archetype this chip deals bonus damage to (a 'weaker Angel').")]
        public string targetEnemyName = "Lesser Seer";
        [Tooltip("Bonus damage fraction per stack against the target archetype (0.5 = +50% per stack).")]
        public float extraDamageToAngelsMultiplier = 0.5f;

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out BurdenController burden))
            {
                burden.AccumulationMultiplier = Mathf.Max(0.1f, 1f - (burdenResistanceIncrease * stacks));
                Debug.Log($"[Cleanser Chip] Burden accumulation reduced to {burden.AccumulationMultiplier * 100f:F0}%.");
            }
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out BurdenController burden))
            {
                burden.AccumulationMultiplier = 1f;
            }
        }

        public void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            // Bonus damage against a specific enemy archetype (the Lesser Seer — a "weaker Angel").
            if (ctx.AttackType == AttackType.Secondary) return;      // don't recurse off our own bonus hit
            if (ctx.AttackType == AttackType.DamageOverTime) return; // don't proc off burn/bleed ticks
            if (ctx.Target == null) return;

            // Match on archetype via the Core IEnemyIdentity contract — no Enemies-feature dependency.
            var identity = ctx.Target.GetComponentInParent<IEnemyIdentity>();
            if (identity == null || identity.EnemyTypeName != targetEnemyName) return;

            if (!ctx.Target.TryGetComponent(out CharacterStats targetStats)) return;
            if (targetStats.IsDead) return;

            int bonus = Mathf.Max(1, Mathf.RoundToInt(ctx.Damage * extraDamageToAngelsMultiplier * stacks));
            var bonusCtx = new DamageContext
            {
                Source = owner,
                Target = ctx.Target,
                Damage = bonus,
                DamageType = ctx.DamageType,
                AttackType = AttackType.Secondary,
                IsCritical = false
            };
            targetStats.TakeDamage(bonusCtx);
        }
    }
}