using UnityEngine;

namespace _Project.Features.Enemies.Scripts.Core
{
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "Enemies/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        public string enemyName = "New Enemy";
        public float maxHealth = 100f;
        public float baseDamage = 10f;
        public int moneyReward = 10;
        public float xpFromKill = 25f;
        public bool canDropLoot = true;

        [Tooltip("Spawn-budget cost. Heavier units cost more of the per-second budget.")]
        public float spawnCost = 1f;
    }
}
