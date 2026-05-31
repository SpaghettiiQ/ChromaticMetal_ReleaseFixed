using System;
using UnityEngine;
using _Project.Core.Structs;
using _Project.Core.Enums;

namespace _Project.Core.Events
{
    public delegate void DamageModifier(ref DamageContext ctx);

    public class CharacterEventBus : MonoBehaviour
    {
        public event Action<DamageContext> OnHit;
        public event Action<DamageContext> OnKill;
        public event Action<DamageContext> OnDamageTaken;
        public event DamageModifier OnBeforeTakeDamage;
        public event Action<ConditionType, bool> OnConditionChanged;
        public event Action OnDeath;
        public event Action OnJump;
        public event Action<float> OnLand;
        public event Action<AbilitySlot> OnAbilityUsed;
        public event Action<ushort> OnItemPickedUp;
        public event Action<int, int> OnHealed; // appliedAmount, overhealAmount — raised server-side from CharacterStats.Heal
        
        // Game Feel Events
        public event Action<Vector3> OnWeaponFiredRecoil;
        public event Action<float, float> OnCameraShake;

        public void RaiseOnHit(DamageContext ctx) => OnHit?.Invoke(ctx);
        public void RaiseOnKill(DamageContext ctx) => OnKill?.Invoke(ctx);
        public void RaiseOnDamageTaken(DamageContext ctx) => OnDamageTaken?.Invoke(ctx);
        public void RaiseOnBeforeTakeDamage(ref DamageContext ctx) => OnBeforeTakeDamage?.Invoke(ref ctx);
        public void RaiseOnConditionChanged(ConditionType type, bool value) => OnConditionChanged?.Invoke(type, value);
        public void RaiseOnDeath() => OnDeath?.Invoke();
        public void RaiseOnJump() => OnJump?.Invoke();
        public void RaiseOnLand(float impactSpeed) => OnLand?.Invoke(impactSpeed);
        public void RaiseOnAbilityUsed(AbilitySlot slot) => OnAbilityUsed?.Invoke(slot);
        public void RaiseOnItemPickedUp(ushort itemID) => OnItemPickedUp?.Invoke(itemID);
        public void RaiseOnHealed(int applied, int overheal) => OnHealed?.Invoke(applied, overheal);
        
        public void RaiseOnWeaponFiredRecoil(Vector3 recoil) => OnWeaponFiredRecoil?.Invoke(recoil);
        public void RaiseOnCameraShake(float magnitude, float duration) => OnCameraShake?.Invoke(magnitude, duration);
    }
}