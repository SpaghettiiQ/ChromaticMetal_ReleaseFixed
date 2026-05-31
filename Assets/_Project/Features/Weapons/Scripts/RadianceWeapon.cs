using Unity.Netcode;
using UnityEngine;
using _Project.Features.WeaponCore.Scripts;
using _Project.Core.Audio;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Core.Enums;
using _Project.Features.WeaponCore.Enums;

namespace _Project.Features.Weapons.Scripts
{
    /// <summary>
    /// Conduit's B1M Raycannon "Radiance". AmmoMechanic.Overheat, FireMode.Charge.
    /// Holding fire charges the beam; releasing fires it. Reaching overheat applies extra
    /// Burden to the wielder. Bloodthirster (secondary) temporarily flips the weapon to a
    /// 6-round Magazine that fires instantly without charge; after the 6th shot the base
    /// reload kicks in and Bloodthirster auto-deactivates when the reload finishes.
    /// </summary>
    public class RadianceWeapon : NetworkWeapon
    {
        [Header("Radiance Specifics")]
        [Tooltip("Extra Burden applied to the wielder the first frame their heat hits max.")]
        public float burdenOnOverheat = 15f;
        [Tooltip("Damage of a fully-charged shot. Releasing before full charge scales linearly " +
                 "with how charged the beam was.")]
        public float baseDamage = 50f;
        [Tooltip("Minimum charge fraction (0-1) that produces a shot on release. Below this, " +
                 "release just cancels with no fire — prevents accidental dribbling.")]
        public float minReleaseFraction = 0.15f;

        [Header("Cosmetics")]
        public ParticleSystem BeamEffect;
        public AudioClip FireSound;

        [Header("Tracer Visual")]
        [Tooltip("Tracer line color for the standard beam.")]
        public Color tracerColor = new Color(0.78f, 0.42f, 1f, 1f);
        [Tooltip("Tracer line color while Bloodthirster is active — visually distinct so the " +
                 "player can read the buff state.")]
        public Color bloodthirsterTracerColor = new Color(1f, 0.25f, 0.55f, 1f);
        [Tooltip("Tracer thickness at full charge, in metres. Width scales linearly with the " +
                 "shot's charge fraction (so a half-charged release is half as thick).")]
        public float maxTracerWidth = 0.18f;
        [Tooltip("Seconds the tracer LineRenderer stays alive before destroying itself.")]
        public float tracerLifetime = 0.1f;

        [Header("Bloodthirster (Secondary)")]
        [Tooltip("Damage multiplier while Bloodthirster is active.")]
        public float bloodthirsterDamageMult = 0.5f;
        [Tooltip("Flat addition to ProcChanceFlatAdd applied to the wielder for the duration " +
                 "of each Bloodthirster shot's TakeDamage call, so on-hit items roll higher proc " +
                 "chances during the burst.")]
        public float bloodthirsterProcFlatAdd = 40f;
        [Tooltip("How many shots Bloodthirster grants. After firing all of them the base reload " +
                 "lockout runs, then Bloodthirster auto-deactivates.")]
        public int bloodthirsterShotCount = 6;

        // Replicated server→all-client state so non-host clients also know when Bloodthirster
        // is active (for VFX hooks + UI ammo display).
        private NetworkVariable<bool> _bloodthirsterActive = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool IsBloodthirsterActive => _bloodthirsterActive.Value;

        // Charge fraction of the shot being fired right now (set in HandleChargeReleased before
        // ExecuteShot, read in PerformServerShot for damage + tracer width). 1.0 for
        // Bloodthirster instant-fires.
        private float _pendingChargeFraction = 1f;

        private Camera _cachedCamera;
        private bool _burdenPenaltyAppliedThisCycle;

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

        // --- Effective mechanic overrides (Bloodthirster flips Overheat → Magazine 6) ---
        protected override float EffectiveChargeTime
            => _bloodthirsterActive.Value ? 0f : base.EffectiveChargeTime;

        public override AmmoMechanic EffectiveAmmoMechanic
            => _bloodthirsterActive.Value ? AmmoMechanic.Magazine : base.EffectiveAmmoMechanic;

        public override int EffectiveMaxAmmo
            => _bloodthirsterActive.Value ? bloodthirsterShotCount : base.EffectiveMaxAmmo;

