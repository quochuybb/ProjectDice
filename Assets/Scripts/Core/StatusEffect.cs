public class StatusEffect
{
    public StatusEffectType Type { get; private set; }
    public int Duration { get; set; }
    public EffectClassification Classification { get; private set; }
    
    // --- THE NEW FLAG ---
    public bool IsNewlyApplied { get; set; }

    public StatusEffect(StatusEffectType type, int duration, EffectClassification classification)
    {
        Type = type;
        Duration = duration;
        Classification = classification;
        
        // --- SET THE FLAG ON CREATION ---
        IsNewlyApplied = true;
    }
}