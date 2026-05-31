using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit
{
    [CreateAssetMenu(fileName = "BloodthirsterAbility", menuName = "Proxy Abilities/Conduit/Bloodthirster")]
    public class BloodthirsterAbility : ScriptableObject, IAbilityEffect
    {
        public bool DeferCooldownOnCast(GameObject instigator) => true;

        // Disabled while the empowered magazine is still active — block re-casting until it's spent
        // and the deferred cooldown starts (mirrors Bulwark Advance's mid-air gate). Checked by both
        // the client prediction (TryUseAbility) and the server (RequestUseAbilityServerRpc).
        public bool CanBeUsed(GameObject instigator)
        {
            var weapon = instigator.GetComponentInChildren<_Project.Features.Weapons.Scripts.RadianceWeapon>();
            return weapon == null || !weapon.IsBloodthirsterActive;
        }

        public void Execute(GameObject instigator)
        {
            if (!instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking.BloodthirsterTracker tracker))
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking.BloodthirsterTracker>();
            }
            tracker.ExecuteAbility();
        }
    }
}