using Unity.Netcode;
using UnityEngine;
using System;
using _Project.Core.Interfaces;
using _Project.Core.Structs;
using _Project.Core.Stats;

namespace _Project.Features.Weapons.Scripts
{
    public class AngelRayProjectile : NetworkBehaviour
    {
        public float speed = 20f;
        public float maxLifeTime = 5f;
        public int damage = 10;
        public float hitRadius = 0.25f;
        public float burdenAmount = 5f;
        public TrailRenderer cosmeticTrail;
        
        private float _lifeTimer;
        [HideInInspector] public GameObject source;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _lifeTimer = maxLifeTime;
            }
        }

        private void Update()
        {
            if (IsServer)
            {
                _lifeTimer -= Time.deltaTime;
                if (_lifeTimer <= 0)
                {
                    DespawnProjectile();
                    return;
                }

                float moveDistance = speed * Time.deltaTime;
                
                // Prevent passing through targets at high speeds by sweeping ahead
                RaycastHit[] hits = Physics.SphereCastAll(transform.position, hitRadius, transform.forward, moveDistance, Physics.AllLayers, QueryTriggerInteraction.Collide);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var hit in hits)
                {
                    if (HandleHit(hit.collider))
                    {
                        return; // Hit something valid and despawned, stop moving
                    }
                }

                transform.position += transform.forward * moveDistance;
            }
            else
            {
                // Smooth client-side prediction movement
                transform.position += transform.forward * (speed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            HandleHit(other);
        }

        private bool HandleHit(Collider other)
        {
            // Prevent hitting ourselves intrinsically
            if (other.gameObject == gameObject) return false;

            // Prevent hitting the source (who fired us)
            if (source != null && other.transform.root == source.transform.root) return false;

            // Friendly-fire block: enemies all default to TeamAffiliation.None, so without a
            // same-team filter their projectiles would happily damage each other. Compare both
            // teams including None — only block when they match so cross-team combat still works.
            CharacterStats sourceStats = source != null ? source.GetComponentInParent<CharacterStats>() : null;
            CharacterStats targetCharStats = other.GetComponentInParent<CharacterStats>();
            if (sourceStats != null && targetCharStats != null && targetCharStats.Team == sourceStats.Team)
            {
                return false; // Pass through the ally without despawning.
            }

            bool hitCharacter = false;

            DamageContext ctx = new DamageContext
            {
                Source = source,
                Target = other.gameObject,
                Damage = damage
            };

            // First check for IDamageContextReceiver to apply standard damage cleanly
            if (other.GetComponentInParent<IDamageContextReceiver>() is { } receiver)
            {
                receiver.SetDamageContext(ctx);
                hitCharacter = true;
            }
            // Fallback for CharacterStats if it doesn't implement it but has TakeDamage
            else if (other.GetComponentInParent<CharacterStats>() is { } stats)
            {
                ctx.Target = stats.gameObject;
                stats.TakeDamage(ctx);
                hitCharacter = true;
            }

            if (other.GetComponentInParent<IBurdenable>() is { } burdenable)
            {
                burdenable.AddBurden(burdenAmount);
            }

            // Only ignore triggers if they aren't part of a character being damaged.
            // This prevents the projectile from breaking on things like "Room Volumes" 
            if (other.isTrigger && !hitCharacter)
            {
                return false;
            }

            DespawnProjectile();
            return true;
        }

        private void DespawnProjectile()
        {
            if (cosmeticTrail != null)
            {
                cosmeticTrail.transform.SetParent(null);
                cosmeticTrail.autodestruct = true;
            }
            NetworkObject.Despawn();
        }
    }
}
