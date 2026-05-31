using UnityEngine;
using Unity.Netcode;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Core.Enums;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Projectiles
{
    /// <summary>
    /// Acheron's standard-fire projectile. Hits an enemy → immediate impact damage + poison
    /// DOT via PoisonTracker. Hits geometry / lifetime expires → just despawns. Same-team
    /// targets pass through unharmed.
    /// </summary>
    public class MagmaBlobProjectile : NetworkBehaviour
    {
        public float speed = 25f;
        public float lifetime = 5f;
        public float baseDamage = 15f;

        [Header("Poison Settings")]
        public float poisonDuration = 4f;
        public float poisonDamagePerTick = 3f;
        public float poisonTickRate = 0.5f;

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

            // Pass through our own colliders entirely.
            if (_sourceActor != null && other.transform.IsChildOf(_sourceActor.transform)) return;

            CharacterStats stats = other.GetComponentInParent<CharacterStats>();
            if (stats != null)
            {
                // Friendly-fire: same-team (including None == None) → projectile passes through
                // the ally without resolving, matching the project-wide convention.
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

                // Refresh-or-apply poison via PoisonTracker (single component per target).
                if (!stats.TryGetComponent(out Tracking.PoisonTracker poison))
                {
                    poison = stats.gameObject.AddComponent<Tracking.PoisonTracker>();
                }
                poison.Initialize(_sourceActor, poisonDuration, poisonDamagePerTick, poisonTickRate);
            }

            // Whether we hit a valid target or just geometry, resolve here. (If we hit nothing
            // damageable, this is just "the blob splatted on the wall" — despawn quietly.)
            PlayImpactVFXClientRpc(transform.position);
            Despawn();
        }

        private void Despawn()
        {
            _hasResolved = true;
            if (TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                netObj.Despawn(true);
            else
                Destroy(gameObject);
        }

        [ClientRpc]
        private void PlayImpactVFXClientRpc(Vector3 pos)
        {
            // Optional impact particle hook — assigned via separate prefab if desired.
        }
    }
}
