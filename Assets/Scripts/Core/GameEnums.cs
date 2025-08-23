public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Relic,
    Mythic
}

public enum ElementType
{
    None,
    Inferno,
    Quake,
    Tide,
    Cyclone,
    Verdant
}

public enum CombatState { START, PLAYERTURN, ENEMYTURN, PROCESSING, WON, LOST }
public enum SkillEffectType { Damage, Healing }
public enum TargetType { Enemy, Self }

public enum StatModType
{
    Flat,       // Adds a flat value, calculated first.
    Percent     // Adds a percentage, calculated after all flat bonuses.
}
 
public enum StatType
{
    None, // Add this default value
    MaxHealth,
    Energy,
    EnergyRegen,
    Might,
    Intelligence,
    Armor,
    Speed,
    Grit,
    Luck,
    Growth
}

public enum StatusEffectType
{
    None,
    Fortify,
    Empower,
    Burn,
    Regeneration,
    PowerUp,
    Vulnerable,
    Weaken,
    Stun,
    Freeze,
    Poison,
    MortalWound,
    Blight,
    Dodge,
    Wound,
    Bleed,
    StatUp,
    StatDown
}

public enum EffectClassification
{
    Buff,
    Debuff
}