using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;
using _Project.Features.BurdenSystem.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Chaplain
{
    public class RestorationFieldEntity : NetworkBehaviour
    {
        [Header("Field Settings")]
        [SerializeField] private float radius = 8f;
        [SerializeField] private float tickInterval = 1f;
        [SerializeField] private int healPerTick = 5;
        [SerializeField] private float burdenReductionPerTick = 2f;
        [SerializeField] private float lifetime = 15f;

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
                StartCoroutine(FieldRoutine());
                Invoke(nameof(DespawnSelf), lifetime);

                if (TryGetComponent(out CharacterStats selfStats))
                {
                    selfStats.OnDeath += DespawnSelf;
                    // If Team needs to match for some reason, we could set selfStats.Team = _ownerTeam;
                    selfStats.Team = _ownerTeam; 
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && TryGetComponent(out CharacterStats selfStats))
            {
                selfStats.OnDeath -= DespawnSelf;
            }
        }

        private IEnumerator FieldRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(tickInterval);
                ProcessFieldTick();
                TriggerPulseCosmeticsClientRpc();
            }
        }

        private void ProcessFieldTick()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.TryGetComponent(out CharacterStats targetStats) && !targetStats.IsDead)
                {
                    // Check if it's an ally
                    if ((_ownerTeam != TeamAffiliation.None && targetStats.Team == _ownerTeam) || hitCollider.gameObject == _owner)
                    {
                        // Calculate potential bonus heal from Chaplain's passive
                        int finalHeal = healPerTick;
                        if (_owner != null && _owner.TryGetComponent(out FieldMedicPassiveTracker medic))
                        {
                            finalHeal = medic.CalculateModifiedHeal(targetStats, healPerTick);
                        }
                        
                        targetStats.Heal(finalHeal);

                        // Reduce burden
                        if (hitCollider.TryGetComponent(out BurdenController targetBurden))
                        {
                            targetBurden.RemoveBurden(burdenReductionPerTick);
                        }
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

        private void DespawnSelf()
        {
            if (TryGetComponent(out NetworkObject netObj))
            {
                netObj.Despawn();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}