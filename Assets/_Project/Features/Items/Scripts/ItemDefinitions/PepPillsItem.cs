using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Pep Pills", fileName = "PepPills")]
    public class PepPillsItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Attack speed added per stack, as a Percent multiplier (0.10 = +10%).")]
        public float attackSpeedPerStack = 0.10f;

        // NOTE: AttackSpeed is consumed owner-side in NetworkWeapon.ExecuteShot, and modifiers only
        // live on the server's stat dict — so this buff is host-only for remote clients (latent
        // stat-replication bug). Works fully in singleplayer/host.
        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
                stats.AddModifier(StatType.AttackSpeed, new StatModifier(StatModType.Percent, attackSpeedPerStack * stacks));
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
                stats.RemoveModifier(StatType.AttackSpeed, new StatModifier(StatModType.Percent, attackSpeedPerStack * stacks));
        }
    }
}