        /// <summary>
        /// Called by BloodthirsterTracker (server-side). Switches to Magazine-style fire with
        /// bloodthirsterShotCount ammo. Heat / charge / cooling are reset to clean state so
        /// the player can immediately spray.
        /// </summary>
        public void ActivateBloodthirster()
        {
            if (!IsServer) return;
            _bloodthirsterActive.Value = true;
            CurrentAmmo = bloodthirsterShotCount;
            CurrentHeat = 0f;
            CurrentChargeTimer = 0f;
            IsReloadingOrCooling = false;
            CancelInvoke(); // cancel any in-flight reload / cool-off
            _burdenPenaltyAppliedThisCycle = false;
        }

        /// <summary>
        /// Override the base FinishReload so the reload that runs after the 6th Bloodthirster
        /// shot also flips the buff off and resets to a clean Overheat state. Any non-
        /// Bloodthirster reload (theoretically impossible on this weapon today) still behaves
        /// like the base — fills ammo and ends.
        /// </summary>
        protected override void FinishReload()
        {
            if (_bloodthirsterActive.Value)
            {
                _bloodthirsterActive.Value = false;
                // EffectiveMaxAmmo is now the base (weaponData.maxAmmo). Heat starts fresh.
                CurrentHeat = 0f;

                // Bloodthirster opted out of the cast-time cooldown via DeferCooldownOnCast,
                // so we explicitly start it now that the buff has fully concluded.
                if (transform.root.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityController ac))
                {
                    var secondary = ac.GetAbility(_Project.Core.Enums.AbilitySlot.Secondary);
                    if (secondary != null)
                    {
                        ac.SetCooldown(_Project.Core.Enums.AbilitySlot.Secondary, secondary.cooldownTime);
                    }
                }
            }
            base.FinishReload();
        }

        private void FixedUpdate()
        {
            if (!IsServer || weaponData == null) return;

            if (CurrentHeat >= weaponData.maxHeat && !_burdenPenaltyAppliedThisCycle)
            {
                _burdenPenaltyAppliedThisCycle = true;
                if (transform.root.TryGetComponent(out IBurdenable burdenable))
                {
                    burdenable.AddBurden(burdenOnOverheat);
                }
            }
            else if (CurrentHeat < weaponData.maxHeat * 0.25f && _burdenPenaltyAppliedThisCycle)
            {
                _burdenPenaltyAppliedThisCycle = false;
            }
        }

        protected override void HandleChargeReleased(float chargeFraction)
        {
            // While Bloodthirster is active the weapon is in Magazine mode and ProcessCharging
            // fires instantly via EffectiveChargeTime = 0, so the release hook just resets.
            if (_bloodthirsterActive.Value)
            {
                CancelCharge();
                return;
            }

            if (chargeFraction >= minReleaseFraction)
            {
                _pendingChargeFraction = chargeFraction;
                ExecuteShot();
            }

            CancelCharge();
        }

        protected override void PerformLocalCosmeticShot()
        {
            if (BeamEffect != null) BeamEffect.Play();
            if (FireSound != null && SfxManager.Instance != null)
            {
                SfxManager.Instance.PlayOneShot3D(FireSound, transform.position);
            }
        }

