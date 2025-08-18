using UnityEngine;

[CreateAssetMenu(fileName = "New Skill", menuName = "Characters/Skill")]
public class Skill : ScriptableObject
{
    [Header("Core Identity")]
    public string skillName;
    [TextArea]
    public string description;
    // We will use enums for Rarity, Element, etc. later for robustness.
    public string rarity; 
    public string element;

    [Header("Resource & Timing")]
    public int energyCost;
    public int cooldown;

    [Header("Mechanical Effects")]
    public int baseDamage;
    public float mightRatio;
    
    // Future additions based on your GDD:
    // public TargetType targetType;
    // public StatusEffect appliedStatus;
    // public DetonationType detonationType;
}