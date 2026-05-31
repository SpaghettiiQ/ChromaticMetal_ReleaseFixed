using System.Collections.Generic;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;
using _Project.Features.Items.Scripts;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Synaptic Resonator", fileName = "SynapticResonator")]
    public class SynapticResonatorItem : ItemDefinition, IOnHitItem, IPassiveItem
    {
        [Tooltip("Radius for finding the next chain target.")]
        public float chainRadius = 8f;

        [Header("Tracer Visual")]
        public Color tracerColor = new Color(1f, 0.55f, 0.1f, 1f);
        public float tracerWidth = 0.025f;
        public float tracerLifetime = 0.1f;

        [Tooltip("Crit chance granted per stack (0.10 = +10%). Lets the item self-enable its " +
                 "own on-crit chain proc without depending on other crit sources.")]
        public float critChancePerStack = 0.10f;

        public void ApplyPassive(GameObject owner, int stacks)
        {
            if (!owner.TryGetComponent(out CharacterStats stats)) return;
            float chance = critChancePerStack * stacks;
            if (chance <= 0f) return;
            stats.AddModifier(StatType.CritChance, new StatModifier(StatModType.Flat, chance));
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            if (!owner.TryGetComponent(out CharacterStats stats)) return;
            float chance = critChancePerStack * stacks;
            if (chance <= 0f) return;
            stats.RemoveModifier(StatType.CritChance, new StatModifier(StatModType.Flat, chance));
        }

        public void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            if (ctx.AttackType == AttackType.Secondary) return;
            if (ctx.AttackType == AttackType.DamageOverTime) return; // burn/bleed shouldn't chain
            if (!ctx.IsCritical) return;
            if (ctx.Target == null) return;

            TeamAffiliation ownerTeam = TeamAffiliation.None;
            if (owner.TryGetComponent(out CharacterStats ownerStats)) ownerTeam = ownerStats.Team;

            var hitSet = new HashSet<GameObject> { owner, ctx.Target };
            Vector3 originPos = ctx.Target.transform.position;

            for (int i = 0; i < stacks; i++)
            {
                Collider[] hits = Physics.OverlapSphere(originPos, chainRadius);
                GameObject nearest = null;
                CharacterStats nearestStats = null;
                float nearestDist = float.MaxValue;

                foreach (var hit in hits)
                {
                    if (hitSet.Contains(hit.gameObject)) continue;
                    if (!hit.TryGetComponent(out CharacterStats targetStats)) continue;
                    if (targetStats.IsDead) continue;
                    if (ownerTeam != TeamAffiliation.None && targetStats.Team != TeamAffiliation.None && targetStats.Team == ownerTeam) continue;

                    float d = Vector3.Distance(originPos, hit.transform.position);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = hit.gameObject;
                        nearestStats = targetStats;
                    }
                }

                if (nearest == null) break;

                // Visualize the chain on every client. Aim slightly above the foot so the line
                // reads as a beam through the body rather than the ground.
                Vector3 tracerStart = originPos + Vector3.up * 1.2f;
                Vector3 tracerEnd = nearest.transform.position + Vector3.up * 1.2f;
                if (owner.TryGetComponent(out CharacterInventory inventory))
                {
                    inventory.PlayItemTracerClientRpc(tracerStart, tracerEnd, tracerColor, tracerWidth, tracerLifetime);
                }

                var chainCtx = new DamageContext
                {
                    Source = owner,
                    Target = nearest,
                    Damage = ctx.Damage,
                    DamageType = ctx.DamageType,
                    AttackType = AttackType.Secondary,
                    IsCritical = false
                };
                nearestStats.TakeDamage(chainCtx);

                hitSet.Add(nearest);
                originPos = nearest.transform.position;
            }
        }
    }
}
