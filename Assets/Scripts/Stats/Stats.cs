[System.Serializable]
public class Stat
{
    public int baseValue;

    // You can add more complex logic for modifiers from items, buffs, etc. later
    public int GetValue()
    {
        // For now, it's simple. Later, this will apply the formula from your GDD:
        // (Base + Flat Bonuses) * (1 + Percent Bonuses)
        return baseValue;
    }
}