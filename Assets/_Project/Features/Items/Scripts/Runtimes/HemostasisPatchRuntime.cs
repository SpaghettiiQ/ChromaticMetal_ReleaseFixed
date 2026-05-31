using System.Collections;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Events;
using _Project.Core.Stats;
using _Project.Core.Structs;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    // Per-owner runtime for HemostasisPatchItem (out-of-combat regen).
    public class HemostasisPatchRuntime : ItemEffectBehaviour<HemostasisPatchItem>
    {
        private CharacterEventBus _bus;
        private CharacterStats _stats;
        private float _lastDamageTime;
        private Coroutine _tickRoutine;

        protected override void OnActivate()
        {
            TryGetComponent(out _bus);
            TryGetComponent(out _stats);
            if (_bus != null) _bus.OnDamageTaken += HandleDamageTaken;
            _tickRoutine = StartCoroutine(TickRoutine());
        }

        protected override void OnDeactivate()
        {
            if (_bus != null) _bus.OnDamageTaken -= HandleDamageTaken;
            if (_tickRoutine != null) { StopCoroutine(_tickRoutine); _tickRoutine = null; }
        }

        private void HandleDamageTaken(DamageContext ctx) => _lastDamageTime = Time.time;

        private IEnumerator TickRoutine()
        {
            var wait = new WaitForSeconds(1f);
            while (_stats != null)
            {
                yield return wait;
                float delay = Mathf.Max(Config.minDelay, Config.baseDelay - Config.delayReductionPerStack * (Stacks - 1));
                if (Time.time - _lastDamageTime < delay) continue;
                if (_stats.IsDead) continue;

                float maxHp = _stats.GetStat(StatType.MaxHealth);
                if (maxHp <= 0f) continue;
                if (_stats.CurrentHealth.Value >= maxHp) continue;

                float pct = Config.healPctBase + Config.healPctPerStack * (Stacks - 1);
                int amount = Mathf.Max(1, Mathf.RoundToInt(maxHp * pct));
                _stats.Heal(amount);
            }
        }
    }
}
