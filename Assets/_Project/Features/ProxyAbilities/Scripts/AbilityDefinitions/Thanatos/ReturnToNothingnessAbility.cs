using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos
{
    [CreateAssetMenu(fileName = "ReturnToNothingnessAbility", menuName = "Proxy Abilities/Thanatos/Return To Nothingness")]
    public class ReturnToNothingnessAbility : ScriptableObject, IAbilityEffect
    {
        [Tooltip("The flat amount of cooldown reduction in seconds when taking damage.")]
        public float cooldownReductionAmount = 1.0f;

        public void Execute(GameObject instigator)
        {
            if (!instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Tracking.ReturnToNothingnessTracker tracker))
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Tracking.ReturnToNothingnessTracker>();
            }
            tracker.Initialize(cooldownReductionAmount);
        }
    }
}