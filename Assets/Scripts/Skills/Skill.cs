using UnityEngine;

// --- ADD THESE ENUMS AT THE TOP OF THE FILE ---
public enum SkillEffectType { Damage, Healing }
public enum TargetType { Enemy, Self }

[CreateAssetMenu(fileName = "New Skill", menuName = "Characters/Skill")]
public class Skill : ScriptableObject
{
    [Header("Core Identity")]
    public string skillName;
    [TextArea]
    public string description;
    public string rarity; 
    public string element;

    [Header("Resource & Timing")]
    public int energyCost;
    public int cooldown;

    // --- NEW FIELDS FOR FLEXIBILITY ---
    [Header("Targeting & Effect")]
    public SkillEffectType effectType;
    public TargetType targetType;

    [Header("Damage Effect")]
    public int baseDamage;
    public float mightRatio;
    
    [Header("Healing Effect")]
    public int baseHeal;
    public float intelligenceRatio;
}