using UnityEngine;

[CreateAssetMenu(fileName = "New Skill", menuName = "Characters/Skill")]
public class Skill : ScriptableObject
{
    [Header("Core Identity")]
    public string skillName;
    [TextArea]
    public string description;
    public Rarity rarity; 
    public ElementType element;

    [Header("Resource & Timing")]
    public int energyCost;
    public int cooldown;

    [Header("Targeting & Effect")]
    public SkillEffectType effectType;
    public TargetType targetType;

    [Header("Damage Effect")]
    public int baseDamage;
    public float mightRatio;
    
    [Header("Healing Effect")]
    public int baseHeal;
    public float intelligenceRatio;

    [Header("DoT / HoT Effect")]
    [Tooltip("Base damage/healing per turn for DoT/HoT effects.")]
    public int baseDotHotValue;
    [Tooltip("How much the DoT/HoT scales with the caster's Intelligence.")]
    public float dotHotIntelligenceRatio;

    [Header("Status Effect")]
    public bool appliesStatusEffect;
    public StatusEffectType effectToApply;
    public EffectClassification effectClassification;
    public int effectDuration;
    
    // --- NEW FIELD ---
    [Tooltip("For stacking effects like Wound, how many stacks does this skill apply?")]
    public int stacksToApply = 1;
}