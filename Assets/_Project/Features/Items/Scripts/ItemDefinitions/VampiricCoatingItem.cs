using UnityEngine;
using _Project.Core.Events;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Vampiric Coating", fileName = "VampiricCoating")]
    public class VampiricCoatingItem : ItemDefinition, IOnKillItem
    {
        [Tooltip("Percentage of max health restored upon killing an enemy")]
        public float healPercentagePerStack = 0.05f; // 5% max health

        public void OnKillEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                // Calculate the heal amount based on the character's Max HP and the item stacks
                float maxHealth = stats.GetStat(_Project.Core.Enums.StatType.MaxHealth);
                int healAmount = Mathf.RoundToInt(maxHealth * (healPercentagePerStack * stacks));

                Debug.Log($"[Vampiric Coating] Killed enemy! Healing for {healAmount} HP ({healPercentagePerStack * stacks * 100}% of Max HP).");
                stats.Heal(healAmount);
            }
        }
    }
}