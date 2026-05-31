using UnityEngine;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    /// <summary>
    /// Base for per-owner item runtime components. Stateful items keep their ScriptableObject as
    /// config-only and push per-owner state (timers, coroutines, event subscriptions) onto one of
    /// these, added to the owner via AddComponent. This fixes the shared-ScriptableObject aliasing
    /// bug (one SO instance shared by all players) — each owner gets its own runtime + fields.
    ///
    /// Lifecycle: CharacterInventory.RebuildEventCaches calls RemovePassive (→ Deactivate) then
    /// ApplyPassive (→ Activate) on EVERY inventory change. We toggle active rather than destroy, so
    /// per-owner timer fields persist across that churn exactly as the old shared fields did — only
    /// now they're per-owner. A fully-removed item leaves an inert, unsubscribed component.
    /// </summary>
    public abstract class ItemEffectBehaviour<TConfig> : MonoBehaviour where TConfig : ItemDefinition
    {
        protected TConfig Config { get; private set; }
        protected int Stacks { get; private set; }
        private bool _active;

        public void Activate(TConfig config, int stacks)
        {
            Config = config;
            Stacks = stacks;
            if (!_active)
            {
                _active = true;
                OnActivate();
            }
            else
            {
                // Defensive: the rebuild always Deactivates before re-Activating, so this rarely runs.
                OnReconfigure();
            }
        }

        public void Deactivate()
        {
            if (!_active) return;
            _active = false;
            OnDeactivate();
        }

        // Owner despawn/death destroys the component — make sure we still tear down.
        protected virtual void OnDestroy()
        {
            if (_active)
            {
                _active = false;
                OnDeactivate();
            }
        }

        /// <summary>Subscribe events and start coroutines. Re-runs on each rebuild after OnDeactivate.</summary>
        protected abstract void OnActivate();

        /// <summary>Called if Activate runs while already active (without a preceding Deactivate).</summary>
        protected virtual void OnReconfigure() { }

        /// <summary>Unsubscribe events, stop coroutines, and remove any active temporary modifiers.</summary>
        protected abstract void OnDeactivate();
    }
}
