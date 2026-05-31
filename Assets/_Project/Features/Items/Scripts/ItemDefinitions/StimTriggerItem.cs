using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Stim Trigger", fileName = "StimTrigger")]
    public class StimTriggerItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Fraction of max HP healed each proc.")]
        public float healPct = 0.10f;
        [Tooltip("Cooldown in seconds at 1 stack.")]
        public float baseCooldown = 10f;
        [Tooltip("Each stack beyond the first shortens cooldown by this many seconds.")]
        public float cooldownReductionPerStack = 1f;
        [Tooltip("Minimum cooldown floor.")]
        public float minCooldown = 3f;

        // Per-owner state lives on StimTriggerRuntime (avoids shared-ScriptableObject aliasing).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            var rt = owner.GetComponent<StimTriggerRuntime>();
            if (rt == null) rt = owner.AddComponent<StimTriggerRuntime>();
            rt.Activate(this, stacks);
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out StimTriggerRuntime rt)) rt.Deactivate();
        }
    }
}
