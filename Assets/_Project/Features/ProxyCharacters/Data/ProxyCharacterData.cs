using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Data;
using _Project.Features.ProxyAbilities.Data;

namespace _Project.Features.ProxyCharacters.Data
{
    [CreateAssetMenu(fileName = "NewProxyCharacter", menuName = "Game Data/Proxy Character Data")]
    public class ProxyCharacterData : ScriptableObject
    {
        [Header("Identity")]
        public string proxyName;
        
        [TextArea(2, 4)] 
        public string description;
        
        public Sprite icon;
        
        [Tooltip("The team this proxy belongs to (Cleansers or THRIVE).")]
        public TeamDefinition teamDefinition;

        [Header("Visuals")]
        [Tooltip("The 3D model/animator to child to the generic Base Player Prefab.")]
        public GameObject visualPrefab;

        [Header("Base Stats")]
        public int baseMaxHealth = 100;
        public float moveSpeed = 7f;
        public float jumpHeight = 8f;
        
        [Range(0f, 100f)]
        [Tooltip("Percentage of incoming damage reduced.")]
        public float damageResistance = 0f;

        [Header("Starting Loadout")]
        [Tooltip("Must be a ScriptableObject that implements IWeaponData (e.g., WeaponData).")]
        public ScriptableObject defaultWeaponData;

        // Core systems will read this safely without knowing it's a WeaponData
        public IWeaponData DefaultWeapon => defaultWeaponData as IWeaponData;

        [Header("Active Abilities (Keybinds)")]
        [Tooltip("M2 - Secondary")]
        public ScriptableObject secondaryAbilityData;
        public IProxyAbilityData SecondaryAbility => secondaryAbilityData as IProxyAbilityData;
        
        [Tooltip("Shift - Utility")]
        public ScriptableObject utilityAbilityData;
        public IProxyAbilityData UtilityAbility => utilityAbilityData as IProxyAbilityData;
        
        [Tooltip("R - Special")]
        public ScriptableObject specialAbilityData;
        public IProxyAbilityData SpecialAbility => specialAbilityData as IProxyAbilityData;

        [Header("Passive Abilities")]
        [Tooltip("Abilities that provide constant stat buffs or persistent effects.")]
        public ProxyAbilityData[] passiveAbilities;

        // Editor validation to ensure designers only slot valid objects
        private void OnValidate()
        {
            if (defaultWeaponData != null && !(defaultWeaponData is IWeaponData))
            {
                Debug.LogError($"[{name}] The assigned default weapon '{defaultWeaponData.name}' does not implement IWeaponData!");
                defaultWeaponData = null;
            }
            if (secondaryAbilityData != null && !(secondaryAbilityData is IProxyAbilityData))
            {
                Debug.LogError($"[{name}] Secondary Ability does not implement IProxyAbilityData!");
                secondaryAbilityData = null;
            }
            if (utilityAbilityData != null && !(utilityAbilityData is IProxyAbilityData))
            {
                Debug.LogError($"[{name}] Utility Ability does not implement IProxyAbilityData!");
                secondaryAbilityData = null;
            }
            if (specialAbilityData != null && !(specialAbilityData is IProxyAbilityData))
            {
                Debug.LogError($"[{name}] Special Ability does not implement IProxyAbilityData!");
                secondaryAbilityData = null;
            }
        }
    }
}