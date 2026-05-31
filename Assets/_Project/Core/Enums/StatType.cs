namespace _Project.Core.Enums
{
    public enum StatType
    {
        MaxHealth,
        MovementSpeed,
        AttackSpeed,
        DamageReduction,
        ExtraJumps,
        SpecialCharges,
        ContainerCostMultiplier,
        MoneyMultiplier,
        ProcChanceMultiplier,
        ProcChanceFlatAdd, // Adds a flat chance
        WeakPointDamageMultiplier,
        RecoilReductionMultiplier,
        DamageVulnerability, // Extra percentage damage taken
        CritChance,          // 0-1 chance per attack to crit
        CritDamageMultiplier, // Final multiplier applied to damage on crit. Seeded to 2f in CharacterStats.Initialize.
        WeaponDamageMultiplier, // Outgoing damage multiplier for the attacker's "weapon-class" attacks
                               // (skips AttackType.Secondary so item proc damage doesn't get
                               // double-stacked). Seeded to 1f in CharacterStats.Initialize.
                               // Conduit's Presence Power scales this for Thrive players via
                               // BurdenController when Burden Filter is equipped.
        FireDamageMultiplier,  // Outgoing multiplier applied ONLY to DamageType.Fire, centrally in
                               // CharacterStats.TakeDamage (so every fire source benefits). Seeded
                               // to 1f. Thermite Fuel adds Percent modifiers to it.
        ReloadSpeedMultiplier  // Reload-speed multiplier read in NetworkWeapon.StartReload (>1 =
                               // faster). Seeded to 1f. Autoloader stacks it multiplicatively via a
                               // Flat modifier so reload TIME = reloadTime / multiplier shrinks.
    }
}