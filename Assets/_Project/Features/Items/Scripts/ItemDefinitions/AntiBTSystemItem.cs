using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;
using _Project.Features.Items.Scripts;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Anti-BT System", fileName = "AntiBTSystem")]
    public class AntiBTSystemItem : ItemDefinition, IUsableItem
    {
        public void UseItem(GameObject owner, int stacks)
        {
            Debug.Log("[Anti-BT System] Cleansing Burst activated! Removing burdened status!");

            if (owner.TryGetComponent(out _Project.Features.BurdenSystem.Scripts.BurdenController burden))
            {
                burden.RemoveBurden(burden.GetCurrentBurden());
            }

            if (owner.TryGetComponent(out CharacterInventory inventory))
            {
                inventory.RemoveItemServer(this.itemID, 1);
                Debug.Log($"Removed 1 {this.itemName} from inventory.");
            }
        }
    }
}