using UnityEngine;



[CreateAssetMenu(fileName = "New Skill", menuName = "Characters/Skill")]
public class Skill : ScriptableObject
{
    [Header("Core Identity")]
    public string skillName;
    [TextArea]
    public string description;

    // --- THE CHANGED LINES ---
    public Rarity rarity;
    public ElementType element;
    // --- END OF CHANGES ---

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

    [Header("Status Effect")]
    [Tooltip("Does this skill apply a status effect? If this a ")]
    public bool appliesStatusEffect;
    public StatusEffectType effectToApply;
    [Tooltip("How many turns does the effect last?")]
    public int effectDuration;

    [Tooltip("Classification for the tickdown system")]
    public EffectClassification effectClassification;
    
    
}