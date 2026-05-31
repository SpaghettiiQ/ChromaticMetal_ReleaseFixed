using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit
{
    [CreateAssetMenu(fileName = "DolorAbility", menuName = "Proxy Abilities/Conduit/Dolor")]
    public class DolorAbility : ScriptableObject, IAbilityEffect
    {
        public float burdenMultiplier = 1.5f;

        public void Execute(GameObject instigator)
        {
            if (!instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking.DolorTracker tracker))
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking.DolorTracker>();
            }
            tracker.Initialize(burdenMultiplier);
        }
    }
}