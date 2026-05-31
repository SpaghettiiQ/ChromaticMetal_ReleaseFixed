using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Nervous System Overclock", fileName = "NervousSystemOverclock")]
    public class NervousSystemOverclockItem : ItemDefinition, IPassiveItem
    {
        public float slowMotionScale = 0.2f;
        public float duration = 5f;

        // Per-owner state lives on NervousSystemOverclockRuntime (avoids shared-ScriptableObject aliasing).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            var rt = owner.GetComponent<NervousSystemOverclockRuntime>();
            if (rt == null) rt = owner.AddComponent<NervousSystemOverclockRuntime>();
            rt.Activate(this, stacks);
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out NervousSystemOverclockRuntime rt)) rt.Deactivate();
        }
    }
}
