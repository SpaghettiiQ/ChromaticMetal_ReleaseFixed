using Unity.Netcode;
using UnityEngine;

namespace _Project.Features.Weapons.Scripts
{
    public class AngelRayWeapon : MonoBehaviour
    {
        public AngelRayProjectile projectilePrefab;
        public Transform firePoint;
        public float fireCooldown = 1f;
        
        private float _nextFireTime;

        public bool CanFire => Time.time >= _nextFireTime;

        public void Fire(Vector3 direction)
        {
            Fire(direction, -1);
        }

        public void Fire(Vector3 direction, int damageOverride)
        {
            if (Time.time < _nextFireTime) return;
            _nextFireTime = Time.time + fireCooldown;

            if (projectilePrefab != null)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    Quaternion rotation = Quaternion.LookRotation(direction);
                    Vector3 position = firePoint != null ? firePoint.position : transform.position;

                    AngelRayProjectile proj = Instantiate(projectilePrefab, position, rotation);

                    if (damageOverride > 0)
                    {
                        proj.damage = damageOverride;
                    }

                    if (proj.TryGetComponent<NetworkObject>(out var netObj))
                    {
                        netObj.Spawn(true);
                    }
                    proj.source = transform.root.gameObject;
                }
            }
        }
    }
}
