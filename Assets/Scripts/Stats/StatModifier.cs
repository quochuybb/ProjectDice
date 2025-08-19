
public class StatModifier
{
    public readonly float Value;
    public readonly StatModType Type;
    public readonly object Source; // The item, skill, buff, etc., that applied this.

    public StatModifier(float value, StatModType type, object source)
    {
        Value = value;
        Type = type;
        Source = source;
    }
}