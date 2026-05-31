using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron
{
    [CreateAssetMenu(fileName = "TheWayOfAllFleshAbility", menuName = "Proxy Abilities/Acheron/The Way of All Flesh")]
    public class TheWayOfAllFleshAbility : ScriptableObject, IAbilityEffect
    {
        public _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Projectiles.FleshDenseProjectile projectilePrefab;

        public bool CanBeUsed(GameObject instigator)
        {
            // Block while Liquid Fire is channeling — the flame ult already commits the
            // caster to a sustained-burden expulsion; firing the burden-projectile mid-channel
            // would feel chaotic and skip the cooldown for free.
            if (instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking.LiquidFireTracker tracker)
                && tracker.IsChanneling)
            {
                return false;
            }
            return true;
        }

        public void Execute(GameObject instigator)
        {
            if (instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking.TheWayOfAllFleshTracker tracker))
            {
                tracker.ExecuteAbility(projectilePrefab);
            }
            else
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking.TheWayOfAllFleshTracker>();
                tracker.ExecuteAbility(projectilePrefab);
            }
        }
    }
}
