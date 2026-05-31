using UnityEngine;
using Unity.Netcode;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;
using _Project.Core.Interfaces;
using _Project.Features.BurdenSystem.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Projectiles
{
    /// <summary>
    /// "The Way of All Flesh" (secondary) projectile. Enemy hit → impact damage, burden,
    /// movement slow. Ground hit → spawn a residue pool that's friendly to Acheron's team.
    /// </summary>
    public class FleshDenseProjectile : NetworkBehaviour
    {
        public float speed = 30f;
        public float baseDamage = 35f;
        public float lifetime = 6f;

        [Header("Target Debuff")]
        public float addedBurden = 15f;
        public float slowDuration = 3f;
        [Tooltip("Percentage to reduce speed (e.g. -0.5 is 50% slow). Applied as a Percent " +
                 "modifier to MovementSpeed.")]
        public float slowPercent = -0.5f;

        [Header("Ground Impact")]
        public GameObject residuePoolPrefab;

        private GameObject _sourceActor;
        private TeamAffiliation _sourceTeam = TeamAffiliation.None;
        private float _spawnTime;
        private bool _hasResolved;

        public void Initialize(GameObject sourceActor, Vector3 direction)
        {
            _sourceActor = sourceActor;
            if (sourceActor != null && sourceActor.TryGetComponent(out CharacterStats stats))
            {
                _sourceTeam = stats.Team;
            }
            if (TryGetComponent(out Rigidbody rb))
            {
                rb.linearVelocity = direction.normalized * speed;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _spawnTime = Time.time;
        }

        private void Update()
        {
            if (!IsServer || _hasResolved) return;
            if (Time.time - _spawnTime >= lifetime) Despawn();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || _hasResolved) return;

            // Pass through our own colliders.
            if (_sourceActor != null && other.transform.IsChildOf(_sourceActor.transform)) return;

            Vector3 hitPoint = transform.position;

            CharacterStats stats = other.GetComponentInParent<CharacterStats>();
            if (stats != null)
            {
                // Friendly-fire: same-team passes through unharmed (no slow, no burden, no
                // residue pool — the projectile just keeps going for the ally).
                if (stats.Team == _sourceTeam) return;
                if (stats.IsDead) return;

                DamageContext ctx = new DamageContext
                {
                    Source = _sourceActor,
                    Target = stats.gameObject,
                    Damage = Mathf.RoundToInt(baseDamage),
                    DamageType = DamageType.Presence,
                    AttackType = AttackType.Projectile
                };
                stats.TakeDamage(ctx);

                if (stats.TryGetComponent(out IBurdenable burdenTarget))
                {
                    burdenTarget.AddBurden(addedBurden);
                }

                if (!stats.TryGetComponent(out Tracking.SlowTracker slow))
                {
                    slow = stats.gameObject.AddComponent<Tracking.SlowTracker>();
                }
                slow.Initialize(slowDuration, slowPercent);
            }
            else
            {
                // Geometry hit — spawn a friendly residue pool.
                hitPoint = other.ClosestPoint(transform.position);
                SpawnResiduePool(hitPoint);
            }

            PlayImpactVFXClientRpc(hitPoint);
            Despawn();
        }

        private void SpawnResiduePool(Vector3 position)
        {
            if (residuePoolPrefab == null) return;

            // Snap to ground so the pool sits flush instead of floating.
            if (!Physics.Raycast(position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 5f)) return;

            GameObject pool = Instantiate(residuePoolPrefab, hit.point + Vector3.up * 0.05f, Quaternion.identity);

            // Mark the pool as ability-originated so Acheron's own team doesn't eat its damage
            // or burden (burden has a true-damage DOT at high tiers — see Of The Abyss bug).
            // GetComponentInChildren because residue prefabs typically put the ResidueZone on
            // a visual child of the NetworkObject root.
            var zone = pool.GetComponentInChildren<ResidueZone>(true);
            if (zone != null)
            {
                zone.MarkAsAbilityOriginated(_sourceTeam);
            }

            if (pool.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
            }
        }

        private void Despawn()
        {
            _hasResolved = true;
            if (TryGetComponent(out NetworkObject nm) && nm.IsSpawned)
                nm.Despawn(true);
            else
                Destroy(gameObject);
        }

        [ClientRpc]
        private void PlayImpactVFXClientRpc(Vector3 pos)
        {
            // Optional impact splash hook.
        }
    }
}
