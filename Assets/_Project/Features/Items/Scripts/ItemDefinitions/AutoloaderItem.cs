using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Autoloader", fileName = "Autoloader")]
    public class AutoloaderItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Reload time reduction per stack, stacked MULTIPLICATIVELY (0.05 = -5%/stack → " +
                 "reloadTime × 0.95^stacks).")]
        public float reloadTimeReductionPerStack = 0.05f;

        // NetworkWeapon.StartReload computes reloadTime / ReloadSpeedMultiplier, so we want
        // ReloadSpeedMultiplier = 1 / (1 - reduction)^stacks (the inverse of the time factor).
        // ReloadSpeedMultiplier is seeded at 1 (base), so adding this Flat modifier makes
        // GetStat = 1 + (speedMult - 1) = speedMult. min-reload floor is enforced in StartReload.
        //
        // ⚠️ Host-only per the stat-replication latent bug (read owner-side in StartReload).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
                stats.AddModifier(StatType.ReloadSpeedMultiplier, new StatModifier(StatModType.Flat, SpeedBonus(stacks)));
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
                stats.RemoveModifier(StatType.ReloadSpeedMultiplier, new StatModifier(StatModType.Flat, SpeedBonus(stacks)));
        }

        private float SpeedBonus(int stacks)
        {
            float timeFactor = Mathf.Pow(1f - reloadTimeReductionPerStack, stacks); // 0.95^stacks
            if (timeFactor <= 0.01f) timeFactor = 0.01f; // safety
            return (1f / timeFactor) - 1f;
        }
    }
}
