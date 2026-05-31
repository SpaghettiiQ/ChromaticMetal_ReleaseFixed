using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/T1 Thunderhead", fileName = "T1Thunderhead")]
    public class T1ThunderheadItem : ItemDefinition, IOnHitItem
    {
        [Tooltip("A hit dealing more than this multiple of the attacker's weapon BaseDamage triggers " +
                 "the electric explosion (4 = >400%).")]
        public float damageThresholdMultiplier = 4f;
        [Tooltip("Radius of the electric AoE explosion.")]
        public float aoeRadius = 5f;
        [Tooltip("Explosion damage at 1 stack.")]
        public float explosionDamageBase = 40f;
        [Tooltip("Extra explosion damage per stack beyond the first.")]
        public float explosionDamagePerStack = 20f;

        [Header("VFX")]
        public GameObject explosionVfxPrefab;

        public void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            // "In one hit" → only big direct hits. Skip our own AoE (Secondary) and DOT ticks.
            if (ctx.AttackType == AttackType.Secondary) return;
            if (ctx.AttackType == AttackType.DamageOverTime) return;
            if (ctx.Target == null) return;

            // Threshold = damageThresholdMultiplier × the attacker's equipped weapon BaseDamage.
            IWeapon weapon = owner.GetComponentInChildren<IWeapon>();
            if (weapon == null || weapon.WeaponData == null) return;
            int baseDamage = weapon.WeaponData.BaseDamage;
            if (baseDamage <= 0) return;
            if (ctx.Damage <= baseDamage * damageThresholdMultiplier) return;

            TeamAffiliation ownerTeam = TeamAffiliation.None;
            if (owner.TryGetComponent(out CharacterStats ownerStats)) ownerTeam = ownerStats.Team;

            int explosionDamage = Mathf.Max(1, Mathf.RoundToInt(explosionDamageBase + explosionDamagePerStack * (stacks - 1)));
            Vector3 center = ctx.Target.transform.position;

            Collider[] hits = Physics.OverlapSphere(center, aoeRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == owner) continue;
                var targetStats = hit.GetComponentInParent<CharacterStats>();
                if (targetStats == null || targetStats.IsDead) continue;
                if (ownerTeam != TeamAffiliation.None && targetStats.Team != TeamAffiliation.None && targetStats.Team == ownerTeam) continue;

                targetStats.TakeDamage(new DamageContext
                {
                    Source = owner,
                    Target = targetStats.gameObject,
                    Damage = explosionDamage,
                    DamageType = DamageType.Explosive,
                    AttackType = AttackType.Secondary, // proc-recursion guard (see top early-return)
                    IsCritical = false
                });
            }

            if (explosionVfxPrefab != null)
            {
                GameObject vfx = Object.Instantiate(explosionVfxPrefab, center, Quaternion.identity);
                Object.Destroy(vfx, 2f);
            }
        }
    }
}
