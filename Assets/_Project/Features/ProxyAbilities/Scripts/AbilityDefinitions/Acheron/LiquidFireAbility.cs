using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron
{
    [CreateAssetMenu(fileName = "LiquidFireAbility", menuName = "Proxy Abilities/Acheron/Liquid Fire")]
    public class LiquidFireAbility : ScriptableObject, IAbilityEffect
    {
        public _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking.LiquidFireHitbox hitboxPrefab;
        
        [Header("Scaling")]
        public float baseDamagePerTick = 5f;
        public float damagePerBurdenConsumed = 1f;

        public void Execute(GameObject instigator)
        {
            if (instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking.LiquidFireTracker tracker))
            {
                tracker.ExecuteAbility(hitboxPrefab, baseDamagePerTick, damagePerBurdenConsumed);
            }
            else
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking.LiquidFireTracker>();
                tracker.ExecuteAbility(hitboxPrefab, baseDamagePerTick, damagePerBurdenConsumed);
            }
        }
    }
}