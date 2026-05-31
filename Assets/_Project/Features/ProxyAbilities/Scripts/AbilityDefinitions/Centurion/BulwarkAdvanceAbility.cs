using System.Collections;
using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;
using _Project.Features.Player.Scripts;
using Unity.Netcode;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Centurion
{
    [CreateAssetMenu(fileName = "BulwarkAdvance", menuName = "Proxy Abilities/Centurion/Bulwark Advance")]
    public class BulwarkAdvanceAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Advance Settings")]
        [SerializeField] private float duration = 2f;
        [SerializeField] private float damageReductionBonus = 50f; // +50% Damage Reduction
        [SerializeField] private float advanceSpeed = 2f; // Desired movement speed in meters/second
        [SerializeField] private float knockbackForce = 15f;
        [SerializeField] private float knockbackRadius = 3f;
        [SerializeField] private int contactDamage = 40;

        public bool CanBeUsed(GameObject instigator)
        {
            if (instigator.TryGetComponent(out PlayerMovement movement))
            {
                return movement.IsGrounded;
            }
            return true;
        }

        public void Execute(GameObject instigator)
        {
            if (instigator.TryGetComponent(out MonoBehaviour runner))
            {
                runner.StartCoroutine(AdvanceRoutine(instigator));
            }
        }

        private IEnumerator AdvanceRoutine(GameObject instigator)
        {
            bool isServer = NetworkManager.Singleton.IsServer;

            CharacterStats stats = instigator.GetComponent<CharacterStats>();
            PlayerMovement movement = instigator.GetComponent<PlayerMovement>();

            // Ground check
            if (movement != null && !movement.IsGrounded)
            {
                yield break;
            }

            if (movement != null)
            {
                movement.MovementLocked = true;
                // Optional: clear current momentum to fully plant them
                movement.ApplyForce(Vector3.zero, true);
            }

            StatModifier drMod = new StatModifier { Type = StatModType.Flat, Value = damageReductionBonus };

            if (stats != null && isServer)
            {
                stats.AddModifier(StatType.DamageReduction, drMod);
            }

            Transform aimTransform = instigator.transform;
            Camera cam = instigator.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                aimTransform = cam.transform;
            }

            float timer = 0f;
            
            // To ensure we only perform the heavy knockback once per target during the advance
            System.Collections.Generic.HashSet<GameObject> knockedObjects = new System.Collections.Generic.HashSet<GameObject>();

            if (movement == null && instigator.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent))
            {
                agent.ResetPath();
            }

            while (timer < duration)
            {
                // Constantly update direction based on where the camera/transform is looking to allow steering
                Vector3 aimDir = aimTransform.forward;
                aimDir.y = 0;
                if (aimDir.sqrMagnitude < 0.01f) aimDir = instigator.transform.forward;
                Vector3 advanceVelocity = aimDir.normalized * advanceSpeed;

                if (movement != null)
                {
                    // Only apply sustained force locally on the owner each frame to steer without spamming RPCs
                    if (movement.IsOwner)
                    {
                        movement.ApplySustainedForce(advanceVelocity * movement.friction, 0.1f);
                    }
                }
                else if (isServer)
                {
                    if (instigator.TryGetComponent(out UnityEngine.AI.NavMeshAgent agentLoop) && agentLoop.isActiveAndEnabled)
                    {
                        agentLoop.Move(advanceVelocity * Time.deltaTime);
                    }
                    else
                    {
                        instigator.transform.position += advanceVelocity * Time.deltaTime;
                    }
                }

                if (isServer)
                {
                    // Knock aside targets using an impulse rather than a static push
                    Collider[] hitColliders = Physics.OverlapSphere(instigator.transform.position, knockbackRadius);
                    foreach (var hitCollider in hitColliders)
                    {
                        if (hitCollider.gameObject == instigator) continue;

                        CharacterStats targetStats = hitCollider.GetComponentInParent<CharacterStats>();
                        if (targetStats == null || targetStats.IsDead) continue;
                        if (targetStats.gameObject == instigator) continue;

                        // Dedupe by character root so multiple child colliders on one enemy don't double-knock.
                        if (!knockedObjects.Add(targetStats.gameObject)) continue;

                        if (stats != null && stats.Team == targetStats.Team && stats.Team != TeamAffiliation.None)
                            continue;

                        // Deal contact damage once per target during the advance. Credited to the
                        // Centurion so on-hit/on-kill hooks (items, kill events) attribute correctly.
                        if (contactDamage > 0)
                        {
                            DamageContext dmgCtx = new DamageContext
                            {
                                Source = instigator,
                                Target = targetStats.gameObject,
                                Damage = contactDamage,
                                DamageType = DamageType.Physical,
                                AttackType = AttackType.Contact,
                                IsCritical = false
                            };
                            targetStats.TakeDamage(dmgCtx);
                            if (targetStats.IsDead) continue; // Don't bother knocking a corpse.
                        }

                        // Calculate a strong upward knockback direction
                        Vector3 pushDir = (targetStats.transform.position - instigator.transform.position).normalized;
                        pushDir.y = 0.5f; // Upward knock
                        pushDir.Normalize();

                        PlayerMovement targetMovement = targetStats.GetComponent<PlayerMovement>();
                        UnityEngine.AI.NavMeshAgent targetAgent = targetStats.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        Rigidbody targetRb = targetStats.GetComponent<Rigidbody>();

                        if (targetMovement != null)
                        {
                            // Apply immediate impulse for players
                            targetMovement.ApplyForce(pushDir * knockbackForce, false);
                        }
                        else if (targetAgent != null && targetAgent.isActiveAndEnabled)
                        {
                            // Enemies get forcefully pushed over a timeframe to mimic an impulse on the
                            // NavMesh. Collision-aware so the heavy advance can't punt them through a wall.
                            instigator.GetComponent<MonoBehaviour>().StartCoroutine(
                                _Project.Core.Managers.EnemyKnockback.Run(
                                    targetAgent, pushDir, knockbackForce,
                                    duration: 1f, gravityMultiplier: 1.5f, horizontalDrag: 1.5f));
                        }
                        else if (targetRb != null && !targetRb.isKinematic)
                        {
                            targetRb.AddForce(pushDir * knockbackForce, ForceMode.Impulse);
                        }
                    }
                }

                yield return null;
                timer += Time.deltaTime;
            }

            if (movement != null)
            {
                movement.MovementLocked = false;
            }

            if (stats != null && isServer)
            {
                stats.RemoveModifier(StatType.DamageReduction, drMod);
            }

            Debug.Log($"[{nameof(BulwarkAdvanceAbility)}] {instigator.name} finished Bulwark Advance!");
        }
    }
}
