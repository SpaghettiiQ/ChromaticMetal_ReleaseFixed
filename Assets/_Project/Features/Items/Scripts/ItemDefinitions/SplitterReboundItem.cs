using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;
using _Project.Features.Items.Scripts;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Splitter Rebound", fileName = "SplitterRebound")]
    public class SplitterReboundItem : ItemDefinition, IOnHitItem
    {
        [Tooltip("Base ricochet chance at 1 stack (0-1).")]
        public float baseProcChance = 0.10f;
        [Tooltip("Added chance per stack beyond the first.")]
        public float procChancePerStack = 0.05f;
        [Tooltip("Search radius around the original target.")]
        public float ricochetRadius = 6f;

        [Header("Tracer Visual")]
        public Color tracerColor = new Color(0.1f, 0.85f, 1f, 1f);
        public float tracerWidth = 0.025f;
        public float tracerLifetime = 0.1f;

        public void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            if (ctx.AttackType == AttackType.Secondary) return;
            if (ctx.AttackType == AttackType.DamageOverTime) return; // burn/bleed shouldn't ricochet
            if (ctx.Target == null) return;

            float chance = baseProcChance + procChancePerStack * (stacks - 1);

            TeamAffiliation ownerTeam = TeamAffiliation.None;
            if (owner.TryGetComponent(out CharacterStats ownerStats))
            {
                ownerTeam = ownerStats.Team;
                float bonusFlat = ownerStats.GetStat(StatType.ProcChanceFlatAdd);
                float mult = ownerStats.GetStat(StatType.ProcChanceMultiplier);
                if (mult <= 0f) mult = 1f;
                chance = (chance * mult) + bonusFlat;
            }

            if (Random.value > chance) return;

            owner.TryGetComponent(out CharacterInventory inventory);
            Vector3 tracerStart = ctx.Target.transform.position + Vector3.up * 1.2f;

            Collider[] hits = Physics.OverlapSphere(ctx.Target.transform.position, ricochetRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == owner) continue;
                if (hit.gameObject == ctx.Target) continue;
                if (!hit.TryGetComponent(out CharacterStats targetStats)) continue;
                if (targetStats.IsDead) continue;
                if (ownerTeam != TeamAffiliation.None && targetStats.Team != TeamAffiliation.None && targetStats.Team == ownerTeam) continue;

                if (inventory != null)
                {
                    Vector3 tracerEnd = hit.transform.position + Vector3.up * 1.2f;
                    inventory.PlayItemTracerClientRpc(tracerStart, tracerEnd, tracerColor, tracerWidth, tracerLifetime);
                }

                var ricochetCtx = new DamageContext
                {
                    Source = owner,
                    Target = hit.gameObject,
                    Damage = ctx.Damage,
                    DamageType = ctx.DamageType,
                    AttackType = AttackType.Secondary,
                    IsCritical = false
                };
                targetStats.TakeDamage(ricochetCtx);
            }
        }
    }
}
