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
    [SerializeField] private Button skipTurnButton;

    // --- NEW LIST TO HOLD OUR BUTTONS ---
    private List<SkillButtonUI> skillButtons = new List<SkillButtonUI>();

    [Header("Status Effects")]
    [SerializeField] private TMP_Text playerStatusText;
    private Combatant playerCombatantRef;
    private class SkillButtonUI
    {
        public Skill associatedSkill;
        public Button button;
        public TMP_Text text;
    }


    public void SetupPlayerUI(Combatant combatant)
    {
        playerCombatantRef = combatant;
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
        playerStatsText.text = BuildStatsString(combatant);
    }
    
    public void UpdateEnemyStats(Combatant combatant)
    {
        enemyStatsText.text = BuildStatsString(combatant);
    }

    // --- ADD THIS NEW, POWERFUL HELPER METHOD ---
    private string BuildStatsString(Combatant combatant)
    {
        StringBuilder sb = new StringBuilder();
        var stats = combatant.Stats;

        // A little local function to make formatting each line easy and consistent.
        void AppendStatLine(string statName, Stat stat)
        {
            float finalValue = stat.Value;
            float baseValue = stat.baseValue;
            float bonus = finalValue - baseValue;

            sb.Append(statName).Append(": ").Append(finalValue);
            
            // Only show the breakdown if there's a bonus (positive or negative)
            if (bonus != 0)
            {
                // The "+0;-#" format string ensures a '+' sign for positive bonuses.
                sb.Append(" (").Append(baseValue).Append(bonus.ToString(" +0;-#")).Append(")");
            }
            sb.AppendLine(); // Add a new line
        }

        // --- Now, we call the helper for every stat ---
        AppendStatLine("Max HP", stats.MaxHealth);
        AppendStatLine("Energy", stats.Energy);
        AppendStatLine("EN Regen", stats.EnergyRegen);
        sb.AppendLine("-----------------"); // Separator
        AppendStatLine("Might", stats.Might);
        AppendStatLine("Intelligence", stats.Intelligence);
        AppendStatLine("Armor", stats.Armor);
        AppendStatLine("Speed", stats.Speed);
        AppendStatLine("Grit", stats.Grit);
        sb.AppendLine("-----------------"); // Separator
        AppendStatLine("Luck", stats.Luck);
        AppendStatLine("Growth", stats.Growth);

        return sb.ToString();
    }


    public void CreatePlayerSkillButtons(Combatant player, CombatManager combatManager)
    {
        // Clear old buttons and the list
        foreach (Transform child in skillButtonContainer)
        {
            Destroy(child.gameObject);
        }
        skillButtons.Clear();

        // Create a new button for each skill
        foreach (Skill skill in player.characterSheet.startingSkills)
        {
            GameObject buttonGO = Instantiate(skillButtonPrefab, skillButtonContainer);

            // Create a new SkillButtonUI instance and populate it
            SkillButtonUI newButtonUI = new SkillButtonUI
            {
                associatedSkill = skill,
                button = buttonGO.GetComponent<Button>(),
                text = buttonGO.GetComponentInChildren<TMP_Text>()
            };

            // Add the listener
            newButtonUI.button.onClick.AddListener(() => combatManager.OnPlayerSkillSelection(skill));

            // Add the new button object to our list
            skillButtons.Add(newButtonUI);
        }

        // Do an initial update of the button states
        UpdateSkillButtons(player);
    }

    public void UpdateSkillButtons(Combatant player)
    {
        // First, check if the player is empowered. This affects all buttons.
        bool playerIsEmpowered = player.HasStatusEffect(StatusEffectType.Empower);

        foreach (var sb in skillButtons)
        {
            bool enoughEnergy = player.currentEnergy >= sb.associatedSkill.energyCost;
            bool onCooldown = player.IsSkillOnCooldown(sb.associatedSkill);

            // --- THE NEW LOGIC ---
            // A skill is usable if:
            // 1. It is NOT on cooldown.
            // 2. AND (You have enough energy OR you are empowered).
            sb.button.interactable = !onCooldown && (enoughEnergy || playerIsEmpowered);
            // --- END OF NEW LOGIC ---

            // Update the text to show cooldown or energy cost
            if (onCooldown)
            {
                sb.text.text = $"{sb.associatedSkill.skillName}\n({player.skillCooldowns[sb.associatedSkill]} T)";
            }
            else
            {
                sb.text.text = $"{sb.associatedSkill.skillName}\n({sb.associatedSkill.energyCost} EN)";
            }
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
        if (effects == null || effects.Count == 0)
        {
            playerStatusText.text = "";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Effects:</b>");
        foreach (var effect in effects)
        {
            sb.AppendLine($"- {effect.Type} ({effect.Duration})");
        }
        playerStatusText.text = sb.ToString();
    }

    public void EnablePlayerActions()
    {
        // This method already handles cooldowns and energy, so we just call it.
        UpdateSkillButtons(playerCombatantRef); 
        skipTurnButton.interactable = true;
    }

    // --- NEW METHOD TO DISABLE ACTIONS ---
    public void DisablePlayerActions()
    {
        foreach (var sb in skillButtons)
        {
            sb.button.interactable = false;
        }
        skipTurnButton.interactable = false;
    }
}