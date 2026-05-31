using UnityEngine;

namespace  _Project.Core.Interfaces
{
    public interface IAbilityEffect
    {
        bool CanBeUsed(GameObject instigator) => true; // Default implementation
        void Execute(GameObject instigator);

        /// <summary>
        /// If true, AbilityController will NOT set the slot's cooldown on cast. The effect
        /// (or whatever it spawns) is expected to call AbilityController.SetCooldown(slot, ...)
        /// itself when it's truly "done" (e.g. Bloodthirster keeps the cooldown frozen until
        /// the 6-round magazine is empty and the reload completes).
        /// </summary>
        bool DeferCooldownOnCast(GameObject instigator) => false;
    }
}