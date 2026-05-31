using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos
{
    [CreateAssetMenu(fileName = "OfTheAbyssAbility", menuName = "Proxy Abilities/Thanatos/Of The Abyss")]
    public class OfTheAbyssAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Dash Settings")]
        [Tooltip("Instant velocity burst added on cast. Big enough to overcome PlayerMovement's " +
                 "ground friction so the dash actually covers distance.")]
        public float dashImpulse = 35f;
        [Tooltip("Seconds the jump-height boost (and trail spawning) lasts.")]
        public float dashDuration = 0.6f;
        [Tooltip("Reset the player's pre-dash velocity so the cast feels punchy regardless of " +
                 "what they were doing. False preserves momentum (the dash adds on top).")]
        public bool resetVelocityOnDash = false;

        [Header("Jump Boost (active during dashDuration)")]
        [Tooltip("Multiplier applied to PlayerMovement.jumpHeight while the dash is active. " +
                 "1.0 = no change. Restored automatically when dashDuration elapses.")]
        public float jumpHeightMultiplier = 1.8f;

        [Header("Residue Trail")]
        [Tooltip("Residue prefab dropped behind the dash. Should contain a ResidueHazardZone " +
                 "or similar networked trigger.")]
        public GameObject residueTrailPrefab;
        [Tooltip("How many residue segments to drop across dashDuration.")]
        public int trailSegments = 8;
        [Tooltip("Seconds before a dropped residue segment is destroyed.")]
        public float trailLifetime = 10f;

        public void Execute(GameObject instigator)
        {
            if (instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Tracking.OfTheAbyssTracker tracker))
            {
                tracker.ExecuteAbility(this);
            }
            else
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Tracking.OfTheAbyssTracker>();
                tracker.ExecuteAbility(this);
            }
        }
    }
}
