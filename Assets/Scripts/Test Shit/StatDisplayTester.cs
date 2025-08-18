using UnityEngine;

public class StatDisplayTester : MonoBehaviour
{
    void Start()
    {
        // Give the InventoryComponent a frame to run its own Start() method and equip items
        Invoke(nameof(DisplayStats), 0.1f); 
    }

    void DisplayStats()
    {
        var combatant = GetComponent<Combatant>();
        if (combatant == null) return;

        var stats = combatant.Stats;
        Debug.Log("--- STATS AFTER EQUIPPING ---");
        Debug.Log($"HEALTH: Base = {stats.MaxHealth.baseValue}, Final = {stats.MaxHealth.Value}");
        Debug.Log($"ARMOR:  Base = {stats.Armor.baseValue}, Final = {stats.Armor.Value}");
        Debug.Log($"SPEED:  Base = {stats.Speed.baseValue}, Final = {stats.Speed.Value}");
        Debug.Log("---------------------------");
    }
}