using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Centurion
{
    [CreateAssetMenu(fileName = "PurgeBeacon", menuName = "Proxy Abilities/Centurion/Purge Beacon")]
    public class PurgeBeaconAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Beacon Settings")]
        [SerializeField] private GameObject beaconPrefab;
        [SerializeField] private float placementDistance = 2f;

        [Header("Ability VFX/SFX Overrides")]
        [SerializeField] private AudioClip deploySound;

        public void Execute(GameObject instigator)
        {
            if (beaconPrefab == null)
            {
                Debug.LogWarning("PurgeBeaconAbility has no beacon prefab set!");
                return;
            }

            // Only the server should spawn network objects (or the client asks the server to spawn it)
            // Assuming AbilityController triggers this, ideally this logic is ran on Server or locally requests a spawn.
            // For now, if we are the server, we spawn it. If not, the instigator needs to request it.
            if (NetworkManager.Singleton.IsServer)
            {
                Vector3 spawnPos = instigator.transform.position + (instigator.transform.forward * placementDistance);
                GameObject beaconInstance = Instantiate(beaconPrefab, spawnPos, Quaternion.identity);
                
                if (beaconInstance.TryGetComponent(out PurgeBeaconEntity beaconEntity))
                {
                    beaconEntity.Initialize(instigator);
                }

                if (beaconInstance.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();
                }
            }
            else
            {
                // Note: To be fully network compliant if a client triggers this, 
                // the AbilityController should ideally call a ServerRpc that then calls Execute on the server.
                // Assuming that architecture is in place, this will only run when the server executes it.
                Debug.Log("Client invoked PurgeBeacon. Ensure AbilityController calls this on Server!");
            }

            if (deploySound != null)
            {
                AudioSource.PlayClipAtPoint(deploySound, instigator.transform.position);
            }

            Debug.Log($"[{nameof(PurgeBeaconAbility)}] {instigator.name} deployed a Purge Beacon!");
        }
    }
}
