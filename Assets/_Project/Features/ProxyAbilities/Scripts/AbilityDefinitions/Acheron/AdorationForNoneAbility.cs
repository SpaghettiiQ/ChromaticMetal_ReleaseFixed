using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron
{
    [CreateAssetMenu(fileName = "AdorationForNoneAbility", menuName = "Proxy Abilities/Acheron/Adoration for None")]
    public class AdorationForNoneAbility : ScriptableObject, IAbilityEffect
    {
        public float duration = 5f;
        public float damageReductionAmount = 50f;
        [Tooltip("Flat additive bonus to AttackSpeed while planted. AttackSpeed is seeded to 1f " +
                 "and divides fireRate, so 0.5 here = +50% fire rate (final = 1.5x).")]
        public float attackSpeedBonus = 0.5f;

        public void Execute(GameObject instigator)
        {
            if (instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking.AdorationForNoneTracker tracker))
            {
                tracker.ExecuteAbility(duration, damageReductionAmount, attackSpeedBonus);
            }
            else
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking.AdorationForNoneTracker>();
                tracker.ExecuteAbility(duration, damageReductionAmount, attackSpeedBonus);
            }
        }
    }
}