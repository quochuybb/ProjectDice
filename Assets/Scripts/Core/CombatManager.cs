using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class CombatManager : MonoBehaviour
{
    public CombatState state;
    public Combatant playerCombatant;
    public Combatant enemyCombatant;
    [SerializeField] private CombatUI combatUI;

    void Start()
    {
        state = CombatState.START;
        StartCoroutine(SetupCombat());
    }

    IEnumerator SetupCombat()
    {
        Debug.Log("Setting up combat...");

        // --- UI INITIALIZATION (Now cleaner) ---
        combatUI.SetupPlayerUI(playerCombatant);
        combatUI.SetupEnemyUI(enemyCombatant);
        combatUI.CreatePlayerSkillButtons(playerCombatant, this);

        // --- SUBSCRIBE TO EVENTS ---
        playerCombatant.OnHealthChanged += combatUI.UpdatePlayerHealth;
        playerCombatant.OnEnergyChanged += (current, max) => {
            combatUI.UpdatePlayerStats(playerCombatant);
            combatUI.UpdateSkillButtons(playerCombatant); // Also update buttons on energy change
        };
        playerCombatant.GetComponent<InventoryComponent>().OnInventoryChanged += combatUI.UpdateInventoryUI;
        
        // --- THIS IS THE KEY CHANGE ---
        // When status effects change, update BOTH the status display AND the skill buttons
        playerCombatant.OnStatusEffectsChanged += (effects) => {
            combatUI.UpdateStatusEffectsUI(effects);
            combatUI.UpdateSkillButtons(playerCombatant);
        };
        // --- END OF KEY CHANGE ---
        
        playerCombatant.OnCooldownsChanged += () => combatUI.UpdateSkillButtons(playerCombatant);

        enemyCombatant.OnHealthChanged += combatUI.UpdateEnemyHealth;
        enemyCombatant.OnEnergyChanged += (current, max) => combatUI.UpdateEnemyStats(enemyCombatant);

        yield return new WaitForSeconds(1f);
        state = CombatState.PLAYERTURN;
        StartCoroutine(PlayerTurn());
    }

    IEnumerator PlayerTurn()
    {
        playerCombatant.TickDownDebuffsAtTurnStart();
        
        // --- ADD COOLDOWN TICKDOWN ---
        playerCombatant.TickDownCooldowns(); 

        playerCombatant.RegenerateEnergy();
        yield return new WaitForSeconds(0.5f);
        Debug.Log("Player's Turn. Select an action.");
    }

    
    public void OnPlayerSkillSelection(Skill skill)
    {
        if (state != CombatState.PLAYERTURN)
            return;

        StartCoroutine(PlayerAttack(skill));
    }
    
    IEnumerator PlayerAttack(Skill skill)
    {
        playerCombatant.UseSkill(skill, enemyCombatant);
        yield return new WaitForSeconds(1.5f);
        
        // --- NEW LOGIC: TICK BUFFS AT END OF ACTION ---
        playerCombatant.TickDownBuffsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);

        if (enemyCombatant.currentHealth <= 0)
        {
            state = CombatState.WON;
            EndCombat();
        }
        else
        {
            state = CombatState.ENEMYTURN;
            StartCoroutine(EnemyTurn());
        }
    }
    
    // --- THIS METHOD IS COMPLETELY REWRITTEN FOR AI ---
    IEnumerator EnemyTurn()
    {

        Debug.Log("Enemy's Turn.");
        enemyCombatant.TickDownDebuffsAtTurnStart();
        enemyCombatant.RegenerateEnergy();
        yield return new WaitForSeconds(1f);
        
        // --- AI LOGIC START ---
        
        // 1. Get a list of all skills the enemy *can afford* to use.
        var allSkills = enemyCombatant.characterSheet.startingSkills;
        var affordableSkills = new List<Skill>();
        foreach (var skill in allSkills)
        {
            if (skill.energyCost <= enemyCombatant.currentEnergy)
            {
                affordableSkills.Add(skill);
            }
        }

        // 2. Decide what to do.
        if (affordableSkills.Count > 0)
        {
            // Pick a random skill from the affordable list.
            int randomIndex = Random.Range(0, affordableSkills.Count);
            Skill skillToUse = affordableSkills[randomIndex];

            // Use the skill on the player
            enemyCombatant.UseSkill(skillToUse, playerCombatant);
        }
        else
        {
            // If the enemy can't afford any skills, it passes the turn.
            Debug.Log($"<color=orange>{enemyCombatant.characterSheet.name} has no affordable skills and passes its turn.</color>");
        }

        // --- AI LOGIC END ---

        yield return new WaitForSeconds(1.5f);
        
        // --- NEW LOGIC: TICK BUFFS AT END OF ACTION ---
        enemyCombatant.TickDownBuffsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);

        if (playerCombatant.currentHealth <= 0)
        {
            state = CombatState.LOST;
            EndCombat();
        }
        else
        {
            state = CombatState.PLAYERTURN;
            StartCoroutine(PlayerTurn());
        }
    }

    void EndCombat()
    {
        if(state == CombatState.WON)
        {
            Debug.Log("<color=green>You Won!</color>");
        }
        else if (state == CombatState.LOST)
        {
            Debug.Log("<color=red>You Lost.</color>");
        }
    }
}