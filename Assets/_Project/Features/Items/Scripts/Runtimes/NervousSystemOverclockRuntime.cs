using System.Collections;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Events;
using _Project.Core.Stats;
using _Project.Core.Structs;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    // Per-owner runtime for NervousSystemOverclockItem (low-health slow-mo for the local owner).
    public class NervousSystemOverclockRuntime : ItemEffectBehaviour<NervousSystemOverclockItem>
    {
        private CharacterEventBus _bus;

        protected override void OnActivate()
        {
            TryGetComponent(out _bus);
            if (_bus != null) _bus.OnConditionChanged += CheckLowHealth;
        }

        protected override void OnDeactivate()
        {
            if (_bus != null) _bus.OnConditionChanged -= CheckLowHealth;
        }

        private void CheckLowHealth(ConditionType condition, bool isActive)
        {
            if (condition != ConditionType.LowHealth || !isActive) return;

            // Only the client that owns this character should apply a local time-scale effect.
            if (TryGetComponent(out Unity.Netcode.NetworkBehaviour nb) && !nb.IsOwner) return;

            Debug.Log($"[Nervous System Overclock] Low Health Detected! Activating combat reflexes (Slow-mo) for {Config.duration} seconds!");
            StartCoroutine(OverclockRoutine());
        }

        private IEnumerator OverclockRoutine()
        {
            float originalTimeScale = Time.timeScale;
            Time.timeScale = Config.slowMotionScale;

            if (TryGetComponent(out CharacterStats stats))
            {
                // Speed up the local player relative to the slowed time
                var speedBuff = new StatModifier(StatModType.Percent, (1f / Config.slowMotionScale) - 1f);
                stats.AddModifier(StatType.MovementSpeed, speedBuff);
                stats.AddModifier(StatType.AttackSpeed, speedBuff);

                yield return new WaitForSecondsRealtime(Config.duration); // realtime because timescale is slow

                stats.RemoveModifier(StatType.MovementSpeed, speedBuff);
                stats.RemoveModifier(StatType.AttackSpeed, speedBuff);
            }
            else
            {
                yield return new WaitForSecondsRealtime(Config.duration);
            }

            Time.timeScale = originalTimeScale;
        }
    }
}
