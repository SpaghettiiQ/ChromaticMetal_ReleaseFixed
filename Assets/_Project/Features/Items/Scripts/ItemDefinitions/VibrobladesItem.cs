using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Structs;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Vibroblades", fileName = "Vibroblades")]
    public class VibrobladesItem : ItemDefinition, IOnHitItem
    {
        public float baseBleedChance = 0.2f;
        public float bleedDamagePerSecond = 2f;
        public float bleedDuration = 5f;

        [Header("VFX")]
        public GameObject bleedVfxPrefab;

        public void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx)
        {
            // Proc convention: don't re-bleed off our own bleed ticks (DamageOverTime) — each tick
            // raises OnHit and would re-roll a fresh bleed — nor off item-proc follow-ups (Secondary).
            if (ctx.AttackType == AttackType.Secondary) return;
            if (ctx.AttackType == AttackType.DamageOverTime) return;

            float bonusChance = 0f;
            float multiplier = 1f;

            if (owner.TryGetComponent(out _Project.Core.Stats.CharacterStats ownerStats))
            {
                bonusChance = ownerStats.GetStat(_Project.Core.Enums.StatType.ProcChanceFlatAdd);
                multiplier = ownerStats.GetStat(_Project.Core.Enums.StatType.ProcChanceMultiplier);
                if (multiplier == 0f) multiplier = 1f;
            }

            float bleedChance = ctx.AttackType == AttackType.Melee ? 1.0f : ((baseBleedChance * multiplier) + bonusChance);

            if (Random.value <= bleedChance)
            {
                Debug.Log($"[Vibroblades] Applied Bleed stack! Target bleeds out {bleedDamagePerSecond * stacks} HP/s for {bleedDuration}s.");
                if (owner.TryGetComponent(out MonoBehaviour runner))
                {
                    runner.StartCoroutine(BleedRoutine(ctx.Target, stacks, ctx));
                }
            }
        }

        private System.Collections.IEnumerator BleedRoutine(GameObject target, int stacks, DamageContext originalCtx)
        {
            GameObject activeBleed = null;
            if (target != null && bleedVfxPrefab != null)
            {
                activeBleed = Instantiate(bleedVfxPrefab, target.transform.position, Quaternion.identity, target.transform);
            }

            float elapsed = 0f;
            while (elapsed < bleedDuration)
            {
                yield return new WaitForSeconds(1f);
                elapsed += 1f;

                if (target != null && target.TryGetComponent(out _Project.Core.Stats.CharacterStats targetStats) && !targetStats.IsDead)
                {
                    var bleedCtx = new DamageContext
                    {
                        Source = originalCtx.Source,
                        Target = target,
                        Damage = Mathf.RoundToInt(bleedDamagePerSecond * stacks),
                        // Bleed (not True): armor/vuln apply, but it leaks through Shield — only
                        // Barrier/health stop it. Still DamageOverTime so procs ignore it.
                        DamageType = _Project.Core.Enums.DamageType.Bleed,
                        AttackType = _Project.Core.Enums.AttackType.DamageOverTime,
                        IsCritical = false
                    };
                    targetStats.TakeDamage(bleedCtx);
                }
                else
                {
                    break;
                }
            }

            if (activeBleed != null) Destroy(activeBleed);
        }
    }
}