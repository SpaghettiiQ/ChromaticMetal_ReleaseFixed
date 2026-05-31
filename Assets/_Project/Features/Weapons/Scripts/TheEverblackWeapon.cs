using Unity.Netcode;
using UnityEngine;
using _Project.Core.Audio;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.WeaponCore.Scripts;

namespace _Project.Features.Weapons.Scripts
{
    public class TheEverblackWeapon : NetworkWeapon
    {
        [Header("Standard Fire")]
        public int pelletsPerShot = 8;
        public float spreadAngle = 5f;

        [Header("Heat Core Eject (fired automatically when heat hits max)")]
        [Tooltip("Heat-core projectile prefab — spawned + thrown from ejectPoint when the weapon overheats. " +
                 "Mirrors Sonic Grenade's launch behavior.")]
        public _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Projectiles.HeatCoreProjectile heatCorePrefab;
        [Tooltip("Initial launch speed of the heat core, in m/s.")]
        public float heatCoreLaunchVelocity = 15f;
        [Tooltip("Extra upward arc applied on launch so the core lobs forward instead of flying flat.")]
        public float heatCoreUpwardArc = 2f;
        [Tooltip("Seconds between the heat core being thrown and the cool-off mount-dip starting. " +
                 "Lets the player see the core leave before the weapon goes off-screen.")]
        public float coreEjectToDipDelay = 0.4f;

        [Header("Dual-Wield")]
        [Tooltip("Optional second muzzle. If assigned, standard shots alternate primary/secondary; " +
                 "Overdrive fires from both simultaneously.")]
        public Transform secondaryMuzzlePoint;

        [Header("Per-Barrel Kick Animation (fakes recoil by translating + rotating each model)")]
        [Tooltip("The visual model for the PRIMARY (right) shotgun — gets kicked back when it fires.")]
        public Transform primaryShotgunModel;
        [Tooltip("The visual model for the SECONDARY (left) shotgun — gets kicked back when it fires.")]
        public Transform secondaryShotgunModel;
        [Tooltip("How far the model snaps back along its local -Z when it fires, in metres.")]
        public float kickBackOffset = 0.08f;
        [Tooltip("Pitch up applied to the firing model (degrees on local X). Negative for nose-up.")]
        public float kickPitchDegrees = -8f;
        [Tooltip("Seconds for the model to snap back to the kick pose.")]
        public float kickOutDuration = 0.04f;
        [Tooltip("Seconds for the model to spring back to its rest pose.")]
        public float kickReturnDuration = 0.12f;

        [Header("Audio / VFX")]
        public ParticleSystem muzzleFlash;
        public AudioClip fireSound;

        [Header("Tracer Visual")]
        public Color tracerColor = new Color(1f, 0.85f, 0.2f, 1f);
        public float tracerWidth = 0.02f;
        public float tracerLifetime = 0.05f;

        private Camera _cachedCamera;
        // Alternates each standard shot. Even index = primary muzzle, odd = secondary.
        private int _shotIndex;

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

        protected override void PerformLocalCosmeticShot()
        {
            // Pick muzzle for this shot, play flash + sound there, then advance the toggle so the
            // next shot lands on the other barrel. Runs on owner (from ExecuteShot) AND on remote
            // clients (from NetworkWeapon.PlayCosmeticShotClientRpc), so both stay in sync.
            bool isSecondary = secondaryMuzzlePoint != null && (_shotIndex & 1) == 1;
            Transform muzzle = isSecondary ? secondaryMuzzlePoint : muzzlePoint;

            PlayShotFlashLocal(muzzle);
            KickShotgunModel(isSecondary ? secondaryShotgunModel : primaryShotgunModel);

            _shotIndex++;
        }

        // Per-model kick state. Each shotgun model gets its own rest pose + active coroutine so
        // a rapid back-and-forth between barrels can be mid-animation on both simultaneously.
        private readonly System.Collections.Generic.Dictionary<Transform, (Vector3 pos, Quaternion rot)> _modelRest =
            new System.Collections.Generic.Dictionary<Transform, (Vector3, Quaternion)>();
        private readonly System.Collections.Generic.Dictionary<Transform, Coroutine> _kickRoutines =
            new System.Collections.Generic.Dictionary<Transform, Coroutine>();

        private void KickShotgunModel(Transform model)
        {
            if (model == null) return;

            // Capture the model's rest pose the first time we kick it, then anchor every future
            // tween off that snapshot so repeat kicks always land back on the prefab's neutral.
            if (!_modelRest.ContainsKey(model))
            {
                _modelRest[model] = (model.localPosition, model.localRotation);
            }

            if (_kickRoutines.TryGetValue(model, out var existing) && existing != null)
            {
                StopCoroutine(existing);
            }
            _kickRoutines[model] = StartCoroutine(KickRoutine(model));
        }

