using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Hollow-Point Slugs", fileName = "HollowPointSlugs")]
    public class HollowPointSlugsItem : ItemDefinition, IOnHitItem
    {
        [Tooltip("Target must be above this HP fraction (pre-hit) to take bonus damage.")]
        public float hpThreshold = 0.8f;
        [Tooltip("Bonus damage fraction at 1 stack.")]
        public float bonusFracBase = 0.05f;
        [Tooltip("Additional bonus fraction per stack beyond the first.")]
        public float bonusFracPerStack = 0.025f;

        public void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            if (ctx.AttackType == AttackType.Secondary) return;
            if (ctx.AttackType == AttackType.DamageOverTime) return; // don't proc off burn/bleed ticks
            if (ctx.Target == null) return;
            if (!ctx.Target.TryGetComponent(out CharacterStats targetStats)) return;
            if (targetStats.IsDead) return;

            float maxHp = targetStats.GetStat(StatType.MaxHealth);
            if (maxHp <= 0f) return;

            float preHp = targetStats.CurrentHealth.Value + ctx.Damage;
            if (preHp / maxHp <= hpThreshold) return;

            float frac = bonusFracBase + bonusFracPerStack * (stacks - 1);
            int bonus = Mathf.Max(1, Mathf.RoundToInt(ctx.Damage * frac));

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
