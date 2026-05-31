using UnityEngine;
using Unity.Netcode;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking
{
    public class PoisonTracker : MonoBehaviour
    {
        private float _duration;
        private float _damagePerTick;
        private float _tickRate;
        private GameObject _source;
        private CharacterStats _targetStats;

        private float _timeElapsed = 0f;
        private float _tickTimer = 0f;

        public void Initialize(GameObject source, float duration, float damagePerTick, float tickRate)
        {
            _source = source;
            _duration = duration;
            _damagePerTick = damagePerTick;
            _tickRate = tickRate;
            _targetStats = GetComponent<CharacterStats>();
        }

        private void Update()
        {
            // The server completely owns damage ticking
            if (_targetStats == null || !_targetStats.IsServer) return;

            _timeElapsed += Time.deltaTime;
            _tickTimer += Time.deltaTime;

            if (_timeElapsed >= _duration)
            {
                Destroy(this);
                return;
            }

            if (_tickTimer >= _tickRate)
            {
                _tickTimer = 0f;
                DamageContext ctx = new DamageContext
                {
                    Source = _source,
                    Target = gameObject,
                    Damage = Mathf.RoundToInt(_damagePerTick),
                    DamageType = DamageType.Presence,
                    AttackType = AttackType.Effect
                };
                
                _targetStats.TakeDamage(ctx);
            }
        }
    }
}