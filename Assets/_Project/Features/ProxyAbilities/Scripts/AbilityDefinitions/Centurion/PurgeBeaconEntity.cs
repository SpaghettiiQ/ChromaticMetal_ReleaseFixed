using System.Collections;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;
using _Project.Features.BurdenSystem.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Centurion
{
    public class PurgeBeaconEntity : NetworkBehaviour
    {
        [Header("Beacon Settings")]
        [SerializeField] private float activeDuration = 10f;
        [SerializeField] private float pulseInterval = 2f;
        [SerializeField] private float pulseRadius = 10f;
        [SerializeField] private int damagePerPulse = 15;

        [Header("Audiovisual")]
        [SerializeField] private GameObject pulseVfxPrefab;
        [SerializeField] private AudioClip pulseSound;
        [SerializeField] private AudioSource audioSource;

        private TeamAffiliation _ownerTeam = TeamAffiliation.None;
        private GameObject _owner;

        public void Initialize(GameObject owner)
        {
            _owner = owner;
            if (owner != null && owner.TryGetComponent(out CharacterStats ownerStats))
            {
                _ownerTeam = ownerStats.Team;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            if (IsServer)
            {
                StartCoroutine(BeaconRoutine());
            }
        }

        private IEnumerator BeaconRoutine()
        {
            float timer = 0f;
            while (timer < activeDuration)
            {
                EmitPulse();
                TriggerPulseCosmeticsClientRpc();
                
                yield return new WaitForSeconds(pulseInterval);
                timer += pulseInterval;
            }
            
            // Cleanup
            if (TryGetComponent(out NetworkObject netObj))
            {
                netObj.Despawn();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void EmitPulse()
        {
            System.Collections.Generic.HashSet<CharacterStats> damaged = new System.Collections.Generic.HashSet<CharacterStats>();
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, pulseRadius);
            foreach (var hitCollider in hitColliders)
            {
                // 1. Damage Enemies
                CharacterStats stats = hitCollider.GetComponentInParent<CharacterStats>();
                if (stats != null && !stats.IsDead && damaged.Add(stats))
                {
                    // Damage if it's an enemy or if owner team is None (meaning hit everyone except owner)
                    if (stats.gameObject != _owner && (stats.Team != _ownerTeam || _ownerTeam == TeamAffiliation.None))
                    {
                        DamageContext ctx = new DamageContext
                        {
                            Source = _owner != null ? _owner : gameObject, // Attribute to owner if possible
                            Target = stats.gameObject,
                            Damage = damagePerPulse,
                            DamageType = DamageType.Purifying,
                            AttackType = AttackType.Effect,
                            IsCritical = false
                        };
                        stats.TakeDamage(ctx);
                    }
                }

                // 2. Remove Residue Zones
                if (hitCollider.TryGetComponent(out ResidueZone residueZone))
                {
                    if (residueZone.TryGetComponent(out NetworkObject residueNetObj))
                    {
                        residueNetObj.Despawn();
                    }
                    else
                    {
                        Destroy(residueZone.gameObject);
                    }
                }
            }
        }

        [ClientRpc]
        private void TriggerPulseCosmeticsClientRpc()
        {
            if (pulseVfxPrefab != null)
            {
                Instantiate(pulseVfxPrefab, transform.position, Quaternion.identity);
            }

            if (pulseSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(pulseSound);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, pulseRadius);
        }
    }
}
