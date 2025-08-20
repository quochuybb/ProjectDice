using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

public class CombatUI : MonoBehaviour
{
    [Header("Player HUD")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerHealthText;
    [SerializeField] private TMP_Text playerStatsText;
    [SerializeField] private TMP_Text playerInventoryText;

    [Header("Enemy HUD")]
    [SerializeField] private TMP_Text enemyNameText;
    [SerializeField] private TMP_Text enemyHealthText;
    [SerializeField] private TMP_Text enemyStatsText;

    [Header("Skill Bar")]
    [SerializeField] private Transform skillButtonContainer;
    [SerializeField] private GameObject skillButtonPrefab;

    [Header("Status Effects")]
    [SerializeField] private TMP_Text playerStatusText;

    public void SetupPlayerUI(Combatant combatant)
    {
        playerNameText.text = combatant.characterSheet.name;
        UpdatePlayerHealth(combatant.currentHealth, (int)combatant.Stats.MaxHealth.Value);
        UpdatePlayerStats(combatant);
        UpdateInventoryUI(combatant.GetComponent<InventoryComponent>().equippedItems);
    }

    // --- THIS IS THE MISSING METHOD ---
    public void SetupEnemyUI(Combatant combatant)
    {
        enemyNameText.text = combatant.characterSheet.name;
        UpdateEnemyHealth(combatant.currentHealth, (int)combatant.Stats.MaxHealth.Value);
        UpdateEnemyStats(combatant);
    }
    // ------------------------------------

    public void UpdatePlayerHealth(int current, int max)
    {
        playerHealthText.text = $"HP: {current} / {max}";
    }

    public void UpdateEnemyHealth(int current, int max)
    {
        enemyHealthText.text = $"HP: {current} / {max}";
    }

    public void UpdatePlayerStats(Combatant combatant)
    {
        var stats = combatant.Stats;
        playerStatsText.text = $"EN: {combatant.currentEnergy} / {stats.Energy.Value}\n" +
                                $"Might: {stats.Might.Value}\n" +
                                $"Armor: {stats.Armor.Value}\n" +
                                $"Speed: {stats.Speed.Value}";
    }

    public void UpdateEnemyStats(Combatant combatant)
    {
        var stats = combatant.Stats;
        enemyStatsText.text = $"EN: {combatant.currentEnergy} / {stats.Energy.Value}\n" +
                                $"Might: {stats.Might.Value}\n" +
                                $"Armor: {stats.Armor.Value}\n" +
                                $"Speed: {stats.Speed.Value}";
    }

    public void CreatePlayerSkillButtons(Combatant player, CombatManager combatManager)
    {
        foreach (Transform child in skillButtonContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (Skill skill in player.characterSheet.startingSkills)
        {
            GameObject buttonGO = Instantiate(skillButtonPrefab, skillButtonContainer);
            Button button = buttonGO.GetComponent<Button>();
            TMP_Text buttonText = buttonGO.GetComponentInChildren<TMP_Text>();
            buttonText.text = $"{skill.skillName}\n({skill.energyCost} EN)";
            button.onClick.AddListener(() => combatManager.OnPlayerSkillSelection(skill));
        }
    }

    public void UpdateInventoryUI(List<Item> items)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Items:</b>");

        if (items.Count == 0)
        {
            sb.AppendLine("None");
        }
        else
        {
            foreach (Item item in items)
            {
                sb.AppendLine("- " + item.itemName);
            }
        }

        playerInventoryText.text = sb.ToString();
    }

        public void UpdateStatusEffectsUI(List<StatusEffect> effects)
    {
        if(effects == null || effects.Count == 0)
        {
            playerStatusText.text = "";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Effects:</b>");
        foreach(var effect in effects)
        {
            sb.AppendLine($"- {effect.Type} ({effect.Duration})");
        }
        playerStatusText.text = sb.ToString();
    }
}