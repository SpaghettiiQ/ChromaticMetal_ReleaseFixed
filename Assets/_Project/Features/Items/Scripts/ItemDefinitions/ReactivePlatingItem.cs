using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Reactive Plating", fileName = "ReactivePlating")]
    public class ReactivePlatingItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Damage-reduction value (flat, in %) applied on hit at 1 stack.")]
        public float drBase = 20f;
        [Tooltip("Extra DR per stack beyond the first.")]
        public float drPerStack = 1f;
        [Tooltip("How long each DR window lasts.")]
        public float windowDuration = 2f;
        [Tooltip("Internal cooldown between procs at 1 stack.")]
        public float baseCooldown = 6f;
        [Tooltip("Cooldown reduction per stack beyond the first.")]
        public float cooldownReductionPerStack = 1f;
        [Tooltip("Minimum cooldown floor.")]
        public float minCooldown = 1f;

        // Per-owner state lives on ReactivePlatingRuntime (avoids shared-ScriptableObject aliasing).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            var rt = owner.GetComponent<ReactivePlatingRuntime>();
            if (rt == null) rt = owner.AddComponent<ReactivePlatingRuntime>();
            rt.Activate(this, stacks);
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out ReactivePlatingRuntime rt)) rt.Deactivate();
        }
    }
}
