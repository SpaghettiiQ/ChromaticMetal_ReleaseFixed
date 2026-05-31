using UnityEngine;

namespace _Project.Core.Enums
{
    public enum ItemRarity
    {
        Tier1,
        Tier2,
        Tier3,
        Tier3Plus,
        Tier0,
        Angel
    }

    public static class ItemRarityExtensions
    {
        public static string GetDisplayName(this ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Tier1 => "Tier 1 | Common",
                ItemRarity.Tier2 => "Tier 2 | Rare",
                ItemRarity.Tier3 => "Tier 3 | Restricted",
                ItemRarity.Tier3Plus => "Tier 3+ | Renowned",
                ItemRarity.Tier0 => "Tier 0 | Prototype",
                ItemRarity.Angel => "Angel",
                _ => rarity.ToString()
            };
        }
        
        public static Color GetColor(this ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Tier1 => new Color(0.75f, 0.75f, 0.75f),
                ItemRarity.Tier2 => new Color(0.21f, 0.9f, 0.29f),
                ItemRarity.Tier3 => new Color(0.56f, 0.29f, 1f),
                ItemRarity.Tier3Plus => new Color(0.89f, 1f, 0.12f),
                ItemRarity.Tier0 => new Color(0.96f, 0.62f, 0.1f),
                ItemRarity.Angel => new Color(1f, 0.12f, 0.17f),
                _ => Color.white
            };
        }
    }
}