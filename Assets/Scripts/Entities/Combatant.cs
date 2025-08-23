using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic; // For List
using System.Linq; // For Find

[RequireComponent(typeof(InventoryComponent))] // It's good practice to keep this here
public class Combatant : MonoBehaviour
{
    public CharacterSheet characterSheet;
    public CharacterStats Stats { get; private set; }

    public int currentHealth;
    public int currentEnergy;

    // We add an event for Energy, just like we have for Health. This is vital for UI.
    public UnityAction<int, int> OnHealthChanged;
    public UnityAction<int, int> OnEnergyChanged;

    public List<StatusEffect> activeStatusEffects = new List<StatusEffect>();
    public UnityAction<List<StatusEffect>> OnStatusEffectsChanged;

    public Dictionary<Skill, int> skillCooldowns = new Dictionary<Skill, int>();
    public UnityAction OnCooldownsChanged;

    void Awake()
    {
        Stats = new CharacterStats(characterSheet);
    }

    void Start()
    {
        currentHealth = (int)Stats.MaxHealth.Value;
        currentEnergy = (int)Stats.Energy.Value;
        // Invoke events on Start to set initial UI state (when UI exists)
        OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
        OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);
    }

    // --- ADD THIS NEW METHOD ---
    public void RegenerateEnergy()
    {
        int regenAmount = (int)Stats.EnergyRegen.Value;
        currentEnergy += regenAmount;

        // Clamp the value so it doesn't exceed the max energy
        currentEnergy = Mathf.Min(currentEnergy, (int)Stats.Energy.Value);

        Debug.Log($"<color=cyan>{characterSheet.name} regenerates {regenAmount} energy. Now has {currentEnergy}.</color>");
        OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);
    }

    public bool AttemptToHit()
    {
        // 1. Check for the Dodge BUFF first.
        if (HasStatusEffect(StatusEffectType.Dodge))
        {
            // The buff is consumed whether the dodge succeeds or fails.
            RemoveStatusEffect(StatusEffectType.Dodge);

            // Roll for the buff's dodge chance (75%).
            if (Random.value < 0.75f)
            {
                Debug.Log($"<color=cyan>{characterSheet.name} dodged the attack via Dodge effect!</color>");
                return false; // The attack is dodged.
            }
            else
            {
                Debug.Log($"<color=grey>{characterSheet.name} failed to dodge the empowered attack.</color>");
                // If the dodge fails, continue to the passive check.
            }
        }

        // 2. If no Dodge buff or if it failed, check for passive dodge from the Speed stat.
        float speed = Stats.Speed.Value;
        float passiveDodgeChance = (speed / (speed + 150f)) * 0.3f;

        if (Random.value < passiveDodgeChance)
        {
            Debug.Log($"<color=cyan>{characterSheet.name} passively dodged due to high Speed!</color>");
            return false; // The attack is dodged.
        }

        // 3. If all checks fail, the attack hits.
        return true;
    }
    public void TakeDamage(int damage)
    {
        // The dodge logic has been removed from here.

        float armor = Stats.Armor.Value;
        float damageReduction = (armor / (armor + 150));
        int finalDamage = Mathf.RoundToInt(damage * (1 - damageReduction));

        if (HasStatusEffect(StatusEffectType.Vulnerable))
        {
            finalDamage = Mathf.RoundToInt(finalDamage * 1.5f);
            Debug.Log($"<color=orange>Target is Vulnerable! Damage increased to {finalDamage}.</color>");
        }

        currentHealth -= finalDamage;
        if (currentHealth < 0) currentHealth = 0;

        OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
        Debug.Log($"{characterSheet.name} takes {finalDamage} damage after armor reduction.");

        if (currentHealth <= 0)
        {
            Die();
        }
    }
    public void UseSkill(Skill skill, Combatant target)
    {
        if (IsSkillOnCooldown(skill))
        {
            Debug.Log($"<color=orange>Cannot use {skill.skillName}, it is on cooldown!</color>");
            return;
        }

        bool isEmpowered = HasStatusEffect(StatusEffectType.Empower);
        int finalEnergyCost = isEmpowered ? 0 : skill.energyCost;

        if (currentEnergy < finalEnergyCost)
        {
            Debug.Log($"<color=orange>{characterSheet.name} does not have enough energy for {skill.skillName}!</color>");
            return;
        }

        // --- NEW DODGE CHECK ---
        // We only check for dodges on hostile actions. You can't "dodge" your own healing spell.
        if (skill.targetType == TargetType.Enemy && !target.AttemptToHit())
        {
            // If the target dodges, the skill has no effect.
            Debug.Log($"{characterSheet.name}'s attack was dodged by {target.characterSheet.name}!");
            // We must also consume the energy for the attempt.
            currentEnergy -= finalEnergyCost;
            OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);
            // Exit the UseSkill method entirely. No damage, no status effects.
            return;
        }
        // --- END OF NEW DODGE CHECK ---
        currentEnergy -= finalEnergyCost;
        if (isEmpowered)
        {
            Debug.Log($"<color=yellow>Empower consumed!</color>");
            RemoveStatusEffect(StatusEffectType.Empower);
        }
        OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);
        Debug.Log($"{characterSheet.name} uses {skill.skillName}! ({finalEnergyCost} EN cost)");

        if (skill.cooldown > 0)
        {
            skillCooldowns[skill] = skill.cooldown;
            OnCooldownsChanged?.Invoke();
        }

        Combatant effectTarget = (skill.targetType == TargetType.Self) ? this : target;
        if (skill.appliesStatusEffect)
        {
            StatusEffect newEffect;
            // Check if the effect is a stat modifier to use the correct constructor
            if (skill.effectToApply == StatusEffectType.StatUp || skill.effectToApply == StatusEffectType.StatDown)
            {
                newEffect = new StatusEffect(skill.effectToApply, skill.effectDuration, skill.effectClassification,
                                            skill.statToModify, skill.modificationType, skill.modificationValue);
            }
            else
            {
                newEffect = new StatusEffect(skill.effectToApply, skill.effectDuration, skill.effectClassification);
            }

            effectTarget.ApplyStatusEffect(newEffect, this, skill);
        }

        switch (skill.effectType)
        {
            case SkillEffectType.Damage:
                float damageMultiplier = 1.0f;
                if (HasStatusEffect(StatusEffectType.PowerUp))
                {
                    damageMultiplier *= 1.5f;
                    Debug.Log($"<color=yellow>Power Up consumed! Damage multiplied.</color>");
                    RemoveStatusEffect(StatusEffectType.PowerUp);
                }
                if (HasStatusEffect(StatusEffectType.Weaken))
                {
                    damageMultiplier *= 0.5f;
                    Debug.Log($"<color=brown>Weaken consumed! Damage reduced.</color>");
                    RemoveStatusEffect(StatusEffectType.Weaken);
                }
                int baseSkillDamage = skill.baseDamage + (int)(Stats.Might.Value * skill.mightRatio);
                int totalDamage = Mathf.RoundToInt(baseSkillDamage * damageMultiplier);
                Debug.Log($"Deals {totalDamage} damage to {target.characterSheet.name}.");
                effectTarget.TakeDamage(totalDamage);
                break;
            case SkillEffectType.Healing:
                int totalHeal = skill.baseHeal + (int)(Stats.Intelligence.Value * skill.intelligenceRatio);
                effectTarget.ReceiveHeal(totalHeal);
                break;

        }

        if (skill.appliesStatusEffect)
        {
            effectTarget.ApplyStatusEffect(
                new StatusEffect(skill.effectToApply, skill.effectDuration, skill.effectClassification),
                this,
                skill
            );
        }
    }

    // --- NEW HELPER & TICKDOWN METHODS ---
    public bool IsSkillOnCooldown(Skill skill)
    {
        return skillCooldowns.ContainsKey(skill);
    }

    public void TickDownCooldowns()
    {
        if (skillCooldowns.Count == 0) return;

        // We use .ToList() to create a temporary copy of the keys,
        // allowing us to safely remove items from the dictionary while iterating.
        List<Skill> skillsOnCooldown = skillCooldowns.Keys.ToList();
        bool changed = false;

        foreach (Skill skill in skillsOnCooldown)
        {
            skillCooldowns[skill]--;
            if (skillCooldowns[skill] <= 0)
            {
                skillCooldowns.Remove(skill);
            }
            changed = true;
        }

        if (changed) OnCooldownsChanged?.Invoke();
    }

    // --- NEW METHODS FOR STATUS EFFECT MANAGEMENT ---
    public bool HasStatusEffect(StatusEffectType type)
    {
        return activeStatusEffects.Any(effect => effect.Type == type);
    }

    public void ApplyStatusEffect(StatusEffect effect, Combatant caster, Skill sourceSkill)
    {
        // --- NEW: WOUND STACKING LOGIC ---
        if (effect.Type == StatusEffectType.Wound)
        {
            StatusEffect existingWound = activeStatusEffects.FirstOrDefault(e => e.Type == StatusEffectType.Wound);
            if (existingWound != null)
            {
                // If Wound already exists, just add stacks and check for Bleed.
                existingWound.Stacks += sourceSkill.stacksToApply;
                Debug.Log($"<color=red>{characterSheet.name} gains {sourceSkill.stacksToApply} Wound stacks! (Total: {existingWound.Stacks})</color>");
                CheckForBleed(existingWound, caster, sourceSkill);
                OnStatusEffectsChanged?.Invoke(activeStatusEffects);
                return; // Exit here to prevent adding a duplicate Wound effect
            }
            else
            {
                // If this is the first Wound application.
                effect.Stacks = sourceSkill.stacksToApply;
                // Set a high duration as Wound is a counter, not a timed effect.
                effect.Duration = 99;
            }
        }

        // Grit resistance logic
        if (effect.Type == StatusEffectType.Stun || effect.Type == StatusEffectType.Freeze)
        {
            float grit = Stats.Grit.Value;
            float resistChance = (grit / (grit + 100f)) * 0.5f;
            if (Random.value < resistChance)
            {
                Debug.Log($"<color=yellow>{characterSheet.name} resisted the {effect.Type} effect!</color>");
                return;
            }
        }
        if (effect.Type == StatusEffectType.StatUp || effect.Type == StatusEffectType.StatDown)
        {
            // Get the specific stat we need to change (e.g., Armor, Might)
            Stat targetStat = GetStat(effect.TargetStat);
            if (targetStat != null)
            {
                // For StatDown, the value must be negative.
                float value = (effect.Type == StatusEffectType.StatUp) ? effect.ModValue : -effect.ModValue;
                var modifier = new StatModifier(value, effect.ModType, effect); // The effect itself is the source
                targetStat.AddModifier(modifier);
                Debug.Log($"Applied {effect.TargetStat} {effect.Type} of {value} to {characterSheet.name}.");
            }
        }
        // TickValue calculation
        if (sourceSkill != null && (effect.Type == StatusEffectType.Burn || effect.Type == StatusEffectType.Regeneration || effect.Type == StatusEffectType.Poison))
        {
            effect.TickValue = sourceSkill.baseDotHotValue + (int)(caster.Stats.Intelligence.Value * sourceSkill.dotHotIntelligenceRatio);
        }

        activeStatusEffects.Add(effect);

        // Stun/Freeze IsNewlyApplied flag fix
        if (effect.Type == StatusEffectType.Stun || effect.Type == StatusEffectType.Freeze)
        {
            effect.IsNewlyApplied = false;
        }

        Debug.Log($"<color=lightblue>{characterSheet.name} gained {effect.Type} for {effect.Duration} turn(s).</color>");

        // Freeze's Vulnerable effect
        if (effect.Type == StatusEffectType.Freeze)
        {
            var vulnerableDebuff = new StatusEffect(StatusEffectType.Vulnerable, 1, EffectClassification.Debuff);
            ApplyStatusEffect(vulnerableDebuff, caster, null);
        }

        // Fortify's armor buff
        if (effect.Type == StatusEffectType.Fortify)
        {
            StatModifier armorBuff = new StatModifier(150, StatModType.Flat, effect);
            Stats.Armor.AddModifier(armorBuff);
        }

        OnStatusEffectsChanged?.Invoke(activeStatusEffects);
    }

    private void CheckForBleed(StatusEffect woundEffect, Combatant caster, Skill sourceSkill)
    {
        if (woundEffect.Stacks >= 10)
        {
            Debug.Log($"<color=darkred>Wound threshold reached! {characterSheet.name} starts to Bleed!</color>");

            woundEffect.Stacks -= 10;
            if (woundEffect.Stacks <= 0)
            {
                RemoveStatusEffect(StatusEffectType.Wound);
            }

            // Apply a 3-turn Bleed effect
            var bleedEffect = new StatusEffect(StatusEffectType.Bleed, 3, EffectClassification.Debuff);
            ApplyStatusEffect(bleedEffect, caster, sourceSkill);

            // --- THE FIX IS HERE ---
            // GDD: Bleed also applies Mortal Wound for its duration.
            var mortalWoundEffect = new StatusEffect(StatusEffectType.MortalWound, 3, EffectClassification.Debuff);
            ApplyStatusEffect(mortalWoundEffect, caster, null); // No source skill needed for Mortal Wound
        }
    }
    public void RemoveStatusEffect(StatusEffectType type)
    {
        StatusEffect effectToRemove = activeStatusEffects.FirstOrDefault(e => e.Type == type);
        if (effectToRemove != null)
        {
            // --- NEW STAT MODIFIER CLEANUP ---
            if (effectToRemove.Type == StatusEffectType.StatUp || effectToRemove.Type == StatusEffectType.StatDown)
            {
                Stat targetStat = GetStat(effectToRemove.TargetStat);
                if (targetStat != null)
                {
                    targetStat.RemoveAllModifiersFromSource(effectToRemove);
                    Debug.Log($"Removed {effectToRemove.TargetStat} {effectToRemove.Type} from {characterSheet.name}.");
                }
            }

            if (effectToRemove.Type == StatusEffectType.Fortify)
            {
                Stats.Armor.RemoveAllModifiersFromSource(effectToRemove);
            }

            activeStatusEffects.Remove(effectToRemove);
            OnStatusEffectsChanged?.Invoke(activeStatusEffects);
        }
    }

    public void TickDownStatusEffectsAtTurnEnd()
    {
        // Iterate backwards to safely remove items from the list while looping
        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeStatusEffects[i];

            // --- THE "NEWLY APPLIED" FLAG NOW APPLIES TO ALL EFFECTS ---
            if (effect.IsNewlyApplied)
            {
                effect.IsNewlyApplied = false;
                continue; // Skip to the next effect in the list
            }

            effect.Duration--;

            if (effect.Duration <= 0)
            {
                Debug.Log($"<color=grey>{characterSheet.name}'s {effect.Type} has expired at turn end.</color>");
                // The existing RemoveStatusEffect method correctly handles cleanup (like for Fortify)
                RemoveStatusEffect(effect.Type);
            }
        }
        OnStatusEffectsChanged?.Invoke(activeStatusEffects);
    }

    private void Die()
    {
        Debug.Log($"<color=red>{characterSheet.name} has been defeated!</color>");
        gameObject.SetActive(false);
    }

    public void ReceiveHeal(int healAmount)
    {
        // Work with a local variable to hold the final, modified heal amount.
        int finalHealAmount = healAmount;

        // --- NEW HEALING DISRUPTION LOGIC ---
        // Check for Blight first, as it's the most powerful effect.
        if (HasStatusEffect(StatusEffectType.Blight))
        {
            finalHealAmount = 0;
            Debug.Log($"<color=purple>{characterSheet.name} is Blighted and cannot be healed!</color>");
        }
        // If not Blighted, then check for Mortal Wound. They do not stack.
        else if (HasStatusEffect(StatusEffectType.MortalWound))
        {
            // Reduce healing by 50%
            finalHealAmount = Mathf.RoundToInt(finalHealAmount * 0.5f);
            Debug.Log($"<color=maroon>{characterSheet.name} has a Mortal Wound! Healing reduced to {finalHealAmount}.</color>");
        }

        // If the heal amount was reduced to 0, no need to proceed further.
        if (finalHealAmount <= 0)
        {
            // Still log a "healed for 0" message to make it clear why health didn't change.
            Debug.Log($"<color=green>{characterSheet.name} is healed for 0. New HP: {currentHealth}.</color>");
            return;
        }

        currentHealth += finalHealAmount;
        currentHealth = Mathf.Min(currentHealth, (int)Stats.MaxHealth.Value);

        OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
        Debug.Log($"<color=green>{characterSheet.name} is healed for {finalHealAmount}. New HP: {currentHealth}.</color>");
    }

    public void ProcessDoTsAndHoTs()
    {
        var effectsToProcess = activeStatusEffects.ToList();

        foreach (var effect in effectsToProcess)
        {
            switch (effect.Type)
            {
                case StatusEffectType.Burn:
                    Debug.Log($"{characterSheet.name} is burned!");
                    TakeDamage(effect.TickValue);
                    break;
                case StatusEffectType.Poison:
                    Debug.Log($"{characterSheet.name} is poisoned!");
                    TakeTrueDamage(effect.TickValue);
                    break;
                case StatusEffectType.Regeneration:
                    Debug.Log($"{characterSheet.name} regenerates health!");
                    ReceiveHeal(effect.TickValue);
                    break;
                // --- ADD THIS CASE ---
                case StatusEffectType.Bleed:
                    // GDD: 20% of Max HP as True Damage
                    int bleedDamage = Mathf.RoundToInt(Stats.MaxHealth.Value * 0.20f);
                    Debug.Log($"{characterSheet.name} is Bleeding heavily!");
                    TakeTrueDamage(bleedDamage);
                    break;
            }
        }
    }

    public void TakeTrueDamage(int damage)
    {
        // This method bypasses all armor and damage reduction calculations.

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
        Debug.Log($"{characterSheet.name} takes {damage} TRUE damage!");

        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public Stat GetStat(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth:    return Stats.MaxHealth;
            case StatType.Energy:       return Stats.Energy;
            case StatType.EnergyRegen:  return Stats.EnergyRegen;
            case StatType.Might:        return Stats.Might;
            case StatType.Intelligence: return Stats.Intelligence;
            case StatType.Armor:        return Stats.Armor;
            case StatType.Speed:        return Stats.Speed;
            case StatType.Grit:         return Stats.Grit;
            case StatType.Luck:         return Stats.Luck;
            case StatType.Growth:       return Stats.Growth;
            default:                    return null; // Return null if the type is invalid
        }
    }
}