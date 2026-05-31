using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Stats; 
using _Project.Core.Enums;
using _Project.Features.Interactables.Data;
using _Project.Features.Items.Scripts;
using _Project.Features.LobbySystem.Scripts;
using _Project.Features.StageSystem.Scripts;
using _Project.Features.DifficultySystem.Scripts;

namespace _Project.Features.Interactables.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class NetworkChest : NetworkBehaviour, IInteractable
    {
        [Header("Data & Components")]
        [SerializeField] private ContainerDefinition definition;
        [SerializeField] private LidOpener lidOpener; 
        [SerializeField] private LidOpener lidOpener2;

        // Networked state so late-joining clients see the chest is already open
        private NetworkVariable<bool> _isOpen = new NetworkVariable<bool>();
        private NetworkVariable<ushort> _droppedItemID = new NetworkVariable<ushort>();
        
        private NetworkVariable<int> _containerCost = new NetworkVariable<int>();
        private GameObject _localDropVisual;

        public override void OnNetworkSpawn()
        {
            if (IsServer && definition != null)
            {
                float diffMult = 1f;
                IDifficultyManager difficulty = DifficultyNetworkController.Singleton;
                if (difficulty != null)
                {
                    diffMult = difficulty.ContainerCostMultiplier;
                }
                if (definition.baseCost <= 0)
                {
                    _containerCost.Value = 0;
                }
                else
                {
                    _containerCost.Value = Mathf.Max(1, (int)(definition.baseCost * definition.costScale * diffMult));
                }
            }

            _droppedItemID.OnValueChanged += (oldVal, newVal) => UpdateDropVisual(newVal);

            // Sync visual state for late joiners
            if (_isOpen.Value)
            {
                if (lidOpener != null) lidOpener.OpenImmediate();
                if (lidOpener2 != null) lidOpener2.OpenImmediate();
                if (_droppedItemID.Value > 0)
                {
                    UpdateDropVisual(_droppedItemID.Value);
                }
            }
        }

        private void Update()
        {
            if (_localDropVisual != null)
            {
                _localDropVisual.transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
            }
        }

        public string GetPromptSuffix()
        {
            if (_isOpen.Value)
            {
                if (_droppedItemID.Value > 0 && definition?.itemDatabase != null)
                {
                    var item = definition.itemDatabase.GetItem(_droppedItemID.Value);
                    return item != null ? $"pick up {item.itemName}" : "pick up item";
                }
                return string.Empty;
            }
            
            string containerName = definition != null ? definition.name : "Chest";
            string actionWord = definition != null && definition.dropsImplants ? "use" : "open";
            
            // Allow for free chests if cost is 0
            if (_containerCost.Value <= 0) return $"{actionWord} {containerName}";

            return $"{actionWord} {containerName} (${_containerCost.Value})";
        }

        public bool CanInteract(GameObject interactor)
        {
            if (_isOpen.Value && _droppedItemID.Value == 0) return false;
            return true; // Decoupled cost check so it shows the prompt even without enough money!
        }

        public void Interact(GameObject interactor)
        {
            if (!IsServer) return; // STRICT RULE: Only the server executes interaction logic

            if (!CanInteract(interactor)) return;

            if (!_isOpen.Value)
            {
                // 1. Check and deduct cost if applicable
                if (_containerCost.Value > 0 && interactor.TryGetComponent(out CharacterStats stats))
                {
                    // Per-player interactable discount (Discount Chip). ContainerCostMultiplier is
                    // seeded to 1 (no discount) so this is a no-op without the item.
                    float costMult = stats.GetStat(StatType.ContainerCostMultiplier);
                    if (costMult <= 0f) costMult = 1f;
                    int cost = Mathf.Max(1, Mathf.FloorToInt(_containerCost.Value * costMult));
                    if (stats.CurrentMoney.Value < cost) return;
                    stats.RemoveMoney(cost);
                }
                
                // 2. Mark as open
                _isOpen.Value = true;

                // 3. Roll Loot dynamically
                RollLoot(interactor);

                // 4. Tell all clients to play the open animation/sound
                OpenChestClientRpc();
            }
            else if (_droppedItemID.Value > 0)
            {
                // 5. Pick up the dropped item
                if (interactor.TryGetComponent<CharacterInventory>(out var inventory))
                {
                    inventory.AddItemServer(_droppedItemID.Value, 1);
                    _droppedItemID.Value = 0; // Clears the drop, updating visual/collider on all clients
                }
            }
        }

        private void RollLoot(GameObject interactor)
        {
            if (definition == null) return;

            // 1. Identify the opener's Team Affiliation via the Lobby
            TeamAffiliation openerTeam = TeamAffiliation.None;
            if (interactor.TryGetComponent<NetworkObject>(out var netObj))
            {
                if (LobbyNetworkController.Singleton != null)
                {
                    foreach (var playerState in LobbyNetworkController.Singleton.LobbyPlayers)
                    {
                        if (playerState.ClientId == netObj.OwnerClientId)
                        {
                            openerTeam = playerState.Team;
                            break;
                        }
                    }
                }
            }

            // 2. Identify the current Game Mode
            GameMode currentMode = GameMode.Coop; // Safe fallback
            if (RunNetworkController.Singleton != null)
            {
                currentMode = RunNetworkController.Singleton.activeRunMode.Value;
            }

            // 3. Generate the dynamic, filtered loot
            var drop = definition.GetRandomDrop(openerTeam, currentMode);
            if (drop == null) return;

            // 4. Set the dropped item ID to spawn the visual 
            _droppedItemID.Value = drop.itemID;
        }

        private void UpdateDropVisual(ushort itemID)
        {
            if (_localDropVisual != null)
            {
                Destroy(_localDropVisual);
            }

            if (itemID == 0)
            {
                // If it's 0 and open, chest is completely empty, disable collider to hide interact prompt
                if (_isOpen.Value && TryGetComponent(out Collider col))
                {
                    col.enabled = false;
                }
                return;
            }

            if (definition == null || definition.itemDatabase == null) return;

            var itemDef = definition.itemDatabase.GetItem(itemID);
            if (itemDef == null) return;

            // Create a local, non-networked rotating visual 
            _localDropVisual = new GameObject("LocalItemVisual");
            var sr = _localDropVisual.AddComponent<SpriteRenderer>();
            sr.sprite = itemDef.icon;
            
            // Adjust sorting layer if necessary (assuming standard default)
            sr.sortingOrder = 5;

            _localDropVisual.transform.position = transform.position + Vector3.up * 1.5f;
            _localDropVisual.transform.localScale = Vector3.one * 0.5f;
        }

        [ClientRpc]
        private void OpenChestClientRpc()
        {
            if (lidOpener != null) lidOpener.OpenLid();
            if (lidOpener2 != null) lidOpener2.OpenLid();
        }
    }
}