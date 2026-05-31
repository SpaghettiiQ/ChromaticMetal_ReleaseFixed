using UnityEngine;
using Unity.Netcode;
using _Project.Core.Interfaces;
using _Project.Features.WeaponCore.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking
{
    /// <summary>
    /// "Liquid Fire" — drains all of Acheron's accumulated Burden, then spits out a
    /// forward flamethrower-shaped hitbox in front of the camera. Damage scales with the
    /// drained Burden, so the ability rewards letting your meter ride high (which pairs
    /// with Only Pain's DR scaling).
    ///
    /// While the hitbox is alive: the weapon's FireLocked flag is on (no shooting), and
    /// TheWayOfAllFleshAbility.CanBeUsed reads IsChanneling to block the secondary cast.
    /// </summary>
    public class LiquidFireTracker : MonoBehaviour
    {
        private LiquidFireHitbox _activeHitbox;
        private NetworkWeapon _weapon;
        private bool _weaponLockApplied;

        /// <summary>True while the flame channel is live. Read by TheWayOfAllFleshAbility.CanBeUsed.</summary>
        public bool IsChanneling => _activeHitbox != null;

        public void ExecuteAbility(LiquidFireHitbox hitboxPrefab, float baseDamage, float damagePerBurden)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (hitboxPrefab == null) return;

            float consumedBurden = 0f;
            if (TryGetComponent(out IBurdenable burdenable))
            {
                consumedBurden = burdenable.GetCurrentBurden();
                burdenable.RemoveBurden(consumedBurden);
            }

            float finalDamage = baseDamage + (consumedBurden * damagePerBurden);

            // Spawn the hitbox at the camera's current world pose. The hitbox itself
            // updates its position every server tick from the source actor's camera, so
            // we don't need to parent under a non-NetworkObject (which would silently
            // fail with Netcode). See LiquidFireHitbox.Update.
            Camera cam = GetComponentInChildren<Camera>();
            Vector3 spawnPos = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.5f;
            Quaternion spawnRot = cam != null ? cam.transform.rotation : transform.rotation;

            _activeHitbox = Instantiate(hitboxPrefab, spawnPos, spawnRot);

            if (_activeHitbox.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
            }

            _activeHitbox.Initialize(gameObject, finalDamage);

            // Lock standard fire on whichever weapon Acheron is holding. Cleared in Update
            // once the hitbox despawns (Unity nulls the field via fake-null on Destroy).
            _weapon = GetComponentInChildren<NetworkWeapon>();
            if (_weapon != null)
            {
                _weapon.FireLocked = true;
                _weaponLockApplied = true;
            }
        }

        private void Update()
        {
            // Hitbox lifetime is the source of truth — when LiquidFireHitbox.Update times
            // out and Despawn fires, _activeHitbox transitions to fake-null on the next
            // frame, which is our signal to release the fire lock.
            if (_weaponLockApplied && _activeHitbox == null)
            {
                if (_weapon != null) _weapon.FireLocked = false;
                _weaponLockApplied = false;
                _weapon = null;
            }
        }

        private void OnDisable()
        {
            // Defensive: never leave a weapon stuck FireLocked if Acheron dies / despawns
            // mid-channel.
            if (_weaponLockApplied && _weapon != null)
            {
                _weapon.FireLocked = false;
            }
            _weaponLockApplied = false;
        }
    }
}
