using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations;
using _Project.Core.Interfaces;

namespace _Project.Features.Weapons.Scripts
{
    public class WeaponSpawner : NetworkBehaviour, IWeaponSpawner, IWeaponMount
    {
        [Header("Mount Points")]
        [Tooltip("The Transform where the weapon should be attached (e.g., Viewmodel Camera)")]
        [SerializeField] private Transform weaponMountPoint;

        [Header("Reload / Overheat Dip")]
        [Tooltip("How far the mount point drops (along local Y) while reloading or cooling.")]
        [SerializeField] private float dipDistance = 2f;
        [Tooltip("Seconds to drop down to the dipped position.")]
        [SerializeField] private float dipDownDuration = 0.15f;
        [Tooltip("Seconds to return back up when reload finishes.")]
        [SerializeField] private float dipUpDuration = 0.2f;

        private NetworkObject _currentWeaponNetObj;
        private Vector3 _mountRestLocalPos;
        private bool _mountRestCaptured;
        private Coroutine _dipRoutine;

        public void SpawnWeaponForCharacter(IWeaponData weaponData)
        {
            Debug.Log($"[WeaponSpawner] SpawnWeaponForCharacter Triggered.");
            if (!IsServer) { Debug.LogError($"[WeaponSpawner] Failed: Not Server"); return; }
            if (weaponData == null) { Debug.LogError($"[WeaponSpawner] Failed: WeaponData is null"); return; }
            if (weaponData.WeaponPrefab == null) { Debug.LogError($"[WeaponSpawner] Failed: WeaponPrefab is null (Data name: {weaponData.WeaponName})"); return; }
            if (weaponMountPoint == null) { Debug.LogError($"[WeaponSpawner] Failed: Mount Point is missing!"); return; }

            if (_currentWeaponNetObj != null && _currentWeaponNetObj.IsSpawned)
            {
                _currentWeaponNetObj.Despawn(true);
            }

            Debug.Log($"[WeaponSpawner] Instantiating Weapon: {weaponData.WeaponPrefab.name} at {weaponMountPoint.name}");
            GameObject weaponInstance = Instantiate(weaponData.WeaponPrefab, weaponMountPoint.position, weaponMountPoint.rotation);

            // Inherit phase from the owning player so the weapon hides/renders with them.
            var ownerPhased = GetComponent<_Project.Core.Networking.PhasedNetworkObject>();
            if (ownerPhased != null &&
                weaponInstance.TryGetComponent<_Project.Core.Networking.PhasedNetworkObject>(out var weaponPhased))
            {
                weaponPhased.InitialPhase = ownerPhased.Phase;
            }

            if (weaponInstance.TryGetComponent<NetworkObject>(out _currentWeaponNetObj))
            {
                _currentWeaponNetObj.SpawnWithOwnership(OwnerClientId);
                
                // 1. Parent to the root NetworkObject to satisfy Unity Netcode requirements
                _currentWeaponNetObj.TrySetParent(GetComponent<NetworkObject>(), false);

                // Tell clients to attach it using their local mount point
                RpcAttachWeaponClientRpc(_currentWeaponNetObj);
            }
            else 
            {
                Debug.LogError($"[WeaponSpawner] Weapon Prefab ({weaponData.WeaponPrefab.name}) missing NetworkObject component! It cannot be synced across the network!");
            }
        }

        [ClientRpc]
        private void RpcAttachWeaponClientRpc(NetworkObjectReference weaponObRef)
        {
            if (weaponObRef.TryGet(out NetworkObject weaponObj))
            {
                // Visually snap it to the weaponMountPoint bone using Unity's ParentConstraint
                ParentConstraint constraint = weaponObj.gameObject.AddComponent<ParentConstraint>();
                ConstraintSource source = new ConstraintSource { sourceTransform = weaponMountPoint, weight = 1f };
                constraint.AddSource(source);
                
                // Set offsets to zero so it perfectly aligns
                constraint.SetTranslationOffset(0, Vector3.zero);
                constraint.SetRotationOffset(0, Vector3.zero);
                
                constraint.constraintActive = true;
            }
        }

        public void BeginReloadDip()
        {
            if (weaponMountPoint == null) return;
            CaptureRest();
            if (_dipRoutine != null) StopCoroutine(_dipRoutine);
            _dipRoutine = StartCoroutine(DipRoutine(_mountRestLocalPos + Vector3.down * dipDistance, dipDownDuration));
        }

        public void EndReloadDip()
        {
            if (weaponMountPoint == null || !_mountRestCaptured) return;
            if (_dipRoutine != null) StopCoroutine(_dipRoutine);
            _dipRoutine = StartCoroutine(DipRoutine(_mountRestLocalPos, dipUpDuration));
        }

        private void CaptureRest()
        {
            if (_mountRestCaptured) return;
            _mountRestLocalPos = weaponMountPoint.localPosition;
            _mountRestCaptured = true;
        }

        private IEnumerator DipRoutine(Vector3 target, float duration)
        {
            Vector3 start = weaponMountPoint.localPosition;
            float t = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                weaponMountPoint.localPosition = Vector3.Lerp(start, target, a);
                yield return null;
            }
            weaponMountPoint.localPosition = target;
            _dipRoutine = null;
        }
    }
}