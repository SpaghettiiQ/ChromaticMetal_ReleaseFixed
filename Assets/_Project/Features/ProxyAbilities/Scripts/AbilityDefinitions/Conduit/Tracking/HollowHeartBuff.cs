using UnityEngine;
using Unity.Netcode;
using _Project.Core.Stats;
using _Project.Core.Interfaces;
using _Project.Features.BurdenSystem.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking
{
    /// <summary>
    /// Hollow Heart buff applied to a target (self or ally) while the channel is active.
    ///   - Burden penalties suppressed (BurdenController.SuppressPenalties).
    ///   - Burden gained → heals the target by gained × healMult.
    ///   - Burden gained during the channel is rolled back when the channel ends; existing
    ///     burden from before the cast is untouched.
    ///   - Spawns a networked VFX prefab parented to the target so every client sees the
    ///     channel visually.
    /// </summary>
    public class HollowHeartBuff : MonoBehaviour
    {
        private float _duration;
        private float _healMult;

        private CharacterStats _stats;
        private BurdenController _burdenController;
        private float _timeElapsed = 0f;

        private float _previousBurden;
        private float _accumulatedBurdenDuringEffect = 0f;

        private NetworkObject _vfxInstance;

        public void Initialize(float duration, float healMult, GameObject vfxPrefab)
        {
            _duration = duration;
            _healMult = healMult;

            _stats = GetComponent<CharacterStats>();
            _burdenController = GetComponent<BurdenController>();

            if (_stats == null || !_stats.IsServer || _burdenController == null)
            {
                Destroy(this);
                return;
            }

            _previousBurden = _burdenController.GetCurrentBurden();
            _burdenController.SuppressPenalties = true;

            // Refresh state if this is a re-cast on an existing target.
            _timeElapsed = 0f;
            _accumulatedBurdenDuringEffect = 0f;

            // VFX: replace any in-flight instance from a prior cast so the channel always
            // shows a single, fresh particle system on the target.
            DespawnVfx();
            SpawnVfx(vfxPrefab);
        }

        private void SpawnVfx(GameObject vfxPrefab)
        {
            if (vfxPrefab == null) return;

            GameObject vfx = Instantiate(vfxPrefab, transform.position, transform.rotation);
            _vfxInstance = vfx.GetComponent<NetworkObject>();
            if (_vfxInstance == null)
            {
                // Non-networked VFX — just parent locally on the server (client-side visuals
                // won't show, but at least the server-side instance cleans up properly).
                vfx.transform.SetParent(transform, false);
                vfx.transform.localPosition = Vector3.zero;
                vfx.transform.localRotation = Quaternion.identity;
                return;
            }

            _vfxInstance.Spawn();
            // Parent the VFX under the target's NetworkObject so it follows the target around
            // the map. NetworkObject parent must itself be a NetworkObject — the player root is.
            if (TryGetComponent(out NetworkObject targetNet))
            {
                _vfxInstance.TrySetParent(targetNet, false);
                _vfxInstance.transform.localPosition = Vector3.zero;
                _vfxInstance.transform.localRotation = Quaternion.identity;
            }
        }

        private void DespawnVfx()
        {
            if (_vfxInstance == null) return;
            if (_vfxInstance.IsSpawned) _vfxInstance.Despawn(true);
            else if (_vfxInstance.gameObject != null) Destroy(_vfxInstance.gameObject);
            _vfxInstance = null;
        }

        private void Update()
        {
            if (_stats == null || !_stats.IsServer) return;

            _timeElapsed += Time.deltaTime;

            if (_timeElapsed >= _duration)
            {
                EndEffect();
                return;
            }

            float currentBurden = _burdenController.GetCurrentBurden();
            if (currentBurden > _previousBurden)
            {
                float gained = currentBurden - _previousBurden;
                _accumulatedBurdenDuringEffect += gained;

                int healAmount = Mathf.RoundToInt(gained * _healMult);
                if (healAmount > 0)
                {
                    _stats.Heal(healAmount);
                }
            }

            _previousBurden = currentBurden;
        }

        private void EndEffect()
        {
            if (_burdenController != null)
            {
                _burdenController.SuppressPenalties = false;

                if (_accumulatedBurdenDuringEffect > 0)
                {
                    _burdenController.RemoveBurden(_accumulatedBurdenDuringEffect);
                }
            }

            DespawnVfx();
            Destroy(this);
        }

        private void OnDestroy()
        {
            // Belt-and-suspenders if the component is destroyed by death/scene unload mid-channel.
            if (_stats != null && _stats.IsServer && _burdenController != null)
            {
                _burdenController.SuppressPenalties = false;
            }
            DespawnVfx();
        }
    }
}
