namespace _Project.Core.Enums
{
    public enum DamageType
    {
        All,
        Physical,
        Explosive,
        Fire,
        Presence,
        Purifying,
        Bleed,  // Mitigated normally (armor/vuln apply), but leaks through Shield — only Barrier
                // and health stop it. DOT, so excluded from the crit roll. See CharacterStats.TakeDamage.
        True
    }
}