        private System.Collections.IEnumerator KickRoutine(Transform model)
        {
            var (restPos, restRot) = _modelRest[model];

            Vector3 startPos = model.localPosition;
            Quaternion startRot = model.localRotation;

            Vector3 kickPos = restPos + Vector3.back * kickBackOffset;
            Quaternion kickRot = restRot * Quaternion.Euler(kickPitchDegrees, 0f, 0f);

            // Phase 1: snap back to the kick pose.
            float outDur = Mathf.Max(0.001f, kickOutDuration);
            float t = 0f;
            while (t < outDur)
            {
                t += Time.deltaTime;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / outDur));
                model.localPosition = Vector3.Lerp(startPos, kickPos, a);
                model.localRotation = Quaternion.Slerp(startRot, kickRot, a);
                yield return null;
            }
            model.localPosition = kickPos;
            model.localRotation = kickRot;

            // Phase 2: spring back to rest.
            float retDur = Mathf.Max(0.001f, kickReturnDuration);
            t = 0f;
            while (t < retDur)
            {
                t += Time.deltaTime;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / retDur));
                model.localPosition = Vector3.Lerp(kickPos, restPos, a);
                model.localRotation = Quaternion.Slerp(kickRot, restRot, a);
                yield return null;
            }
            model.localPosition = restPos;
            model.localRotation = restRot;
            _kickRoutines[model] = null;
        }

        protected override void PerformServerShot()
        {
            if (PlayerCamera == null) return;

            // PerformLocalCosmeticShot already ran for this shot and advanced _shotIndex, so the
            // muzzle "just used" is one back. Resolving it this way (rather than caching a
            // Transform field) keeps owner and remote-replay frames decoupled.
            Transform muzzle = MuzzleAtIndex(_shotIndex - 1);

            int damagePerPellet = Mathf.Max(1, weaponData.baseDamage / Mathf.Max(1, pelletsPerShot));
            FirePelletsAndTracers(pelletsPerShot, PlayerCamera.transform.forward, muzzle, spreadAngle, damagePerPellet);

            // Base ExecuteShot incremented CurrentHeat before us. If this shot tipped us over,
            // trigger the auto-eject + cool-off right now rather than waiting for the player to
            // press fire again and hit the base's reject path.
            if (weaponData.ammoMechanic == _Project.Features.WeaponCore.Enums.AmmoMechanic.Overheat
                && CurrentHeat >= weaponData.maxHeat
                && !IsReloadingOrCooling)
            {
                TriggerHeatOverflow();
            }
        }

        /// <summary>
        /// Secondary ability "To The Hellfire" calls this. Fires both barrels simultaneously
        /// with the ability's own damage/pellet/spread, then sets heat to max which triggers
        /// the standard overheat path — ejecting the heat core and starting the cool-off.
        /// Returns true if at least one pellet (from either barrel) actually applied damage,
        /// so the ability can gate its hit-stop on the blast genuinely connecting.
        /// </summary>
        public bool HellfireBlast(int pelletsPerBarrel, float blastSpread, int damagePerPellet)
        {
            if (!IsServer) return false;
            if (PlayerCamera == null) return false;

            Vector3 dir = PlayerCamera.transform.forward;

            int hits = FirePelletsAndTracers(pelletsPerBarrel, dir, muzzlePoint, blastSpread, damagePerPellet);
            if (secondaryMuzzlePoint != null)
            {
                hits += FirePelletsAndTracers(pelletsPerBarrel, dir, secondaryMuzzlePoint, blastSpread, damagePerPellet);
            }

            // Kick the host's barrels for instant local feedback; non-host owners get the kicks
            // via OverdriveCosmeticClientRpc.
            KickShotgunModel(primaryShotgunModel);
            if (secondaryShotgunModel != null) KickShotgunModel(secondaryShotgunModel);
            OverdriveCosmeticClientRpc();

            // Top off heat so the standard "heat >= max" path takes over — eject + cool-off.
            CurrentHeat = weaponData.maxHeat;
            if (!IsReloadingOrCooling) TriggerHeatOverflow();

            return hits > 0;
        }

        public void AutoTargetFire(Vector3 targetDirection, int pelletCount)
        {
            // Used by Infernum to auto-fire at view targets. Single muzzle (primary) keeps the
            // _shotIndex alternation untouched for the next manual shot.
            if (!IsServer) return;
            int damagePerPellet = Mathf.Max(1, weaponData.baseDamage / Mathf.Max(1, pelletsPerShot));
            FirePelletsAndTracers(pelletCount, targetDirection, muzzlePoint, spreadAngle, damagePerPellet);

            // Accumulate heat like a normal shot so the regular overheat path eventually
            // triggers the heat-core eject. Skipping this would let Infernum dump unlimited
            // shots with no cool-off. The check is one-way (heatPerShot adds) so shots still
            // fire regardless of current heat — they "ignore the limit".
            if (weaponData.ammoMechanic == _Project.Features.WeaponCore.Enums.AmmoMechanic.Overheat)
            {
                CurrentHeat = Mathf.Min(weaponData.maxHeat, CurrentHeat + weaponData.heatPerShot);
                if (CurrentHeat >= weaponData.maxHeat && !IsReloadingOrCooling)
                {
                    TriggerHeatOverflow();
                }
            }
        }

        private void TriggerHeatOverflow()
        {
            // Eject the heat core projectile from the existing ejectPoint, then run the standard
            // base-class overheat penalty (which dips the mount off-screen and resets heat
            // after weaponData.overheatPenaltyTime). A short configurable delay separates the
            // two so the player visibly sees the core leave before the weapon dips away.
            EjectHeatCoreServer();

            // Lock fire input immediately so the player can't squeeze another shot during the
            // ejection→dip window. StartOverheatPenalty would set this too, but we need it set
            // before the delay elapses.
            IsReloadingOrCooling = true;

            if (coreEjectToDipDelay > 0f)
            {
                Invoke(nameof(StartOverheatPenalty), coreEjectToDipDelay);
            }
            else
            {
                StartOverheatPenalty();
            }
        }

        private void EjectHeatCoreServer()
        {
            if (heatCorePrefab == null) return;

            // Use the eject point's world pose if available, otherwise the weapon root.
            Transform pivot = ejectPoint != null ? ejectPoint : transform;
            Vector3 spawnPos = pivot.position;

            // Throw toward where the player is looking (mirrors Sonic Grenade) so the core
            // doesn't lob into the ceiling/floor at awkward angles.
            Vector3 forward = pivot.forward;
            var rootCamera = transform.root.GetComponentInChildren<Camera>();
            if (rootCamera != null) forward = rootCamera.transform.forward;

            var projectile = Instantiate(heatCorePrefab, spawnPos, Quaternion.LookRotation(forward));
            projectile.launchVelocity = heatCoreLaunchVelocity;

            if (projectile.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
            }

            // Initialize sets the rigidbody linearVelocity (= forward * launchVelocity). Must
            // run after Spawn so any NetworkRigidbody doesn't clobber the velocity during the
            // spawn handshake. Then we boost vertical separately so the core lobs in a real arc
            // instead of skimming flat.
            projectile.Initialize(transform.root.gameObject, forward);
            if (heatCoreUpwardArc > 0f && projectile.TryGetComponent(out Rigidbody rb))
            {
                rb.linearVelocity += Vector3.up * heatCoreUpwardArc;
            }
        }

        [ClientRpc]
        private void OverdriveCosmeticClientRpc()
        {
            // Skip the host because OverdriveFire already handled its flashes + kicks server-side.
            if (IsServer) return;
            // Flash both barrels for non-owners. Doesn't advance _shotIndex so the alternation
            // for standard shots isn't desynced by interleaving an Overdrive.
            PlayShotFlashLocal(muzzlePoint);
            if (secondaryMuzzlePoint != null) PlayShotFlashLocal(secondaryMuzzlePoint);
            KickShotgunModel(primaryShotgunModel);
            if (secondaryShotgunModel != null) KickShotgunModel(secondaryShotgunModel);
        }

        private Transform CurrentMuzzleForShot() => MuzzleAtIndex(_shotIndex);

        private Transform MuzzleAtIndex(int index)
        {
            if (secondaryMuzzlePoint != null && (index & 1) == 1)
            {
                return secondaryMuzzlePoint;
            }
            return muzzlePoint;
        }

        private void PlayShotFlashLocal(Transform muzzle)
        {
            Vector3 pos = muzzle != null ? muzzle.position : transform.position;
            if (muzzleFlash != null)
            {
                muzzleFlash.transform.position = pos;
                muzzleFlash.Play();
            }
            if (fireSound != null && SfxManager.Instance != null)
            {
                SfxManager.Instance.PlayOneShot3D(fireSound, pos);
            }
        }

        private int FirePelletsAndTracers(int count, Vector3 baseDirection, Transform muzzle, float spread, int damagePerPellet)
        {
            if (PlayerCamera == null) return 0;

            Vector3 origin = PlayerCamera.transform.position;
            Vector3 tracerStart = muzzle != null ? muzzle.position : origin;

            Vector3[] tracerEnds = new Vector3[count];
            int connectedHits = 0;

            for (int i = 0; i < count; i++)
            {
                Vector3 spreadDir = GetSpreadDirection(baseDirection, spread);
                Ray ray = new Ray(origin, spreadDir);

                Vector3 endPoint;
                if (Physics.Raycast(ray, out RaycastHit hit, weaponData.range))
                {
                    endPoint = hit.point;
                    CharacterStats targetStats = hit.collider.GetComponentInParent<CharacterStats>();
                    if (targetStats != null && targetStats.TryGetComponent<NetworkObject>(out var netObj))
                    {
                        if (ProcessHitServer(netObj.NetworkObjectId, damagePerPellet))
                        {
                            connectedHits++;
                        }
                    }
                }
                else
                {
                    endPoint = ray.GetPoint(weaponData.range);
                }

                tracerEnds[i] = endPoint;
                // Local prediction: spawn the tracer immediately on this machine.
                _Project.Core.Managers.TracerLine.SpawnLocal(tracerStart, endPoint, tracerColor, tracerWidth, tracerLifetime);
            }

            // Broadcast to other clients so they see the same pellets fly.
            if (IsServer)
            {
                // Already authoritative (Hellfire path). Direct ClientRpc.
                PlayPelletTracersClientRpc(tracerStart, tracerEnds);
            }
            else
            {
                // Owner-side prediction: bounce through ServerRpc → ClientRpc.
                BroadcastPelletTracersServerRpc(tracerStart, tracerEnds);
            }

            return connectedHits;
        }

        [ServerRpc]
        private void BroadcastPelletTracersServerRpc(Vector3 start, Vector3[] ends)
        {
            PlayPelletTracersClientRpc(start, ends);
        }

        [ClientRpc]
        private void PlayPelletTracersClientRpc(Vector3 start, Vector3[] ends)
        {
            if (IsOwner) return; // Owner already drew them locally.
            for (int i = 0; i < ends.Length; i++)
            {
                _Project.Core.Managers.TracerLine.SpawnLocal(start, ends[i], tracerColor, tracerWidth, tracerLifetime);
            }
        }

        // Returns true if damage was actually applied (used by HellfireBlast to gate hit-stop
        // on a real connection, ignoring whiffs and friendly-fire-skipped pellets).
        private bool ProcessHitServer(ulong targetNetworkId, int damagePerPellet)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkId, out NetworkObject targetObject)) return false;
            if (!targetObject.TryGetComponent<CharacterStats>(out var targetStats)) return false;

            // Friendly-fire block: same-team (Thanatos hitting Thrive teammates, or the firer's
            // own pellets clipping their own collider during a teleport) does no damage. Matches
            // the same-team filter the projectiles use.
            var sourceStats = transform.root.GetComponent<CharacterStats>();
            if (sourceStats != null && targetStats.Team == sourceStats.Team) return false;

            DamageContext ctx = new DamageContext
            {
                Source = transform.root.gameObject,
                Target = targetObject.gameObject,
                Damage = damagePerPellet
            };

            targetStats.TakeDamage(ctx);
            return true;
        }

        /// <summary>Used by the Infernum special ability after its auto-target volley.</summary>
        public void ResetHeat() => CurrentHeat = 0f;

        /// <summary>
        /// Used by Infernum at the end of its volley to guarantee the heat-core eject runs
        /// regardless of whether heatPerShot × shotCount lands exactly on maxHeat. No-op if
        /// the weapon is already cooling, so calling it on a barrel that's mid-eject can't
        /// double-fire the projectile.
        /// </summary>
        public void ForceHeatEject()
        {
            if (!IsServer) return;
            if (IsReloadingOrCooling) return;
            CurrentHeat = weaponData.maxHeat;
            TriggerHeatOverflow();
        }

        private Vector3 GetSpreadDirection(Vector3 forward, float spread)
        {
            float horizontalSpread = Random.Range(-spread, spread);
            float verticalSpread = Random.Range(-spread, spread);

            Quaternion spreadRotation = Quaternion.Euler(verticalSpread, horizontalSpread, 0f);
            return spreadRotation * forward;
        }
    }
}
