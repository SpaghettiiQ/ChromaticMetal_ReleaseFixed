using UnityEngine;
using Unity.Netcode;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Core.Enums;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking
{
    public class OnlyPainTracker : MonoBehaviour
    {
        private CharacterStats _stats;
        private IBurdenable _burdenable;
        private float _reductionMultiplier;
        
        private StatModifier _currentModifier;
        private bool _hasModifier = false;
        private float _lastUpdateBurden = -1f;

        public void Initialize(float reductionPerBurden)
        {
            _reductionMultiplier = reductionPerBurden;
            _stats = GetComponent<CharacterStats>();
            _burdenable = GetComponent<IBurdenable>();
        }

        private void Update()
        {
            // Only update on the server where Burden and Stats are managed authoritatively
            if (_stats == null || _burdenable == null || !_stats.IsServer) return;

            float currentBurden = _burdenable.GetCurrentBurden();

            // Optimization: Only cycle modifiers if burden has actually changed
            if (Mathf.Abs(currentBurden - _lastUpdateBurden) > 0.1f)
            {
                if (_hasModifier)
                {
                    _stats.RemoveModifier(StatType.DamageReduction, _currentModifier);
                }

                float calculatedReduction = currentBurden * _reductionMultiplier;
                
                _currentModifier = new StatModifier(StatModType.Flat, calculatedReduction);
                _stats.AddModifier(StatType.DamageReduction, _currentModifier);
                _hasModifier = true;
                _lastUpdateBurden = currentBurden;
            }
        }

        private void OnDestroy()
        {
            if (_hasModifier && _stats != null)
            {
                _stats.RemoveModifier(StatType.DamageReduction, _currentModifier);
            }
        }
    }
}