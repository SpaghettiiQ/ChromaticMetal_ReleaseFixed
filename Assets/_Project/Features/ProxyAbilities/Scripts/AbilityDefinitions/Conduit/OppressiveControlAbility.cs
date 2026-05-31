using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit
{
    [CreateAssetMenu(fileName = "OppressiveControlAbility", menuName = "Proxy Abilities/Conduit/Oppressive Control")]
    public class OppressiveControlAbility : ScriptableObject, IAbilityEffect
    {
        public _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Projectiles.PresenceRift riftPrefab;
        public float selfKnockbackForce = 15f;
        public float burdenRemovedOnUse = 30f;
        public float maxCastRange = 40f;

        public void Execute(GameObject instigator)
        {
            if (!instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking.OppressiveControlTracker tracker))
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking.OppressiveControlTracker>();
            }
            tracker.ExecuteAbility(riftPrefab, selfKnockbackForce, burdenRemovedOnUse, maxCastRange);
        }
    }
}