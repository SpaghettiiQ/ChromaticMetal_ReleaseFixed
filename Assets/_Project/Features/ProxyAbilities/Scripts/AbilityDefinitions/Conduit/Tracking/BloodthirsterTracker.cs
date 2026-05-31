using UnityEngine;
using Unity.Netcode;
using _Project.Features.Weapons.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking
{
    /// <summary>
    /// Forwards the Bloodthirster ability to the Radiance weapon. The actual shot count is
    /// configured on RadianceWeapon.bloodthirsterShotCount so the weapon's effective Magazine
    /// max matches without round-tripping a parameter.
    /// </summary>
    public class BloodthirsterTracker : MonoBehaviour
    {
        public void ExecuteAbility()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            RadianceWeapon weapon = GetComponentInChildren<RadianceWeapon>();
            if (weapon == null) return;

            weapon.ActivateBloodthirster();
        }
    }
}
