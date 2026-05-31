using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Dead Man's Switch", fileName = "DeadMansSwitch")]
    public class DeadMansSwitchItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Cooldown in seconds at 1 stack.")]
        public float baseCooldown = 45f;
        [Tooltip("Each stack beyond the first shortens cooldown by this many seconds.")]
        public float cooldownReductionPerStack = 5f;
        [Tooltip("Minimum cooldown floor.")]
        public float minCooldown = 5f;
        [Tooltip("AoE pulse damage as fraction of owner's max HP.")]
        public float pulseDamageFrac = 0.04f;
        [Tooltip("AoE pulse radius.")]
        public float pulseRadius = 8f;

        // Per-owner state lives on DeadMansSwitchRuntime (avoids shared-ScriptableObject aliasing).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            var rt = owner.GetComponent<DeadMansSwitchRuntime>();
            if (rt == null) rt = owner.AddComponent<DeadMansSwitchRuntime>();
            rt.Activate(this, stacks);
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out DeadMansSwitchRuntime rt)) rt.Deactivate();
        }
    }
}
