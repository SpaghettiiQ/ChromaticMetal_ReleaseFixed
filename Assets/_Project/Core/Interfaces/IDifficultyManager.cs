using System;

namespace _Project.Core.Interfaces
{
    public interface IDifficultyManager
    {
        // Properties
        int CurrentLevel { get; }
        int CurrentXP { get; }

        int FullDamage { get; }
        int FullHealth { get; }
        int FullCost { get; }

        float MatchTime { get; }

        float DamageBaseValue { get; }
        float DifficultyCoefficient { get; }
        float SpawnBudgetPerSecond { get; }
        float MoneyRewardMultiplier { get; }
        float ContainerCostMultiplier { get; }

        // Events
        event Action<int> OnLevelUp;
        event Action<int> OnXPChanged;
        event Action<float> OnMatchTimeChanged;

        // Methods
        void AddXP(int amount);
        void RemoveXP(int amount);
        int GetXPNeededForNextLevel();
        int GetXPForLevel(int level);

        void StartMatchTimer();
        void StopMatchTimer();
    }
}
