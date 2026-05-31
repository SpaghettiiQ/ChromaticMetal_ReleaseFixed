using Unity.Netcode;
using UnityEngine;
using _Project.Core.Audio;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.WeaponCore.Scripts;

namespace _Project.Features.Weapons.Scripts
{
    public class HitscanWeapon : NetworkWeapon
    {
        public ParticleSystem muzzleFlash;
        public AudioClip fireSound;
        public Transform muzzleTransform;

        [Header("Tracer Visuals")]
        public float tracerLifetime = 0.05f;
        public float tracerWidth = 0.02f;
        public Color tracerColor = Color.softYellow;

        private Camera _cachedCamera;

        private Camera PlayerCamera
        {
            get
            {
                if (_cachedCamera == null)
                {
                    _cachedCamera = GetComponentInParent<Camera>();
                    if (_cachedCamera == null)
                    {
                        _cachedCamera = Camera.main;
                    }

                    if (_cachedCamera == null)
                    {
                        Debug.LogError($"[HitscanWeapon] Could not find a camera for {gameObject.name}!");
                    }
                }
                return _cachedCamera;
            }
        }

        protected override void PerformLocalCosmeticShot()
        {
            if (muzzleFlash != null) muzzleFlash.Play();
            if (fireSound != null && SfxManager.Instance != null)
            {
                SfxManager.Instance.PlayOneShot3D(fireSound, transform.position);
            }

            Debug.Log($"[Client] Hitscan fired! Ammo left: {CurrentAmmo}");
        }

        protected override void PerformServerShot()
        {
            if (PlayerCamera == null) return;

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            Vector3 tracerStart = muzzleTransform != null
                ? muzzleTransform.position
                : (muzzleFlash != null ? muzzleFlash.transform.position : ray.origin);

            // Owner gets instant visual feedback; server resolves the authoritative endpoint and damage.
            Vector3 predictedEnd = ray.GetPoint(weaponData.range);
            if (Physics.Raycast(ray, out RaycastHit predictedHit, weaponData.range))
            {
                predictedEnd = predictedHit.point;
            }

            PlayTracer(tracerStart, predictedEnd);
            ProcessShotServerRpc(ray.origin, ray.direction, tracerStart);
        }

        [ServerRpc]
        private void ProcessShotServerRpc(Vector3 rayOrigin, Vector3 rayDirection, Vector3 tracerStart)
        {
            Ray ray = new Ray(rayOrigin, rayDirection.normalized);
            Vector3 tracerEnd = ray.GetPoint(weaponData.range);

            bool didHit = Physics.Raycast(ray, out RaycastHit hit, weaponData.range);
            Debug.Log($"[Hitscan][srv] owner={OwnerClientId} rayOrigin={rayOrigin} dir={rayDirection.normalized} range={weaponData.range} " +
                      $"didHit={didHit} hitObj={(didHit ? hit.collider.transform.root.name : "<miss>")} hitDist={(didHit ? hit.distance : -1f)}");

            if (didHit)
            {
                tracerEnd = hit.point;
                ApplyDamageToHitTarget(hit.collider);
            }

            PlayTracerClientRpc(tracerStart, tracerEnd);
        }

        private void ApplyDamageToHitTarget(Collider hitCollider)
        {
            if (hitCollider == null || hitCollider.transform.root == transform.root)
            {
                return;
            }

            DamageContext ctx = new DamageContext
            {
                Source = transform.root.gameObject,
                Target = hitCollider.gameObject,
                Damage = weaponData.baseDamage
            };

            CharacterStats targetStats = hitCollider.GetComponentInParent<CharacterStats>();
            if (targetStats != null)
            {
                if (!CanDamagePlayerTarget(targetStats))
                {
                    return;
                }

                ctx.Target = targetStats.gameObject;
                targetStats.TakeDamage(ctx);
                Debug.Log($"[Server] Hitscan dealt {weaponData.baseDamage} damage to {targetStats.name}");
                return;
            }

            IDamageContextReceiver receiver = hitCollider.GetComponentInParent<IDamageContextReceiver>();
            if (receiver != null)
            {
                receiver.SetDamageContext(ctx);
                Debug.Log($"[Server] Hitscan dealt {weaponData.baseDamage} damage to {hitCollider.name}");
            }
        }

        private bool CanDamagePlayerTarget(CharacterStats targetStats)
        {
            if (targetStats == null || targetStats.IsDead)
            {
                return false;
            }

            CharacterStats ownerStats = GetComponentInParent<CharacterStats>();
            TeamAffiliation ownerTeam = ownerStats != null ? ownerStats.Team : TeamAffiliation.None;
            TeamAffiliation targetTeam = targetStats.Team;

            // Team None is considered enemy-affiliated.
            if (targetTeam == TeamAffiliation.None)
            {
                return true;
            }

            // If shooter has no explicit team (or is enemy-affiliated), allow damage.
            if (ownerTeam == TeamAffiliation.None)
            {
                return true;
            }

            // Block friendly fire between explicit player teams.
            return targetTeam != ownerTeam;
        }

        private void PlayTracer(Vector3 start, Vector3 end)
        {
            _Project.Core.Managers.TracerLine.SpawnLocal(start, end, tracerColor, tracerWidth, tracerLifetime);
        }

        [ClientRpc]
        private void PlayTracerClientRpc(Vector3 start, Vector3 end)
        {
            if (IsOwner) return;

            PlayTracer(start, end);
        }
    }
}