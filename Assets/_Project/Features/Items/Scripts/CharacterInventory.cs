using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Events;
using _Project.Core.Interfaces;
using _Project.Core.Structs;
using _Project.Features.Items.Data;
using _Project.Features.Items.Structs;

namespace _Project.Features.Items.Scripts
{
    [RequireComponent(typeof(CharacterEventBus))]
    public class CharacterInventory : NetworkBehaviour, IItemMutator
    {
        [Header("Data Core")]
        [SerializeField] private ItemDatabase _database;
        public ItemDatabase Database => _database;
        
        private CharacterEventBus _eventBus;

        // --- NETWORKED STATE ---
        // Synced automatically to all clients. Only Server modifies.
        public NetworkList<ItemStackStruct> NetworkedItems;

        // --- LOCAL CACHES ---
        // Tuples pair the interface hook with its item ID so dispatch never needs an unsafe cast.
        private List<(IOnHitItem hook, ushort id)> _onHitItems = new();
        private List<(IOnDamageTakenItem hook, ushort id)> _onDamageTakenItems = new();
        private List<(IOnKillItem hook, ushort id)> _onKillItems = new();
        private Dictionary<IPassiveItem, int> _activePassives = new(); // Tracks current passives for clean removal

        private void Awake()
        {
            NetworkedItems = new NetworkList<ItemStackStruct>();
            _eventBus = GetComponent<CharacterEventBus>();
            _database.Initialize();
        }

        public override void OnNetworkSpawn()
        {
            NetworkedItems.OnListChanged += HandleInventoryChanged;

            if (IsServer)
            {
                _eventBus.OnHit += HandleOnHit;
                _eventBus.OnDamageTaken += HandleOnDamageTaken;
                _eventBus.OnKill += HandleOnKill;
            }

            // Sync up in case of late-join
            RebuildEventCaches();
        }

        public override void OnNetworkDespawn()
        {
            NetworkedItems.OnListChanged -= HandleInventoryChanged;
            
            if (IsServer)
            {
                _eventBus.OnHit -= HandleOnHit;
                _eventBus.OnDamageTaken -= HandleOnDamageTaken;
                _eventBus.OnKill -= HandleOnKill;
            }
        }

        // ==========================================
        // SERVER AUTHORITY: MODIFYING INVENTORY
        // ==========================================
        public void AddItemServer(ushort itemID, int amount = 1)
        {
            if (!IsServer) return;

            for (int i = 0; i < NetworkedItems.Count; i++)
            {
                if (NetworkedItems[i].ItemID == itemID)
                {
                    var stack = NetworkedItems[i];
                    stack.Stacks += amount;
                    NetworkedItems[i] = stack; // Reassign to trigger NetworkList update
                    return;
                }
            }
            
            // Item not found, add new
            NetworkedItems.Add(new ItemStackStruct { ItemID = itemID, Stacks = amount });
        }

        public void RemoveItemServer(ushort itemID, int amount = 1)
        {
            if (!IsServer) return;

            for (int i = 0; i < NetworkedItems.Count; i++)
            {
                if (NetworkedItems[i].ItemID == itemID)
                {
                    var stack = NetworkedItems[i];
                    stack.Stacks -= amount;
                    
                    if (stack.Stacks <= 0)
                    {
                        NetworkedItems.RemoveAt(i);
                    }
                    else
                    {
                        NetworkedItems[i] = stack;
                    }
                    return;
                }
            }
        }

        // ==========================================
        // LOCAL LOGIC: EVENT CACHING & PASSIVES
        // ==========================================
        private void HandleInventoryChanged(NetworkListEvent<ItemStackStruct> changeEvent)
        {
            RebuildEventCaches();

            // Fire a pickup notification on the local owner so the HUD can pop a card.
            // Only Add (brand new stack) and Value (existing stack incremented) count as pickups —
            // RemoveAt/Clear shouldn't trigger it. Owner-only because each player should only
            // see their own pickups.
            if (!IsOwner) return;
            if (changeEvent.Type == NetworkListEvent<ItemStackStruct>.EventType.Add)
            {
                _eventBus?.RaiseOnItemPickedUp(changeEvent.Value.ItemID);
            }
            else if (changeEvent.Type == NetworkListEvent<ItemStackStruct>.EventType.Value &&
                     changeEvent.Value.Stacks > changeEvent.PreviousValue.Stacks)
            {
                _eventBus?.RaiseOnItemPickedUp(changeEvent.Value.ItemID);
            }
        }

