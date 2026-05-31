using System.Collections;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Events;
using _Project.Core.Stats;
using _Project.Core.Structs;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    // Per-owner runtime for ReactivePlatingItem (on-hit temporary damage reduction window).
    public class ReactivePlatingRuntime : ItemEffectBehaviour<ReactivePlatingItem>
    {
        private CharacterEventBus _bus;
        private CharacterStats _stats;
        private float _nextAllowedTime;
        private bool _hasActiveMod;
        private StatModifier _activeMod;
        private Coroutine _removeRoutine;

        protected override void OnActivate()
        {
            TryGetComponent(out _bus);
            TryGetComponent(out _stats);
            if (_bus != null) _bus.OnDamageTaken += HandleDamageTaken;
        }

        protected override void OnDeactivate()
        {
            if (_bus != null) _bus.OnDamageTaken -= HandleDamageTaken;
            if (_removeRoutine != null) { StopCoroutine(_removeRoutine); _removeRoutine = null; }
            if (_hasActiveMod && _stats != null) _stats.RemoveModifier(StatType.DamageReduction, _activeMod);
            _hasActiveMod = false;
        }

        private void HandleDamageTaken(DamageContext ctx)
        {
            if (_stats == null) return;
            if (Time.time < _nextAllowedTime) return;

            float drValue = Config.drBase + Config.drPerStack * (Stacks - 1);
            _activeMod = new StatModifier(StatModType.Flat, drValue);
            _stats.AddModifier(StatType.DamageReduction, _activeMod);
            _hasActiveMod = true;

            if (_removeRoutine != null) StopCoroutine(_removeRoutine);
            _removeRoutine = StartCoroutine(RemoveAfter(Config.windowDuration));

            _nextAllowedTime = Time.time + Mathf.Max(Config.minCooldown, Config.baseCooldown - Config.cooldownReductionPerStack * (Stacks - 1));
        }

        private IEnumerator RemoveAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_hasActiveMod && _stats != null) _stats.RemoveModifier(StatType.DamageReduction, _activeMod);
            _hasActiveMod = false;
            _removeRoutine = null;
        }
    }
}
