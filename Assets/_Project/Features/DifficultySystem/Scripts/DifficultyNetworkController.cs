using System;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.DifficultySystem.Scripts
{
    public class DifficultyNetworkController : NetworkBehaviour, IDifficultyManager
    {
        public static DifficultyNetworkController Singleton { get; private set; }

        private const int MAX_LEVEL = 99;

        [Header("Scaling Base Values")]
        [SerializeField] private int baseXP = 100;
        [SerializeField] private float xpExponent = 1.5f;

        [SerializeField] private float damageBaseValue = 10f;
        [SerializeField] private float healthBaseValue = 100f;
        [SerializeField] private float damageGrowthRate = 1.15f;
        [SerializeField] private float healthGrowthRate = 1.15f;

        [SerializeField] private float costBaseValue = 24f;
        [SerializeField] private float costGrowthRate = 1.25f;

        [Header("XP Over Time")]
        [SerializeField] private bool enableXPOverTime = true;
        [SerializeField] private float xpTickInterval1to15 = 2f;
        [SerializeField] private float xpTickInterval16to30 = 1f;
        [SerializeField] private float xpExponentialBase = 1.2f;
        [SerializeField] private int xpPerTick = 1;

        [Header("Final Stage Boost")]
        [SerializeField] private float finalStageBoostMultiplier = 1.5f;

        [Header("Match Time Settings")]
        [SerializeField] private float baseMatchTimeCoefficient = 0.05f; // Difficulty rises slightly with time

        [Header("Spawn / Reward Scaling")]
        [SerializeField] private float baselineBudget = 1.0f;
        [SerializeField] private float budgetPerLevelGrowth = 0.1f;
        [SerializeField] private float moneyRewardMaxMultiplier = 1.5f;
        [SerializeField] private float moneyRewardRampSeconds = 1200f;

        [Header("Container Cost Scaling")]
        [Tooltip("Exponent applied to FullCost/costBaseValue. 1=linear (steep), 0.5=square root (moderate), 0=disabled.")]
        [SerializeField] private float containerCostScaleStrength = 0.5f;
        [Tooltip("Hard cap on the container cost multiplier to prevent late-game runaway pricing.")]
        [SerializeField] private float maxContainerCostMultiplier = 5f;

        // Network Variables for Global Sync
        public NetworkVariable<int> NetworkCurrentLevel = new NetworkVariable<int>(1);
        public NetworkVariable<int> NetworkCurrentXP = new NetworkVariable<int>(0);
        public NetworkVariable<float> NetworkMatchTime = new NetworkVariable<float>(0f);
        public NetworkVariable<float> NetworkStageTime = new NetworkVariable<float>(0f);

        // Cached values for quick local access
        public int CurrentLevel => NetworkCurrentLevel.Value;
        public int CurrentXP => NetworkCurrentXP.Value;
        public float MatchTime => NetworkMatchTime.Value;
        public float StageTime => NetworkStageTime.Value;

        // Calculated Full Stats
        public int FullDamage { get; private set; }
        public int FullHealth { get; private set; }
        public int FullCost { get; private set; }

        public float DamageBaseValue => damageBaseValue;
        public float DifficultyCoefficient { get; private set; }
        public float SpawnBudgetPerSecond { get; private set; }
        public float MoneyRewardMultiplier { get; private set; }
        public float ContainerCostMultiplier { get; private set; }

        // Local Events
        public event Action<int> OnLevelUp;
        public event Action<int> OnXPChanged;
        public event Action<float> OnMatchTimeChanged;

        // Server-Side Only Tracking
        private float _nextXPTickTime = 0f;
        private bool _isFinalStageInLoop = false;
        private bool _isMatchTimerRunning = false;

        private void Awake()
        {
            if (Singleton != null && Singleton != this)
            {
                Destroy(gameObject);
                return;
            }
            Singleton = this;
            DontDestroyOnLoad(gameObject);
            UpdateBaseValuesLocally();
        }

        public override void OnNetworkSpawn()
        {
            // Subscribe to NetworkVariable changes to invoke local events
            NetworkCurrentXP.OnValueChanged += (oldVal, newVal) =>
            {
                OnXPChanged?.Invoke(newVal);
            };

            NetworkCurrentLevel.OnValueChanged += (oldVal, newVal) =>
            {
                UpdateBaseValuesLocally();
                OnLevelUp?.Invoke(newVal);
            };

            NetworkMatchTime.OnValueChanged += (oldVal, newVal) =>
            {
                OnMatchTimeChanged?.Invoke(newVal);
                UpdateBaseValuesLocally(); // Recalculate based on time
            };

            if (IsServer)
            {
                _nextXPTickTime = Time.time;
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            if (_isMatchTimerRunning)
            {
                NetworkMatchTime.Value += Time.deltaTime;
                NetworkStageTime.Value += Time.deltaTime;
            }

            if (enableXPOverTime && Time.time >= _nextXPTickTime)
            {
                AddXP(xpPerTick);
                _nextXPTickTime = Time.time + GetXPTickInterval();
            }
        }

        private float GetXPTickInterval()
        {
            if (CurrentLevel <= 15) return xpTickInterval1to15;
            if (CurrentLevel <= 30) return xpTickInterval16to30;
            
            float levelAbove30 = CurrentLevel - 30;
            return xpTickInterval16to30 / Mathf.Pow(xpExponentialBase, levelAbove30);
        }

        public void AddXP(int amount)
        {
            if (!IsServer) return;
            if (amount <= 0 || CurrentLevel >= MAX_LEVEL) return;

            NetworkCurrentXP.Value += amount;
            CheckLevelUp();
        }

        public void RemoveXP(int amount)
        {
            if (!IsServer) return;
            if (amount <= 0) return;

            NetworkCurrentXP.Value = Mathf.Max(0, NetworkCurrentXP.Value - amount);
            RecalculateLevel();
        }

        public int GetXPNeededForNextLevel()
        {
            if (CurrentLevel >= MAX_LEVEL) return 0;
            return Mathf.Max(0, GetXPForLevel(CurrentLevel + 1) - CurrentXP);
        }

        public int GetXPForLevel(int level)
        {
            if (level <= 1) return 0;
            return Mathf.FloorToInt(baseXP * Mathf.Pow(level - 1, xpExponent));
        }

        public void StartMatchTimer()
        {
            if (IsServer) _isMatchTimerRunning = true;
        }

        public void StopMatchTimer()
        {
            if (IsServer) _isMatchTimerRunning = false;
        }

        public void ResetStageTimer()
        {
            if (IsServer) NetworkStageTime.Value = 0f;
        }

        private void CheckLevelUp()
        {
            if (!IsServer) return;

            bool leveledUp = false;
            while (CurrentXP >= GetXPForLevel(CurrentLevel + 1) && CurrentLevel < MAX_LEVEL)
            {
                NetworkCurrentLevel.Value++;
                leveledUp = true;
            }

            if (leveledUp)
            {
                UpdateBaseValuesLocally();
            }
        }

        private void RecalculateLevel()
        {
            if (!IsServer) return;

            int newLevel = 1;
            while (CurrentXP >= GetXPForLevel(newLevel + 1) && newLevel < MAX_LEVEL)
                newLevel++;

            if (newLevel != CurrentLevel)
            {
                NetworkCurrentLevel.Value = newLevel;
                UpdateBaseValuesLocally();
            }
        }

        private void UpdateBaseValuesLocally()
        {
            float boostMultiplier = _isFinalStageInLoop ? finalStageBoostMultiplier : 1f;
            
            // Risk of Rain 2 styled time coefficient on top of level scaling
            float timeCoefficient = 1f + (MatchTime / 60f) * baseMatchTimeCoefficient;

            FullDamage = (int)(damageBaseValue * Mathf.Pow(damageGrowthRate, CurrentLevel - 1) * boostMultiplier * timeCoefficient);
            FullHealth = (int)(healthBaseValue * Mathf.Pow(healthGrowthRate, CurrentLevel - 1) * boostMultiplier * timeCoefficient);
            FullCost = (int)(costBaseValue * Mathf.Pow(costGrowthRate, CurrentLevel - 1));

            DifficultyCoefficient = damageBaseValue > 0f ? FullDamage / damageBaseValue : 1f;
            SpawnBudgetPerSecond = baselineBudget * (1f + budgetPerLevelGrowth * (CurrentLevel - 1)) * timeCoefficient;

            float rampT = moneyRewardRampSeconds > 0f ? Mathf.Clamp01(MatchTime / moneyRewardRampSeconds) : 0f;
            MoneyRewardMultiplier = Mathf.Lerp(1f, moneyRewardMaxMultiplier, rampT);

            float rawCostRatio = costBaseValue > 0f ? FullCost / costBaseValue : 1f;
            float scaledCostMult = containerCostScaleStrength <= 0f
                ? 1f
                : Mathf.Pow(Mathf.Max(1f, rawCostRatio), containerCostScaleStrength);
            ContainerCostMultiplier = Mathf.Min(maxContainerCostMultiplier, scaledCostMult);
        }

        public void SetFinalStageBoost(bool isActive)
        {
            if (!IsServer) return;
            _isFinalStageInLoop = isActive;
            UpdateBaseValuesLocally();
        }

        public void ResetProgress()
        {
            if (!IsServer) return;
            NetworkCurrentXP.Value = 0;
            NetworkCurrentLevel.Value = 1;
            NetworkMatchTime.Value = 0f;
            NetworkStageTime.Value = 0f;
            _isFinalStageInLoop = false;
            UpdateBaseValuesLocally();
        }
    }
}
