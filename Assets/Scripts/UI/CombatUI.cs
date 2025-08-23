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
    [SerializeField] private TMP_Text enemyStatusText;

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
        // --- ADD THIS LINE to clear the status on startup ---
        UpdateEnemyStatusEffectsUI(combatant.activeStatusEffects);
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

    private string BuildStatsString(Combatant combatant)
    {
        StringBuilder sb = new StringBuilder();
        var stats = combatant.Stats;

        // A local helper function to format each stat line consistently.
        // It neatly handles displaying the base value and the calculated bonus.
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
            sb.AppendLine(); // Add a new line at the end
        }

        // --- Section 1: Resource Pools (Current / Max) ---
        // Health is handled by the dedicated Health Text, but we can show Max HP here.
        AppendStatLine("Max Health", stats.MaxHealth);
        
        // Energy is handled specially to show the current value from the Combatant.
        sb.Append("Energy: ").Append(combatant.currentEnergy).Append(" / ").Append(stats.Energy.Value);
        float energyBonus = stats.Energy.Value - stats.Energy.baseValue;
        if (energyBonus != 0)
        {
            sb.Append(" (").Append(stats.Energy.baseValue).Append(energyBonus.ToString(" +0;-#")).Append(")");
        }
        sb.AppendLine();
        
        AppendStatLine("EN Regen", stats.EnergyRegen);
        
        sb.AppendLine("-----------------"); // Separator

        // --- Section 2: Core Combat Stats ---
        AppendStatLine("Might", stats.Might);
        AppendStatLine("Intelligence", stats.Intelligence);
        AppendStatLine("Armor", stats.Armor);
        AppendStatLine("Speed", stats.Speed);
        AppendStatLine("Grit", stats.Grit);
        
        sb.AppendLine("-----------------"); // Separator

        // --- Section 3: Board / Utility Stats ---
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

    public void UpdateInventoryUI(Dictionary<Item, int> items)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Items:</b>");

        if (items.Count == 0)
        {
            sb.AppendLine("None");
        }
        else
        {
            // --- NEW LOGIC to iterate through the dictionary ---
            foreach (KeyValuePair<Item, int> entry in items)
            {
                Item item = entry.Key;
                int count = entry.Value;
                
                string colorHex = GetRarityColorHex(item.rarity);
                // Display the stack count if it's greater than 1
                string stackText = (count > 1) ? $" x{count}" : "";
                
                sb.AppendLine($"<color={colorHex}>- {item.itemName}{stackText}</color>");
            }
        }
        
        playerInventoryText.text = sb.ToString();
    }

    // --- ADD THIS NEW HELPER METHOD at the bottom of the class ---
    private string GetRarityColorHex(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common:
                return "#B0B0B0"; // Grey
            case Rarity.Uncommon:
                return "#3DFF3D"; // Green
            case Rarity.Rare:
                return "#4D8CFF"; // Blue
            case Rarity.Relic:
                return "#C56BFF"; // Purple
            case Rarity.Mythic:
                return "#FF9A3D"; // Orange/Gold
            default:
                return "#FFFFFF"; // White
        }
    }


    public void UpdatePlayerStatusEffectsUI(List<StatusEffect> effects)
    {
        if (effects == null || effects.Count == 0)
        {
            playerStatusText.text = "";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Effects:</b>");

        foreach(var effect in effects)
        {
            // Case 1: Stacking effects like Wound
            if (effect.Type == StatusEffectType.Wound)
            {
                sb.AppendLine($"- {effect.Type} (x{effect.Stacks})");
            }
            // Case 2: DoT/HoT effects that show a value per turn
            else if (effect.Type == StatusEffectType.Burn || 
                    effect.Type == StatusEffectType.Regeneration || 
                    effect.Type == StatusEffectType.Poison)
            {
                sb.AppendLine($"- {effect.Type} ({effect.TickValue}/t) ({effect.Duration})");
            }
            // Case 3: Stat modifier effects
            else if (effect.Type == StatusEffectType.StatUp || effect.Type == StatusEffectType.StatDown)
            {
                // Determine the text based on the effect type
                string effectName = (effect.Type == StatusEffectType.StatUp) ? "Up" : "Down";
                
                // Determine the text for the value
                string valueText = "";
                if (effect.ModType == StatModType.Flat)
                {
                    // Show a "+" for positive flat values
                    valueText = effect.ModValue.ToString("+#;-#");
                }
                else // Percent
                {
                    // Show as a percentage with a "+"
                    valueText = (effect.ModValue * 100).ToString("+#;-#") + "%";
                }
                
                sb.AppendLine($"- {effect.TargetStat} {effectName} ({valueText}) ({effect.Duration})");
            }
            // Case 4: All other standard, timed effects
            else
            {
                sb.AppendLine($"- {effect.Type} ({effect.Duration})");
            }
        }
        playerStatusText.text = sb.ToString();
    }
        
    public void UpdateEnemyStatusEffectsUI(List<StatusEffect> effects)
    {
        if (effects == null || effects.Count == 0)
        {
            enemyStatusText.text = "";
            return;
        }

        StringBuilder sb = new StringBuilder();
        // You can optionally add a title here if you want
        // sb.AppendLine("<b>Effects:</b>"); 

        foreach(var effect in effects)
        {
            // Case 1: Stacking effects like Wound
            if (effect.Type == StatusEffectType.Wound)
            {
                sb.AppendLine($"- {effect.Type} (x{effect.Stacks})");
            }
            // Case 2: DoT/HoT effects that show a value per turn
            else if (effect.Type == StatusEffectType.Burn || 
                    effect.Type == StatusEffectType.Regeneration || 
                    effect.Type == StatusEffectType.Poison)
            {
                sb.AppendLine($"- {effect.Type} ({effect.TickValue}/t) ({effect.Duration})");
            }
            // Case 3: Stat modifier effects
            else if (effect.Type == StatusEffectType.StatUp || effect.Type == StatusEffectType.StatDown)
            {
                // Determine the text based on the effect type
                string effectName = (effect.Type == StatusEffectType.StatUp) ? "Up" : "Down";
                
                // Determine the text for the value
                string valueText = "";
                if (effect.ModType == StatModType.Flat)
                {
                    // Show a "+" for positive flat values
                    valueText = effect.ModValue.ToString("+#;-#");
                }
                else // Percent
                {
                    // Show as a percentage with a "+"
                    valueText = (effect.ModValue * 100).ToString("+#;-#") + "%";
                }
                
                sb.AppendLine($"- {effect.TargetStat} {effectName} ({valueText}) ({effect.Duration})");
            }
            // Case 4: All other standard, timed effects
            else
            {
                sb.AppendLine($"- {effect.Type} ({effect.Duration})");
            }
        }
        enemyStatusText.text = sb.ToString();
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