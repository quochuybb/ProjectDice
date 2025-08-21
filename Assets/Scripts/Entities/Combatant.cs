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

        currentHealth -= finalDamage;
        if (currentHealth < 0) currentHealth = 0;

        OnHealthChanged?.Invoke(currentHealth, (int)Stats.MaxHealth.Value);
        Debug.Log($"{characterSheet.name} takes {finalDamage} damage after {damageReduction * 100:F0}% reduction.");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void UseSkill(Skill skill, Combatant target)
    {
        // --- Phase 1: Pre-checks (Guard Clauses) ---
        // These checks will exit the method early if the skill cannot be used.

        // Check 1: Is the skill on cooldown?
        if (IsSkillOnCooldown(skill))
        {
            Debug.Log($"<color=orange>Cannot use {skill.skillName}, it is on cooldown!</color>");
            return;
        }

        // Check 2: Does the combatant have enough energy? (Factoring in Empower)
        bool isEmpowered = HasStatusEffect(StatusEffectType.Empower);
        int finalEnergyCost = isEmpowered ? 0 : skill.energyCost;

        if (currentEnergy < finalEnergyCost)
        {
            Debug.Log($"<color=orange>{characterSheet.name} does not have enough energy for {skill.skillName}!</color>");
            return;
        }


        // --- Phase 2: Commit to Action (Pay the Costs) ---
        // If we reach this point, the skill will definitely be used.

        // Pay the energy cost and consume Empower if it was used.
        currentEnergy -= finalEnergyCost;
        if (isEmpowered)
        {
            Debug.Log($"<color=yellow>Empower consumed!</color>");
            RemoveStatusEffect(StatusEffectType.Empower);
        }
        // Notify the UI that energy has changed.
        OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);

        // Put the skill on cooldown if it has one.
        if (skill.cooldown > 0)
        {
            skillCooldowns[skill] = skill.cooldown;
            // Notify the UI that cooldowns have changed.
            OnCooldownsChanged?.Invoke();
        }


        // --- Phase 3: Apply the Effects ---

        Debug.Log($"{characterSheet.name} uses {skill.skillName}! ({finalEnergyCost} EN cost)");

        // Determine the correct target for the skill's effects.
        Combatant effectTarget = (skill.targetType == TargetType.Self) ? this : target;

        // Apply the skill's primary effect (Damage or Healing).
        switch (skill.effectType)
        {
            case SkillEffectType.Damage:
                int totalDamage = skill.baseDamage + (int)(Stats.Might.Value * skill.mightRatio);
                effectTarget.TakeDamage(totalDamage);
                break;

            case SkillEffectType.Healing:
                int totalHeal = skill.baseHeal + (int)(Stats.Intelligence.Value * skill.intelligenceRatio);
                effectTarget.ReceiveHeal(totalHeal);
                break;
        }

        // Apply the skill's status effect, if it has one.
        if (skill.appliesStatusEffect)
        {
            // Create the new status effect object.
            var newEffect = new StatusEffect(skill.effectToApply, skill.effectDuration, skill.effectClassification);

            // Apply the effect to the target, passing 'this' (the current Combatant) as the caster.
            // This is crucial for calculating DoT/HoT damage based on the caster's stats.
            effectTarget.ApplyStatusEffect(newEffect, this);
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

    public void ApplyStatusEffect(StatusEffect effect, Combatant caster)
    {
        // --- CALCULATE AND STORE THE TICK VALUE ---
        if (effect.Type == StatusEffectType.Burn || effect.Type == StatusEffectType.Regeneration)
        {
            // We need the skill that applied this, but we can assume for now that only one skill applies one effect type
            // A more robust system might pass the skill itself, but this works for now.
            // Let's find the skill from the caster's sheet that applies this effect.
            Skill sourceSkill = caster.characterSheet.startingSkills.FirstOrDefault(s => s.effectToApply == effect.Type);
            if (sourceSkill != null)
            {
                effect.TickValue = sourceSkill.baseDotHotValue + (int)(caster.Stats.Intelligence.Value * sourceSkill.dotHotIntelligenceRatio);
            }
        }
        
        activeStatusEffects.Add(effect);
        Debug.Log($"<color=lightblue>{characterSheet.name} gained {effect.Type} for {effect.Duration} turn(s).</color>");
        
        if (effect.Type == StatusEffectType.Fortify)
        {
            StatModifier armorBuff = new StatModifier(150, StatModType.Flat, effect);
            Stats.Armor.AddModifier(armorBuff);
        }
        
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

    public void TickDownDebuffsAtTurnStart()
    {
        // Iterate backwards to safely remove items from the list while looping
        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeStatusEffects[i];
            if (effect.Classification == EffectClassification.Debuff)
            {
                effect.Duration--;
                if (effect.Duration <= 0)
                {
                    Debug.Log($"<color=grey>{characterSheet.name}'s {effect.Type} debuff has expired at turn start.</color>");
                    RemoveStatusEffect(effect.Type);
                }
            }
        }
        OnStatusEffectsChanged?.Invoke(activeStatusEffects);
    }

    public void TickDownBuffsAtTurnEnd()
    {
        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeStatusEffects[i];

            if (effect.Classification == EffectClassification.Buff)
            {
                // --- THE CRUCIAL NEW LOGIC ---
                if (effect.IsNewlyApplied)
                {
                    // This buff was just applied this turn.
                    // Don't tick its duration down. Just unset the flag.
                    effect.IsNewlyApplied = false;
                    continue; // Skip to the next effect in the list
                }
                // --- END OF NEW LOGIC ---

                effect.Duration--;

                if (effect.Duration <= 0)
                {
                    Debug.Log($"<color=grey>{characterSheet.name}'s {effect.Type} buff has expired at turn end.</color>");
                    RemoveStatusEffect(effect.Type);
                }
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