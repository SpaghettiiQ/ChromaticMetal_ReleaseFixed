using UnityEngine;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking
{
    public class SlowTracker : MonoBehaviour
    {
        private float _duration;
        private float _timeElapsed;
        private CharacterStats _stats;
        private StatModifier _modifier;
        private bool _hasModifier;

        public void Initialize(float duration, float slowPercent)
        {
            if (_stats == null) _stats = GetComponent<CharacterStats>();
            if (_stats == null || !_stats.IsServer) return;

            // Clear any prior modifier so re-applying the slow refreshes instead of stacking/leaking.
            if (_hasModifier)
            {
                _stats.RemoveModifier(StatType.MovementSpeed, _modifier);
                _hasModifier = false;
            }

            _modifier = new StatModifier(StatModType.Percent, slowPercent);
            _stats.AddModifier(StatType.MovementSpeed, _modifier);
            _hasModifier = true;

            _duration = duration;
            _timeElapsed = 0f;
        }

        private void Update()
        {
            if (_stats == null || !_stats.IsServer) return;

            _timeElapsed += Time.deltaTime;
            if (_timeElapsed >= _duration)
            {
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (_hasModifier && _stats != null && _stats.IsServer)
            {
                _stats.RemoveModifier(StatType.MovementSpeed, _modifier);
                _hasModifier = false;
            }
        }
    }
}
