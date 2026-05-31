using System.Collections;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Hemorrhaging Optics", fileName = "HemorrhagingOptics")]
    public class HemorrhagingOpticsItem : ItemDefinition, IOnHitItem, IPassiveItem
    {
        [Tooltip("Crit chance granted per stack (0.05 = +5%).")]
        public float critChancePerStack = 0.05f;

        [Header("Bleed")]
        [Tooltip("Bleed damage per second at 1 stack.")]
        public float bleedDamagePerSecond = 6f;
        [Tooltip("Extra bleed DPS per stack beyond the first.")]
        public float bleedDamagePerStack = 3f;
        [Tooltip("Bleed duration in seconds.")]
        public float duration = 4f;

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (!owner.TryGetComponent(out CharacterStats stats)) return;
            float chance = critChancePerStack * stacks;
            if (chance > 0f) stats.AddModifier(StatType.CritChance, new StatModifier(StatModType.Flat, chance));
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (!owner.TryGetComponent(out CharacterStats stats)) return;
            float chance = critChancePerStack * stacks;
            if (chance > 0f) stats.RemoveModifier(StatType.CritChance, new StatModifier(StatModType.Flat, chance));
        }

        public void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            if (!ctx.IsCritical) return;                                   // only crits bleed
            if (ctx.AttackType == AttackType.DamageOverTime) return;       // don't bleed off DOT ticks
            if (ctx.AttackType == AttackType.Secondary) return;           // nor off item-proc follow-ups
            if (ctx.Target == null) return;
            if (!ctx.Target.TryGetComponent(out CharacterStats targetStats) || targetStats.IsDead) return;

            if (owner.TryGetComponent(out MonoBehaviour runner))
                runner.StartCoroutine(BleedRoutine(owner, ctx.Target, stacks));
        }

        // Bleed = DamageType.Bleed (mitigated normally, leaks Shield) tagged DamageOverTime so it
        // doesn't cascade procs and can't crit. Mirrors VibrobladesItem.
        private IEnumerator BleedRoutine(GameObject owner, GameObject target, int stacks)
        {
            float elapsed = 0f;
            int dps = Mathf.Max(1, Mathf.RoundToInt(bleedDamagePerSecond + bleedDamagePerStack * (stacks - 1)));
            while (elapsed < duration)
            {
                yield return new WaitForSeconds(1f);
                elapsed += 1f;

                if (target == null || !target.TryGetComponent(out CharacterStats targetStats) || targetStats.IsDead)
                    break;

                targetStats.TakeDamage(new DamageContext
                {
                    Source = owner,
                    Target = target,
                    Damage = dps,
                    DamageType = DamageType.Bleed,
                    AttackType = AttackType.DamageOverTime,
                    IsCritical = false
                });
            }
        }
    }
}
