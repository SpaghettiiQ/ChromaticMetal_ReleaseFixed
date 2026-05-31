using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.BurdenSystem.Scripts;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    /// <summary>
    /// Burden Filter — reduces incoming Burden accumulation. On Thrive characters, the
    /// "filtered" portion of each burden gain also converts into Presence Power, which
    /// scales the player's outgoing weapon damage (WeaponDamageMultiplier).
    /// </summary>
    [CreateAssetMenu(menuName = "Game Data/Items/Burden Filter", fileName = "BurdenFilter")]
    public class BurdenFilterItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Fraction of burden accumulation reduced per stack (0.5 = 50% slower per stack). " +
                 "Clamped so multiplier never drops below 0.1 (10% of normal).")]
        public float burdenConversionRate = 0.5f;

        [Header("Presence Power (Thrive only)")]
        [Tooltip("How much Presence Power is gained per unit of burden the filter removes. " +
                 "Scales linearly with stacks via Power conversion = powerPerStack × stacks.")]
        public float powerPerStack = 1f;

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out BurdenController burden))
            {
                burden.AccumulationMultiplier = Mathf.Max(0.1f, 1f - (burdenConversionRate * stacks));

                // Presence Power conversion is Thrive-only. Cleansers picking this up just get
                // the burden reduction; the Power bar stays hidden for them.
                if (owner.TryGetComponent(out CharacterStats stats) && stats.Team == TeamAffiliation.Thrive)
                {
                    burden.EnablePresencePower(powerPerStack * stacks);
                }

                Debug.Log($"[Burden Filter] x{stacks} → accumulation {burden.AccumulationMultiplier * 100f:F0}%" +
                          (burden.PresencePowerEnabled.Value ? $", PresencePower conversion {powerPerStack * stacks}/burden" : ""));
            }
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out BurdenController burden))
            {
                burden.AccumulationMultiplier = 1f;

                if (owner.TryGetComponent(out CharacterStats stats) && stats.Team == TeamAffiliation.Thrive)
                {
                    burden.DisablePresencePower();
                }
            }
        }
    }
}
