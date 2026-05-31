using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos
{
    /// <summary>
    /// Thanatos's Special — teleports up to a max distance in the look direction (or in front
    /// of the nearest enemy if closer), then auto-fires N shotgun shots distributed across
    /// enemies in view. Resets heat to 0 first so the natural overheat path triggers the
    /// heat-core eject once the auto-fire fills the bar back to max.
    /// </summary>
    [CreateAssetMenu(fileName = "InfernumAbility", menuName = "Proxy Abilities/Thanatos/Infernum")]
    public class InfernumAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Teleport")]
        [Tooltip("Maximum teleport distance in metres. The dash uses the full 3D look direction " +
                 "(including vertical). If an enemy is closer, lands just in front of them instead.")]
        public float teleportDistance = 15f;
        [Tooltip("How far short of the targeted enemy Thanatos stops, so pellets fly forward " +
                 "instead of clipping into the target's collider.")]
        public float stoppingDistance = 4f;

        [Header("Auto-Fire")]
        [Tooltip("Total shotgun shots fired across all enemies in view (round-robin).")]
        public int postTeleportShots = 8;
        [Tooltip("Radius used when SEARCHING for the teleport's primary target (so Thanatos " +
                 "lands in front of an enemy you can see at distance).")]
        public float viewRadius = 20f;
        [Tooltip("Auto-fire targeting radius from the post-teleport position. Enemies outside " +
                 "this range or without line-of-sight aren't shot at. If NO enemies are within " +
                 "this range, the ability fires no shots and no heat is generated.")]
        public float targetingRadius = 15f;
        [Tooltip("Seconds between consecutive auto-fired shots — small delay reads as a fan-out " +
                 "rather than a single burst.")]
        public float shotInterval = 0.06f;
        [Tooltip("Seconds to wait after teleporting before the first shot fires.")]
        public float teleportToFireDelay = 0.15f;

        public void Execute(GameObject instigator)
        {
            if (instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Tracking.InfernumTracker tracker))
            {
                tracker.ExecuteAbility(this);
            }
            else
            {
                tracker = instigator.AddComponent<_Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Tracking.InfernumTracker>();
                tracker.ExecuteAbility(this);
            }
        }
    }
}
