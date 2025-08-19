using UnityEngine;
using UnityEngine.Events;

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
        if (currentEnergy < skill.energyCost)
        {
            Debug.Log($"<color=orange>{characterSheet.name} does not have enough energy for {skill.skillName}!</color>");
            return; // Exit the method early if not enough energy
        }
        
        currentEnergy -= skill.energyCost;
        OnEnergyChanged?.Invoke(currentEnergy, (int)Stats.Energy.Value);
        Debug.Log($"{characterSheet.name} uses {skill.skillName}! ({skill.energyCost} EN cost)");

        // --- NEW LOGIC USING A SWITCH STATEMENT ---
        switch (skill.effectType)
        {
            case SkillEffectType.Damage:
                // This assumes damage skills always target an enemy.
                int totalDamage = skill.baseDamage + (int)(Stats.Might.Value * skill.mightRatio);
                Debug.Log($"It deals {totalDamage} damage to {target.characterSheet.name}.");
                target.TakeDamage(totalDamage);
                break;

            case SkillEffectType.Healing:
                // This assumes healing skills always target self.
                int totalHeal = skill.baseHeal + (int)(Stats.Intelligence.Value * skill.intelligenceRatio);
                this.ReceiveHeal(totalHeal); // 'this' refers to the combatant using the skill
                break;
        }
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