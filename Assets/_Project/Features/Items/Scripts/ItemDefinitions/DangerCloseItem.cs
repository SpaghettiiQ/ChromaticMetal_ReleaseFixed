using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Danger Close", fileName = "DangerClose")]
    public class DangerCloseItem : ItemDefinition, IPassiveItem
    {
        [Tooltip("Seconds between missiles while the exit door charges.")]
        public float missileInterval = 5f;
        [Tooltip("Total missiles fired per charge sequence.")]
        public int missileCount = 5;
        [Tooltip("Radius around the owner to pick a random target enemy from.")]
        public float searchRadius = 25f;
        [Tooltip("How high above the target the missile spawns.")]
        public float spawnHeight = 20f;
        [Tooltip("Descent speed of the missile (units/sec).")]
        public float missileSpeed = 35f;
        [Tooltip("Explosion AoE radius on impact.")]
        public float aoeRadius = 5f;
        [Tooltip("Missile explosion damage at 1 stack.")]
        public float missileDamageBase = 60f;
        [Tooltip("Missile damage bonus per stack beyond the first (0.025 = +2.5%/stack).")]
        public float missileDamageBonusPerStack = 0.025f;

        [Header("VFX")]
        public GameObject missilePrefab;
        public GameObject explosionVfxPrefab;

        // Per-owner state lives on DangerCloseRuntime (avoids shared-ScriptableObject aliasing).
        public void ApplyPassive(GameObject owner, int stacks)
        {
            var rt = owner.GetComponent<DangerCloseRuntime>();
            if (rt == null) rt = owner.AddComponent<DangerCloseRuntime>();
            rt.Activate(this, stacks);
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out DangerCloseRuntime rt)) rt.Deactivate();
        }
    }
}
