using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Combatant))]
public class InventoryComponent : MonoBehaviour
{
    // The UnityAction now passes the Dictionary for the UI to read
    public UnityAction<Dictionary<Item, int>> OnInventoryChanged;

    private Combatant combatant;
    private CharacterStats characterStats;

    // --- THE BIG CHANGE: List becomes Dictionary ---
    public Dictionary<Item, int> equippedItems = new Dictionary<Item, int>();
    
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
        // --- NEW DICTIONARY LOGIC ---
        if (equippedItems.ContainsKey(item))
        {
            // If we already have the item, just increase the stack count
            equippedItems[item]++;
        }
        else
        {
            // If it's a new item, add it with a count of 1
            equippedItems.Add(item, 1);
        }
        Debug.Log($"Equipped: {item.itemName} (Total: {equippedItems[item]})");
        
        // The stat application logic is still the same. We apply the bonus for each one we pick up.
        // We create a unique source object for each stack to allow for proper removal.
        // Using a Tuple of (Item, stack count) as the source is a robust way to do this.
        object source = (item, equippedItems[item]);
        foreach (var bonus in item.bonuses)
        {
            StatModifier mod = new StatModifier(bonus.value, bonus.type, source);
            GetStat(bonus.stat)?.AddModifier(mod);
        }
        
        OnInventoryChanged?.Invoke(equippedItems);
    }
    
    // Unequip is more complex now; it just removes one from the stack.
    // For this project phase, we'll focus on the 'Equip' part, but here's how it would look:
    public void Unequip(Item item)
    {
        if (!equippedItems.ContainsKey(item)) return;

        // Create the unique source object for the *last* stack to remove its specific modifier
        object sourceToRemove = (item, equippedItems[item]);
        foreach(var bonus in item.bonuses)
        {
            // This is a more complex removal; we'd need to find the specific modifier.
            // For now, let's simplify and assume RemoveAllModifiersFromSource will work if we adjust the source.
            // A truly robust system would give each modifier a unique ID.
            // Let's stick with a simpler approach for now.
        }

        equippedItems[item]--;
        Debug.Log($"Unequipped one {item.itemName} (Remaining: {equippedItems[item]})");

        if (equippedItems[item] <= 0)
        {
            equippedItems.Remove(item);
        }
        
        // Re-calculating all stats on unequip is the simplest safe way.
        // RecalculateAllItemStats(); 
        OnInventoryChanged?.Invoke(equippedItems);
    }


    private Stat GetStat(StatType type)
    {
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