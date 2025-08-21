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

    public void TakeDamage(int damage)
    {
        float armor = Stats.Armor.Value;
        float damageReduction = (armor / (armor + 150));
        int finalDamage = Mathf.RoundToInt(damage * (1 - damageReduction));

        // --- VULNERABLE LOGIC ---
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
        // --- 1. PRE-ACTION CHECKS ---
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
        
        // --- 2. CONSUME RESOURCES ---
        currentEnergy -= finalEnergyCost;
        if (isEmpowered)
        {
            Debug.Log($"<color=yellow>Empower consumed!</color>");
            RemoveStatusEffect(StatusEffectType.Empower);
        }
        OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);
        Debug.Log($"{characterSheet.name} uses {skill.skillName}! ({finalEnergyCost} EN cost)");
        
        // Put the skill on cooldown after it has been successfully used
        if (skill.cooldown > 0)
        {
            skillCooldowns[skill] = skill.cooldown;
            OnCooldownsChanged?.Invoke();
        }

        // --- 3. DETERMINE TARGET ---
        Combatant effectTarget = (skill.targetType == TargetType.Self) ? this : target;

        // --- 4. APPLY PRIMARY EFFECT (DAMAGE/HEAL) ---
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
                this.ReceiveHeal(totalHeal);
                break;
        }

        // --- 5. APPLY STATUS EFFECT (with the fix) ---
        if (skill.appliesStatusEffect)
        {
            effectTarget.ApplyStatusEffect(
                new StatusEffect(skill.effectToApply, skill.effectDuration, skill.effectClassification),
                this, // The caster (this combatant)
                skill // **THE FIX**: The source skill itself is passed directly
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
        // --- 1. GRIT RESISTANCE CHECK ---
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

        // --- 2. DOT/HOT TICK VALUE CALCULATION (with the fix) ---
        // We check if a sourceSkill was provided, as some effects (like Vulnerable from Freeze) won't have one.
        if (sourceSkill != null && (effect.Type == StatusEffectType.Burn || effect.Type == StatusEffectType.Regeneration))
        {
            // **THE FIX**: Directly use the data from the passed-in sourceSkill. No more searching!
            effect.TickValue = sourceSkill.baseDotHotValue + (int)(caster.Stats.Intelligence.Value * sourceSkill.dotHotIntelligenceRatio);
        }
        
        // --- 3. APPLY THE EFFECT ---
        activeStatusEffects.Add(effect);
        Debug.Log($"<color=lightblue>{characterSheet.name} gained {effect.Type} for {effect.Duration} turn(s).</color>");
        
        // --- 4. HANDLE SPECIAL CASE LOGIC ---
        // If Freeze lands, also apply Vulnerable
        if (effect.Type == StatusEffectType.Freeze)
        {
            var vulnerableDebuff = new StatusEffect(StatusEffectType.Vulnerable, 1, EffectClassification.Debuff);
            // We pass 'null' for the skill because Vulnerable isn't a DoT/HoT.
            ApplyStatusEffect(vulnerableDebuff, caster, null); 
        }
        
        // If Fortify lands, apply the stat modifier
        if (effect.Type == StatusEffectType.Fortify)
        {
            // The StatusEffect object itself is the source, allowing clean removal.
            StatModifier armorBuff = new StatModifier(150, StatModType.Flat, effect);
            Stats.Armor.AddModifier(armorBuff);
        }
        
        // --- 5. NOTIFY UI ---
        OnStatusEffectsChanged?.Invoke(activeStatusEffects);
    }

    public void RemoveStatusEffect(StatusEffectType type)
    {
        StatusEffect effectToRemove = activeStatusEffects.FirstOrDefault(e => e.Type == type);
        if (effectToRemove != null)
        {
            // --- FORTIFY CLEANUP ---
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
        currentHealth += healAmount;
        // Clamp health so it doesn't go over the maximum
        currentHealth = Mathf.Min(currentHealth, (int)Stats.MaxHealth.Value);

        OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
        Debug.Log($"<color=green>{characterSheet.name} is healed for {healAmount}. New HP: {currentHealth}.</color>");
    }
    
    public void ProcessDoTsAndHoTs()
    {
        // Create a copy to avoid issues if an effect is removed during processing
        var effectsToProcess = activeStatusEffects.ToList();

        foreach (var effect in effectsToProcess)
        {
            switch (effect.Type)
            {
                case StatusEffectType.Burn:
                    Debug.Log($"{characterSheet.name} is burned!");
                    TakeDamage(effect.TickValue);
                    break;
                case StatusEffectType.Regeneration:
                    Debug.Log($"{characterSheet.name} regenerates health!");
                    ReceiveHeal(effect.TickValue);
                    break;
            }
        }
    }
}