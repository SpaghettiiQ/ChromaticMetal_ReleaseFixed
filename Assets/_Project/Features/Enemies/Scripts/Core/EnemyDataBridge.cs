using UnityEngine;
namespace _Project.Features.Enemies.Scripts.Core
{
    public class EnemyDataBridge : MonoBehaviour
    {
        [SerializeField] private EnemyData enemyData;
        public EnemyData Data => enemyData;
        public int GetMoneyReward()
        {
            return enemyData != null ? enemyData.moneyReward : 0;
        }
        public float GetXPReward()
        {
            return enemyData != null ? enemyData.xpFromKill : 0f;
        }
        public string GetEnemyName()
        {
            return enemyData != null ? enemyData.enemyName : gameObject.name;
        }
        public bool CanDropLoot()
        {
            return enemyData != null && enemyData.canDropLoot;
        }
    }
}
