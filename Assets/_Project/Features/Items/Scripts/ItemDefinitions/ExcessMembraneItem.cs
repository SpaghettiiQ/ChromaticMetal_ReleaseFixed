using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Excess Membrane", fileName = "ExcessMembrane")]
    public class ExcessMembraneItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Fraction of clipped overheal converted to Barrier at 1 stack.")]
        public float conversionBase = 0.5f;
        [Tooltip("Added conversion fraction per stack beyond the first.")]
        public float conversionPerStack = 0.5f;
        [Tooltip("Maximum Barrier this item will maintain from overheal, per stack.")]
        public int maxBarrierPerStack = 100;

        // Per-owner state lives on ExcessMembraneRuntime (avoids shared-ScriptableObject aliasing).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            var rt = owner.GetComponent<ExcessMembraneRuntime>();
            if (rt == null) rt = owner.AddComponent<ExcessMembraneRuntime>();
            rt.Activate(this, stacks);
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out ExcessMembraneRuntime rt)) rt.Deactivate();
        }
    }
}
