using System.Collections;
using UnityEngine;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;
using _Project.Features.Player.Scripts;
using _Project.Features.Weapons.Scripts;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Acheron.Tracking
{
    /// <summary>
    /// "Adoration for None" — Acheron plants in place, gains heavy damage resistance, and
    /// bypasses weapon overheating for the duration. Uses PlayerMovement.MovementLocked
    /// instead of a -100% movement-speed modifier so other items' Percent stat buffs can't
    /// leak through into a partial slow.
    /// </summary>
    public class AdorationForNoneTracker : MonoBehaviour
    {
        private CharacterStats _stats;
        private MagmaWeapon _weapon;
        private PlayerMovement _movement;
        private StatModifier _resistMod;
        private StatModifier _atkSpeedMod;
        private bool _modApplied;
        private bool _atkSpeedApplied;
        private bool _heatBypassApplied;
        private bool _movementLocked;
        private Coroutine _routine;

        public void ExecuteAbility(float duration, float dmgReduction, float attackSpeedBonus)
        {
            // Re-pressing inside the active window restarts cleanly — don't stack effects.
            if (_routine != null) StopCoroutine(_routine);
            ClearEffects();

            _routine = StartCoroutine(Routine(duration, dmgReduction, attackSpeedBonus));
        }

        private IEnumerator Routine(float duration, float dmgReduction, float attackSpeedBonus)
        {
            _stats = GetComponent<CharacterStats>();
            _weapon = GetComponentInChildren<MagmaWeapon>();
            _movement = GetComponent<PlayerMovement>();

            _resistMod = new StatModifier(StatModType.Flat, dmgReduction);
            // AttackSpeed is seeded to 1f and applied as `fireRate /= GetStat(AttackSpeed)` in
            // NetworkWeapon.ExecuteShot. Flat add of 0.5 → final = (1 + 0.5) * 1 = 1.5x rate.
            _atkSpeedMod = new StatModifier(StatModType.Flat, attackSpeedBonus);

            if (_stats != null && _stats.IsServer)
            {
                _stats.AddModifier(StatType.DamageReduction, _resistMod);
                _modApplied = true;

                if (attackSpeedBonus > 0f)
                {
                    _stats.AddModifier(StatType.AttackSpeed, _atkSpeedMod);
                    _atkSpeedApplied = true;
                }
            }

            if (_movement != null)
            {
                _movement.MovementLocked = true;
                _movementLocked = true;
            }

            if (_weapon != null)
            {
                _weapon.SetHeatBypass(true);
                _heatBypassApplied = true;
            }

            yield return new WaitForSeconds(duration);

            ClearEffects();
            _routine = null;
        }

        private void ClearEffects()
        {
            if (_modApplied && _stats != null && _stats.IsServer)
            {
                _stats.RemoveModifier(StatType.DamageReduction, _resistMod);
                _modApplied = false;
            }
            if (_atkSpeedApplied && _stats != null && _stats.IsServer)
            {
                _stats.RemoveModifier(StatType.AttackSpeed, _atkSpeedMod);
                _atkSpeedApplied = false;
            }
            if (_movementLocked && _movement != null)
            {
                _movement.MovementLocked = false;
                _movementLocked = false;
            }
            if (_heatBypassApplied && _weapon != null)
            {
                _weapon.SetHeatBypass(false);
                _heatBypassApplied = false;
            }
        }

        private void OnDisable()
        {
            // Belt-and-suspenders — if the player dies, despawns, or the tracker is otherwise
            // disabled mid-channel, never leave lingering DR / lock / heat-bypass behind.
            ClearEffects();
        }
    }
}
