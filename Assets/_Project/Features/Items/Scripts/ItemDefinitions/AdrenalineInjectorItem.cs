using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Adrenaline Injector", fileName = "AdrenalineInjector")]
    public class AdrenalineInjectorItem : ItemDefinition, IOnKillItem
    {
        public float movementSpeedBonus = 0.5f; // +50% speed
        public float duration = 3f;

        public void OnKillEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            if (owner.TryGetComponent(out MonoBehaviour runner))
            {
                Debug.Log($"[Adrenaline Injector] Killed enemy! Boosting speed by {movementSpeedBonus * stacks * 100}% for {duration} seconds.");
                runner.StartCoroutine(AdrenalineRoutine(owner, stacks));
            }
        }

        private System.Collections.IEnumerator AdrenalineRoutine(GameObject owner, int stacks)
        {
            if (owner.TryGetComponent(out CharacterStats stats))
            {
                var speedBuff = new StatModifier(StatModType.Percent, movementSpeedBonus * stacks);
                stats.AddModifier(StatType.MovementSpeed, speedBuff);

                yield return new WaitForSeconds(duration);

                // If player is still alive / exists
                if (stats != null)
                {
                    stats.RemoveModifier(StatType.MovementSpeed, speedBuff);
                }
            }
        }
    }
}