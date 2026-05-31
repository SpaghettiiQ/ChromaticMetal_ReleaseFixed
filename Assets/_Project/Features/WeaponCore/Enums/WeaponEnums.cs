namespace _Project.Features.WeaponCore.Enums
{
    public enum AmmoMechanic
    {
        Infinite,   // No ammo, fire forever (e.g., standard melee, basic lasers)
        Magazine,   // Traditional ammo pool, brief pause to reload when empty
        Overheat    // Heat builds up per shot. Decays over time. Penalizes with a longer pause if fully filled.
    }

    public enum FireMode
    {
        SemiAuto,   // One click = one attack. Must release and click again.
        Automatic,  // Hold click to continuously attack at the fire rate.
        Charge      // Hold click to build up power, releases attack when full (or upon release, depending on implementation).
    }
}