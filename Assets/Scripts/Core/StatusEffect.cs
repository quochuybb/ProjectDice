public class StatusEffect
{
    public StatusEffectType Type { get; private set; }
    public int Duration { get; set; }
    public EffectClassification Classification { get; private set; }
    public bool IsNewlyApplied { get; set; }
    public int TickValue { get; set; }
    
    // --- NEW FIELD FOR STACKS ---
    public int Stacks { get; set; }

    public StatusEffect(StatusEffectType type, int duration, EffectClassification classification)
    {
        Type = type;
        Duration = duration;
        Classification = classification;
        IsNewlyApplied = true;
        // --- SET DEFAULT STACK COUNT ---
        Stacks = 1; 
    }
}