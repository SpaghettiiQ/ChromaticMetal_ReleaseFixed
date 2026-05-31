using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Berserk Stim", fileName = "BerserkStim")]
    public class BerserkStimItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Movement-speed Percent bonus at 1 stack.")]
        public float moveBonusBase = 0.20f;
        [Tooltip("Extra movement-speed Percent per stack beyond the first.")]
        public float moveBonusPerStack = 0.05f;
        [Tooltip("Attack-speed Percent bonus at 1 stack.")]
        public float atkBonusBase = 0.40f;
        [Tooltip("Extra attack-speed Percent per stack beyond the first.")]
        public float atkBonusPerStack = 0.10f;

        // Per-owner state lives on BerserkStimRuntime (avoids shared-ScriptableObject aliasing).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            var rt = owner.GetComponent<BerserkStimRuntime>();
            if (rt == null) rt = owner.AddComponent<BerserkStimRuntime>();
            rt.Activate(this, stacks);
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out BerserkStimRuntime rt)) rt.Deactivate();
        }
    }
}
