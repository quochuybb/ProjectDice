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
    public int stacksToApply = 1;

    [Header("Stat Modifier Effect")]
    [Tooltip("If this skill applies a StatUp or StatDown effect, define it here.")]
    public StatType statToModify;
    public StatModType modificationType;
    public float modificationValue;
    
    [Header("Cleansing & Purging")]
    [Tooltip("Does this skill instantly remove debuffs?")]
    public bool doesCleanse;
    public int cleanseAmount;

    [Tooltip("Does this skill instantly remove buffs?")]
    public bool doesPurge;
    public int purgeAmount;
}