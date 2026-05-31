using System.Collections;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;
using _Project.Features.Player.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Chaplain
{
    [CreateAssetMenu(fileName = "TargetDesignator", menuName = "Proxy Abilities/Chaplain/Target Designator")]
    public class TargetDesignatorAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Designator Settings")]
        [SerializeField] private float range = 50f;

        [Header("Tracer Visual")]
        [SerializeField] private Color tracerColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private float tracerWidth = 0.03f;
        [SerializeField] private float tracerLifetime = 0.08f;
        
        [Header("Enemy Target Settings")]
        [SerializeField] private float markDuration = 5f;
        [SerializeField] private float damageVulnerability = 0.25f; // Extra 25% damage taken
        [Tooltip("Damage dealt instantly on cast, in addition to the lingering vulnerability mark. " +
                 "Gives the ability immediate bite instead of a silent passive bonus.")]
        [SerializeField] private int instantDamageBase = 35;
        
        [Header("Ally Target Settings")]
        [SerializeField] private int instantHealBase = 20;
        [SerializeField] private int hotTotalHealBase = 30; // Heal Over Time
        [SerializeField] private float hotDuration = 3f;

        public void Execute(GameObject instigator)
        {
            // AbilityController invokes Execute only on the server. Bail otherwise so the routine
            // doesn't double-run on the host (which is both server and owner).
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            Camera playerCam = instigator.GetComponentInChildren<Camera>();
            if (playerCam == null) playerCam = Camera.main;

            Vector3 rayOrigin = playerCam != null
                ? playerCam.transform.position
                : instigator.transform.position + Vector3.up * 1.5f;
            Vector3 rayDir = playerCam != null
                ? playerCam.transform.forward
                : instigator.transform.forward;

            bool didHit = Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, range);

            // Tracer goes from the player's weapon muzzle (if available) to the hit point — or
            // to the ray's far end if nothing was hit. Same visual on every client.
            Vector3 tracerStart = rayOrigin;
            var muzzleSource = instigator.GetComponentInChildren<_Project.Features.WeaponCore.Scripts.NetworkWeapon>();
            if (muzzleSource != null && muzzleSource.muzzlePoint != null)
            {
                tracerStart = muzzleSource.muzzlePoint.position;
            }
            Vector3 tracerEnd = didHit ? hit.point : rayOrigin + rayDir * range;

            var controller = instigator.GetComponent<AbilityController>();
            if (controller != null)
            {
                controller.PlayTracerClientRpc(tracerStart, tracerEnd, tracerColor, tracerWidth, tracerLifetime);
            }

            if (!didHit)
            {
                Debug.Log($"[{nameof(TargetDesignatorAbility)}] {instigator.name} cast — ray missed (range {range}m).");
                return;
            }

            CharacterStats targetStats = hit.collider.GetComponentInParent<CharacterStats>();
            if (targetStats == null || targetStats.IsDead)
            {
                Debug.Log($"[{nameof(TargetDesignatorAbility)}] {instigator.name} cast — hit '{hit.collider.name}' but no live CharacterStats.");
                return;
            }

            ApplyDesignatorEffect(instigator, targetStats);
        }

        private void ApplyDesignatorEffect(GameObject instigator, CharacterStats targetStats)
        {
            TeamAffiliation ownerTeam = TeamAffiliation.None;
            if (instigator.TryGetComponent(out CharacterStats ownerStats))
            {
                ownerTeam = ownerStats.Team;
            }

            if (ownerTeam != TeamAffiliation.None && targetStats.Team == ownerTeam)
            {
                // Ally hit
                int instantHeal = instantHealBase;
                int hotTotalHeal = hotTotalHealBase;

                // Field Medic Passive check
                if (instigator.TryGetComponent(out FieldMedicPassiveTracker medicPassive))
                {
                    instantHeal = medicPassive.CalculateModifiedHeal(targetStats, instantHeal);
                    hotTotalHeal = medicPassive.CalculateModifiedHeal(targetStats, hotTotalHeal);
                }

                targetStats.Heal(instantHeal);
                
                if (targetStats.TryGetComponent(out MonoBehaviour runner))
                {
                    runner.StartCoroutine(HealOverTime(targetStats, instigator, hotTotalHeal, hotDuration));
                }
            }
            else
            {
                // Enemy hit — immediate damage on cast so the ability has visible bite, then a
                // lingering vulnerability mark that amps follow-up damage.
                if (instantDamageBase > 0)
                {
                    DamageContext ctx = new DamageContext
                    {
                        Source = instigator,
                        Target = targetStats.gameObject,
                        Damage = instantDamageBase,
                        DamageType = DamageType.Purifying,
                        AttackType = AttackType.Effect,
                        IsCritical = false
                    };
                    targetStats.TakeDamage(ctx);
                }

                // Re-cast on an already-marked enemy refreshes the duration instead of no-opping.
                var existing = targetStats.gameObject.GetComponent<TargetDesignatorMark>();
                if (existing != null) UnityEngine.Object.Destroy(existing);

                var mark = targetStats.gameObject.AddComponent<TargetDesignatorMark>();
                mark.Initialize(damageVulnerability, markDuration);

                Debug.Log($"[{nameof(TargetDesignatorAbility)}] {instigator.name} marked '{targetStats.name}' " +
                          $"(+{damageVulnerability * 100f:0}% vulnerability for {markDuration}s, {instantDamageBase} instant dmg).");
            }
        }

        private IEnumerator HealOverTime(CharacterStats target, GameObject source, int totalHeal, float duration)
        {
            float ticks = 5f;
            float timePerTick = duration / ticks;
            int healPerTick = Mathf.RoundToInt((float)totalHeal / ticks);

            for (int i = 0; i < ticks; i++)
            {
                yield return new WaitForSeconds(timePerTick);
                if (target != null && !target.IsDead)
                {
                    int healAmount = healPerTick;
                    // Apply Field Medic passive per tick if source still exists
                    if (source != null && source.TryGetComponent(out FieldMedicPassiveTracker medicPassive))
                    {
                        healAmount = medicPassive.CalculateModifiedHeal(target, healAmount);
                    }
                    target.Heal(healAmount);
                }
            }
        }
    }

    public class TargetDesignatorMark : MonoBehaviour
    {
        private CharacterStats _stats;
        private StatModifier _vulnerabilityMod;
        private bool _modifierApplied;

        public void Initialize(float vulnerabilityAmount, float duration)
        {
            _stats = GetComponent<CharacterStats>();
            if (_stats != null)
            {
                _vulnerabilityMod = new StatModifier { Type = StatModType.Flat, Value = vulnerabilityAmount };
                _stats.AddModifier(StatType.DamageVulnerability, _vulnerabilityMod);
                _modifierApplied = true;
            }

            Destroy(this, duration);
        }

        private void OnDestroy()
        {
            if (_stats != null && _modifierApplied)
            {
                _stats.RemoveModifier(StatType.DamageVulnerability, _vulnerabilityMod);
            }
        }
    }
}