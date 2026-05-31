using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit
{
    [CreateAssetMenu(fileName = "HollowHeartAbility", menuName = "Proxy Abilities/Conduit/Hollow Heart")]
    public class HollowHeartAbility : ScriptableObject, IAbilityEffect
    {
        public float duration = 6f;
        public float targetRange = 30f;
        public float healMultiplierPerBurden = 2.0f; // E.g., 10 burden -> 20 healing

        [Header("VFX")]
        [Tooltip("Networked particle prefab spawned + parented to the buffed target while " +
                 "active. Must have a NetworkObject so every client sees it. Despawned when " +
                 "the buff ends.")]
        public GameObject buffVfxPrefab;

        public void Execute(GameObject instigator)
        {
            if (!instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking.HollowHeartCaster tracker))
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking.HollowHeartCaster>();
            }
            tracker.ExecuteAbility(duration, targetRange, healMultiplierPerBurden, buffVfxPrefab);
        }
    }
}