        protected override void PerformServerShot()
        {
            if (PlayerCamera == null) return;

            float chargeFraction = _bloodthirsterActive.Value
                ? 1f
                : Mathf.Clamp01(_pendingChargeFraction);
            _pendingChargeFraction = 1f; // reset so the next shot starts fresh

            float finalDamage = baseDamage * chargeFraction;
            bool applyProcBuff = _bloodthirsterActive.Value;
            if (applyProcBuff) finalDamage *= bloodthirsterDamageMult;

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            Vector3 tracerStart = muzzlePoint != null ? muzzlePoint.position : ray.origin;
            Vector3 tracerEnd;

            if (applyProcBuff)
            {
                // Bloodthirster: standard single-hit raycast. First thing the ray touches
                // (character or geometry) takes the shot, beam stops there.
                if (Physics.Raycast(ray, out RaycastHit hit, weaponData.range))
                {
                    tracerEnd = hit.point;
                    CharacterStats targetStats = hit.collider.GetComponentInParent<CharacterStats>();
                    if (targetStats != null && targetStats.TryGetComponent<NetworkObject>(out var netObj))
                    {
                        ProcessHitServer(netObj.NetworkObjectId, finalDamage, applyProcBuff);
                    }
                }
                else
                {
                    tracerEnd = ray.GetPoint(weaponData.range);
                }
            }
            else
            {
                // Standard fire: pierce. RaycastAll sorted by distance — apply damage to every
                // CharacterStats the ray passes through until it hits a non-character collider
                // (wall / floor / world geometry), which stops the beam visually and damage-wise.
                RaycastHit[] allHits = Physics.RaycastAll(ray, weaponData.range, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));

                tracerEnd = ray.GetPoint(weaponData.range);
                var pierced = new System.Collections.Generic.HashSet<NetworkObject>();

                for (int i = 0; i < allHits.Length; i++)
                {
                    var h = allHits[i];
                    if (h.transform.root == transform.root) continue; // pass through self

                    CharacterStats targetStats = h.collider.GetComponentInParent<CharacterStats>();
                    if (targetStats != null && targetStats.TryGetComponent<NetworkObject>(out var netObj))
                    {
                        // Dedupe per character root (multi-collider rigs shouldn't take stacked hits).
                        if (pierced.Add(netObj))
                        {
                            ProcessHitServer(netObj.NetworkObjectId, finalDamage, applyProcBuff);
                        }
                        continue; // pierce through enemies — keep iterating
                    }

                    // First non-character collider stops the beam.
                    tracerEnd = h.point;
                    break;
                }
            }

            // Tracer width scales linearly with charge so half-charged releases visibly
            // produce a thinner beam. Bloodthirster always reads as full charge (since the
            // weapon is in instant-fire mode) — distinguished by the alternate color.
            float width = Mathf.Max(0.005f, maxTracerWidth * chargeFraction);
            Color shotColor = applyProcBuff ? bloodthirsterTracerColor : tracerColor;

            // Local prediction on the firing machine.
            _Project.Core.Managers.TracerLine.SpawnLocal(tracerStart, tracerEnd, shotColor, width, tracerLifetime);

            // Broadcast for non-owners. Owner path: ServerRpc → ClientRpc. Server path
            // (Bloodthirster doesn't go through it but defensive): direct ClientRpc.
            if (IsServer)
            {
                PlayBeamTracerClientRpc(tracerStart, tracerEnd, width, shotColor);
            }
            else
            {
                BroadcastBeamTracerServerRpc(tracerStart, tracerEnd, width, shotColor);
            }
        }

        [ServerRpc]
        private void BroadcastBeamTracerServerRpc(Vector3 start, Vector3 end, float width, Color color)
        {
            PlayBeamTracerClientRpc(start, end, width, color);
        }

        [ClientRpc]
        private void PlayBeamTracerClientRpc(Vector3 start, Vector3 end, float width, Color color)
        {
            if (IsOwner) return;
            _Project.Core.Managers.TracerLine.SpawnLocal(start, end, color, width, tracerLifetime);
        }

        private void ProcessHitServer(ulong targetNetworkId, float damage, bool applyProcBuff)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkId, out NetworkObject targetObject)) return;
            if (!targetObject.TryGetComponent<CharacterStats>(out var targetStats)) return;

            CharacterStats shooterStats = transform.root.GetComponent<CharacterStats>();
            if (shooterStats != null && targetStats.Team == shooterStats.Team) return;

            StatModifier procMod = default;
            bool modInstalled = false;
            if (applyProcBuff && shooterStats != null)
            {
                procMod = new StatModifier(StatModType.Flat, bloodthirsterProcFlatAdd);
                shooterStats.AddModifier(StatType.ProcChanceFlatAdd, procMod);
                modInstalled = true;
            }

            DamageContext ctx = new DamageContext
            {
                Source = transform.root.gameObject,
                Target = targetObject.gameObject,
                Damage = Mathf.RoundToInt(damage),
                DamageType = DamageType.Presence,
                AttackType = AttackType.Projectile
            };

            targetStats.TakeDamage(ctx);

            if (modInstalled && shooterStats != null)
            {
                shooterStats.RemoveModifier(StatType.ProcChanceFlatAdd, procMod);
            }
        }
    }
}
