using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Thermite Fuel", fileName = "ThermiteFuel")]
    public class ThermiteFuelItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Added fire damage per stack, as a Percent multiplier. 1.0 = +100% " +
                 "(×2 fire damage at 1 stack, ×3 at 2 stacks...).")]
        public float bonusPerStack = 1.0f;

        // Amplifies fire damage at the source via StatType.FireDamageMultiplier, applied centrally
        // in CharacterStats.TakeDamage. Percent modifiers sum, so 1.0/stack gives ×(1+stacks):
        // (base 1) * (1 + bonusPerStack*stacks).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
                stats.AddModifier(StatType.FireDamageMultiplier, new StatModifier(StatModType.Percent, bonusPerStack * stacks));
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
                stats.RemoveModifier(StatType.FireDamageMultiplier, new StatModifier(StatModType.Percent, bonusPerStack * stacks));
        }
    }
}
