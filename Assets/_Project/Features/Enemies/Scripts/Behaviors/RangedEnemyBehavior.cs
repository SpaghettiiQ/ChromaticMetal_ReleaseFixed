using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.Netcode;
using _Project.Features.Weapons.Scripts;
using _Project.Features.Enemies.Scripts.Core;
using _Project.Core.Interfaces;
using _Project.Core.Structs;
using _Project.Core.Enums;
using _Project.Core.Stats;
using _Project.Features.DifficultySystem.Scripts;
namespace _Project.Features.Enemies.Scripts.Behaviors
{
    public class RangedEnemyBehavior : EnemyControllerBase
    {
        [Header("Detection")]
        [SerializeField] private float detectionRange = 20f;
        [SerializeField] private float sightAngle = 90f;
        [SerializeField] private float loseTargetTime = 5f;
        [SerializeField] private LayerMask targetLayer;
        private bool targetDetected = false;
        private float lastTargetSightTime;
        [Header("Combat Settings")]
        public float optimalRange = 10f;
        public float aimInaccuracy = 15f;
        public Transform weaponHolder;
        public AngelRayWeapon equippedWeapon;
        [Header("Movement")]
        public float pathUpdateInterval = 0.2f;
        [Header("Tactical")]
        [SerializeField] private int positionSampleCount = 8;
        [SerializeField] private float maxRepositionAngle = 120f;
        private List<Vector3> previousPositions = new List<Vector3>();
        private const int MAX_PREVIOUS_POSITIONS = 3;
        
        [Header("Animation")]
        [SerializeField] private float shootDelay = 25f;
        private bool isPreparingShot = false;
        private float actualShotTime;

        private float nextAttackTime;
        [SerializeField] private float attackCooldown = 2f;
        private float nextPathUpdate;
        
        private Vector3 _lastPos;
        private float _clientSpeed;

        protected override void Start()
        {
            base.Start();
            Agent.stoppingDistance = optimalRange - 1f;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            // NOTE: In production, instantiate weapon through feature systems.
            // As this is a clean up, we remove direct tightly coupled weapon behavior and rely on an interface if possible.
        }
        private TeamPhase GetOwnPhase()
        {
            var phased = GetComponent<_Project.Core.Networking.PhasedNetworkObject>();
            return phased != null ? phased.Phase : TeamPhase.Both;
        }

        private void FindTarget()
        {
            if (target != null) return;
            // Target acquisition independent of specifically checking string tags
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, targetLayer);
            if (hits.Length == 0) return;

            TeamPhase myPhase = GetOwnPhase();
            for (int i = 0; i < hits.Length; i++)
            {
                var hitStats = hits[i].GetComponentInParent<CharacterStats>();

                // Only target entities with an explicit team — players have one, enemies don't.
                // Prevents enemies from picking each other as targets when they share a phase layer.
                if (hitStats == null || hitStats.Team == TeamAffiliation.None) continue;

                // Phase filter: only target entities sharing our phase (or the Shared "Both" phase).
                // Belt-and-suspenders for the same-map PvP case where both teams' players coexist.
                var hitPhased = hits[i].GetComponentInParent<_Project.Core.Networking.PhasedNetworkObject>();
                if (hitPhased != null && !hitPhased.Phase.VisibleTo(myPhase)) continue;

                target = hits[i].transform;
                return;
            }
        }
        private bool IsTargetInSight()
        {
            if (target == null) return false;
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > detectionRange) return false;
            Vector3 directionToTarget = (target.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            if (angle > sightAngle * 0.5f) return false;
            Vector3 targetCenter = target.position + Vector3.up * 0.5f;
            Vector3 eyePosition = transform.position + Vector3.up * 1.6f;
            directionToTarget = (targetCenter - eyePosition).normalized;
            if (Physics.Raycast(eyePosition, directionToTarget, out RaycastHit hit, detectionRange))
            {
                // Simple assumption: if you hit the target's collider
                if (((1 << hit.collider.gameObject.layer) & targetLayer) != 0)
                {
                    lastTargetSightTime = Time.time;
                    return true;
                }
            }
            return false;
        }
        private bool HasLineOfSight()
        {
            if (target == null) return false;
            Transform origin = weaponHolder != null ? weaponHolder : transform;
            Vector3 targetCenter = target.position + Vector3.up * 1.5f;
            Vector3 startPos = origin.position + (origin == transform ? Vector3.up * 1.5f : Vector3.zero);
            Vector3 directionToTarget = (targetCenter - startPos).normalized;
            if (Physics.Raycast(startPos, directionToTarget, out RaycastHit hit, optimalRange))
            {
               if (((1 << hit.collider.gameObject.layer) & targetLayer) != 0) return true;
            }
            return false;
        }
        private Vector3 CalculateShootDirection()
        {
            if (target == null) return transform.forward;
            Vector3 targetCenter = target.position + Vector3.up * 1.5f;
            Transform origin = weaponHolder != null ? weaponHolder : transform;
            Vector3 shootFrom = origin.position + (origin == transform ? Vector3.up * 1.5f : Vector3.zero);
            Vector3 baseDir = (targetCenter - shootFrom).normalized;
            if (baseDir.sqrMagnitude < 0.001f) return transform.forward;
            float yaw = Random.Range(-aimInaccuracy, aimInaccuracy);
            float pitch = Random.Range(-aimInaccuracy, aimInaccuracy);
            return Quaternion.Euler(pitch, yaw, 0f) * baseDir;
        }

