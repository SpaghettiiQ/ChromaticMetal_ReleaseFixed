using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron
{
    [CreateAssetMenu(fileName = "OnlyPainAbility", menuName = "Proxy Abilities/Acheron/Only Pain")]
    public class OnlyPainAbility : ScriptableObject, IAbilityEffect
    {
        [Tooltip("Amount of damage reduction granted per 1 unit of Burden.")]
        public float damageReductionPerBurden = 0.5f;

        public void Execute(GameObject instigator)
        {
            if (!instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking.OnlyPainTracker tracker))
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking.OnlyPainTracker>();
            }
            tracker.Initialize(damageReductionPerBurden);
        }
    }
}