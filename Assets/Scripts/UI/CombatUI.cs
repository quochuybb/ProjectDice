using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CombatUI : MonoBehaviour
{
    public static CombatUI Instance { get; private set; }

    [Header("Player HUD")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerHealthText;
    [SerializeField] private TMP_Text playerStatsText;
    [SerializeField] private TMP_Text playerStatusText;

    [Header("Target HUD")]
    [SerializeField] private GameObject targetHudPanel;
    [SerializeField] private TMP_Text targetNameText;
    [SerializeField] private TMP_Text targetHealthText;
    [SerializeField] private TMP_Text targetStatsText;
    [SerializeField] private TMP_Text targetStatusText;

    [Header("Prompts")]
    [SerializeField] private TMP_Text targetingPromptText;

    [Header("Skill Bar")]
    [SerializeField] private Transform skillButtonContainer;
    [SerializeField] private GameObject skillButtonPrefab;
    [SerializeField] private Button skipTurnButton;

    private List<SkillButtonUI> skillButtons = new List<SkillButtonUI>();
    private Combatant playerCombatantRef;

    private class SkillButtonUI
    {
        public Skill associatedSkill;
        public Button button;
        public TMP_Text text;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void SetupPlayerUI(Combatant combatant)
    {
        playerCombatantRef = combatant;
        playerNameText.text = combatant.characterSheet.name;
        UpdatePlayerHealth(combatant.currentHealth, (int)combatant.Stats.MaxHealth.Value);
        UpdatePlayerStats(combatant);
        UpdateInventoryUI(combatant.GetComponent<InventoryComponent>().equippedItems);
        UpdatePlayerStatusEffectsUI(combatant.activeStatusEffects);
    }

    public void UpdateTargetHUD(Combatant target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            targetHudPanel.SetActive(false);
            return;
        }

        targetHudPanel.SetActive(true);
        targetNameText.text = target.characterSheet.name;
        UpdateTargetHealth(target.currentHealth, (int)target.Stats.MaxHealth.Value);
        UpdateTargetStats(target);
        UpdateTargetStatusEffectsUI(target.activeStatusEffects);
    }

    public void CreatePlayerSkillButtons(Combatant player, CombatManager combatManager)
    {
        foreach (Transform child in skillButtonContainer) Destroy(child.gameObject);
        skillButtons.Clear();

        foreach (Skill skill in player.characterSheet.startingSkills)
        {
            GameObject buttonGO = Instantiate(skillButtonPrefab, skillButtonContainer);
            SkillButtonUI newButtonUI = new SkillButtonUI
            {
                associatedSkill = skill,
                button = buttonGO.GetComponent<Button>(),
                text = buttonGO.GetComponentInChildren<TMP_Text>()
            };
            newButtonUI.button.onClick.AddListener(() => combatManager.OnPlayerSkillSelection(skill));
            skillButtons.Add(newButtonUI);
        }
        UpdateSkillButtons(player);
    }

    public void EnablePlayerActions()
    {
        if(playerCombatantRef != null) UpdateSkillButtons(playerCombatantRef);
        skipTurnButton.interactable = true;
    }

    public void DisablePlayerActions()
    {
        foreach (var sb in skillButtons) sb.button.interactable = false;
        skipTurnButton.interactable = false;
    }

    public void UpdateSkillButtons(Combatant player)
    {
        bool playerIsEmpowered = player.HasStatusEffect(StatusEffectType.Empower);
        foreach (var sb in skillButtons)
        {
            bool enoughEnergy = player.currentEnergy >= sb.associatedSkill.energyCost;
            bool onCooldown = player.IsSkillOnCooldown(sb.associatedSkill);
            sb.button.interactable = !onCooldown && (enoughEnergy || playerIsEmpowered);

            if (onCooldown) sb.text.text = $"{sb.associatedSkill.skillName}\n({player.skillCooldowns[sb.associatedSkill]} T)";
            else sb.text.text = $"{sb.associatedSkill.skillName}\n({sb.associatedSkill.energyCost} EN)";
        }
    }
    
    // --- UI Update Methods ---
    public void UpdatePlayerHealth(int current, int max) => playerHealthText.text = $"HP: {current} / {max}";
    public void UpdateTargetHealth(int current, int max) => targetHealthText.text = $"HP: {current} / {max}";
    public void UpdatePlayerStats(Combatant c) => playerStatsText.text = BuildStatsString(c);
    public void UpdateTargetStats(Combatant c) => targetStatsText.text = BuildStatsString(c);
    public void UpdatePlayerStatusEffectsUI(List<StatusEffect> e) => playerStatusText.text = BuildStatusEffectsString(e, true);
    public void UpdateTargetStatusEffectsUI(List<StatusEffect> e) => targetStatusText.text = BuildStatusEffectsString(e, false);
    
    public void UpdateInventoryUI(Dictionary<Item, int> items)
    {
        StringBuilder sb = new StringBuilder("<b>Items:</b>\n");
        if (items == null || items.Count == 0) sb.AppendLine("None");
        else
        {
            foreach (KeyValuePair<Item, int> entry in items)
            {
                string colorHex = GetRarityColorHex(entry.Key.rarity);
                string stackText = (entry.Value > 1) ? $" x{entry.Value}" : "";
                sb.AppendLine($"<color={colorHex}>- {entry.Key.itemName}{stackText}</color>");
            }
        }
        // playerInventoryText.text = sb.ToString();
    }
    
    public void ShowTargetingPrompt(bool show, string skillName = "")
    {
        if (show)
        {
            targetingPromptText.text = $"Use '{skillName}' on...";
            targetingPromptText.gameObject.SetActive(true);
        }
        else
        {
            targetingPromptText.gameObject.SetActive(false);
        }
    }
    
    // --- Helper Methods ---
    private string BuildStatsString(Combatant combatant)
    {
        StringBuilder sb = new StringBuilder();
        var stats = combatant.Stats;

        void AppendStatLine(string statName, Stat stat)
        {
            float finalValue = stat.Value;
            float baseValue = stat.baseValue;
            float bonus = finalValue - baseValue;
            sb.Append(statName).Append(": ").Append(finalValue);
            if (bonus != 0) sb.Append(" (").Append(baseValue).Append(bonus.ToString(" +0;-#")).Append(")");
            sb.AppendLine();
        }

        sb.Append("Energy: ").Append(combatant.currentEnergy).Append(" / ").Append(stats.Energy.Value);
        float energyBonus = stats.Energy.Value - stats.Energy.baseValue;
        if (energyBonus != 0) sb.Append(" (").Append(stats.Energy.baseValue).Append(energyBonus.ToString(" +0;-#")).Append(")");
        sb.AppendLine();

        AppendStatLine("EN Regen", stats.EnergyRegen);
        AppendStatLine("Max HP", stats.MaxHealth);
        sb.AppendLine("-----------------");
        AppendStatLine("Might", stats.Might);
        AppendStatLine("Intelligence", stats.Intelligence);
        AppendStatLine("Armor", stats.Armor);
        AppendStatLine("Speed", stats.Speed);
        AppendStatLine("Grit", stats.Grit);
        sb.AppendLine("-----------------");
        AppendStatLine("Luck", stats.Luck);
        AppendStatLine("Growth", stats.Growth);

        return sb.ToString();
    }

    private string BuildStatusEffectsString(List<StatusEffect> effects, bool includeTitle)
    {
        if (effects == null || effects.Count == 0) return "";
        StringBuilder sb = new StringBuilder();
        if (includeTitle) sb.AppendLine("<b>Effects:</b>");
        
        foreach(var effect in effects)
        {
            if (effect.TargetStat != StatType.None && (effect.Type == StatusEffectType.StatUp || effect.Type == StatusEffectType.StatDown))
            {
                string effectName = (effect.Type == StatusEffectType.StatUp) ? "Up" : "Down";
                string valueText;
                float valueToDisplay = (effect.Type == StatusEffectType.StatDown) ? -effect.ModValue : effect.ModValue;
                if (effect.ModType == StatModType.Flat) valueText = valueToDisplay.ToString("+#;-#");
                else valueText = (valueToDisplay * 100).ToString("+#;-#") + "%";
                sb.AppendLine($"- {effect.TargetStat} {effectName} ({valueText}) ({effect.Duration})");
            }
            else if (effect.Type == StatusEffectType.Wound) sb.AppendLine($"- {effect.Type} (x{effect.Stacks})");
            else if (effect.Type == StatusEffectType.Burn || effect.Type == StatusEffectType.Regeneration || effect.Type == StatusEffectType.Poison)
                sb.AppendLine($"- {effect.Type} ({effect.TickValue}/t) ({effect.Duration})");
            else sb.AppendLine($"- {effect.Type} ({effect.Duration})");
        }
        return sb.ToString();
    }

    private string GetRarityColorHex(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return "#B0B0B0";
            case Rarity.Uncommon: return "#3DFF3D";
            case Rarity.Rare: return "#4D8CFF";
            case Rarity.Relic: return "#C56BFF";
            case Rarity.Mythic: return "#FF9A3D";
            default: return "#FFFFFF";
        }
    }
}