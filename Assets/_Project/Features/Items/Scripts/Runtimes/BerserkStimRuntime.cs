using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Events;
using _Project.Core.Stats;
using _Project.Core.Structs;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    // Per-owner runtime for BerserkStimItem (low-health movement/attack-speed buff).
    public class BerserkStimRuntime : ItemEffectBehaviour<BerserkStimItem>
    {
        private CharacterEventBus _bus;
        private CharacterStats _stats;
        private bool _buffActive;
        private StatModifier _moveMod;
        private StatModifier _atkMod;

        protected override void OnActivate()
        {
            TryGetComponent(out _bus);
            TryGetComponent(out _stats);
            if (_bus != null) _bus.OnConditionChanged += HandleCondition;
        }

        protected override void OnDeactivate()
        {
            if (_bus != null) _bus.OnConditionChanged -= HandleCondition;
            if (_buffActive && _stats != null)
            {
                _stats.RemoveModifier(StatType.MovementSpeed, _moveMod);
                _stats.RemoveModifier(StatType.AttackSpeed, _atkMod);
            }
            _buffActive = false;
        }

        private void HandleCondition(ConditionType cond, bool active)
        {
            if (cond != ConditionType.LowHealth) return;
            if (_stats == null) return;

            if (active && !_buffActive)
            {
                _moveMod = new StatModifier(StatModType.Percent, Config.moveBonusBase + Config.moveBonusPerStack * (Stacks - 1));
                _atkMod = new StatModifier(StatModType.Percent, Config.atkBonusBase + Config.atkBonusPerStack * (Stacks - 1));
                _stats.AddModifier(StatType.MovementSpeed, _moveMod);
                _stats.AddModifier(StatType.AttackSpeed, _atkMod);
                _buffActive = true;
            }
            else if (!active && _buffActive)
            {
                _stats.RemoveModifier(StatType.MovementSpeed, _moveMod);
                _stats.RemoveModifier(StatType.AttackSpeed, _atkMod);
                _buffActive = false;
            }
        }
    }
}
