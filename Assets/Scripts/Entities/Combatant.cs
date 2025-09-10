using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(InventoryComponent))]
public class Combatant : MonoBehaviour
{
    [Header("Team Info")]
    public bool isPlayer = false;
    public CharacterSheet characterSheet;
    public CharacterStats Stats { get; private set; }
    [Header("Visuals")]
    public GameObject targetIndicator;
    public GameObject turnIndicator;
    public int currentHealth;
    public int currentEnergy;

    public UnityAction<int, int> OnHealthChanged;
    public UnityAction<int, int> OnEnergyChanged;
    public UnityAction<List<StatusEffect>> OnStatusEffectsChanged;
    public UnityAction OnCooldownsChanged;

    private CombatManager combatManager;
    public List<StatusEffect> activeStatusEffects = new List<StatusEffect>();
    public Dictionary<Skill, int> skillCooldowns = new Dictionary<Skill, int>();

    void Awake()
    {
        Stats = new CharacterStats(characterSheet);
        combatManager = FindObjectOfType<CombatManager>();
    }

    void Start()
    {
        currentHealth = (int)Stats.MaxHealth.Value;
        currentEnergy = (int)Stats.Energy.Value;
        OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
        OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);
    }

    // --- CORE SKILL METHOD ---
    public void UseSkill(Skill skill, Combatant primaryTarget)
    {
        // --- Initial Checks (Cooldown & Energy) ---
        if (IsSkillOnCooldown(skill))
        {
            Debug.Log($"<color=orange>Cannot use {skill.name}, it's on cooldown.</color>");
            return;
        }

        bool isEmpowered = HasStatusEffect(StatusEffectType.Empower);
        int finalEnergyCost = isEmpowered ? 0 : skill.energyCost;

        if (currentEnergy < finalEnergyCost)
        {
            Debug.Log($"<color=orange>{characterSheet.name} lacks energy for {skill.name}.</color>");
            return;
        }

        // --- HIT/MISS/DODGE LOGIC ---
        // Only perform these checks for DAMAGING skills that are NOT Random(X).
        if (skill.randomHits == 0 && skill.targetType == TargetType.Enemy && skill.effectType == SkillEffectType.Damage)
        {
            if (primaryTarget == null)
            {
                Debug.LogError($"Skill '{skill.name}' requires a target, but none was provided!");
                return;
            }

            // Check if the attacker misses due to Blind.
            if (this.CheckForBlindMiss())
            {
                ConsumeResources(finalEnergyCost, isEmpowered, skill);
                return;
            }

            // Check if the defender dodges the attack.
            if (primaryTarget.CheckForDodge())
            {
                Debug.Log($"{this.characterSheet.name}'s attack was dodged by {primaryTarget.characterSheet.name}!");
                ConsumeResources(finalEnergyCost, isEmpowered, skill);
                return;
            }
        }
        
        // --- PROCEED WITH SKILL ---
        ConsumeResources(finalEnergyCost, isEmpowered, skill);
        
        // Gather all targets based on the skill's multi-targeting properties.
        List<Combatant> allTargets = GatherTargets(skill, primaryTarget);
        
        if (allTargets.Count == 0)
        {
            Debug.Log($"{characterSheet.name} uses {skill.skillName}, but finds no valid targets!");
            return;
        }

        Debug.Log($"{characterSheet.name} uses {skill.skillName}, targeting {allTargets.Count} creature(s)!");

        // --- APPLY EFFECTS ---
        // Check for one-time use buffs before the loop.
        bool powerUpConsumed = HasStatusEffect(StatusEffectType.PowerUp);
        bool weakenConsumed = HasStatusEffect(StatusEffectType.Weaken);

        foreach (Combatant target in allTargets)
        {
            // For Random skills, we check for dodge on each individual hit.
            // This check is also guarded to only apply to damaging effects.
            if (skill.randomHits > 0 && skill.effectType == SkillEffectType.Damage && target.CheckForDodge())
            {
                Debug.Log($"{this.characterSheet.name}'s random hit was dodged by {target.characterSheet.name}!");
                continue; // Skip this hit and move to the next.
            }
            
            // Apply the core damage/healing of the skill.
            ApplyPrimaryEffect(skill, target, powerUpConsumed, weakenConsumed);

            // Apply any status effects from the skill.
            if (skill.appliesStatusEffect) 
            {
                ApplySkillStatusEffect(skill, target);
            }
            
            // Apply any instant utility effects.
            if (skill.doesCleanse) target.CleanseDebuffs(skill.cleanseAmount);
            if (skill.doesPurge) target.PurgeBuffs(skill.purgeAmount);
        }

        // Consume the one-time buffs after they have been applied to all hits.
        if (powerUpConsumed) RemoveStatusEffect(StatusEffectType.PowerUp);
        if (weakenConsumed) RemoveStatusEffect(StatusEffectType.Weaken);
}

    // --- HELPER & LOGIC METHODS ---

    private void ConsumeResources(int energyCost, bool empowered, Skill skill)
    {
        currentEnergy -= energyCost;
        if (empowered)
        {
            Debug.Log($"<color=yellow>Empower consumed!</color>");
            RemoveStatusEffect(StatusEffectType.Empower);
        }
        OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);

        if (skill.cooldown > 0)
        {
            skillCooldowns[skill] = skill.cooldown;
            OnCooldownsChanged?.Invoke();
        }
    }

