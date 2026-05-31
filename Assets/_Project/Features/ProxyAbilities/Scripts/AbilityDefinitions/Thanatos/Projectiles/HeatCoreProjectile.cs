using UnityEngine;
using Unity.Netcode;
using _Project.Core.Structs;
using _Project.Core.Stats;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Projectiles
{
    public class HeatCoreProjectile : NetworkBehaviour
    {
        public float delay = 2.0f;
        public float explosionRadius = 5.0f;
        public float explosionDamage = 100f;
        public float launchVelocity = 15f;
        public AnimationCurve blastFalloff;

        [Header("Explosion VFX")]
        [Tooltip("Spawned on every client at the detonation point. Should self-destruct via " +
                 "its own particle/animator since the heat core itself despawns immediately.")]
        public GameObject explosionVfxPrefab;
        [Tooltip("Seconds to keep the VFX instance alive before forcing destroy. 0 = trust the " +
                 "prefab's own lifetime (recommended if it has a Particle System with Stop Action: Destroy).")]
        public float explosionVfxLifetime = 0f;

        private float _spawnTime;
        private GameObject _sourceActor;
        private Rigidbody _rb;

        public void Initialize(GameObject sourceActor, Vector3 direction)
        {
            _sourceActor = sourceActor;
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rb.linearVelocity = direction.normalized * launchVelocity;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _spawnTime = Time.time;
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            if (Time.time - _spawnTime >= delay)
            {
                Explode();
            }
        }

        private void Explode()
        {
            // Resolve the firer's team once so we can skip self + teammates in the blast loop.
            _Project.Core.Enums.TeamAffiliation sourceTeam = _Project.Core.Enums.TeamAffiliation.None;
            if (_sourceActor != null && _sourceActor.TryGetComponent(out CharacterStats sourceStats))
            {
                sourceTeam = sourceStats.Team;
            }

            // Dedupe per character root so multi-collider rigs don't take multiple hits from
            // one explosion.
            System.Collections.Generic.HashSet<CharacterStats> damaged = new System.Collections.Generic.HashSet<CharacterStats>();

            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (Collider hit in hits)
            {
                // Walk up to the character root so hitbox children resolve to the same Stats.
                CharacterStats stats = hit.GetComponentInParent<CharacterStats>();
                if (stats == null) continue;
                if (stats.IsDead) continue;
                if (!damaged.Add(stats)) continue;

                // Skip the firer themselves.
                if (_sourceActor != null && stats.gameObject == _sourceActor) continue;

                // Skip teammates (including the Thrive team Thanatos belongs to). Both teams
                // being None doesn't friendly-fire either — keeps enemies safe from each other
                // in case a stray heat core triggers near them.
                if (stats.Team == sourceTeam) continue;

                float dist = Vector3.Distance(transform.position, stats.transform.position);
                float mult = 1f;
                if (blastFalloff != null && blastFalloff.keys.Length > 0)
                    mult = blastFalloff.Evaluate(dist / explosionRadius);

                DamageContext ctx = new DamageContext
                {
                    Source = _sourceActor,
                    Target = stats.gameObject,
                    Damage = (int)(explosionDamage * mult)
                };
                stats.TakeDamage(ctx);
            }
            
            // Tell every client to spawn the VFX at this position before despawning the core
            // itself — otherwise we'd lose transform.position by the time the RPC arrives.
            Vector3 explosionPos = transform.position;
            PlayExplosionVFXClientRpc(explosionPos);

            if (TryGetComponent(out NetworkObject netObj))
                netObj.Despawn(true);
            else
                Destroy(gameObject);
        }

        [ClientRpc]
        private void PlayExplosionVFXClientRpc(Vector3 position)
        {
            if (explosionVfxPrefab == null) return;
            GameObject vfx = Instantiate(explosionVfxPrefab, position, Quaternion.identity);
            if (explosionVfxLifetime > 0f)
            {
                Destroy(vfx, explosionVfxLifetime);
            }
        }
    }
}