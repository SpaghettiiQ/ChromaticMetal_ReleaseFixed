using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Hemostasis Patch", fileName = "HemostasisPatch")]
    public class HemostasisPatchItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Seconds out of combat required to start regenerating at 1 stack.")]
        public float baseDelay = 4f;
        [Tooltip("Each stack beyond the first shortens the delay by this many seconds.")]
        public float delayReductionPerStack = 0.25f;
        [Tooltip("Minimum delay floor.")]
        public float minDelay = 3f;
        [Tooltip("Heal-per-second as a fraction of max HP at 1 stack.")]
        public float healPctBase = 0.04f;
        [Tooltip("Heal-per-second fraction added per stack beyond the first.")]
        public float healPctPerStack = 0.01f;

        // Per-owner state lives on HemostasisPatchRuntime (avoids shared-ScriptableObject aliasing).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            var rt = owner.GetComponent<HemostasisPatchRuntime>();
            if (rt == null) rt = owner.AddComponent<HemostasisPatchRuntime>();
            rt.Activate(this, stacks);
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out HemostasisPatchRuntime rt)) rt.Deactivate();
        }
    }
}
