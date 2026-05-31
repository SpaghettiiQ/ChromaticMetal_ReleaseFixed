using UnityEngine;
using Unity.Netcode;
using _Project.Core.Interfaces;
using _Project.Features.Weapons.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos
{
    /// <summary>
    /// Thanatos's secondary: fires both Everblack shotguns in a tight-spread overdrive blast.
    /// The blast tops the weapon's heat off, which triggers the standard overheat path on
    /// TheEverblackWeapon — ejecting the heat-core grenade from the eject point and starting
    /// the cool-off animation. So this ability is effectively just "force the overheat finale".
    /// </summary>
    [CreateAssetMenu(fileName = "ToTheHellfireAbility", menuName = "Proxy Abilities/Thanatos/To The Hellfire")]
    public class ToTheHellfireAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Hellfire Blast")]
        [Tooltip("Pellets fired from EACH barrel (so 2 × this many pellets total).")]
        public int pelletsPerBarrel = 10;
        [Tooltip("Damage per pellet. Heavy damage at point-blank, falls off via the weapon's range.")]
        public int damagePerPellet = 35;
        [Tooltip("Spread cone in degrees. Tighter than standard fire so the blast lands as a slug.")]
        public float spreadAngle = 1.5f;
        [Tooltip("Hitstop duration in seconds applied to the caster when the blast actually " +
                 "connects with at least one enemy. Set to 0 to disable.")]
        public float hitStopDuration = 0.14f;

        public bool CanBeUsed(GameObject instigator)
        {
            // Block the ability while the weapon is mid cool-off (heat core ejected and dipping
            // off-screen). Without this guard, a second cast during cool-off would try to fire
            // an already-overheated weapon and double-trigger TriggerHeatOverflow.
            var weapon = instigator.GetComponentInChildren<TheEverblackWeapon>();
            if (weapon != null && weapon.IsReloading()) return false;
            return true;
        }

        public void Execute(GameObject instigator)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            var weapon = instigator.GetComponentInChildren<TheEverblackWeapon>();
            if (weapon == null)
            {
                Debug.LogWarning("[ToTheHellfire] Instigator has no TheEverblackWeapon equipped.");
                return;
            }

            // Server-side re-check: the AbilityController predicts CanBeUsed on the owner before
            // the ServerRpc, but a latent state change (e.g. weapon overheated naturally between
            // press and arrival) could let a stale cast through. Bail authoritative.
            if (weapon.IsReloading()) return;

            bool connected = weapon.HellfireBlast(pelletsPerBarrel, spreadAngle, damagePerPellet);

            // Hit-stop the caster only if the blast actually landed on something, mirroring the
            // Cleansing Strike pattern. ApplyHitStopClientRpc is IsOwner-gated so only Thanatos's
            // own view freezes — bystanders don't get a frozen frame from someone else's hit.
            if (connected && hitStopDuration > 0f)
            {
                if (instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityController abilityController))
                {
                    abilityController.ApplyHitStopClientRpc(hitStopDuration);
                }
                else
                {
                    _Project.Core.Managers.HitStopManager.Instance.TriggerHitStop(hitStopDuration);
                }
            }
        }
    }
}
