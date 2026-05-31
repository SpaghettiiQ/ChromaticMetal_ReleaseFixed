using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Events;
using _Project.Core.Stats;
using _Project.Core.Structs;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    // Per-owner runtime for DeadMansSwitchItem (cheat-death + AoE pulse).
    public class DeadMansSwitchRuntime : ItemEffectBehaviour<DeadMansSwitchItem>
    {
        private CharacterEventBus _bus;
        private CharacterStats _stats;
        private float _nextAllowedTime;

        protected override void OnActivate()
        {
            TryGetComponent(out _bus);
            TryGetComponent(out _stats);
            if (_bus != null) _bus.OnBeforeTakeDamage += HandleBeforeTakeDamage;
        }

        protected override void OnDeactivate()
        {
            if (_bus != null) _bus.OnBeforeTakeDamage -= HandleBeforeTakeDamage;
        }

        private void HandleBeforeTakeDamage(ref DamageContext ctx)
        {
            if (_stats == null) return;
            if (Time.time < _nextAllowedTime) return;

            int currentHp = _stats.CurrentHealth.Value;
            if (ctx.Damage < currentHp) return; // not lethal-before-mitigation

            // Survive at 1 HP. Set to True damage so armor/vuln can't push HP below 1 downstream.
            ctx.Damage = Mathf.Max(0, currentHp - 1);
            ctx.DamageType = DamageType.True;

            float maxHp = _stats.GetStat(StatType.MaxHealth);
            int pulseDmg = Mathf.Max(1, Mathf.RoundToInt(maxHp * Config.pulseDamageFrac));
            TeamAffiliation ownerTeam = _stats.Team;

            Collider[] hits = Physics.OverlapSphere(transform.position, Config.pulseRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                if (!hit.TryGetComponent(out CharacterStats targetStats)) continue;
                if (targetStats.IsDead) continue;
                if (ownerTeam != TeamAffiliation.None && targetStats.Team != TeamAffiliation.None && targetStats.Team == ownerTeam) continue;

                targetStats.TakeDamage(new DamageContext
                {
                    Source = gameObject,
                    Target = hit.gameObject,
                    Damage = pulseDmg,
                    DamageType = DamageType.Explosive,
                    AttackType = AttackType.Explosion,
                    IsCritical = false
                });
            }

            _nextAllowedTime = Time.time + Mathf.Max(Config.minCooldown, Config.baseCooldown - Config.cooldownReductionPerStack * (Stacks - 1));
        }
    }
}
