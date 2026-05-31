using UnityEngine;
using Unity.Netcode;
using _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Projectiles;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking
{
    public class TheWayOfAllFleshTracker : MonoBehaviour
    {
        public void ExecuteAbility(FleshDenseProjectile projectilePrefab)
        {
            if (projectilePrefab == null) return;

            // Firing the projectile happens securely on the server
            // Client prediction would fire VFX earlier in AbilityController
            StartCoroutine(SpawnRoutine(projectilePrefab));
        }

        private System.Collections.IEnumerator SpawnRoutine(FleshDenseProjectile projectilePrefab)
        {
            // Give a short delay to match windup animation if any, else fire right away
            yield return null; 

            // Find aiming direction
            Camera cam = GetComponentInChildren<Camera>();
            Vector3 aimPos = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.5f;
            Vector3 lookDir = cam != null ? cam.transform.forward : transform.forward;
            
            // Adjust forward so we don't clip the player's own collider
            Vector3 spawnPoint = aimPos + lookDir * 0.7f;

            var projectile = Instantiate(projectilePrefab, spawnPoint, Quaternion.LookRotation(lookDir));
            
            if (projectile.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
            }

            projectile.Initialize(gameObject, lookDir);
        }
    }
}