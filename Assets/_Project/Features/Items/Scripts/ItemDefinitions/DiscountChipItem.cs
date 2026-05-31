using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Discount Chip", fileName = "DiscountChip")]
    public class DiscountChipItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Interactable cost reduction per stack, stacked MULTIPLICATIVELY (0.05 = 5% " +
                 "cheaper/stack → cost × 0.95^stacks). Read in NetworkChest / ExtractionTerminal.")]
        public float costReductionPerStack = 0.05f;

        // ContainerCostMultiplier is seeded at 1 (base). Adding this Flat modifier makes
        // GetStat = (1 + (0.95^stacks - 1)) = 0.95^stacks — multiplicative, not additive.
        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
                stats.AddModifier(StatType.ContainerCostMultiplier, new StatModifier(StatModType.Flat, CostFactor(stacks) - 1f));
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
                stats.RemoveModifier(StatType.ContainerCostMultiplier, new StatModifier(StatModType.Flat, CostFactor(stacks) - 1f));
        }

        private float CostFactor(int stacks) => Mathf.Pow(1f - costReductionPerStack, stacks); // 0.95^stacks
    }
}
