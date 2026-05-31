using UnityEngine;
using _Project.Core.Enums;

namespace _Project.Features.Items.Data
{
    public abstract class ItemDefinition : ScriptableObject
    {
        [Header("Core Routing Data")]
        [Tooltip("CRITICAL: Must be unique and match its assignment in the ItemDatabase")]
        public ushort itemID; 
        
        [Header("Item Metadata")]
        public string itemName;
        [TextArea] public string description;
        public Sprite icon;
        public ItemRarity rarity;
        public ItemType type;
        public bool isImplant;
        public TeamAffiliation teamLock;
        public bool canBecomeBurdened;
        public ushort burdenedItemID;
    }
}