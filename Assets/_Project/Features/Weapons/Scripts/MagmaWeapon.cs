using Unity.Netcode;
using UnityEngine;
using _Project.Core.Audio;
using _Project.Features.WeaponCore.Scripts;
using _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Projectiles;

namespace _Project.Features.Weapons.Scripts
{
    /// <summary>
    /// Acheron's "Magma" Residue Launcher: AmmoMechanic.Overheat, FireMode.Automatic, fires
    /// MagmaBlobProjectiles that inflict poison on hit. Uses the base NetworkWeapon heat
    /// system (CurrentHeat / weaponData.maxHeat / heatPerShot / heatDecayRate) — no duplicate
    /// state. Adoration for None calls SetHeatBypass(true) to fire freely during the channel.
    /// </summary>
    public class MagmaWeapon : NetworkWeapon
    {
        [Header("Projectile Settings")]
        [Tooltip("Magma blob projectile prefab — must have MagmaBlobProjectile attached. " +
                 "Shooting silently no-ops if this is unassigned.")]
        public MagmaBlobProjectile blobPrefab;
        [Tooltip("Forward offset from the camera at which the blob spawns, in metres. Keeps " +
                 "the projectile clear of the player's own collider.")]
        public float spawnForwardOffset = 0.6f;

        [Header("Audio / VFX")]
        public ParticleSystem muzzleFlash;
        public AudioClip fireSound;

        private Camera _cachedCamera;
        private bool _bypassHeat;

        private Camera PlayerCamera
        {
            get
            {
                if (_cachedCamera == null)
                {
                    _cachedCamera = GetComponentInParent<Camera>();
                    if (_cachedCamera == null) _cachedCamera = Camera.main;
                }
                return _cachedCamera;
            }
        }

        /// <summary>
        /// Adoration for None toggles this on for the channel duration: shots fire even when
        /// heat is full, and no further heat is added. When toggled off, the standard overheat
        /// rules resume from whatever CurrentHeat already was (or 0 if forced).
        /// </summary>
        public void SetHeatBypass(bool bypass)
        {
            _bypassHeat = bypass;
            if (bypass)
            {
                // Zero out heat + clear any in-flight cool-off so the weapon is immediately
                // free to fire for the whole channel.
                CurrentHeat = 0f;
                if (IsReloadingOrCooling)
                {
                    // The base FinishOverheatPenalty would normally handle this; cancel its
                    // pending Invoke and unlock manually.
                    CancelInvoke();
                    IsReloadingOrCooling = false;
                }
            }
        }

        protected override void PerformLocalCosmeticShot()
        {
            if (muzzleFlash != null) muzzleFlash.Play();
            if (fireSound != null && SfxManager.Instance != null)
            {
                SfxManager.Instance.PlayOneShot3D(fireSound, transform.position);
            }
        }

        protected override void PerformServerShot()
        {
            if (PlayerCamera == null || blobPrefab == null) return;

            // When bypass is active, undo the heat the base ExecuteShot just added. We can't
            // easily intercept the base's increment, so this no-op-equivalent is the cleanest
            // way to "fire without consuming heat".
            if (_bypassHeat && weaponData.ammoMechanic == _Project.Features.WeaponCore.Enums.AmmoMechanic.Overheat)
            {
                CurrentHeat = Mathf.Max(0f, CurrentHeat - weaponData.heatPerShot);
            }

            Vector3 spawnPos = PlayerCamera.transform.position + PlayerCamera.transform.forward * spawnForwardOffset;
            Quaternion spawnRot = Quaternion.LookRotation(PlayerCamera.transform.forward);

            var projectile = Instantiate(blobPrefab, spawnPos, spawnRot);

            if (projectile.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
            }

            projectile.Initialize(transform.root.gameObject, PlayerCamera.transform.forward);
        }
    }
}
