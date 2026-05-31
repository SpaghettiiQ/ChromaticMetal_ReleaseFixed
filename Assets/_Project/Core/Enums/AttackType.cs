namespace _Project.Core.Enums
{
    public enum AttackType
    {
        Melee,
        Projectile,
        Explosion,
        Cutting,
        Effect,
        Contact,
        Secondary,
        DamageOverTime, // Burn, bleed, poison ticks. Proc items should universally ignore these.
        MissileImpact,  // Guided Nanomissiles' own hit — lets missiles avoid re-procing themselves
                        // while still allowing chain/ricochet items (which check Secondary) to proc.
        Burden          // Burden-system self-DOT. True damage (bypasses armor) but, like Bleed,
                        // leaks through Shield (only Barrier/health stop it). Proc items ignore it.
    }
}