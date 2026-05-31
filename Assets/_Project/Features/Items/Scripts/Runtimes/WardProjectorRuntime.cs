using System.Collections;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Events;
using _Project.Core.Stats;
using _Project.Core.Structs;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    // Per-owner runtime for WardProjectorItem (out-of-combat Shield recharge).
    public class WardProjectorRuntime : ItemEffectBehaviour<WardProjectorItem>
    {
        private CharacterStats _stats;
        private CharacterEventBus _bus;
        private float _lastDamageTime = -100f; // out of combat at spawn
        private Coroutine _tick;

        protected override void OnActivate()
        {
            TryGetComponent(out _stats);
            TryGetComponent(out _bus);
            if (_bus != null) _bus.OnDamageTaken += HandleDamageTaken;
            _tick = StartCoroutine(TickRoutine());
        }

        protected override void OnDeactivate()
        {
            if (_bus != null) _bus.OnDamageTaken -= HandleDamageTaken;
            if (_tick != null) { StopCoroutine(_tick); _tick = null; }
        }

        private void HandleDamageTaken(DamageContext ctx) => _lastDamageTime = Time.time;

        // Refills the blue Shield to its cap once out of combat for rechargeDelay. SetShield is
        // server-only, so this no-ops on remote clients.
        private IEnumerator TickRoutine()
        {
            var wait = new WaitForSeconds(1f);
            while (_stats != null)
            {
                yield return wait;
                if (_stats.IsDead) continue;
                if (Time.time - _lastDamageTime < Config.rechargeDelay) continue;

                int cap = Mathf.RoundToInt(_stats.GetStat(StatType.MaxHealth) * Config.shieldPctPerStack * Stacks);
                if (cap > 0 && _stats.CurrentShield.Value < cap)
                {
                    _stats.SetShield(cap);
                }
            }
        }
    }
}
