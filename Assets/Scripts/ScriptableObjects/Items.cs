using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Items/Item")]
public class Item : ScriptableObject
{
    [Header("Info")]
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    
    // --- ADD THIS LINE ---
    public Rarity rarity;

    [Header("Stat Bonuses")]
    public List<ItemStatBonus> bonuses;

    [System.Serializable]
    public class ItemStatBonus
    {
        public StatType stat;
        public StatModType type;
        [Tooltip("For percentages, enter the value as a decimal (e.g., 10% = 0.1)")]
        public float value;
    }
}