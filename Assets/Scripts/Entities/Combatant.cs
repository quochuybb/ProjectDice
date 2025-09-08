using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(InventoryComponent))]
public class Combatant : MonoBehaviour
{
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

        // --- HIT/MISS/DODGE CHECKS ---
        if (skill.targetType == TargetType.Enemy)
        {
            if (this.CheckForBlindMiss())
            {
                // Attacker missed, consume energy and end.
                ConsumeResources(finalEnergyCost, isEmpowered, skill);
                return;
            }
            if (primaryTarget.CheckForDodge())
            {
                // Defender dodged, consume energy and end.
                Debug.Log($"{this.characterSheet.name}'s attack was dodged by {primaryTarget.characterSheet.name}!");
                ConsumeResources(finalEnergyCost, isEmpowered, skill);
                return;
            }
        }
        
        // --- PROCEED WITH SKILL ---
        ConsumeResources(finalEnergyCost, isEmpowered, skill);
        
        List<Combatant> allTargets = GatherTargets(skill, primaryTarget);
        
        Debug.Log($"{characterSheet.name} uses {skill.skillName}, targeting {allTargets.Count} creature(s)!");

        // Apply effects to all targets
        bool powerUpConsumed = HasStatusEffect(StatusEffectType.PowerUp);
        bool weakenConsumed = HasStatusEffect(StatusEffectType.Weaken);

        foreach (Combatant target in allTargets)
        {
            ApplyPrimaryEffect(skill, target, powerUpConsumed, weakenConsumed);
            if (skill.appliesStatusEffect) ApplySkillStatusEffect(skill, target);
            if (skill.doesCleanse) target.CleanseDebuffs(skill.cleanseAmount);
            if (skill.doesPurge) target.PurgeBuffs(skill.purgeAmount);
        }

        // Consume one-time buffs after they have been applied to all targets
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
        List<Combatant> allTargets = new List<Combatant> { primaryTarget };

        // --- CASE 1: Area Attack ---
        if (skill.areaTargets > 1 && skill.targetType == TargetType.Enemy)
        {
            int additionalTargetsNeeded = skill.areaTargets - 1;
            List<Combatant> secondaryTargets = combatManager.GetValidEnemyTargets(primaryTarget)
                .OrderBy(e => (e.transform.position - primaryTarget.transform.position).sqrMagnitude)
                .ToList();
            
            int targetsToTake = Mathf.Min(additionalTargetsNeeded, secondaryTargets.Count);
            for (int i = 0; i < targetsToTake; i++)
            {
                allTargets.Add(secondaryTargets[i]);
            }
        }
        // --- CASE 2: Chain Attack ---
        else if (skill.chainBounces > 0 && skill.targetType == TargetType.Enemy)
        {
            // For Chain, the secondary targets are completely random, not sorted by distance.
            List<Combatant> secondaryTargets = combatManager.GetValidEnemyTargets(primaryTarget);
            
            int targetsToTake = Mathf.Min(skill.chainBounces, secondaryTargets.Count);
            
            // Randomly pick targets from the available list.
            for (int i = 0; i < targetsToTake; i++)
            {
                // If there are no more potential targets, stop.
                if (secondaryTargets.Count == 0) break;

                int randomIndex = Random.Range(0, secondaryTargets.Count);
                allTargets.Add(secondaryTargets[randomIndex]);
                
                // Remove the chosen target from the pool so it can't be hit again.
                secondaryTargets.RemoveAt(randomIndex);
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
    
    // ... [ The rest of the script (TakeDamage, ReceiveHeal, ApplyStatusEffect, etc.) is exactly as you provided, which is correct. ] ...
    #region Unchanged Methods
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
        if ((HasStatusEffect(StatusEffectType.Immunity) || HasStatusEffect(StatusEffectType.Ethereal)) && effect.Classification == EffectClassification.Debuff)
        {
            Debug.Log($"<color=yellow>{characterSheet.name} is Immune and resists the {effect.Type} debuff!</color>");
            return;
        }

        if (effect.Type == StatusEffectType.Wound)
        {
            StatusEffect existingWound = activeStatusEffects.FirstOrDefault(e => e.Type == StatusEffectType.Wound);
            if (existingWound != null)
            {
                existingWound.Stacks += effect.Stacks;
                Debug.Log($"<color=red>{characterSheet.name} gains {effect.Stacks} Wound stacks! (Total: {existingWound.Stacks})</color>");
                CheckForBleed(existingWound, caster, sourceSkill);
                OnStatusEffectsChanged?.Invoke(activeStatusEffects);
                return;
            }
            else
            {
                effect.Duration = 99;
            }
        }
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
            Stat targetStat = GetStat(effect.TargetStat);
            if (targetStat != null)
            {
                float value = (effect.Type == StatusEffectType.StatUp) ? effect.ModValue : -effect.ModValue;
                var modifier = new StatModifier(value, effect.ModType, effect);
                targetStat.AddModifier(modifier);
            }
        }
        if (sourceSkill != null && (effect.Type == StatusEffectType.Burn || effect.Type == StatusEffectType.Regeneration || effect.Type == StatusEffectType.Poison))
        {
            effect.TickValue = sourceSkill.baseDotHotValue + (int)(caster.Stats.Intelligence.Value * sourceSkill.dotHotIntelligenceRatio);
        }

        activeStatusEffects.Add(effect);

        if (effect.Type == StatusEffectType.Stun || effect.Type == StatusEffectType.Freeze)
        {
            effect.IsNewlyApplied = false;
        }
        Debug.Log($"<color=lightblue>{characterSheet.name} gained {effect.Type} for {effect.Duration} turn(s).</color>");
        if (effect.Type == StatusEffectType.Freeze)
        {
            var vulnerableDebuff = new StatusEffect(StatusEffectType.Vulnerable, 1, EffectClassification.Debuff);
            ApplyStatusEffect(vulnerableDebuff, caster, null);
        }
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
    #endregion
}