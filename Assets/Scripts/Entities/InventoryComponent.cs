using System.Collections.Generic;
using UnityEngine;

// Add this attribute right above the class definition
[RequireComponent(typeof(Combatant))]
public class InventoryComponent : MonoBehaviour
{
    // We can make this private now, as we'll get the reference reliably
    private Combatant combatant; 
    private CharacterStats characterStats;

    public List<Item> equippedItems = new List<Item>();
    [SerializeField] private List<Item> startingItems;

    // We can go back to using Awake() for getting references, 
    // because this script now has a hard dependency on Combatant.
    void Awake()
    {
        // Get the entire Combatant component.
        combatant = GetComponent<Combatant>();
        
        // The Combatant's Stats property is still set in its own Awake().
        // We will get the reference to Stats in Start() to be safe.
    }

    void Start()
    {
        // Get the reference here, after all Awake() calls are complete.
        characterStats = combatant.Stats;

        foreach (Item item in startingItems)
        {
            Equip(item);
        }
    }

    // ... (The rest of the script remains the same)
    public void Equip(Item item)
    {
        if (equippedItems.Contains(item))
            return;

        equippedItems.Add(item);
        Debug.Log($"Equipped: {item.itemName}");

        foreach (var bonus in item.bonuses)
        {
            StatModifier mod = new StatModifier(bonus.value, bonus.type, item);
            GetStat(bonus.stat)?.AddModifier(mod);
        }
    }

    public void Unequip(Item item)
    {
        if (!equippedItems.Contains(item))
            return;

        equippedItems.Remove(item);
        Debug.Log($"Unequipped: {item.itemName}");
        
        foreach(var bonus in item.bonuses)
        {
            GetStat(bonus.stat)?.RemoveAllModifiersFromSource(item);
        }
    }
    
    private Stat GetStat(StatType type)
    {
        // Using the locally stored characterStats reference
        switch (type)
        {
            case StatType.MaxHealth:    return characterStats.MaxHealth;
            case StatType.Energy:       return characterStats.Energy;
            case StatType.EnergyRegen:  return characterStats.EnergyRegen;
            case StatType.Might:        return characterStats.Might;
            case StatType.Intelligence: return characterStats.Intelligence;
            case StatType.Armor:        return characterStats.Armor;
            case StatType.Speed:        return characterStats.Speed;
            case StatType.Grit:         return characterStats.Grit;
            case StatType.Luck:         return characterStats.Luck;
            case StatType.Growth:       return characterStats.Growth;
            default:                    return null;
        }
    }
}