using Unity.Netcode;
using UnityEngine;
using _Project.Core.Audio;
using _Project.Core.Interfaces;
using _Project.Features.WeaponCore.Data;
using _Project.Features.WeaponCore.Enums;
using _Project.Features.Player.Scripts;

namespace _Project.Features.WeaponCore.Scripts
{
    public abstract class NetworkWeapon : NetworkBehaviour, IWeapon
    {
        [Header("Configuration")]
        public WeaponData weaponData;

        // IWeapon — exposes weapon data through Core so Features (e.g. Thunderhead item) can read
        // BaseDamage without depending on WeaponCore directly.
        public IWeaponData WeaponData => weaponData;
        
        [Header("Weapon Transforms")]
        [Tooltip("Where the muzzle flashes should spawn and orient")]
        public Transform muzzlePoint;
        [Tooltip("Where casings should eject from")]
        public Transform ejectPoint;

        // Local State Tracking (Client-Side Prediction)
        protected int CurrentAmmo;
        protected float CurrentHeat;
        protected bool IsReloadingOrCooling;
        protected float NextAllowedFireTime;
        protected float CurrentChargeTimer;

        /// <summary>
        /// External fire-lock toggled by abilities that need to suppress shooting for their
        /// duration (e.g. Liquid Fire channel). Independent of IsReloadingOrCooling so it
        /// doesn't fight the reload/overheat state machine.
        /// </summary>
        public bool FireLocked;

        // Input Tracking
        protected bool IsInputActive;
        protected bool HasFiredThisClick; // Used for Semi-Auto
        
        // Cache EventBus pointer
        private _Project.Core.Events.CharacterEventBus _eventBus;
        