        protected override void Update()
        {
            base.Update();
            
            if (isDead) return;

            // Simple client speed approximation based on NetworkTransform movement
            if (!IsServer)
            {
                float d = Vector3.Distance(transform.position, _lastPos);
                _clientSpeed = Mathf.Lerp(_clientSpeed, (d / Time.deltaTime) * 0.2f, Time.deltaTime * 10f);
                _lastPos = transform.position;
            }

            if (animator != null)
            {
                float speed = IsServer ? Agent.velocity.magnitude * 0.2f : _clientSpeed;
                animator.SetFloat("Speed", speed);
                animator.SetBool("IsWalking", speed > 0.05f);
            }

            // Only Server runs AI
            if (!IsServer) return;

            if (IsStunned)
            {
                if (Agent != null && Agent.enabled && !Agent.isStopped)
                {
                    Agent.isStopped = true;
                    Agent.ResetPath();
                    Agent.velocity = Vector3.zero;
                }
                isPreparingShot = false;
                if (animator != null) animator.SetBool("IsShooting", false);
                return;
            }

            if (isPreparingShot && Time.time >= actualShotTime)
            {
                isPreparingShot = false;
                ExecuteShot();
            }

            FindTarget();
            bool canSeeTarget = IsTargetInSight();
            if (canSeeTarget) targetDetected = true;
            else if (targetDetected && Time.time - lastTargetSightTime > loseTargetTime) targetDetected = false;
            if (!targetDetected || target == null) return;
            float distance = Vector3.Distance(transform.position, target.position);
            bool canShoot = HasLineOfSight();
            bool shouldUpdatePath = Time.time >= nextPathUpdate;
            bool needsRepositioning = distance <= optimalRange && !canShoot;
            
            if (distance <= optimalRange && canShoot)
            {
                if (!Agent.isStopped)
                {
                    Agent.isStopped = true;
                    Agent.ResetPath();
                    Agent.velocity = Vector3.zero;
                }
                bool weaponReady = equippedWeapon == null || equippedWeapon.CanFire;
                if (Time.time >= nextAttackTime && !isPreparingShot && weaponReady) BeginAttack();
            }
            else if (shouldUpdatePath || needsRepositioning)
            {
                nextPathUpdate = Time.time + pathUpdateInterval;
                if (distance > optimalRange || needsRepositioning)
                {
                    Agent.isStopped = false;
                    Agent.SetDestination(target.position); // Basic pathing for now
                }
            }
            Vector3 targetDir = (target.position - transform.position).normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(targetDir.x, 0, targetDir.z)), Time.deltaTime * 8f);
        }
        private void BeginAttack()
        {
            isPreparingShot = true;
            actualShotTime = Time.time + shootDelay;
            
            if (animator != null)
            {
                animator.SetBool("IsShooting", true);
            }
            SetShootingAnimationClientRpc(true);
        }

        [ClientRpc]
        private void SetShootingAnimationClientRpc(bool isShooting)
        {
            if (IsServer) return;
            if (animator != null)
            {
                animator.SetBool("IsShooting", isShooting);
            }
        }

        private void ExecuteShot()
        {
            nextAttackTime = Time.time + attackCooldown;
            if (animator != null)
            {
                animator.SetBool("IsShooting", false);
            }
            SetShootingAnimationClientRpc(false);
            
            if (equippedWeapon != null)
            {
                int scaledDamage = -1;
                var diff = DifficultyNetworkController.Singleton;
                if (diff != null && enemyData != null && diff.DamageBaseValue > 0f)
                {
                    scaledDamage = Mathf.Max(1, Mathf.RoundToInt(
                        enemyData.baseDamage * (diff.FullDamage / diff.DamageBaseValue)));
                }
                equippedWeapon.Fire(CalculateShootDirection(), scaledDamage);
            }

            PlayShootSfxClientRpc();
        }

        [ClientRpc]
        private void PlayShootSfxClientRpc()
        {
            var sfx = _Project.Core.Audio.SfxManager.Instance;
            if (sfx == null || sfx.Library == null || sfx.Library.enemyShoot == null) return;
            sfx.PlayOneShot3D(sfx.Library.enemyShoot, transform.position, 0.85f);
        }
    }
}
