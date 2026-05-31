using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Armor Vest", fileName = "ArmorVest")]
    public class ArmorVestItem : ItemDefinition, IPassiveItem
    {
        public float damageReductionPerStack = 5f;

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                stats.AddModifier(StatType.DamageReduction, new StatModifier(StatModType.Flat, damageReductionPerStack * stacks));
            }
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                stats.RemoveModifier(StatType.DamageReduction, new StatModifier(StatModType.Flat, damageReductionPerStack * stacks));
            }
        }
    }
}