        private _Project.Core.Events.CharacterEventBus EventBus
        {
            get
            {
                if (_eventBus == null)
                {
                    _eventBus = transform.root.GetComponentInChildren<_Project.Core.Events.CharacterEventBus>();
                }
                return _eventBus;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (weaponData != null)
            {
                CurrentAmmo = weaponData.maxAmmo;
                CurrentHeat = 0f;
            }
            // Will look in parents for the Event Bus (Assuming Weapon is parented to Player capsule eventually)
            _eventBus = transform.root.GetComponentInChildren<_Project.Core.Events.CharacterEventBus>();
        }

        // --- INPUT HANDLING ---
        public void HandleInput(bool isFireInputActive)
        {
            if (!IsOwner || weaponData == null) return;

            IsInputActive = isFireInputActive;

            // Reset Semi-Auto lock when player releases the trigger
            if (!IsInputActive)
            {
                HasFiredThisClick = false;
                
                // Charge weapon released — subclass decides whether to fire-with-partial-charge
                // or just cancel. Default behavior (defined below) is to cancel with no shot,
                // preserving the original "must fully charge to fire" semantics.
                if (weaponData.fireMode == FireMode.Charge && CurrentChargeTimer > 0)
                {
                    float chargeFraction = Mathf.Clamp01(CurrentChargeTimer / Mathf.Max(0.0001f, EffectiveChargeTime));
                    HandleChargeReleased(chargeFraction);
                }
            }
        }

        /// <summary>
        /// Per-instance override point for Charge weapons that need to fire on early release
        /// (e.g. Radiance's beam, where a partial charge produces a weaker shot). Default
        /// behavior just cancels the charge with no fire.
        /// </summary>
        protected virtual void HandleChargeReleased(float chargeFraction)
        {
            CancelCharge();
        }

        /// <summary>
        /// Effective charge time used by ProcessCharging. Override to dynamically shorten or
        /// remove the charge requirement (e.g. Bloodthirster setting it to 0 to fire instantly
        /// without mutating the shared weaponData SO).
        /// </summary>
        protected virtual float EffectiveChargeTime => weaponData != null ? weaponData.chargeTime : 0f;

        /// <summary>
        /// Per-instance override of the ammo mechanic — abilities like Bloodthirster can
        /// temporarily flip an Overheat weapon to behave like a Magazine without mutating the
        /// shared weaponData. UI binders should read this instead of weaponData.ammoMechanic.
        /// </summary>
        public virtual AmmoMechanic EffectiveAmmoMechanic => weaponData != null ? weaponData.ammoMechanic : AmmoMechanic.Infinite;

        /// <summary>
        /// Per-instance override of max ammo for the same reason as EffectiveAmmoMechanic.
        /// UI binders should display CurrentAmmo / EffectiveMaxAmmo.
        /// </summary>
        public virtual int EffectiveMaxAmmo => weaponData != null ? weaponData.maxAmmo : 0;

        [ClientRpc]
        public void ForceBurstFireClientRpc(int burstCount, float timeBetweenShots)
        {
            if (!IsOwner) return;
            StartCoroutine(ForceBurstFireRoutine(burstCount, timeBetweenShots));
        }

        private System.Collections.IEnumerator ForceBurstFireRoutine(int burstCount, float timeBetweenShots)
        {
            for (int i = 0; i < burstCount; i++)
            {
                if (GetCurrentAmmo() > 0 && !IsReloading())
                {
                    ResetFireCooldown();
                    HandleInput(true);
                    yield return null; // Let the weapon register the click
                    HandleInput(false);
                }
                else
                {
                    break;
                }
                yield return new WaitForSeconds(timeBetweenShots);
            }
        }

        private void Update()
        {
            if (!IsOwner || weaponData == null) return;

            HandlePassiveMechanics();
            HandleActiveFiring();
        }

        // --- MECHANIC LOGIC ---
        private void HandlePassiveMechanics()
        {
            // Handle Heat Decay
            if (EffectiveAmmoMechanic == AmmoMechanic.Overheat && CurrentHeat > 0 && !IsInputActive)
            {
                CurrentHeat -= weaponData.heatDecayRate * Time.deltaTime;
                if (CurrentHeat < 0) CurrentHeat = 0;
            }
        }

        private void HandleActiveFiring()
        {
            if (IsReloadingOrCooling || !IsInputActive || FireLocked) return;

            // Check if we are allowed to shoot yet (Fire Rate)
            if (Time.time < NextAllowedFireTime) return;

            // Check Semi-Auto limit
            if (weaponData.fireMode == FireMode.SemiAuto && HasFiredThisClick) return;

            // Check Ammo/Heat states
            if (!CanConsumeAmmoOrHeat()) return;

            // Handle Firing Modes
            switch (weaponData.fireMode)
            {
                case FireMode.SemiAuto:
                case FireMode.Automatic:
                    ExecuteShot();
                    HasFiredThisClick = true;
                    break;
                case FireMode.Charge:
                    ProcessCharging();
                    break;
            }
        }

        private void ProcessCharging()
        {
            CurrentChargeTimer += Time.deltaTime;

            // Use EffectiveChargeTime so subclasses can shorten/zero charge dynamically
            // (Bloodthirster) without mutating weaponData.
            if (CurrentChargeTimer >= EffectiveChargeTime)
            {
                ExecuteShot();
                CurrentChargeTimer = 0f;
                HasFiredThisClick = true; // Force them to re-click for the next charge
            }
        }

        protected void CancelCharge()
        {
            CurrentChargeTimer = 0f;
            // Cancel VFX here
        }

        private bool CanConsumeAmmoOrHeat()
        {
            if (EffectiveAmmoMechanic == AmmoMechanic.Magazine)
            {
                if (CurrentAmmo <= 0)
                {
                    StartReload();
                    return false;
                }
            }
            else if (EffectiveAmmoMechanic == AmmoMechanic.Overheat)
            {
                if (CurrentHeat >= weaponData.maxHeat)
                {
                    StartOverheatPenalty();
                    return false;
                }
            }
            
            return true;
        }

        protected void ExecuteShot()
        {
            // Set cooldown based on fire rate, scaled by attacker's AttackSpeed stat.
            float fireRate = weaponData.fireRate;
            if (transform.root.TryGetComponent(out _Project.Core.Stats.CharacterStats attackerStats))
            {
                float atkMult = attackerStats.GetStat(_Project.Core.Enums.StatType.AttackSpeed);
                if (atkMult > 0f) fireRate /= atkMult;
            }
            NextAllowedFireTime = Time.time + fireRate;

            // Consume Resources Locally (Client Prediction). Uses EffectiveAmmoMechanic so
            // abilities can temporarily switch the cost type (Bloodthirster: Overheat → Magazine).
            if (EffectiveAmmoMechanic == AmmoMechanic.Magazine)
            {
                CurrentAmmo--;
            }
            else if (EffectiveAmmoMechanic == AmmoMechanic.Overheat)
            {
                CurrentHeat += weaponData.heatPerShot;
            }

            // Trigger Slash Animation if configured
            if (weaponData.useSlashOnAttack)
            {
                var playerAnim = GetComponentInParent<PlayerAnimator>();
                if (playerAnim != null)
                {
                    playerAnim.TriggerSlashClientRpc();
                }
            }

            // Game Feel
            if (weaponData.recoilForce.sqrMagnitude > 0.01f && EventBus != null)
            {
                Vector3 finalRecoil = weaponData.recoilForce;
                if (transform.root.TryGetComponent(out _Project.Core.Stats.CharacterStats stats))
                {
                    float recoilReduction = Mathf.Clamp01(stats.GetStat(_Project.Core.Enums.StatType.RecoilReductionMultiplier));
                    finalRecoil *= (1f - recoilReduction);
                }
                EventBus.RaiseOnWeaponFiredRecoil(finalRecoil);
            }
            if (weaponData.cameraShakeDuration > 0f && EventBus != null)
            {
                EventBus.RaiseOnCameraShake(weaponData.cameraShakeMagnitude, weaponData.cameraShakeDuration);
            }
            if (weaponData.triggerHitStopOnFire && weaponData.hitStopDuration > 0)
            {
                _Project.Core.Managers.HitStopManager.Instance.TriggerHitStop(weaponData.hitStopDuration);
            }
            
            // Core Visual Effects (Muzzle Flash, Ejecting Shells) 
            // These play for both local owner visually and remotely when Broadcast kicks in
            SpawnVFX();

            // 1. Perform Local effects instantly (VFX, Recoil, Sound)
            PerformLocalCosmeticShot();

            // NEW 2: Tell the server that a shot was fired so it can tell the other clients!
            BroadcastCosmeticShotServerRpc();

            // 3. Perform the actual weapon logic (Raycasts, Projectiles)
            PerformServerShot();
        }

        public int GetCurrentAmmo() => CurrentAmmo;
        public float GetCurrentHeat() => CurrentHeat;
        public bool IsReloading() => IsReloadingOrCooling;

        public void ResetFireCooldown() => NextAllowedFireTime = 0f;

        public void Reload() => StartReload();

        // --- RELOADING / OVERHEATING ---
        public void StartReload() => StartReload(1f);

        /// speedMultiplier > 1 = faster reload (e.g. 1.3 = 30% faster).
        public void StartReload(float speedMultiplier)
        {
            if (IsReloadingOrCooling || CurrentAmmo >= EffectiveMaxAmmo) return;
            IsReloadingOrCooling = true;
            BeginMountDip();
            PlayReloadSfx();

            // Fold in the owner's ReloadSpeedMultiplier (Autoloader). Seeded to 1f, so a no-op
            // without the item. Read owner-side → host-only for remote clients (stat-replication bug).
            float ownerReloadMult = 1f;
            if (transform.root.TryGetComponent(out _Project.Core.Stats.CharacterStats reloaderStats))
            {
                float m = reloaderStats.GetStat(_Project.Core.Enums.StatType.ReloadSpeedMultiplier);
                if (m > 0f) ownerReloadMult = m;
            }

            float mult = Mathf.Max(0.1f, speedMultiplier * ownerReloadMult);
            float t = Mathf.Max(0.05f, weaponData.reloadTime / mult); // Mathf.Max = min-reload floor
            Debug.Log("Reloading...");
            Invoke(nameof(FinishReload), t);
        }

        protected virtual void FinishReload()
        {
            CurrentAmmo = EffectiveMaxAmmo;
            IsReloadingOrCooling = false;
            EndMountDip();
            Debug.Log("Reload Complete");
        }

        protected void StartOverheatPenalty()
        {
            IsReloadingOrCooling = true;
            BeginMountDip();
            PlayReloadSfx();
            Debug.Log("Weapon Overheated!");
            Invoke(nameof(FinishOverheatPenalty), weaponData.overheatPenaltyTime);
        }

        private void FinishOverheatPenalty()
        {
            CurrentHeat = 0f;
            IsReloadingOrCooling = false;
            EndMountDip();
            Debug.Log("Weapon Cooled Down");
        }

        private IWeaponMount FindMount()
        {
            // Mount lives on the player root (WeaponSpawner implements IWeaponMount).
            var root = transform.root;
            if (root == null) return null;
            return root.GetComponentInChildren<IWeaponMount>();
        }

        private void BeginMountDip()
        {
            if (!IsOwner) return;
            FindMount()?.BeginReloadDip();
        }

        private void EndMountDip()
        {
            if (!IsOwner) return;
            FindMount()?.EndReloadDip();
        }

        private void PlayReloadSfx()
        {
            if (!IsOwner) return;
            var sfx = SfxManager.Instance;
            if (sfx == null || sfx.Library == null || sfx.Library.weaponReload == null) return;
            sfx.PlayOneShot2D(sfx.Library.weaponReload, 0.85f);
        }
        
        private void SpawnVFX()
        {
            if (weaponData == null) return;
            
            if (weaponData.muzzleFlashPrefab != null && muzzlePoint != null)
            {
                var flash = Instantiate(weaponData.muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
                // Assume particle sys handles its own auto destruction properly.
            }
            
            if (weaponData.casingEjectPrefab != null && ejectPoint != null)
            {
                var casing = Instantiate(weaponData.casingEjectPrefab, ejectPoint.position, ejectPoint.rotation);
                // Ejection velocity would be handled by a script on the casing itself
            }
        }
        
        // --- NETWORKED COSMETICS ---

        [ServerRpc]
        private void BroadcastCosmeticShotServerRpc()
        {
            // The Server receives the request and immediately broadcasts it to ALL clients
            PlayCosmeticShotClientRpc();
        }

        [ClientRpc]
        private void PlayCosmeticShotClientRpc()
        {
            // The player who shot the gun already played the effects locally in ExecuteShot()
            // We only want the OTHER players to play the effects!
            if (IsOwner) return;

            // They play general VFX (Flash, casing) + their specific sub-class local cosmetics
            SpawnVFX();
            
            // Trigger the exact same cosmetic function, but on the clones
            PerformLocalCosmeticShot();
        }

        // --- ABSTRACT METHODS FOR CHILD CLASSES ---
        
        // Child class (e.g., RaycastWeapon) will override this to draw local tracers/flash
        protected abstract void PerformLocalCosmeticShot();
        
        // Child class will override this and call its own custom [ServerRpc]
        protected abstract void PerformServerShot();
    }
}
