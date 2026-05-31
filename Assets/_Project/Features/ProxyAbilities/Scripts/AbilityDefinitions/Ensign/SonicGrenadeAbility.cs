using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Ensign
{
    [CreateAssetMenu(fileName = "SonicGrenade", menuName = "Proxy Abilities/Ensign/Sonic Grenade")]
    public class SonicGrenadeAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Grenade Settings")]
        [SerializeField] private GameObject sonicGrenadePrefab;
        [SerializeField] private float throwForce = 15f;
        [SerializeField] private float throwUpwardArc = 3f;

        [Header("Ability VFX/SFX Overrides (Optional)")]
        [SerializeField] private AudioClip throwSound;

        public void Execute(GameObject instigator)
        {
            if (sonicGrenadePrefab == null)
            {
                Debug.LogWarning("SonicGrenadeAbility has no grenade prefab set!");
                return;
            }

            // Execute is always called server-side via RequestUseAbilityServerRpc.
            if (!NetworkManager.Singleton.IsServer) return;

            Camera playerCam = instigator.GetComponentInChildren<Camera>();
            Vector3 forward;
            Vector3 spawnPos;
            if (playerCam != null)
            {
                // Throw along the player's actual aim. Spawn just in front of the camera so the
                // grenade doesn't materialise inside the thrower's collider (which previously
                // absorbed the impulse and left it dropping straight down at head height).
                forward = playerCam.transform.forward;
                spawnPos = playerCam.transform.position + forward * 0.8f;
            }
            else
            {
                forward = instigator.transform.forward;
                spawnPos = instigator.transform.position + Vector3.up * 1.5f + forward * 0.8f;
            }

            GameObject grenadeInstance = Instantiate(sonicGrenadePrefab, spawnPos, Quaternion.LookRotation(forward));

            // Spawn on network before applying physics so the object exists on all clients.
            if (grenadeInstance.TryGetComponent(out NetworkObject netObj))
                netObj.Spawn();

            // Attribute damage to the thrower so on-hit/on-kill hooks credit them, not the grenade.
            if (grenadeInstance.TryGetComponent(out SonicGrenadeProjectile projectile))
                projectile.Initialize(instigator);

            if (throwSound != null)
                AudioSource.PlayClipAtPoint(throwSound, instigator.transform.position);

            // Rigidbody lives on a child of the prefab root (root holds NetworkObject only),
            // so root.TryGetComponent would silently miss it and the impulse would never apply.
            Rigidbody rb = grenadeInstance.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                // Defend against any residual overlap by skipping collision with every collider
                // on the thrower until the grenade has cleared the body.
                var grenadeColliders = grenadeInstance.GetComponentsInChildren<Collider>();
                var instigatorColliders = instigator.GetComponentsInChildren<Collider>();
                foreach (var gc in grenadeColliders)
                {
                    if (gc == null) continue;
                    foreach (var ic in instigatorColliders)
                    {
                        if (ic == null) continue;
                        Physics.IgnoreCollision(gc, ic, true);
                    }
                }

                rb.AddForce(forward * throwForce + Vector3.up * throwUpwardArc, ForceMode.Impulse);
            }
            else
                Debug.LogWarning("SonicGrenadePrefab missing Rigidbody — add a NetworkRigidbody for physics sync.");

            Debug.Log($"[{nameof(SonicGrenadeAbility)}] {instigator.name} threw a Sonic Grenade!");
        }
    }
}