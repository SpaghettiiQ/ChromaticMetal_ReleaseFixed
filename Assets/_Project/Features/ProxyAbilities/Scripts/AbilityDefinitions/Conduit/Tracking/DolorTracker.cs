using UnityEngine;
using Unity.Netcode;
using _Project.Core.Enums;
using _Project.Features.BurdenSystem.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking
{
    /// <summary>
    /// Conduit's Dolor passive — Burden accumulates faster (AccumulationMultiplier × N), and
    /// while sitting in the highest Burden tier (>= 80 by convention, matching
    /// BurdenController's tier-4 threshold) all three ability cooldowns drain faster.
    /// </summary>
    public class DolorTracker : MonoBehaviour
    {
        [Tooltip("Seconds of CDR applied per real second of dwell time at >= 80 Burden. " +
                 "1.0 means cooldowns drain at 2× speed (real-time tick + CDR tick).")]
        public float cdrRate = 1f;

        [Tooltip("How often the batched CDR RPC fires while above the threshold. Smaller " +
                 "feels smoother but costs more network traffic; 0.25s is a sane default.")]
        public float cdrTickInterval = 0.25f;

        private AbilityController _abilityController;
        private BurdenController _burdenController;
        private bool _isServer;
        private float _appliedMultiplier = 1f;
        private bool _multiplierApplied;
        private float _cdrAccumulator;

        public void Initialize(float burdenMultiplier)
        {
            if (TryGetComponent(out NetworkObject _))
            {
                _isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            }

            _abilityController = GetComponent<AbilityController>();
            _burdenController = GetComponent<BurdenController>();

            if (_isServer && _burdenController != null && !_multiplierApplied)
            {
                _appliedMultiplier = burdenMultiplier;
                _burdenController.AccumulationMultiplier *= _appliedMultiplier;
                _multiplierApplied = true;
            }
        }

        private void Update()
        {
            if (!_isServer || _burdenController == null || _abilityController == null) return;

            // Only accumulate CDR while sitting at the high-burden threshold (tier-4 in
            // BurdenController). Outside that, the accumulator resets so we don't carry
            // unspent time into the next eligible window.
            if (_burdenController.GetCurrentBurden() >= 80f)
            {
                _cdrAccumulator += cdrRate * Time.deltaTime;

                // Batch the actual RPC fire to once per cdrTickInterval. Calling
                // AbilityController.ReduceCooldown every frame for 3 slots = 180 RPCs/sec at
                // 60fps, which is what this batching exists to prevent.
                if (_cdrAccumulator >= cdrTickInterval)
                {
                    float amount = _cdrAccumulator;
                    _cdrAccumulator = 0f;
                    _abilityController.ReduceCooldown(AbilitySlot.Secondary, amount);
                    _abilityController.ReduceCooldown(AbilitySlot.Utility, amount);
                    _abilityController.ReduceCooldown(AbilitySlot.Special, amount);
                }
            }
            else
            {
                _cdrAccumulator = 0f;
            }
        }

        private void OnDestroy()
        {
            if (_multiplierApplied && _burdenController != null && _appliedMultiplier != 0f)
            {
                _burdenController.AccumulationMultiplier /= _appliedMultiplier;
                _multiplierApplied = false;
            }
        }
    }
}
