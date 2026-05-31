using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Events;
using _Project.Core.Structs;
using _Project.Features.BurdenSystem.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Centurion
{
    [CreateAssetMenu(fileName = "ContaminationShielding", menuName = "Proxy Abilities/Centurion/Contamination Shielding")]
    public class ContaminationShieldingPassive : ScriptableObject, IAbilityEffect
    {
        [Header("Passive Settings")]
        [SerializeField] private float accumulationMultiplier = 0.5f; // Accumulates 50% slower
        [SerializeField] private float damageReductionPerBurdenPercent = 0.2f; // e.g. Max 20% reduction if enemy has 100% burden

        public void Execute(GameObject instigator)
        {
            if (instigator.GetComponent<ContaminationShieldingTracker>() == null)
            {
                var tracker = instigator.AddComponent<ContaminationShieldingTracker>();
                tracker.Initialize(accumulationMultiplier, damageReductionPerBurdenPercent);
            }
        }
    }

    public class ContaminationShieldingTracker : MonoBehaviour
    {
        private CharacterEventBus _eventBus;
        private BurdenController _burdenController;
        private float _accumulationMultiplier;
        private float _damageReductionPerBurdenPercent;

        public void Initialize(float accumulationMultiplier, float damageReductionPerBurdenPercent)
        {
            _accumulationMultiplier = accumulationMultiplier;
            _damageReductionPerBurdenPercent = damageReductionPerBurdenPercent;

            _eventBus = GetComponent<CharacterEventBus>();
            _burdenController = GetComponent<BurdenController>();

            if (_burdenController != null)
            {
                _burdenController.AccumulationMultiplier *= _accumulationMultiplier;
            }

            if (_eventBus != null)
            {
                _eventBus.OnBeforeTakeDamage += HandleBeforeTakeDamage;
            }
        }

        private void HandleBeforeTakeDamage(ref DamageContext ctx)
        {
            // True damage ignores all mitigation (matches the DamageReduction-stat bypass in
            // CharacterStats.TakeDamage). This passive reduces ctx.Damage directly, so it needs
            // its own guard.
            if (ctx.DamageType == _Project.Core.Enums.DamageType.True) return;

            if (ctx.Source != null && ctx.Source.TryGetComponent(out BurdenController attackerBurden))
            {
                float burdenLevel = attackerBurden.GetCurrentBurden();
                // Max burden is effectively 100+ but let's clamp percentage to 100% for calculation
                float burdenPercent = Mathf.Clamp01(burdenLevel / 100f);
                
                // Reduce incoming damage based on attacker's burden (e.g. up to 20% reduction)
                float reductionMultiplier = 1f - (burdenPercent * _damageReductionPerBurdenPercent);
                ctx.Damage = Mathf.Max(1, Mathf.RoundToInt(ctx.Damage * reductionMultiplier));
            }
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
            {
                _eventBus.OnBeforeTakeDamage -= HandleBeforeTakeDamage;
            }
            if (_burdenController != null)
            {
                _burdenController.AccumulationMultiplier /= _accumulationMultiplier;
            }
        }
    }
}
