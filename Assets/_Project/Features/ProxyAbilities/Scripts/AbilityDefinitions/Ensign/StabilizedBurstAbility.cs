using System.Collections;
using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Enums;
using _Project.Core.Stats;
using _Project.Core.Structs;
using _Project.Features.WeaponCore.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Ensign
{
    [CreateAssetMenu(fileName = "StabilizedBurst", menuName = "Proxy Abilities/Ensign/Stabilized Burst")]
    public class StabilizedBurstAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Stabilized Burst Settings")]
        [SerializeField] private int burstCount = 3;
        [SerializeField] private float timeBetweenShots = 0.1f;
        [SerializeField] private float recoilReductionMultiplier = 0.5f;
        [SerializeField] private float weakPointDamageMultiplier = 1.5f;
        [SerializeField] private float hitStopPerShot = 0.03f;

        public bool CanBeUsed(GameObject instigator)
        {
            NetworkWeapon weapon = instigator.GetComponentInChildren<NetworkWeapon>();
            if (weapon == null) return false;
            if (weapon.IsReloading()) return false;
            return weapon.GetCurrentAmmo() >= burstCount;
        }

        public void Execute(GameObject instigator)
        {
            if (!CanBeUsed(instigator)) return;

            if (instigator.TryGetComponent(out MonoBehaviour runner))
            {
                runner.StartCoroutine(BurstRoutine(instigator));
            }
        }

        private IEnumerator BurstRoutine(GameObject instigator)
        {
            NetworkWeapon weapon = instigator.GetComponentInChildren<NetworkWeapon>();
            CharacterStats stats = instigator.GetComponent<CharacterStats>();

            if (weapon == null) yield break;

            StatModifier recoilMod = new StatModifier { Type = StatModType.Flat, Value = recoilReductionMultiplier };
            StatModifier weakPointMod = new StatModifier { Type = StatModType.Percent, Value = weakPointDamageMultiplier - 1f };

            if (stats != null)
            {
                stats.AddModifier(StatType.RecoilReductionMultiplier, recoilMod);
                stats.AddModifier(StatType.WeakPointDamageMultiplier, weakPointMod);
            }

            // Let the client trigger the firing sequence so all their local visual effects and traces work correctly
            if (weapon.IsServer)
            {
                weapon.ForceBurstFireClientRpc(burstCount, timeBetweenShots);
            }

            for (int i = 0; i < burstCount; i++)
            {
                if (weapon.GetCurrentAmmo() > 0 && !weapon.IsReloading())
                {
                    if (hitStopPerShot > 0f)
                    {
                        if (instigator.TryGetComponent(out _Project.Features.ProxyAbilities.Scripts.AbilityController abilityController))
                        {
                            abilityController.ApplyHitStopClientRpc(hitStopPerShot);
                        }
                        else
                        {
                            _Project.Core.Managers.HitStopManager.Instance.TriggerHitStop(hitStopPerShot);
                        }
                    }
                }
                else
                {
                    break; // Stop burst if out of ammo or reloading
                }
                
                yield return new WaitForSeconds(timeBetweenShots);
            }

            if (stats != null)
            {
                stats.RemoveModifier(StatType.RecoilReductionMultiplier, recoilMod);
                stats.RemoveModifier(StatType.WeakPointDamageMultiplier, weakPointMod);
            }

            Debug.Log($"[{nameof(StabilizedBurstAbility)}] {instigator.name} finished Stabilized Burst!");
        }
    }
}