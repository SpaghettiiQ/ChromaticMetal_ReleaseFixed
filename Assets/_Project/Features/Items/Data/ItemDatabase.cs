using System.Collections.Generic;
using UnityEngine;

namespace _Project.Features.Items.Data
{
    [CreateAssetMenu(menuName = "Game Data/Items/Item Database", fileName = "MasterItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [Tooltip("Assign every ItemDefinition in the game here.")]
        public List<ItemDefinition> allItems = new List<ItemDefinition>();

        // --- NEW ADDITION ---
        // Exposes the list safely for the ContainerDefinition to query!
        public List<ItemDefinition> AllItems => allItems;
        // --------------------

        private Dictionary<ushort, ItemDefinition> _itemDictionary;

        public void Initialize()
        {
            _itemDictionary = new Dictionary<ushort, ItemDefinition>();
            foreach (var item in allItems)
            {
                if (item != null && !_itemDictionary.ContainsKey(item.itemID))
                {
                    _itemDictionary.Add(item.itemID, item);
                }
                else if (item != null)
                {
                    Debug.LogError($"[ItemDatabase] Duplicate ItemID found: {item.itemID} on {item.name}. Fix this immediately!");
                }
            }
        }

        public ItemDefinition GetItem(ushort id)
        {
            if (_itemDictionary == null || _itemDictionary.Count == 0) Initialize();
            
            if (_itemDictionary.TryGetValue(id, out var item))
            {
                return item;
            }
            Debug.LogWarning($"[ItemDatabase] Item with ID {id} not found!");
            return null;
        }
    }
}