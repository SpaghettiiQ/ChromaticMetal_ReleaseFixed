using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Presence Attractor", fileName = "PresenceAttractor")]
    public class PresenceAttractorItem : ItemDefinition, IPassiveItem
    {
        public float presenceAttractionMultiplier = 2f;
        public float autoDecayMultiplier = 0.5f; // Slows down auto decay
        public float accumulationMultiplier = 2f; // Increases accumulation

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out _Project.Features.BurdenSystem.Scripts.BurdenController burden))
            {
                // Multipliers compound with stacks
                float totalAccumulation = 1f + ((accumulationMultiplier - 1f) * stacks);
                float totalDecay = Mathf.Pow(autoDecayMultiplier, stacks); // e.g. 0.5 * 0.5 = 0.25 for 2 stacks

                burden.AccumulationMultiplier = totalAccumulation;
                burden.DecayMultiplier = totalDecay;

                Debug.Log($"[Presence Attractor] Absorbing Presence! Accumulation x{totalAccumulation}, Decay x{totalDecay}");
            }
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out _Project.Features.BurdenSystem.Scripts.BurdenController burden))
            {
                burden.AccumulationMultiplier = 1f;
                burden.DecayMultiplier = 1f;
                Debug.Log($"[Presence Attractor] Removed. Burden mechanics reset.");
            }
        }
    }
}