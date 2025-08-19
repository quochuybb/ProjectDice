using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CombatUI : MonoBehaviour
{
    [Header("Player HUD")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerHealthText;
    [SerializeField] private TMP_Text playerStatsText;

    [Header("Enemy HUD")]
    [SerializeField] private TMP_Text enemyNameText;
    [SerializeField] private TMP_Text enemyHealthText;
    [SerializeField] private TMP_Text enemyStatsText;

    [Header("Skill Bar")]
    [SerializeField] private Transform skillButtonContainer;
    [SerializeField] private GameObject skillButtonPrefab;

    // --- FROM HERE DOWN IS NEW OR MODIFIED ---

    public void SetupPlayerUI(Combatant combatant)
    {
        playerNameText.text = combatant.characterSheet.name;
        UpdatePlayerHealth(combatant.currentHealth, (int)combatant.Stats.MaxHealth.Value);
        UpdatePlayerStats(combatant);
    }

    public void SetupEnemyUI(Combatant combatant)
    {
        enemyNameText.text = combatant.characterSheet.name;
        UpdateEnemyHealth(combatant.currentHealth, (int)combatant.Stats.MaxHealth.Value);
        UpdateEnemyStats(combatant);
    }

    // --- Dedicated Update Methods ---

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
        playerStatsText.text =  $"EN: {combatant.currentEnergy} / {stats.Energy.Value}\n" +
                                $"Might: {stats.Might.Value}\n" +
                                $"Armor: {stats.Armor.Value}\n" +
                                $"Speed: {stats.Speed.Value}";
    }
    
    public void UpdateEnemyStats(Combatant combatant)
    {
        var stats = combatant.Stats;
        enemyStatsText.text =   $"EN: {combatant.currentEnergy} / {stats.Energy.Value}\n" +
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
}