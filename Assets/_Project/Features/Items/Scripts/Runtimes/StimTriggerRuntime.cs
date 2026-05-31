using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Events;
using _Project.Core.Stats;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    // Per-owner runtime for StimTriggerItem (heal on ability use, on cooldown).
    public class StimTriggerRuntime : ItemEffectBehaviour<StimTriggerItem>
    {
        private CharacterEventBus _bus;
        private CharacterStats _stats;
        private float _nextAllowedTime;

        protected override void OnActivate()
        {
            TryGetComponent(out _bus);
            TryGetComponent(out _stats);
            if (_bus != null) _bus.OnAbilityUsed += HandleAbilityUsed;
        }

        protected override void OnDeactivate()
        {
            if (_bus != null) _bus.OnAbilityUsed -= HandleAbilityUsed;
        }

        private void HandleAbilityUsed(AbilitySlot slot)
        {
            if (_stats == null) return;
            if (Time.time < _nextAllowedTime) return;
            if (_stats.IsDead) return;

            float maxHp = _stats.GetStat(StatType.MaxHealth);
            if (maxHp <= 0f) return;
            int amount = Mathf.Max(1, Mathf.RoundToInt(maxHp * Config.healPct));
            _stats.Heal(amount);

            float cd = Mathf.Max(Config.minCooldown, Config.baseCooldown - Config.cooldownReductionPerStack * (Stacks - 1));
            _nextAllowedTime = Time.time + cd;
        }
    }
}
