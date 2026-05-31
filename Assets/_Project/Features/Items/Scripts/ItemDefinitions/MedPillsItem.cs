using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Med Pills", fileName = "MedPills")]
    public class MedPillsItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Flat Max HP granted per stack. Also healed on apply (mirrors Military Frame).")]
        public float hpPerStack = 30f;

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                stats.AddModifier(StatType.MaxHealth, new StatModifier(StatModType.Flat, hpPerStack * stacks));
                stats.Heal(Mathf.RoundToInt(hpPerStack * stacks));
            }
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                stats.RemoveModifier(StatType.MaxHealth, new StatModifier(StatModType.Flat, hpPerStack * stacks));
            }
        }
    }
}
