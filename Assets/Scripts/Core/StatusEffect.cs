public class StatusEffect
{
    public StatusEffectType Type { get; private set; }
    public int Duration { get; set; }
    
    // --- NEW FIELD ---
    public EffectClassification Classification { get; private set; }

    public StatusEffect(StatusEffectType type, int duration, EffectClassification classification)
    {
        Type = type;
        Duration = duration;
        Classification = classification;
    }
}