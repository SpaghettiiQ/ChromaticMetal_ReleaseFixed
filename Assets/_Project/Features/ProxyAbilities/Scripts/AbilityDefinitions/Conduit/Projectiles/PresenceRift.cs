using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Features.Player.Scripts;
using _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Projectiles
{
    /// <summary>
    /// "Oppressive Control" rift. Pulls enemies toward its centre, slows them, ticks small
    /// Presence damage. After `duration` seconds it collapses — heavy damage + huge Burden to
    /// every character in the explosion radius INCLUDING teammates (intentional per the
    /// design: high-risk team-wipe potential is the whole point of the special).
    /// </summary>
    public class PresenceRift : NetworkBehaviour
    {
        [Header("Rift Settings")]
        public float duration = 4f;
        public float pullRadius = 15f;
        public float pullForce = 8f;

        [Header("Tick Settings")]
        public float tickRate = 0.5f;
        public float tickDamage = 5f;
        [Tooltip("Slow applied (and refreshed) on enemies inside the rift each tick. Stat is " +
                 "MovementSpeed Percent — e.g. -0.4 = 40% slower while standing in the pull.")]
        public float slowPercentPerTick = -0.4f;
        [Tooltip("Slow lingers this long after the enemy leaves the rift. Refreshed each tick " +
                 "they're still inside.")]
        public float slowLingerDuration = 0.8f;

        [Header("Collapse Settings")]
        public float explosionRadius = 15f;
        public float explosionDamage = 150f;
        public float explosionBurden = 40f;

        private GameObject _sourceActor;
        private TeamAffiliation _sourceTeam = TeamAffiliation.None;
        private float _spawnTime;
        private float _tickTimer;
        private bool _hasCollapsed;

        // Reused per-tick buffers to keep allocations off the hot path.
        private readonly Collider[] _overlapBuffer = new Collider[64];
        private readonly HashSet<CharacterStats> _processedThisTick = new HashSet<CharacterStats>();

        public void Initialize(GameObject sourceActor, TeamAffiliation sourceTeam)
        {
            _sourceActor = sourceActor;
            _sourceTeam = sourceTeam;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _spawnTime = Time.time;
        }

        private void Update()
        {
            if (!IsServer || _hasCollapsed) return;

            float age = Time.time - _spawnTime;
            if (age >= duration)
            {
                Collapse();
                return;
            }

            ProcessPullAndTick();
        }

        private void ProcessPullAndTick()
        {
            _tickTimer += Time.deltaTime;
            bool doTick = _tickTimer >= tickRate;
            if (doTick) _tickTimer = 0f;

            int count = Physics.OverlapSphereNonAlloc(transform.position, pullRadius, _overlapBuffer, ~0, QueryTriggerInteraction.Ignore);

            _processedThisTick.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null) continue;
                if (_sourceActor != null && col.transform.IsChildOf(_sourceActor.transform)) continue;

                CharacterStats stats = col.GetComponentInParent<CharacterStats>();
                if (stats == null || stats.IsDead) continue;

                // Same-team allies pass through — pull / slow / tick all spare them. Only the
                // final collapse is friendly-fire (per spec).
                if (stats.Team == _sourceTeam) continue;

                if (!_processedThisTick.Add(stats)) continue;

                Vector3 dirToCenter = (transform.position - stats.transform.position).normalized;

                // Pull: PlayerMovement targets get sustained-force-toward-center each tick
                // (each call replaces the prior force, so direction tracks our centre as the
                // target moves). Non-player rigidbodies get a per-frame impulse.
                if (doTick && stats.TryGetComponent(out PlayerMovement movement))
                {
                    // Duration slightly past next tick so there's no gap between refreshes.
                    movement.ApplySustainedForce(dirToCenter * pullForce, tickRate * 1.1f);
                }
                else if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic)
                {
                    col.attachedRigidbody.AddForce(dirToCenter * pullForce * 50f * Time.deltaTime, ForceMode.Force);
                }

                if (!doTick) continue;

                // Tick damage.
                DamageContext ctx = new DamageContext
                {
                    Source = _sourceActor,
                    Target = stats.gameObject,
                    Damage = Mathf.RoundToInt(tickDamage),
                    DamageType = DamageType.Presence,
                    AttackType = AttackType.Effect
                };
                stats.TakeDamage(ctx);

                // Slow: refresh or add the standard SlowTracker so the slow lingers slightly
                // past leaving the volume, then naturally expires.
                if (!stats.TryGetComponent(out SlowTracker slow))
                {
                    slow = stats.gameObject.AddComponent<SlowTracker>();
                }
                slow.Initialize(slowLingerDuration, slowPercentPerTick);
            }
        }

        private void Collapse()
        {
            _hasCollapsed = true;

            int count = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, _overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            _processedThisTick.Clear();

            // Per the design, the collapse hits EVERYONE in radius — allies included. This is
            // the trade-off for the ability's massive damage + burden swing.
            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null) continue;

                CharacterStats stats = col.GetComponentInParent<CharacterStats>();
                if (stats == null || stats.IsDead) continue;
                if (!_processedThisTick.Add(stats)) continue;

                DamageContext ctx = new DamageContext
                {
                    Source = _sourceActor,
                    Target = stats.gameObject,
                    Damage = Mathf.RoundToInt(explosionDamage),
                    DamageType = DamageType.Presence,
                    AttackType = AttackType.Explosion
                };
                stats.TakeDamage(ctx);

                if (stats.TryGetComponent(out IBurdenable burdenable))
                {
                    burdenable.AddBurden(explosionBurden);
                }
            }

            PlayExplosionVFXClientRpc(transform.position);

            if (TryGetComponent(out NetworkObject nm) && nm.IsSpawned)
                nm.Despawn(true);
            else
                Destroy(gameObject);
        }

        [ClientRpc]
        private void PlayExplosionVFXClientRpc(Vector3 position)
        {
            // Hook for explosion VFX prefab spawn on every client — left as a hook so a
            // designer can wire in a particle prefab without changing this script.
        }
    }
}
