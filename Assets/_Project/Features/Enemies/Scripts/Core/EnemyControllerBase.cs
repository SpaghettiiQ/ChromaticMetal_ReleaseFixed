using _Project.Core.Interfaces;
using _Project.Core.Structs;
using _Project.Core.Stats;
using _Project.Features.DifficultySystem.Scripts;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
namespace _Project.Features.Enemies.Scripts.Core
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class EnemyControllerBase : NetworkBehaviour, IDamageContextReceiver, IStunnable, IEnemyIdentity
    {
        [Header("Enemy Data")]
        [SerializeField] protected EnemyData enemyData;
        public EnemyData GetEnemyData() => enemyData;

        // IEnemyIdentity — lets Core/other features match on enemy archetype without
        // referencing the Enemies feature (e.g. CleanserChipItem's bonus vs "Lesser Seer").
        public string EnemyTypeName => enemyData != null ? enemyData.enemyName : null;
        [Header("Components")]
        [SerializeField] protected Transform vfxSpawnPoint;
        [SerializeField] protected GameObject deathVFXPrefab;
        [SerializeField] protected float deathDespawnDelay = 6f;

        protected Animator animator;
        public NavMeshAgent Agent { get; private set; }
        protected bool isDead;
        protected float currentHealth;
        protected Transform target;

        private CharacterStats _characterStats;

        private NetworkVariable<Vector3> _netPosition = new NetworkVariable<Vector3>(writePerm: NetworkVariableWritePermission.Server);
        private NetworkVariable<Quaternion> _netRotation = new NetworkVariable<Quaternion>(writePerm: NetworkVariableWritePermission.Server);

        private float _stunEndTime;
        protected bool IsStunned => Time.time < _stunEndTime;

        public void Stun(float duration)
        {
            if (!IsServer) return;
            float candidate = Time.time + duration;
            if (candidate > _stunEndTime) _stunEndTime = candidate;
            if (Agent != null && Agent.enabled) Agent.isStopped = true;
        }

        protected virtual void Start()
        {
            Agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            _characterStats = GetComponent<CharacterStats>();

            float baseHealth = enemyData != null ? enemyData.maxHealth : 100f;
            if (DifficultyNetworkController.Singleton != null)
            {
                // DifficultyNetworkController's FullHealth scaling starts around 100 base value.
                float healthScale = DifficultyNetworkController.Singleton.FullHealth / 100f;
                baseHealth *= healthScale;
            }

            currentHealth = baseHealth;

            // If this enemy has CharacterStats, make it the health authority.
            if (_characterStats != null)
            {
                _characterStats.OnDeath += HandleCharacterStatsDeath;

                if (_characterStats.IsServer)
                {
                    _characterStats.Initialize(Mathf.RoundToInt(baseHealth));
                }
            }

            if (!IsServer && Agent != null)
            {
                Agent.enabled = false; // Disable NavMeshAgent on clients to prevent it from snapping or fighting sync
            }

            // 3D spawn cue: every client plays it from the enemy's position so nearby
            // players hear it with proper falloff and direction.
            var sfx = _Project.Core.Audio.SfxManager.Instance;
            if (sfx != null && sfx.Library != null && sfx.Library.enemySpawn != null)
            {
                sfx.PlayOneShot3D(sfx.Library.enemySpawn, transform.position, 0.9f);
            }
        }

        protected virtual void Update()
        {
            if (IsServer)
            {
                _netPosition.Value = transform.position;
                _netRotation.Value = transform.rotation;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, _netPosition.Value, Time.deltaTime * 10f);
                transform.rotation = Quaternion.Slerp(transform.rotation, _netRotation.Value, Time.deltaTime * 10f);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_characterStats != null)
            {
                _characterStats.OnDeath -= HandleCharacterStatsDeath;
            }
        }

        public virtual void SetDamageContext(DamageContext ctx)
        {
            if (isDead) return;

            if (_characterStats != null)
            {
                ctx.Target = _characterStats.gameObject;
                _characterStats.TakeDamage(ctx);
                return;
            }

            // Fallback for non-networked or legacy enemies without CharacterStats.
            currentHealth -= ctx.Damage;
            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void HandleCharacterStatsDeath()
        {
            if (isDead) return;
            Die();
        }

        protected virtual void Die()
        {
            isDead = true;

            // Distribute money reward to the last damage dealer on the server wrapper check.
            if (_characterStats != null && _characterStats.IsServer && _characterStats.LastDamageDealerId != 0 && enemyData != null)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_characterStats.LastDamageDealerId, out NetworkObject killerObj))
                {
                    if (killerObj.TryGetComponent<CharacterStats>(out var killerStats))
                    {
                        int moneyReward = enemyData.moneyReward;
                        if (TryGetComponent<EnemyDataBridge>(out var bridge))
                        {
                            moneyReward = bridge.GetMoneyReward();
                        }
                        if (DifficultyNetworkController.Singleton != null)
                        {
                            moneyReward = Mathf.RoundToInt(
                                moneyReward * DifficultyNetworkController.Singleton.MoneyRewardMultiplier);
                        }
                        killerStats.AddMoney(moneyReward);
                    }
                }
            }

            if (Agent != null)
            {
                Agent.isStopped = true;
                Agent.ResetPath();
            }

            // Disable colliders so corpses don't block raycasts, projectiles, melee overlap
            // spheres, or stun-volume scans during their fade-out window.
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                if (col != null) col.enabled = false;
            }

            if (animator != null)
            {
                animator.SetBool("IsDead", true);
            }
            if (deathVFXPrefab != null && vfxSpawnPoint != null)
            {
                Instantiate(deathVFXPrefab, vfxSpawnPoint.position, Quaternion.identity);
            }

            Destroy(gameObject, Mathf.Max(0f, deathDespawnDelay));
        }
    }
}
