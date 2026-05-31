using Unity.Netcode;
using UnityEngine;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Core.Enums;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Ensign
{
    public class SonicGrenadeProjectile : NetworkBehaviour
    {
        [Header("Explosion Settings")]
        [SerializeField] private float explosionDelay = 1.5f;
        [SerializeField] private float explosionRadius = 5f;
        [SerializeField] private float explosionDamage = 50f;

        [Header("Audiovisual")]
        [SerializeField] private GameObject explosionVfxPrefab;
        [SerializeField] private AudioClip explosionSound;

        private GameObject _sourceActor;
        private float _spawnTime;
        private bool _hasExploded;
        private Transform _physicsBody;

        public void Initialize(GameObject sourceActor)
        {
            _sourceActor = sourceActor;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _spawnTime = Time.time;

            // The Rigidbody/visual mesh live on a child of this root, so root.transform stays
            // pinned at the throw origin. Track the child so Explode() detonates at the actual
            // grenade location instead of where the thrower stood.
            Rigidbody childRb = GetComponentInChildren<Rigidbody>();
            _physicsBody = childRb != null ? childRb.transform : transform;
        }

        private void Update()
        {
            if (!IsServer || _hasExploded) return;
            if (Time.time - _spawnTime >= explosionDelay) Explode();
        }

        private void Explode()
        {
            if (_hasExploded) return;
            _hasExploded = true;

            Vector3 explosionPos = _physicsBody != null ? _physicsBody.position : transform.position;

            // Tell every client to play the explosion VFX/SFX at this position.
            PlayExplosionCosmeticsClientRpc(explosionPos);

            // Server-authoritative AoE damage.
            System.Collections.Generic.HashSet<CharacterStats> damaged = new System.Collections.Generic.HashSet<CharacterStats>();
            Collider[] hits = Physics.OverlapSphere(explosionPos, explosionRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                CharacterStats stats = hits[i].GetComponentInParent<CharacterStats>();
                if (stats == null || stats.IsDead) continue;
                if (!damaged.Add(stats)) continue;

                DamageContext ctx = new DamageContext
                {
                    Source = _sourceActor != null ? _sourceActor : gameObject,
                    Target = stats.gameObject,
                    Damage = Mathf.RoundToInt(explosionDamage),
                    DamageType = DamageType.Explosive,
                    AttackType = AttackType.Explosion,
                    IsCritical = false
                };
                stats.TakeDamage(ctx);
            }

            if (TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                netObj.Despawn(true);
            else
                Destroy(gameObject);
        }

        [ClientRpc]
        private void PlayExplosionCosmeticsClientRpc(Vector3 position)
        {
            if (explosionVfxPrefab != null)
            {
                Instantiate(explosionVfxPrefab, position, Quaternion.identity);
            }

            if (explosionSound != null)
            {
                AudioSource.PlayClipAtPoint(explosionSound, position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
