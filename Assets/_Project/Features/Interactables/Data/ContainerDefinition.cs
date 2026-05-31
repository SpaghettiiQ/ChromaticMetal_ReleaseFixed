using System.Collections.Generic;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Features.Items.Data;

namespace _Project.Features.Interactables.Data
{
    [System.Serializable]
    public struct RarityWeight
    {
        public ItemRarity rarity;
        [Tooltip("Relative weight. e.g., Tier1: 15, Tier2: 5 results in 75% / 25%")]
        public float weight;
    }

    [CreateAssetMenu(menuName = "Game Data/Interactables/Container Definition")]
    public class ContainerDefinition : ScriptableObject
    {
        [Header("Economy")]
        public int baseCost = 0;
        public float costScale = 1.0f;

        [Header("Master Database")]
        [Tooltip("The central item database to pull dynamic loot from.")]
        public ItemDatabase itemDatabase;
        
        [Header("Drop Settings")]
        [Tooltip("The prefab spawned into the world when this container is opened.")]
        public GameObject itemPickupPrefab;

        [Header("Loot Generation Rules")]
        [Tooltip("Add the rarities that can drop from this chest and their weights.")]
        public List<RarityWeight> rarityWeights;

        [Tooltip("Leave empty to allow ALL item types. Add types here to restrict drops (e.g., only Weapons and Consumables).")]
        public List<ItemType> allowedTypes;

        [Tooltip("Hard Rule: If TRUE, this container ONLY drops Implants. If FALSE, it drops EVERYTHING EXCEPT Implants.")]
        public bool dropsImplants;

        public ItemDefinition GetRandomDrop(TeamAffiliation openerTeam, GameMode gameMode)
        {
            if (itemDatabase == null)
            {
                Debug.LogError($"[ContainerDefinition] {name} is missing its ItemDatabase reference!");
                return null;
            }

            // 1. Determine the target Rarity via Weighted Roll
            ItemRarity targetRarity = RollRarity();

            // 2. Filter the Master Database based on our strict rules
            List<ItemDefinition> validItems = new List<ItemDefinition>();

            foreach (var item in itemDatabase.AllItems) 
            {
                // Rule 1: Rarity Match
                if (item.rarity != targetRarity) continue;

                // Rule 2: Implant Match (Hard Rule)
                if (item.isImplant != dropsImplants) continue;

                // Rule 3: Type Filter
                if (allowedTypes != null && allowedTypes.Count > 0)
                {
                    if (!allowedTypes.Contains(item.type)) continue;
                }

                // Rule 4: Team lock — applies in every mode, not just PvP. Items with a non-None
                // teamLock are restricted to that team's openers regardless of Coop/PvP, so a
                // Cleanser-team singleplayer doesn't get Thrive-locked drops (and vice versa).
                if (item.teamLock != TeamAffiliation.None && item.teamLock != openerTeam)
                {
                    continue;
                }

                validItems.Add(item);
            }

            // 3. Select a final item from the filtered pool
            if (validItems.Count == 0)
            {
                Debug.LogWarning($"[ContainerDefinition] '{name}' found NO valid items for Rarity: {targetRarity}. Check your database or filter settings!");
                return null;
            }

            return validItems[Random.Range(0, validItems.Count)];
        }

        private ItemRarity RollRarity()
        {
            if (rarityWeights == null || rarityWeights.Count == 0) return default;

            float totalWeight = 0f;
            foreach (var rw in rarityWeights) totalWeight += rw.weight;

            float randomRoll = Random.Range(0f, totalWeight);
            
            foreach (var rw in rarityWeights)
            {
                randomRoll -= rw.weight;
                if (randomRoll <= 0f) return rw.rarity;
            }

            return rarityWeights[rarityWeights.Count - 1].rarity; 
        }
    }
}