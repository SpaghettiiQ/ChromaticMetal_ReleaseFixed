namespace _Project.Core.Interfaces
{
    public interface IWeapon
    {
        // Passes 'true' when the mouse is pressed/held down, and 'false' when released.
        void HandleInput(bool isFireInputActive);

        // Triggers the weapon's reload sequence.
        void Reload();

        // The weapon's static data — lets Core/Features read e.g. BaseDamage without a
        // Feature→WeaponCore dependency (used by T1 Thunderhead's damage-threshold check).
        IWeaponData WeaponData { get; }
    }
}