using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Events;
using _Project.Core.Structs;
using _Project.Features.ProxyAbilities.Scripts;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    // Per-owner runtime for RecuperationModuleItem (kill within window of ability use → reset cooldowns).
    // Holds _lastAbilityTime so the SO's OnKillEnemy hook reads per-owner state, not a shared field.
    public class RecuperationModuleRuntime : ItemEffectBehaviour<RecuperationModuleItem>
    {
        private CharacterEventBus _bus;
        private float _lastAbilityTime = -100f;

        protected override void OnActivate()
        {
            TryGetComponent(out _bus);
            if (_bus != null) _bus.OnAbilityUsed += HandleAbilityUsed;
        }

        protected override void OnDeactivate()
        {
            if (_bus != null) _bus.OnAbilityUsed -= HandleAbilityUsed;
        }

        private void HandleAbilityUsed(AbilitySlot slot) => _lastAbilityTime = Time.time;

        // Called from RecuperationModuleItem.OnKillEnemy (server-side dispatch).
        public void TryProcKill(int stacks, DamageContext ctx)
        {
            float currentWindow = Config.windowDuration + (1f * (stacks - 1));
            if (Time.time <= _lastAbilityTime + currentWindow)
            {
                if (TryGetComponent(out AbilityController abilityController))
                {
                    Debug.Log($"[Recuperation Module] Proxied kill within {currentWindow}s of ability! Reseting all cooldowns.");
                    abilityController.ResetAllCooldowns();
                }
            }
        }
    }
}
