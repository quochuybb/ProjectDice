using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events; // Make sure to add this!

[RequireComponent(typeof(Combatant))]
public class InventoryComponent : MonoBehaviour
{
    // --- ADD THIS EVENT ---
    public UnityAction<List<Item>> OnInventoryChanged;

    private Combatant combatant;
    private CharacterStats characterStats;

    public List<Item> equippedItems = new List<Item>();
    [SerializeField] private List<Item> startingItems;

    void Awake()
    {
        combatant = GetComponent<Combatant>();
    }

    void Start()
    {
        characterStats = combatant.Stats;
        foreach (Item item in startingItems)
        {
            Equip(item);
        }
    }

    public void Equip(Item item)
    {
        if (equippedItems.Contains(item)) return;
        equippedItems.Add(item);
        
        foreach (var bonus in item.bonuses)
        {
            StatModifier mod = new StatModifier(bonus.value, bonus.type, item);
            GetStat(bonus.stat)?.AddModifier(mod);
        }
        
        // --- INVOKE THE EVENT ---
        OnInventoryChanged?.Invoke(equippedItems);
        Debug.Log($"Equipped: {item.itemName}");
    }

    public void Unequip(Item item)
    {
        if (!equippedItems.Contains(item)) return;
        equippedItems.Remove(item);
        
        foreach(var bonus in item.bonuses)
        {
            GetStat(bonus.stat)?.RemoveAllModifiersFromSource(item);
        }

        // --- INVOKE THE EVENT ---
        OnInventoryChanged?.Invoke(equippedItems);
        Debug.Log($"Unequipped: {item.itemName}");
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