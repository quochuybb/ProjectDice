using UnityEngine;
using UnityEngine.Events; // Use events for clean communication

public class Combatant : MonoBehaviour
{
    public CharacterSheet characterSheet;
    
    // Current combat stats
    public int currentHealth;
    public int currentEnergy;

    // An event to notify UI or other systems when health changes.
    public UnityAction<int, int> OnHealthChanged;

    void Start()
    {
        currentHealth = characterSheet.maxHealth.GetValue();
        currentEnergy = characterSheet.energy.GetValue();
    }

    public void TakeDamage(int damage)
    {
        // Future: Implement armor calculation here: Dmg Reduction % = (Armor / (Armor + 150))
        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        OnHealthChanged?.Invoke(currentHealth, characterSheet.maxHealth.GetValue());

        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void UseSkill(Skill skill, Combatant target)
    {
        if(currentEnergy >= skill.energyCost)
        {
            currentEnergy -= skill.energyCost;
            
            // This is the core damage calculation from your GDD
            int totalDamage = skill.baseDamage + (int)(characterSheet.might.GetValue() * skill.mightRatio);
            
            Debug.Log($"{characterSheet.name} uses {skill.skillName} on {target.characterSheet.name} for {totalDamage} damage!");

            target.TakeDamage(totalDamage);
        }
        else
        {
            Debug.Log($"{characterSheet.name} does not have enough energy for {skill.skillName}!");
        }
    }

    private void Die()
    {
        // Future: This can trigger animations, game over sequences, etc.
        Debug.Log($"{characterSheet.name} has been defeated!");
        gameObject.SetActive(false);
    }
}