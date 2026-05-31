using _Project.Core.Enums;

namespace _Project.Core.Interfaces
{
    public interface IAbilityController
    {
        void TryUseAbility(AbilitySlot slot);
        void EquipAbility(AbilitySlot slot, IProxyAbilityData newAbility);
        void ReduceCooldown(AbilitySlot slot, float amount);
    }
}