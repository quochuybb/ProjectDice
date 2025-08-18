using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Items/Item")]
public class Item : ScriptableObject
{
    [Header("Info")]
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Stat Bonuses")]
    public List<ItemStatBonus> bonuses;

    // This is a helper class that lets us define bonuses in the Inspector
    [System.Serializable]
    public class ItemStatBonus
    {
        public StatType stat;
        public StatModType type;
        
        // For percentages, 10% should be entered as 0.1
        [Tooltip("For percentages, enter the value as a decimal (e.g., 10% = 0.1)")]
        public float value;
    }
}