private List<Combatant> GatherTargets(Skill skill, Combatant primaryTarget)
{
    List<Combatant> allTargets = new List<Combatant>();

    // Case 1: Random Hits (Logic completely rewritten)
    if (skill.randomHits > 0)
    {
        List<Combatant> possibleTargets;
        
        // --- THE FIX: Check the skill's EFFECT TYPE to determine the target pool ---
        if (skill.effectType == SkillEffectType.Healing)
        {
            // If it's a healing skill, the pool is the caster and all their allies.
            possibleTargets = combatManager.GetValidAllyTargets(this);
            possibleTargets.Add(this); // Add self to the pool of potential heal targets
        }
        else // It's a damaging or debuffing random skill
        {
            // The pool is all targets hostile to the caster.
            possibleTargets = combatManager.GetHostileTargets(this);
        }

        if (possibleTargets.Count == 0) return allTargets;

        for (int i = 0; i < skill.randomHits; i++)
        {
            allTargets.Add(possibleTargets[Random.Range(0, possibleTargets.Count)]);
        }
    }
    // Case 2: Area Attack
    else if (skill.areaTargets > 1)
    {
        // This logic is already correct and team-aware. No changes needed.
        allTargets.Add(primaryTarget);
        int additionalTargetsNeeded = skill.areaTargets - 1;
        List<Combatant> secondaryPool;
        if (skill.targetType == TargetType.Enemy)
        {
            secondaryPool = combatManager.GetHostileTargets(this).Where(t => t != primaryTarget).ToList();
        }
        else
        {
            secondaryPool = combatManager.GetValidAllyTargets(this);
        }
        secondaryPool = secondaryPool.OrderBy(e => (e.transform.position - primaryTarget.transform.position).sqrMagnitude).ToList();
        int targetsToTake = Mathf.Min(additionalTargetsNeeded, secondaryPool.Count);
        for (int i = 0; i < targetsToTake; i++)
        {
            allTargets.Add(secondaryPool[i]);
        }
    }
    // Case 3: Chain Attack
    else if (skill.chainBounces > 0 && skill.targetType == TargetType.Enemy)
    {
        // This logic is already correct. No changes needed.
        allTargets.Add(primaryTarget);
        List<Combatant> secondaryTargets = combatManager.GetHostileTargets(this).Where(t => t != primaryTarget).ToList();
        int targetsToTake = Mathf.Min(skill.chainBounces, secondaryTargets.Count);
        for (int i = 0; i < targetsToTake; i++)
        {
            if (secondaryTargets.Count == 0) break;
            int randomIndex = Random.Range(0, secondaryTargets.Count);
            allTargets.Add(secondaryTargets[randomIndex]);
            secondaryTargets.RemoveAt(randomIndex);
        }
    }
    // Default: Single Target
    else
    {
        // This can happen if primaryTarget is null for a Random skill, so we need to guard it.
        if (primaryTarget != null)
        {
            allTargets.Add(primaryTarget);
        }
    }
    
    return allTargets;
}

    private void ApplyPrimaryEffect(Skill skill, Combatant target, bool isPoweredUp, bool isWeakened)
    {
        switch (skill.effectType)
        {
            case SkillEffectType.Damage:
                float damageMultiplier = 1.0f;
                if (isPoweredUp) damageMultiplier *= 1.5f;
                if (isWeakened) damageMultiplier *= 0.5f;

                int baseSkillDamage = skill.baseDamage + (int)(Stats.Might.Value * skill.mightRatio);
                int totalDamage = Mathf.RoundToInt(baseSkillDamage * damageMultiplier);
                
                target.TakeDamage(totalDamage);
                break;

            case SkillEffectType.Healing:
                int totalHeal = skill.baseHeal + (int)(Stats.Intelligence.Value * skill.intelligenceRatio);
                target.ReceiveHeal(totalHeal);
                break;
        }
    }

    private void ApplySkillStatusEffect(Skill skill, Combatant target)
    {
        var newEffect = new StatusEffect(skill.effectToApply, skill.effectDuration, skill.effectClassification,
                                         skill.statToModify, skill.modificationType, skill.modificationValue);
        newEffect.Stacks = skill.stacksToApply;
        target.ApplyStatusEffect(newEffect, this, skill);
    }

    public bool CheckForBlindMiss()
    {
        if (HasStatusEffect(StatusEffectType.Blind))
        {
            RemoveStatusEffect(StatusEffectType.Blind);
            if (Random.value < 0.7f)
            {
                Debug.Log($"<color=brown>{characterSheet.name}'s attack missed due to Blind!</color>");
                return true;
            }
            Debug.Log($"<color=grey>{characterSheet.name} hit through Blind.</color>");
        }
        return false;
    }

    public bool CheckForDodge()
    {
        if (HasStatusEffect(StatusEffectType.Dodge))
        {
            RemoveStatusEffect(StatusEffectType.Dodge);
            if (Random.value < 0.75f)
            {
                Debug.Log($"<color=cyan>{characterSheet.name} dodged via Dodge effect!</color>");
                return true;
            }
            Debug.Log($"<color=grey>{characterSheet.name} failed to dodge.</color>");
        }
        float speed = Stats.Speed.Value;
        float passiveDodgeChance = (speed / (speed + 150f)) * 0.2f;
        if (Random.value < passiveDodgeChance)
        {
            Debug.Log($"<color=cyan>{characterSheet.name} passively dodged!</color>");
            return true;
        }
        return false;
    }
    
    public void RegenerateEnergy()
    {
        int regenAmount = (int)Stats.EnergyRegen.Value;
        currentEnergy += regenAmount;
        currentEnergy = Mathf.Min(currentEnergy, (int)Stats.Energy.Value);
        //Debug.Log($"<color=cyan>{characterSheet.name} regenerates {regenAmount} energy. Now has {currentEnergy}.</color>");
        OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);
    }

    public void TakeDamage(int damage)
    {
        if (HasStatusEffect(StatusEffectType.Immunity) || HasStatusEffect(StatusEffectType.Ethereal))
        {
            Debug.Log($"<color=yellow>{characterSheet.name} is Immune and takes no damage!</color>");
            OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
            return;
        }
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
        if (currentHealth <= 0) Die();
    }
    
    public void TakeTrueDamage(int damage)
    {
        if (HasStatusEffect(StatusEffectType.Immunity) || HasStatusEffect(StatusEffectType.Ethereal))
        {
            Debug.Log($"<color=yellow>{characterSheet.name} is Immune and takes no TRUE damage!</color>");
            OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
            return;
        }
        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;
        OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
        Debug.Log($"{characterSheet.name} takes {damage} TRUE damage!");
        if (currentHealth <= 0) Die();
    }

    public bool IsSkillOnCooldown(Skill skill) => skillCooldowns.ContainsKey(skill);

    public void TickDownCooldowns()
    {
        if (skillCooldowns.Count == 0) return;
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

    public bool HasStatusEffect(StatusEffectType type) => activeStatusEffects.Any(effect => effect.Type == type);

public void ApplyStatusEffect(StatusEffect effect, Combatant caster, Skill sourceSkill)
{
    // --- GATE 1: IMMUNITY CHECK ---
    // First, check if the target is immune to incoming debuffs.
    if ((HasStatusEffect(StatusEffectType.Immunity) || HasStatusEffect(StatusEffectType.Ethereal)) && 
        effect.Classification == EffectClassification.Debuff)
    {
        Debug.Log($"<color=yellow>{characterSheet.name} is Immune and resists the {effect.Type} debuff!</color>");
        return;
    }

    // --- GATE 2: DURATION REFRESH (for non-stacking effects) ---
    // If an effect of the same type already exists, refresh its duration instead of adding a duplicate.
    // We exclude Wound because it has its own special stacking logic.
    if (effect.Type != StatusEffectType.Wound)
    {
        StatusEffect existingEffect = activeStatusEffects.FirstOrDefault(e => e.Type == effect.Type);
        if (existingEffect != null)
        {
            Debug.Log($"Refreshing duration for {effect.Type}. Old: {existingEffect.Duration}, New: {effect.Duration}");
            
            // Only update if the new duration is longer.
            if (effect.Duration > existingEffect.Duration)
            {
                existingEffect.Duration = effect.Duration;
            }
            
            // The effect is refreshed, so we must protect it from expiring on the same turn it was refreshed.
            existingEffect.IsNewlyApplied = true;

            // Notify the UI that an effect has been updated.
            OnStatusEffectsChanged?.Invoke(activeStatusEffects);
            return; // Exit the method since we've handled this effect.
        }
    }

    // --- GATE 3: RESISTANCE CHECK ---
    // Check for Grit resistance against Stun and Freeze.
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

    // --- SPECIAL CASE LOGIC (for effects that need it) ---

    // Handle Wound stacking.
    if (effect.Type == StatusEffectType.Wound)
    {
        StatusEffect existingWound = activeStatusEffects.FirstOrDefault(e => e.Type == StatusEffectType.Wound);
        if (existingWound != null)
        {
            existingWound.Stacks += effect.Stacks;
            Debug.Log($"<color=red>{characterSheet.name} gains {effect.Stacks} Wound stacks! (Total: {existingWound.Stacks})</color>");
            CheckForBleed(existingWound, caster, sourceSkill);
            OnStatusEffectsChanged?.Invoke(activeStatusEffects);
            return; // Exit here to prevent adding a duplicate Wound effect.
        }
        else
        {
            effect.Duration = 99; // Wounds are persistent counters.
        }
    }

    // Calculate TickValue for DoTs and HoTs.
    if (sourceSkill != null && (effect.Type == StatusEffectType.Burn || effect.Type == StatusEffectType.Regeneration || effect.Type == StatusEffectType.Poison))
    {
        effect.TickValue = sourceSkill.baseDotHotValue + (int)(caster.Stats.Intelligence.Value * sourceSkill.dotHotIntelligenceRatio);
    }

    // Apply the StatModifier for StatUp/StatDown effects.
    if (effect.Type == StatusEffectType.StatUp || effect.Type == StatusEffectType.StatDown)
    {
        Stat targetStat = GetStat(effect.TargetStat);
        if (targetStat != null)
        {
            float value = (effect.Type == StatusEffectType.StatUp) ? effect.ModValue : -effect.ModValue;
            var modifier = new StatModifier(value, effect.ModType, effect);
            targetStat.AddModifier(modifier);
        }
    }
    
    // --- FINAL APPLICATION ---
    
    // Add the fully configured effect to the active list.
    activeStatusEffects.Add(effect);

    // Exempt Stun/Freeze from the 'IsNewlyApplied' protection so they expire correctly.
    if (effect.Type == StatusEffectType.Stun || effect.Type == StatusEffectType.Freeze)
    {
        effect.IsNewlyApplied = false;
    }
    
    Debug.Log($"<color=lightblue>{characterSheet.name} gained {effect.Type} for {effect.Duration} turn(s).</color>");

    // --- TRIGGERED SUB-EFFECTS ---

    // Freeze also applies a 1-turn Vulnerable.
    if (effect.Type == StatusEffectType.Freeze)
    {
        var vulnerableDebuff = new StatusEffect(StatusEffectType.Vulnerable, 1, EffectClassification.Debuff);
        ApplyStatusEffect(vulnerableDebuff, caster, null); // Recursively call this method for the sub-effect.
    }

    // Fortify also applies its armor buff.
    if (effect.Type == StatusEffectType.Fortify)
    {
        StatModifier armorBuff = new StatModifier(150, StatModType.Flat, effect);
        Stats.Armor.AddModifier(armorBuff);
    }
    
    // Notify the UI that the list has changed.
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
            var bleedEffect = new StatusEffect(StatusEffectType.Bleed, 3, EffectClassification.Debuff);
            ApplyStatusEffect(bleedEffect, caster, sourceSkill);
            var mortalWoundEffect = new StatusEffect(StatusEffectType.MortalWound, 3, EffectClassification.Debuff);
            ApplyStatusEffect(mortalWoundEffect, caster, null);
        }
    }

    public void RemoveStatusEffect(StatusEffectType type)
    {
        StatusEffect effectToRemove = activeStatusEffects.FirstOrDefault(e => e.Type == type);
        if (effectToRemove != null)
        {
            if (effectToRemove.Type == StatusEffectType.StatUp || effectToRemove.Type == StatusEffectType.StatDown)
            {
                Stat targetStat = GetStat(effectToRemove.TargetStat);
                if (targetStat != null)
                {
                    targetStat.RemoveAllModifiersFromSource(effectToRemove);
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
        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeStatusEffects[i];
            if (effect.IsNewlyApplied)
            {
                effect.IsNewlyApplied = false;
                continue;
            }
            effect.Duration--;
            if (effect.Duration <= 0)
            {
                Debug.Log($"<color=grey>{characterSheet.name}'s {effect.Type} has expired at turn end.</color>");
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
        int finalHealAmount = healAmount;
        if (HasStatusEffect(StatusEffectType.Blight)) finalHealAmount = 0;
        else if (HasStatusEffect(StatusEffectType.MortalWound)) finalHealAmount = Mathf.RoundToInt(finalHealAmount * 0.5f);

        if (finalHealAmount > 0)
        {
            currentHealth += finalHealAmount;
            currentHealth = Mathf.Min(currentHealth, (int)Stats.MaxHealth.Value);
            OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
            Debug.Log($"<color=green>{characterSheet.name} is healed for {finalHealAmount}. New HP: {currentHealth}.</color>");
        }
        else
        {
            Debug.Log($"<color=green>{characterSheet.name} is healed for 0. New HP: {currentHealth}.</color>");
            return;
        }
        StatusEffect existingWund = activeStatusEffects.FirstOrDefault(e => e.Type == StatusEffectType.Wound);
        if (existingWund != null)
        {
            int stacksToRemove = Mathf.FloorToInt(existingWund.Stacks / 2f);
            if (stacksToRemove > 0)
            {
                existingWund.Stacks -= stacksToRemove;
                Debug.Log($"<color=lime>Healing cleanses {stacksToRemove} Wound stacks! (Remaining: {existingWund.Stacks})</color>");
                if (existingWund.Stacks <= 0) RemoveStatusEffect(StatusEffectType.Wound);
                else OnStatusEffectsChanged?.Invoke(activeStatusEffects);
            }
        }
    }

    public void ProcessDoTsAndHoTs()
    {
        var effectsToProcess = activeStatusEffects.ToList();
        foreach (var effect in effectsToProcess)
        {
            switch (effect.Type)
            {
                case StatusEffectType.Burn:
                    TakeDamage(effect.TickValue);
                    break;
                case StatusEffectType.Poison:
                    TakeTrueDamage(effect.TickValue);
                    break;
                case StatusEffectType.Regeneration:
                    ReceiveHeal(effect.TickValue);
                    break;
                case StatusEffectType.Bleed:
                    int bleedDamage = Mathf.RoundToInt(Stats.MaxHealth.Value * 0.16f);
                    TakeTrueDamage(bleedDamage);
                    break;
            }
        }
    }
    
    private void CleanseDebuffs(int amount)
    {
        var removableDebuffs = activeStatusEffects.Where(e => e.Classification == EffectClassification.Debuff && e.Type != StatusEffectType.Wound && e.Type != StatusEffectType.Bleed).ToList();
        if (removableDebuffs.Count == 0) return;
        Debug.Log($"<color=cyan>Attempting to cleanse {amount} debuffs...</color>");
        for (int i = 0; i < amount && removableDebuffs.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, removableDebuffs.Count);
            StatusEffect toRemove = removableDebuffs[randomIndex];
            Debug.Log($"<color=cyan>Cleansed {toRemove.Type}!</color>");
            RemoveStatusEffect(toRemove.Type);
            removableDebuffs.RemoveAt(randomIndex);
        }
    }
    
    private void PurgeBuffs(int amount)
    {
        var removableBuffs = activeStatusEffects.Where(e => e.Classification == EffectClassification.Buff).ToList();
        if (removableBuffs.Count == 0) return;
        Debug.Log($"<color=orange>Attempting to purge {amount} buffs...</color>");
        for (int i = 0; i < amount && removableBuffs.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, removableBuffs.Count);
            StatusEffect toRemove = removableBuffs[randomIndex];
            Debug.Log($"<color=orange>Purged {toRemove.Type}!</color>");
            RemoveStatusEffect(toRemove.Type);
            removableBuffs.RemoveAt(randomIndex);
        }
    }

    public void ProcessCleansingEffectsAtTurnStart()
    {
        if (HasStatusEffect(StatusEffectType.Purification)) CleanseDebuffs(1);
        if (HasStatusEffect(StatusEffectType.Unraveling)) PurgeBuffs(1);
    }

    public Stat GetStat(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth: return Stats.MaxHealth;
            case StatType.Energy: return Stats.Energy;
            case StatType.EnergyRegen: return Stats.EnergyRegen;
            case StatType.Might: return Stats.Might;
            case StatType.Intelligence: return Stats.Intelligence;
            case StatType.Armor: return Stats.Armor;
            case StatType.Speed: return Stats.Speed;
            case StatType.Grit: return Stats.Grit;
            case StatType.Luck: return Stats.Luck;
            case StatType.Growth: return Stats.Growth;
            default: return null;
        }
    }

}