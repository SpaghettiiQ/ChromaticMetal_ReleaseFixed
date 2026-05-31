using Unity.Netcode;
using UnityEngine;
using _Project.Core.Events;
using _Project.Core.Managers;
using _Project.Core.Structs;

namespace _Project.Features.GameFeel.Scripts
{
    /// Local-player-only consumer of CharacterEventBus.OnHit/OnKill.
    /// Spawns hit/kill particles, floating damage numbers, hitstop on kill, and raises camera shake.
    /// SFX is handled separately by SfxManager via GameFeelEvents.OnLocalPlayerHit (avoid duplication).
    [RequireComponent(typeof(CharacterEventBus))]
    public class HitFeedbackController : NetworkBehaviour
    {
        [Header("VFX Prefabs (optional)")]
        [SerializeField] private GameObject hitParticlePrefab;
        [SerializeField] private GameObject killParticlePrefab;

        [Header("Hitstop")]
        [SerializeField] private float hitStopDuration = 0.08f;
        [SerializeField] private float hitStopTimeScale = 0.05f;

        [Header("Camera Shake")]
        [SerializeField] private float hitShakeMagnitude = 0.2f;
        [SerializeField] private float hitShakeDuration = 0.05f;
        [SerializeField] private float killShakeMagnitude = 0.5f;
        [SerializeField] private float killShakeDuration = 0.1f;

        [Header("Hit Position")]
        [SerializeField] private float hitYOffset = 1.0f;

        private CharacterEventBus _bus;
        private bool _subscribed;

        private void Awake()
        {
            _bus = GetComponent<CharacterEventBus>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            Subscribe();
        }

        public override void OnNetworkDespawn()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || _bus == null) return;
            _bus.OnHit += HandleOnHit;
            _bus.OnKill += HandleOnKill;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _bus == null) return;
            _bus.OnHit -= HandleOnHit;
            _bus.OnKill -= HandleOnKill;
            _subscribed = false;
        }

        private void HandleOnHit(DamageContext ctx)
        {
            Vector3 pos = ctx.Target != null
                ? ctx.Target.transform.position + Vector3.up * hitYOffset
                : transform.position;

            if (hitParticlePrefab != null) Instantiate(hitParticlePrefab, pos, Quaternion.identity);

            WorldDamageNumberSpawner.Instance.Spawn(pos, ctx.Damage, ctx.IsCritical);

            if (_bus != null) _bus.RaiseOnCameraShake(hitShakeMagnitude, hitShakeDuration);
        }

        private void HandleOnKill(DamageContext ctx)
        {
            Vector3 pos = ctx.Target != null ? ctx.Target.transform.position : transform.position;

            if (killParticlePrefab != null) Instantiate(killParticlePrefab, pos, Quaternion.identity);

            HitStopManager.Instance.TriggerHitStop(hitStopDuration, hitStopTimeScale);

            if (_bus != null) _bus.RaiseOnCameraShake(killShakeMagnitude, killShakeDuration);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Unsubscribe();
        }
    }
}
