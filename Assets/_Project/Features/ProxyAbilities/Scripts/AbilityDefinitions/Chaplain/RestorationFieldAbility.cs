using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Chaplain
{
    [CreateAssetMenu(fileName = "RestorationField", menuName = "Proxy Abilities/Chaplain/Restoration Field")]
    public class RestorationFieldAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Deployable Settings")]
        [SerializeField] private GameObject fieldPrefab;
        [SerializeField] private float throwForce = 10f;
        [SerializeField] private float throwUpwardArc = 2f;

        [Header("Ability VFX/SFX Overrides")]
        [SerializeField] private AudioClip deploySound;

        public void Execute(GameObject instigator)
        {
            if (fieldPrefab == null)
            {
                Debug.LogWarning("RestorationFieldAbility has no deployable prefab set!");
                return;
            }

            if (NetworkManager.Singleton.IsServer)
            {
                Camera playerCam = instigator.GetComponentInChildren<Camera>();
                Vector3 spawnPos = (playerCam != null) ? playerCam.transform.position + playerCam.transform.forward * 1.5f : instigator.transform.position + instigator.transform.forward * 1.5f + Vector3.up;
                // Lift the spawn point so the field deploys 2m higher than the toss origin.
                spawnPos.y += 2f;

                GameObject fieldInstance = Instantiate(fieldPrefab, spawnPos, instigator.transform.rotation);
                
                if (fieldInstance.TryGetComponent(out RestorationFieldEntity fieldEntity))
                {
                    fieldEntity.Initialize(instigator);
                }

                if (fieldInstance.TryGetComponent(out Rigidbody rb))
                {
                    Vector3 forwardDir = (playerCam != null) ? playerCam.transform.forward : instigator.transform.forward;
                    rb.AddForce(forwardDir * throwForce + Vector3.up * throwUpwardArc, ForceMode.Impulse);
                }

                if (fieldInstance.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();
                }
            }

            if (deploySound != null)
            {
                AudioSource.PlayClipAtPoint(deploySound, instigator.transform.position);
            }

            Debug.Log($"[{nameof(RestorationFieldAbility)}] {instigator.name} deployed a Restoration Field!");
        }
    }
}
