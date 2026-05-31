using System.Collections;
using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Player.Scripts;
using _Project.Features.WeaponCore.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Ensign
{
    [CreateAssetMenu(fileName = "CombatSlide", menuName = "Proxy Abilities/Ensign/Combat Slide")]
    public class CombatSlideAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Slide Settings")]
        [SerializeField] private float slideForce = 15f;
        [SerializeField] private float slideDuration = 0.8f;
        [Tooltip("Reload-speed multiplier when the slide triggers a reload. 1.3 = 30% faster.")]
        [SerializeField] private float reloadSpeedMultiplier = 1.3f;

        public void Execute(GameObject instigator)
        {
            if (instigator.TryGetComponent(out PlayerMovement movement)
                && instigator.TryGetComponent(out MonoBehaviour runner))
            {
                runner.StartCoroutine(SlideRoutine(movement));
            }

            // Tactical reload: slide triggers reload regardless of ammo, slightly faster.
            // StartReload no-ops at full mag and is a no-op for non-magazine weapons.
            NetworkWeapon weapon = instigator.GetComponentInChildren<NetworkWeapon>();
            if (weapon != null && !weapon.IsReloading())
            {
                weapon.StartReload(reloadSpeedMultiplier);
            }

            Debug.Log($"[{nameof(CombatSlideAbility)}] {instigator.name} used Combat Slide!");
        }

        private IEnumerator SlideRoutine(PlayerMovement movement)
        {
            // Burst of forward velocity, replacing any prior horizontal motion.
            Vector3 burst = movement.transform.forward * slideForce;
            movement.ApplyForce(burst, true);

            movement.SetSliding(true);
            yield return new WaitForSeconds(slideDuration);
            movement.SetSliding(false);
        }
    }
}