        private void RebuildEventCaches()
        {
            // 1. Cleanly remove all current passive effects using their exact previous stack count
            foreach (var kvp in _activePassives)
            {
                kvp.Key.RemovePassive(gameObject, kvp.Value);
            }
            
            _activePassives.Clear();
            _onHitItems.Clear();
            _onDamageTakenItems.Clear();
            _onKillItems.Clear();

            // 2. Rebuild interface routing based on new list
            foreach (var stack in NetworkedItems)
            {
                ItemDefinition def = _database.GetItem(stack.ItemID);
                if (def == null) continue;

                if (def is IOnHitItem hitItem) _onHitItems.Add((hitItem, stack.ItemID));
                if (def is IOnDamageTakenItem dmgItem) _onDamageTakenItems.Add((dmgItem, stack.ItemID));
                if (def is IOnKillItem killItem) _onKillItems.Add((killItem, stack.ItemID));
                
                if (def is IPassiveItem passiveItem)
                {
                    _activePassives.Add(passiveItem, stack.Stacks);
                    passiveItem.ApplyPassive(gameObject, stack.Stacks);
                }
            }
        }

        // ==========================================
        // FAST EVENT ROUTING (SERVER LOGIC EXECUTION)
        // ==========================================
        private void HandleOnHit(DamageContext ctx)
        {
            if (!IsServer) return;
            foreach (var (hook, id) in _onHitItems)
                hook.OnHitEnemy(gameObject, GetStacks(id), ctx);
        }

        private void HandleOnDamageTaken(DamageContext ctx)
        {
            if (!IsServer) return;
            foreach (var (hook, id) in _onDamageTakenItems)
                hook.OnDamageTaken(gameObject, GetStacks(id), ctx);
        }

        private void HandleOnKill(DamageContext ctx)
        {
            if (!IsServer) return;
            foreach (var (hook, id) in _onKillItems)
                hook.OnKillEnemy(gameObject, GetStacks(id), ctx);
        }

        private int GetStacks(ushort itemID)
        {
            foreach (var stack in NetworkedItems)
            {
                if (stack.ItemID == itemID) return stack.Stacks;
            }
            return 0;
        }
        
        
        public void TriggerRandomMutation()
        {
            if (!IsServer || _database == null || NetworkedItems.Count == 0) return;

            // 1. Gather mutable items
            List<ushort> mutableItems = new List<ushort>();
            foreach (var stack in NetworkedItems)
            {
                ItemDefinition def = _database.GetItem(stack.ItemID);
                if (def != null && def.canBecomeBurdened)
                {
                    mutableItems.Add(stack.ItemID);
                }
            }

            // 2. Abort if nothing can mutate
            if (mutableItems.Count == 0)
            {
                Debug.Log("[SERVER] Player has no mutable items.");
                return;
            }

            // 3. Pick random and swap
            ushort itemToMutateID = mutableItems[UnityEngine.Random.Range(0, mutableItems.Count)];
            ItemDefinition originalDef = _database.GetItem(itemToMutateID);
            
            RemoveItemServer(originalDef.itemID, 1);
            AddItemServer(originalDef.burdenedItemID, 1);
            
            Debug.Log($"[SERVER] Mutated 1 stack of {originalDef.itemName} into ItemID: {originalDef.burdenedItemID}");
        }

        /// <summary>
        /// Server-invoked broadcast that draws a tracer line on every client, using the shared
        /// LineRenderer helper. Items use this to visualize on-hit secondary effects
        /// (Synaptic Resonator chains, Splitter Rebound ricochets, etc.).
        /// </summary>
        [ClientRpc]
        public void PlayItemTracerClientRpc(Vector3 start, Vector3 end, Color color, float width, float lifetime)
        {
            _Project.Core.Managers.TracerLine.SpawnLocal(start, end, color, width, lifetime);
        }
    }
}