using UnityEngine;
using UnityEngine.Events;

public class Combatant : MonoBehaviour
{
    public CharacterSheet characterSheet;
    public CharacterStats Stats { get; private set; }

    public int currentHealth;
    public int currentEnergy;

    public UnityAction<int, int> OnHealthChanged;

    void Awake()
    {
        // Create an instance of the stats based on the character sheet
        Stats = new CharacterStats(characterSheet);
    }

    void Start()
    {
        currentHealth = (int)Stats.MaxHealth.Value;
        currentEnergy = (int)Stats.Energy.Value;
    }

    public void TakeDamage(int damage)
    {
        // GDD Formula: Damage Reduction % = (Armor / (Armor + 150)) * 100
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
        if(currentEnergy >= skill.energyCost)
        {
            currentEnergy -= skill.energyCost;
            
            // Now uses the calculated 'Value' from the Stat object
            int totalDamage = skill.baseDamage + (int)(Stats.Might.Value * skill.mightRatio);
            
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
        Debug.Log($"{characterSheet.name} has been defeated!");
        gameObject.SetActive(false);
    }
}