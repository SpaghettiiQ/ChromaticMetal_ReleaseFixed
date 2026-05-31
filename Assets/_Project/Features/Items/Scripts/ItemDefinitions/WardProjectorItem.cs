using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Ward Projector", fileName = "WardProjector")]
    public class WardProjectorItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Shield capacity as a fraction of Max HP, per stack (0.05 = 5%).")]
        public float shieldPctPerStack = 0.05f;
        [Tooltip("Seconds out of combat before the Shield refills.")]
        public float rechargeDelay = 5f;

        // Per-owner state lives on WardProjectorRuntime (avoids shared-ScriptableObject aliasing).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            var rt = owner.GetComponent<WardProjectorRuntime>();
            if (rt == null) rt = owner.AddComponent<WardProjectorRuntime>();
            rt.Activate(this, stacks);
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out WardProjectorRuntime rt)) rt.Deactivate();
        }
    }
}
