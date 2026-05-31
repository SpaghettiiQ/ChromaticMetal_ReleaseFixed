using Unity.Netcode;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Stats;
using _Project.Core.Interfaces;
using _Project.Features.ProxyCharacters.Data;

namespace _Project.Features.ProxyCharacters.Scripts
{
    public class ProxyCharacterInitializer : NetworkBehaviour
    {
        [Header("Character References")]
        [SerializeField] private Transform _visualsContainer; 
        
        // Internal references to decoupled CORE systems only
        private CharacterStats _stats;
        private IAbilityController _abilityController;

        private GameObject _currentVisualInstance;

        public string CharacterName { get; private set; } = "Agent";

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
            _abilityController = GetComponent<IAbilityController>();
        }
        
        public override void OnNetworkSpawn()
        {
            // Initialization is handled by MatchNetworkController based on selected character.
        }

        /// <summary>
        /// SERVER ONLY: Sets up authoritative stats and tells clients to build visuals/UI.
        /// </summary>
        public void InitializeProxyCharacter(ProxyCharacterData proxyData)
        {
            if (!IsServer || proxyData == null) return;

            // 1. Inject Stats (Server Authoritative)
            if (_stats != null)
            {
                _stats.SetBaseStat(StatType.MaxHealth, proxyData.baseMaxHealth);
                _stats.SetBaseStat(StatType.DamageReduction, proxyData.damageResistance);
                _stats.Initialize(proxyData.baseMaxHealth); 
            }

            ApplyProxyClientRpc(proxyData.name); 

            if (proxyData.DefaultWeapon == null)
            {
                Debug.LogError($"[ProxyCharacterInitializer] Critical: proxyData.DefaultWeapon evaluated to NULL! Check if defaultWeaponData strictly implements IWeaponData.");
            }
            
            IWeaponSpawner spawner = GetComponentInChildren<IWeaponSpawner>();
            if (spawner != null)
            {
                Debug.Log($"[ProxyCharacterInitializer] Calling SpawnWeaponForCharacter for {proxyData.name}...");
                spawner.SpawnWeaponForCharacter(proxyData.DefaultWeapon);
            }
            else
            {
                Debug.LogError($"[{name}] ProxyCharacterInitializer could not find IWeaponSpawner on this object or its children.");
            }

            // Activate passive abilities (server-side). Each passive's Execute hooks event listeners on this character.
            if (proxyData.passiveAbilities != null)
            {
                foreach (var passive in proxyData.passiveAbilities)
                {
                    if (passive != null && passive.Effect != null)
                    {
                        passive.Effect.Execute(gameObject);
                    }
                }
            }
        }

        [ClientRpc]
        private void ApplyProxyClientRpc(string proxyDataName)
        {
            ProxyCharacterData proxyData = Resources.Load<ProxyCharacterData>($"ProxyData/{proxyDataName}");
            if (proxyData == null) return;
            
            CharacterName = proxyData.proxyName;

            // Apply movement stats directly to the localized PlayerMovement component if it exists
            if (TryGetComponent(out _Project.Features.Player.Scripts.PlayerMovement movement))
            {
                movement.walkSpeed = proxyData.moveSpeed;
                movement.runSpeed = proxyData.moveSpeed * 1.57f; // Roughly keeping original ratio 7 -> 11
                movement.jumpHeight = proxyData.jumpHeight;
            }

            // 1. Spawn Visuals
            if (proxyData.visualPrefab != null && _visualsContainer != null)
            {
                if (_currentVisualInstance != null) Destroy(_currentVisualInstance);
                _currentVisualInstance = Instantiate(proxyData.visualPrefab, _visualsContainer);
            }

            // 2. Inject Local Ability Loadout via Core Interface
            if (_abilityController != null)
            {
                _abilityController.EquipAbility(AbilitySlot.Secondary, proxyData.SecondaryAbility);
                _abilityController.EquipAbility(AbilitySlot.Utility, proxyData.UtilityAbility);
                _abilityController.EquipAbility(AbilitySlot.Special, proxyData.SpecialAbility);
            }
        }
    }
}