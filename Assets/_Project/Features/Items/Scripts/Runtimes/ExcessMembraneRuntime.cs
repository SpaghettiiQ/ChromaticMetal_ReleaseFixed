using UnityEngine;
using _Project.Core.Events;
using _Project.Core.Stats;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    // Per-owner runtime for ExcessMembraneItem (overheal → Barrier).
    public class ExcessMembraneRuntime : ItemEffectBehaviour<ExcessMembraneItem>
    {
        private CharacterEventBus _bus;
        private CharacterStats _stats;

        protected override void OnActivate()
        {
            TryGetComponent(out _bus);
            TryGetComponent(out _stats);
            if (_bus != null) _bus.OnHealed += HandleHealed;
        }

        protected override void OnDeactivate()
        {
            if (_bus != null) _bus.OnHealed -= HandleHealed;
        }

        // Overheal (the portion of a heal that was wasted above MaxHealth) becomes Barrier.
        private void HandleHealed(int applied, int overheal)
        {
            if (_stats == null || overheal <= 0) return;

            float rate = Config.conversionBase + Config.conversionPerStack * (Stacks - 1);
            int cap = Config.maxBarrierPerStack * Stacks;
            int add = Mathf.Min(Mathf.RoundToInt(overheal * rate), cap - _stats.CurrentBarrier.Value);
            if (add > 0) _stats.AddBarrier(add);
        }
    }
}
