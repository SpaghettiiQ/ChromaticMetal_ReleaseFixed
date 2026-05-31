using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Ion Surge", fileName = "IonSurge")]
    public class IonSurgeItem : ItemDefinition, IOnDamageTakenItem
    {
        [Tooltip("Damage dealt to nearby enemies when hit, at 1 stack.")]
        public float damageBase = 15f;
        [Tooltip("Extra damage per stack beyond the first.")]
        public float damagePerStack = 10f;
        [Tooltip("Radius around the owner that gets zapped.")]
        public float radius = 6f;

        public void OnDamageTaken(GameObject owner, int stacks, DamageContext ctx)
        {
            // Only react to "real" hits. Don't re-zap on every burn/bleed/burden tick the owner
            // suffers (would be spammy), nor on item-proc follow-ups routed back through us.
            if (ctx.AttackType == AttackType.DamageOverTime) return;
            if (ctx.AttackType == AttackType.Burden) return;
            if (ctx.AttackType == AttackType.Secondary) return;

            TeamAffiliation ownerTeam = TeamAffiliation.None;
            if (owner.TryGetComponent(out CharacterStats ownerStats)) ownerTeam = ownerStats.Team;

            int damage = Mathf.Max(1, Mathf.RoundToInt(damageBase + damagePerStack * (stacks - 1)));

            Collider[] hits = Physics.OverlapSphere(owner.transform.position, radius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == owner) continue;
                var targetStats = hit.GetComponentInParent<CharacterStats>();
                if (targetStats == null || targetStats.IsDead) continue;
                if (ownerTeam != TeamAffiliation.None && targetStats.Team != TeamAffiliation.None && targetStats.Team == ownerTeam) continue;

                // Effect tag (not Secondary): intentionally routes through TakeDamage so the owner's
                // on-hit items can proc off it, but it isn't an IOnDamageTakenItem trigger for the
                // ENEMY (enemies carry no items), so there is no cascade back to this item.
                targetStats.TakeDamage(new DamageContext
                {
                    Source = owner,
                    Target = targetStats.gameObject,
                    Damage = damage,
                    DamageType = DamageType.Physical,
                    AttackType = AttackType.Effect,
                    IsCritical = false
                });
            }
        }
    }
}
