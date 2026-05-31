using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Warpaint", fileName = "Warpaint")]
    public class WarpaintItem : ItemDefinition, IOnKillItem
    {
        [Tooltip("Temporary Barrier (amber, decays) gained per stack on each kill.")]
        public float barrierPerStack = 25f;
        [Tooltip("Maximum Barrier this item will maintain from kills, per stack.")]
        public int maxBarrierPerStack = 75;

        public void OnKillEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            if (!owner.TryGetComponent(out CharacterStats stats)) return;

            // Self-cap (scales with stacks): Core AddBarrier is uncapped, so clamp the top-up here.
            int cap = maxBarrierPerStack * stacks;
            int add = Mathf.Min(Mathf.RoundToInt(barrierPerStack * stacks), cap - stats.CurrentBarrier.Value);
            if (add > 0) stats.AddBarrier(add);
        }
    }
}
