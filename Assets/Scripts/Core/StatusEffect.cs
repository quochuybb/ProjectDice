public class StatusEffect
{
    public StatusEffectType Type { get; private set; }
    public int Duration { get; set; }
    public EffectClassification Classification { get; private set; }
    public bool IsNewlyApplied { get; set; }
    public int TickValue { get; set; } 
    public int Stacks { get; set; }

    // --- NEW FIELDS FOR STAT MODIFICATIONS ---
    public StatType TargetStat { get; private set; }
    public StatModType ModType { get; private set; }
    public float ModValue { get; private set; }

    // Constructor for standard effects
    public StatusEffect(StatusEffectType type, int duration, EffectClassification classification)
    {
        Type = type;
        Duration = duration;
        Classification = classification;
        IsNewlyApplied = true;
        Stacks = 1;
    }

    // --- NEW CONSTRUCTOR for Stat modifying effects ---
    public StatusEffect(StatusEffectType type, int duration, EffectClassification classification, StatType targetStat, StatModType modType, float modValue)
    {
        Type = type;
        Duration = duration;
        Classification = classification;
        IsNewlyApplied = true;
        Stacks = 1;

        TargetStat = targetStat;
        ModType = modType;
        ModValue = modValue;
    }
}