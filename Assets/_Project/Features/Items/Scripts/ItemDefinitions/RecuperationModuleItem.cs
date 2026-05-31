using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Recuperation Module", fileName = "RecuperationModule")]
    public class RecuperationModuleItem : ItemDefinition, IPassiveItem, IOnKillItem
    {
        public float windowDuration = 2f;

        // Per-owner state lives on RecuperationModuleRuntime (avoids shared-ScriptableObject aliasing).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            var rt = owner.GetComponent<RecuperationModuleRuntime>();
            if (rt == null) rt = owner.AddComponent<RecuperationModuleRuntime>();
            rt.Activate(this, stacks);
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out RecuperationModuleRuntime rt)) rt.Deactivate();
        }

        public void OnKillEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            if (owner.TryGetComponent(out RecuperationModuleRuntime rt)) rt.TryProcKill(stacks, ctx);
        }
    }
}
