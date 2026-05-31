using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    /// <summary>
    /// Targeting Optics — +10% crit chance per stack up to a cap (default 10 stacks = 100%).
    /// Every stack past the cap converts into +1% crit damage instead, so the item never
    /// becomes dead weight once you hit guaranteed-crit.
    /// </summary>
    [CreateAssetMenu(menuName = "Game Data/Items/Targeting Optics", fileName = "TargetingOptics")]
    public class TargetingOpticsItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Crit chance added per stack while below the cap. 0.10 = +10%.")]
        public float critChancePerStack = 0.10f;

        [Tooltip("Stacks of crit chance after which excess stacks convert to crit damage.")]
        public int stacksUntilDamageBonus = 10;

        [Tooltip("Crit damage multiplier added per excess stack. 0.01 = +1% crit damage.")]
        public float critDamagePerExcessStack = 0.01f;

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (!owner.TryGetComponent(out CharacterStats stats)) return;

            var (chance, damage) = SplitStacks(stacks);
            if (chance > 0f)
            {
                stats.AddModifier(StatType.CritChance, new StatModifier(StatModType.Flat, chance));
            }
            if (damage > 0f)
            {
                stats.AddModifier(StatType.CritDamageMultiplier, new StatModifier(StatModType.Flat, damage));
            }

            Debug.Log($"[Targeting Optics] +{chance * 100f:0.#}% crit chance, +{damage * 100f:0.#}% crit damage ({stacks} stacks)");
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (!owner.TryGetComponent(out CharacterStats stats)) return;

            var (chance, damage) = SplitStacks(stacks);
            if (chance > 0f)
            {
                stats.RemoveModifier(StatType.CritChance, new StatModifier(StatModType.Flat, chance));
            }
            if (damage > 0f)
            {
                stats.RemoveModifier(StatType.CritDamageMultiplier, new StatModifier(StatModType.Flat, damage));
            }
        }

        private (float chance, float damage) SplitStacks(int stacks)
        {
            int chanceStacks = Mathf.Min(stacks, stacksUntilDamageBonus);
            int damageStacks = Mathf.Max(0, stacks - stacksUntilDamageBonus);
            return (chanceStacks * critChancePerStack, damageStacks * critDamagePerExcessStack);
        }
    }
}
