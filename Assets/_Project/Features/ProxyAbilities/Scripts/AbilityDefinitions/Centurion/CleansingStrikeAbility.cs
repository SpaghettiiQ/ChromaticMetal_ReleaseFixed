using System.Collections;
using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;
using _Project.Features.BurdenSystem.Scripts;
using Unity.Netcode;
using _Project.Features.Player.Scripts;
using _Project.Features.Weapons.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Centurion
{
    [CreateAssetMenu(fileName = "CleansingStrike", menuName = "Proxy Abilities/Centurion/Cleansing Strike")]
    public class CleansingStrikeAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Strike Settings")]
        [SerializeField] private float damageRadius = 4f;
        [SerializeField] private int baseDamage = 30;
        [SerializeField] private int extraDamagePerBurdenStack = 15;
        [SerializeField] private float pushbackForce = 20f;
        [Tooltip("Hitstop applied to the casting player when the slam connects, in seconds. " +
                 "Set to 0 to disable.")]
        [SerializeField] private float hitStopDuration = 0.12f;
        
        [Header("Audiovisual")]
        [SerializeField] private GameObject strikeVfxPrefab;

        public void Execute(GameObject instigator)
        {
            if (instigator.TryGetComponent(out MonoBehaviour runner))
            {
                // Give a slight delay for an "overhead slam" animation to play if needed
                runner.StartCoroutine(StrikeRoutine(instigator));
            }
        }

        private IEnumerator StrikeRoutine(GameObject instigator)
        {
            // Kick the maul into its overhead-slam viewmodel sequence on every client. The
            // windup phase of that sequence is roughly 0.2s, which matches the existing strike
            // delay below, so impact lands at the bottom of the slam.
            // (ProxyAbilities -> Weapons is a pre-existing CLAUDE.md violation; not adding a new
            // class of dependency, just extending the existing pile until the tech-debt pass.)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                AoEMeleeWeapon maul = instigator.GetComponentInChildren<AoEMeleeWeapon>();
                if (maul != null) maul.PlayOverheadSwingClientRpc();
            }

            // Wait 0.2s for the windup before the impact resolves.
            yield return new WaitForSeconds(0.2f);

            // Spawn VFX
            if (strikeVfxPrefab != null)
            {
                // Spawn slightly in front
                Vector3 spawnPos = instigator.transform.position + instigator.transform.forward * 1.5f;
                Instantiate(strikeVfxPrefab, spawnPos, Quaternion.identity);
            }

            if (!NetworkManager.Singleton.IsServer) yield break;

            Vector3 impactCenter = instigator.transform.position + instigator.transform.forward * 1.5f;
            Collider[] hitColliders = Physics.OverlapSphere(impactCenter, damageRadius);

            TeamAffiliation ownerTeam = TeamAffiliation.None;
            if (instigator.TryGetComponent(out CharacterStats ownerStats))
            {
                ownerTeam = ownerStats.Team;
            }

            System.Collections.Generic.HashSet<CharacterStats> processed = new System.Collections.Generic.HashSet<CharacterStats>();
            bool didConnect = false;

            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.gameObject == instigator) continue;

                CharacterStats targetStats = hitCollider.GetComponentInParent<CharacterStats>();
                if (targetStats == null || targetStats.IsDead) continue;
                if (targetStats.gameObject == instigator) continue;
                if (!processed.Add(targetStats)) continue;
                {
                    // Basic team check
                    if (ownerTeam != TeamAffiliation.None && targetStats.Team == ownerTeam) continue;

                    int totalDamage = baseDamage;

                    // Check if target is burdened for bonus damage
                    BurdenController targetBurden = targetStats.GetComponent<BurdenController>();
                    if (targetBurden != null)
                    {
                        float burden = targetBurden.GetCurrentBurden();
                        // Assume every 25 burden is roughly 1 "stack" conceptually for this bonus
                        int stacks = Mathf.FloorToInt(burden / 25f);
                        if (stacks > 0)
                        {
                            totalDamage += (extraDamagePerBurdenStack * stacks);
                            // Visual or debug log
                            Debug.Log($"Cleansing Strike hit burdened target. Added {extraDamagePerBurdenStack * stacks} bonus damage!");
                        }
                    }

                    // Apply damage
                    DamageContext ctx = new DamageContext
                    {
                        Source = instigator,
                        Target = hitCollider.gameObject,
                        Damage = totalDamage,
                        DamageType = DamageType.Purifying,
                        AttackType = AttackType.Melee,
                        IsCritical = false
                    };
                    targetStats.TakeDamage(ctx);
                    didConnect = true;

                    // Pushback
                    Vector3 pushDir = (hitCollider.transform.position - impactCenter).normalized;
                    pushDir.y = 0.5f; // Add a bit of lift

                    if (hitCollider.TryGetComponent(out PlayerMovement targetMovement))
                    {
                        targetMovement.ApplyForce(pushDir * pushbackForce, false);
                    }
                    else if (hitCollider.TryGetComponent(out Rigidbody rb) && !rb.isKinematic)
                    {
                        rb.AddForce(pushDir * pushbackForce, ForceMode.Impulse);
                    }
                }
            }

            // Trigger hitstop on the caster once if at least one enemy was hit. Routed via the
            // AbilityController's existing owner-gated ClientRpc so only the casting player
            // freezes — the same pattern Stabilized Burst uses.
            if (didConnect && hitStopDuration > 0f)
            {
                if (instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityController abilityController))
                {
                    abilityController.ApplyHitStopClientRpc(hitStopDuration);
                }
                else
                {
                    _Project.Core.Managers.HitStopManager.Instance.TriggerHitStop(hitStopDuration);
                }
            }
        }
    }
}
