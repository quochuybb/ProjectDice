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
        // Add a check to prevent using a skill on cooldown (backend safety)
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

        // --- All checks passed, proceed to use the skill ---
        
        currentEnergy -= finalEnergyCost;
        if (isEmpowered)
        {
            Debug.Log($"<color=yellow>Empower consumed!</color>");
            RemoveStatusEffect(StatusEffectType.Empower);
        }
        OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);
        Debug.Log($"{characterSheet.name} uses {skill.skillName}! ({finalEnergyCost} EN cost)");
        
        // --- PUT THE SKILL ON COOLDOWN ---
        if (skill.cooldown > 0)
        {
            skillCooldowns[skill] = skill.cooldown + 1;
            OnCooldownsChanged?.Invoke(); // Notify UI that cooldowns have changed
        }

        // Determine the target for effects
        Combatant effectTarget = (skill.targetType == TargetType.Self) ? this : target;

        // Apply skill's primary effect (Damage/Heal)
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

        // Apply skill's status effect, if any
        if (skill.appliesStatusEffect)
        {
            // --- PASS THE NEW PARAMETER ---
            effectTarget.ApplyStatusEffect(new StatusEffect(skill.effectToApply, skill.effectDuration, skill.effectClassification));
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

        if(changed) OnCooldownsChanged?.Invoke();
    }

    // --- NEW METHODS FOR STATUS EFFECT MANAGEMENT ---
    public bool HasStatusEffect(StatusEffectType type)
    {
        return activeStatusEffects.Any(effect => effect.Type == type);
    }

    public void ApplyStatusEffect(StatusEffect effect)
    {
        // The rest of this method is the same, just the signature changed
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
}