using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Credit Chip", fileName = "CreditChip")]
    public class CreditChipItem : ItemDefinition, IPassiveItem
    {
        public float multiplierBonus = 0.5f; // 1.5x at 1 stack

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                stats.AddModifier(StatType.MoneyMultiplier, new StatModifier(StatModType.Flat, multiplierBonus * stacks));
                Debug.Log($"[Credit Chip] Granted +{multiplierBonus * stacks * 100}% extra credits.");
            }
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                stats.RemoveModifier(StatType.MoneyMultiplier, new StatModifier(StatModType.Flat, multiplierBonus * stacks));
                Debug.Log($"[Credit Chip] Removed extra credit modifier.");
            }
        }
    }
}