public class StatusEffect
{
    public StatusEffectType Type { get; private set; }
    public int Duration { get; set; }
    public EffectClassification Classification { get; private set; }
    public bool IsNewlyApplied { get; set; }
    
    // --- NEW FIELD ---
    // Stores the calculated damage/healing per turn
    public int TickValue { get; set; } 

    public StatusEffect(StatusEffectType type, int duration, EffectClassification classification)
    {
        Type = type;
        Duration = duration;
        Classification = classification;
        IsNewlyApplied = true;
    }
}