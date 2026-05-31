using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Military Frame", fileName = "MilitaryFrame")]
    public class MilitaryFrameItem : ItemDefinition, IPassiveItem
    {
        public float hpBonusPerStack = 50f;
        public int specialChargeBonusPerStack = 1;

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                stats.AddModifier(StatType.MaxHealth, new StatModifier(StatModType.Flat, hpBonusPerStack * stacks));
                stats.AddModifier(StatType.SpecialCharges, new StatModifier(StatModType.Flat, specialChargeBonusPerStack * stacks));
                stats.Heal(Mathf.RoundToInt(hpBonusPerStack * stacks));
                Debug.Log($"[Military Frame] Granted +{hpBonusPerStack * stacks} Max HP and +{specialChargeBonusPerStack * stacks} Special Charges.");
            }
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                stats.RemoveModifier(StatType.MaxHealth, new StatModifier(StatModType.Flat, hpBonusPerStack * stacks));
                stats.RemoveModifier(StatType.SpecialCharges, new StatModifier(StatModType.Flat, specialChargeBonusPerStack * stacks));
                Debug.Log($"[Military Frame] Removed frame bonuses.");
            }
        }
    }
}