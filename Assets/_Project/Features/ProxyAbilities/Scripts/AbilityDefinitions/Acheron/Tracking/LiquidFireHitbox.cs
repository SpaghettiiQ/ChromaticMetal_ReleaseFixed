using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Core.Enums;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking
{
    /// <summary>
    /// Forward "flamethrower" hitbox spawned by Liquid Fire. Stays in front of the source
    /// actor's camera each server tick (instead of parenting to a non-NetworkObject), and
    /// runs an OverlapBox scan each tick to find enemies inside the volume — Unity's
    /// OnTriggerEnter silently misses NavMeshAgent enemies because they don't carry a
    /// Rigidbody. Applies flame damage + burden + a refreshing PoisonTracker DOT.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LiquidFireHitbox : NetworkBehaviour
    {
        public float duration = 3.0f;
        public float tickRate = 0.25f;
        public float burdenAddedPerTick = 2f;
        [Tooltip("Distance along the source's camera-forward at which the hitbox sits each tick.")]
        public float forwardOffset = 2.0f;

        [Header("Poison (refreshed every tick an enemy is in the flame)")]
        [Tooltip("Lingering poison duration applied (and refreshed) on each enemy the flame ticks.")]
        public float poisonDuration = 4f;
        public float poisonDamagePerTick = 4f;
        public float poisonTickRate = 0.5f;

        private GameObject _sourceActor;
        private TeamAffiliation _sourceTeam = TeamAffiliation.None;
        private Camera _sourceCamera;
        private float _damagePerTick;

        private float _timeElapsed;
        private float _tickTimer;

        private BoxCollider _box;
        // Reusable buffers so the per-tick scan doesn't allocate.
        private readonly Collider[] _overlapBuffer = new Collider[32];
        private readonly HashSet<CharacterStats> _hitThisTick = new HashSet<CharacterStats>();

        public void Initialize(GameObject sourceActor, float damagePerTick)
        {
            _sourceActor = sourceActor;
            _damagePerTick = damagePerTick;
            if (sourceActor != null)
            {
                if (sourceActor.TryGetComponent(out CharacterStats stats)) _sourceTeam = stats.Team;
                _sourceCamera = sourceActor.GetComponentInChildren<Camera>();
            }
        }

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            // The BoxCollider isn't used for triggers (enemies don't have rigidbodies). It's
            // just the shape we OverlapBox against each tick, so flagging it non-trigger /
            // disabled doesn't matter — but disabling avoids any incidental physics interaction.
            if (_box != null) _box.isTrigger = true;
        }

        private void Update()
        {
            if (!IsServer) return;

            // Follow the source's camera each tick. NetworkObject can't parent under a
            // non-NetworkObject (camera transform), so we keep pose in sync manually; the
            // hitbox's NetworkTransform replicates the new pose to remote clients.
            if (_sourceCamera != null)
            {
                transform.position = _sourceCamera.transform.position + _sourceCamera.transform.forward * forwardOffset;
                transform.rotation = _sourceCamera.transform.rotation;
            }
            else if (_sourceActor != null)
            {
                transform.position = _sourceActor.transform.position + _sourceActor.transform.forward * forwardOffset + Vector3.up * 1.2f;
                transform.rotation = Quaternion.LookRotation(_sourceActor.transform.forward);
            }

            _timeElapsed += Time.deltaTime;
            _tickTimer += Time.deltaTime;

            if (_timeElapsed >= duration)
            {
                if (TryGetComponent(out NetworkObject nm) && nm.IsSpawned)
                    nm.Despawn(true);
                else
                    Destroy(gameObject);
                return;
            }

            if (_tickTimer >= tickRate)
            {
                _tickTimer = 0f;
                ProcessDamageTick();
            }
        }

        private void ProcessDamageTick()
        {
            if (_box == null) return;

            // World-space oriented bounds for the OverlapBox query.
            Vector3 worldCenter = transform.TransformPoint(_box.center);
            Vector3 halfExtents = Vector3.Scale(_box.size * 0.5f, transform.lossyScale);

            int count = Physics.OverlapBoxNonAlloc(worldCenter, halfExtents, _overlapBuffer, transform.rotation, ~0, QueryTriggerInteraction.Ignore);

            _hitThisTick.Clear();
            for (int i = 0; i < count; i++)
            {
                var hit = _overlapBuffer[i];
                if (hit == null) continue;
                if (_sourceActor != null && hit.transform.IsChildOf(_sourceActor.transform)) continue;

                CharacterStats stats = hit.GetComponentInParent<CharacterStats>();
                if (stats == null || stats.IsDead) continue;

                // Dedupe per character root — a multi-collider enemy rig shouldn't take multiple
                // hits from one tick.
                if (!_hitThisTick.Add(stats)) continue;

                // Friendly-fire: same-team teammates pass through unharmed. PvE enemies are
                // TeamAffiliation.None while Acheron is Thrive, so the check correctly damages
                // them (None != Thrive).
                if (stats.Team == _sourceTeam) continue;

                DamageContext ctx = new DamageContext
                {
                    Source = _sourceActor,
                    Target = stats.gameObject,
                    Damage = Mathf.RoundToInt(_damagePerTick),
                    DamageType = DamageType.Presence,
                    AttackType = AttackType.Contact
                };
                stats.TakeDamage(ctx);

                if (stats.TryGetComponent(out IBurdenable burdenable))
                {
                    burdenable.AddBurden(burdenAddedPerTick);
                }

                // Apply / refresh the Magma blob's poison DOT. Single PoisonTracker per
                // target — Initialize refreshes the existing one rather than stacking.
                if (!stats.TryGetComponent(out PoisonTracker poison))
                {
                    poison = stats.gameObject.AddComponent<PoisonTracker>();
                }
                poison.Initialize(_sourceActor, poisonDuration, poisonDamagePerTick, poisonTickRate);
            }
        }
    }
}
