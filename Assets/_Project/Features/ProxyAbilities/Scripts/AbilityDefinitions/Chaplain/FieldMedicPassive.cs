using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Stats;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Chaplain
{
    [CreateAssetMenu(fileName = "FieldMedic", menuName = "Proxy Abilities/Chaplain/Field Medic (Passive)")]
    public class FieldMedicPassive : ScriptableObject, IAbilityEffect
    {
        [Header("Medic Settings")]
        [SerializeField] private float maxHealBonusMultiplier = 1.5f; // Up to +50% extra healing on a 1-hp ally.

        public void Execute(GameObject instigator)
        {
            if (instigator.GetComponent<FieldMedicPassiveTracker>() == null)
            {
                var tracker = instigator.AddComponent<FieldMedicPassiveTracker>();
                tracker.Initialize(maxHealBonusMultiplier);
            }
        }
    }

    public class FieldMedicPassiveTracker : MonoBehaviour
    {
        private float _maxHealBonusMultiplier;

        public void Initialize(float maxHealBonus)
        {
            _maxHealBonusMultiplier = maxHealBonus;
        }

        public int CalculateModifiedHeal(CharacterStats target, int baseAmount)
        {
            // Healing scales up the more wounded the ally is. At full HP: 1.0x. At ~0 HP: maxHealBonusMultiplier.
            float currentHealth = target.CurrentHealth.Value;
            float maxHealth = target.GetStat(_Project.Core.Enums.StatType.MaxHealth);

            float healthPercent = Mathf.Clamp01(currentHealth / Mathf.Max(1, maxHealth));
            float missingHealthPercent = 1f - healthPercent;

            float runMultiplier = 1f + (missingHealthPercent * (_maxHealBonusMultiplier - 1f));

            return Mathf.FloorToInt(baseAmount * runMultiplier);
        }
    }
}
