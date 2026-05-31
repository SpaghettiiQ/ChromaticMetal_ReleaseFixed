using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Jump Cell", fileName = "JumpCell")]
    public class JumpCellItem : ItemDefinition, IPassiveItem
    {
        public int extraJumpsPerStack = 1;

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                stats.AddModifier(StatType.ExtraJumps, new StatModifier(StatModType.Flat, extraJumpsPerStack * stacks));
                Debug.Log($"[Jump Cell] Granted +{extraJumpsPerStack * stacks} Extra Jump(s)");
            }
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                stats.RemoveModifier(StatType.ExtraJumps, new StatModifier(StatModType.Flat, extraJumpsPerStack * stacks));
                Debug.Log($"[Jump Cell] Removed +{extraJumpsPerStack * stacks} Extra Jump(s)");
            }
        }
    }
}