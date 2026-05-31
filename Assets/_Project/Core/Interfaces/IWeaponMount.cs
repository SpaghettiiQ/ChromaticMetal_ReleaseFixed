namespace _Project.Core.Interfaces
{
    /// Implemented by whatever owns the player's weapon mount transform.
    /// Lets weapons request visual "drop" feedback (reload / overheat dip) without
    /// taking a direct dependency on the Weapons feature.
    public interface IWeaponMount
    {
        void BeginReloadDip();
        void EndReloadDip();
    }
}
