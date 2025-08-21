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

        // --- 1. INITIAL UI DISPLAY ---
        // These methods set the initial state of the UI panels.
        combatUI.SetupPlayerUI(playerCombatant);
        combatUI.SetupEnemyUI(enemyCombatant);
        combatUI.CreatePlayerSkillButtons(playerCombatant, this);


        // --- 2. SUBSCRIBE UI TO BACKEND EVENTS ---
        // This is the core of the reactive UI. We tell the UI how to update
        // itself whenever a specific event happens in the backend.

        // When the player's health changes, update the player's health text.
        playerCombatant.OnHealthChanged += combatUI.UpdatePlayerHealth;

        // When the player's energy changes, we need to update two things:
        // - The stats panel (to show the new energy value).
        // - The skill buttons (to check for affordability).
        playerCombatant.OnEnergyChanged += (current, max) => {
            combatUI.UpdatePlayerStats(playerCombatant);
            combatUI.UpdateSkillButtons(playerCombatant);
        };
        
        // When the player's inventory changes (e.g., equipping an item):
        // - Update the inventory list in the UI.
        // - Update the stats panel, as items directly modify stats.
        playerCombatant.GetComponent<InventoryComponent>().OnInventoryChanged += (itemDict) =>
        {
            combatUI.UpdateInventoryUI(itemDict);
            combatUI.UpdatePlayerStats(playerCombatant);
        };
        
        // When the player's status effects change (gained or lost):
        // - Update the status effect list in the UI.
        // - Update the stats panel (for effects like Fortify that change stats).
        // - Update the skill buttons (for effects like Empower that change affordability).
        playerCombatant.OnStatusEffectsChanged += (effects) => {
            combatUI.UpdateStatusEffectsUI(effects);
            combatUI.UpdatePlayerStats(playerCombatant);
            combatUI.UpdateSkillButtons(playerCombatant);
        };
        
        // When a skill's cooldown status changes, update the skill buttons.
        playerCombatant.OnCooldownsChanged += () => combatUI.UpdateSkillButtons(playerCombatant);

        // --- Enemy Event Subscriptions (simpler for now) ---
        enemyCombatant.OnHealthChanged += combatUI.UpdateEnemyHealth;
        enemyCombatant.OnEnergyChanged += (current, max) => combatUI.UpdateEnemyStats(enemyCombatant);
        enemyCombatant.OnStatusEffectsChanged += combatUI.UpdateEnemyStatusEffectsUI;

        // --- 3. START THE BATTLE ---
        // Wait a moment for the player to process the initial screen state.
        yield return new WaitForSeconds(1f);
        
        // Set the state to the player's turn and begin the first turn.
        state = CombatState.PLAYERTURN;
        StartCoroutine(PlayerTurn());
    }

    IEnumerator PlayerTurn()
    {
        // 1. Process DoTs and HoTs first.
        playerCombatant.ProcessDoTsAndHoTs();
        
        // 2. Then, reduce cooldowns.
        playerCombatant.TickDownCooldowns(); 
        
        // 3. Finally, regenerate resources.
        playerCombatant.RegenerateEnergy();
        
        yield return new WaitForSeconds(0.5f);
        Debug.Log("Player's Turn. Select an action.");
        combatUI.EnablePlayerActions();
    }


    public void OnPlayerSkillSelection(Skill skill)
    {
        if (state != CombatState.PLAYERTURN)
            return;

        StartCoroutine(PlayerAttack(skill));
    }

    IEnumerator PlayerAttack(Skill skill)
    {
        combatUI.DisablePlayerActions();

        // Player takes their action here...
        playerCombatant.UseSkill(skill, enemyCombatant);
        yield return new WaitForSeconds(1.5f);
        
        // --- TICK DOWN ALL EFFECTS AT THE END OF THE TURN ---
        playerCombatant.TickDownStatusEffectsAtTurnEnd();
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

    private IEnumerator EnemyTurn()
    {
        Debug.Log("Enemy's Turn.");

        // ===================================
        // 1. START OF TURN PHASE
        // ===================================
        // DoTs and HoTs are processed first, before any actions.
        enemyCombatant.ProcessDoTsAndHoTs();

        // Enemy cooldowns are ticked down (for future use).
        enemyCombatant.TickDownCooldowns();

        // Resources are regenerated.
        enemyCombatant.RegenerateEnergy();
        
        // A delay to pace the turn.
        yield return new WaitForSeconds(1f);

        // ===================================
        // 2. ACTION PHASE (AI LOGIC)
        // ===================================

        // Get a list of all skills the enemy can currently afford to use.
        var allSkills = enemyCombatant.characterSheet.startingSkills;
        var affordableSkills = new List<Skill>();
        foreach (var skill in allSkills)
        {
            if (skill.energyCost <= enemyCombatant.currentEnergy && !enemyCombatant.IsSkillOnCooldown(skill))
            {
                affordableSkills.Add(skill);
            }
        }

        // Decide what action to take.
        if (affordableSkills.Count > 0)
        {
            // Pick a random skill from the affordable list.
            int randomIndex = Random.Range(0, affordableSkills.Count);
            Skill skillToUse = affordableSkills[randomIndex];

            // Use the skill on the player.
            enemyCombatant.UseSkill(skillToUse, playerCombatant);
        }
        else
        {
            // If the enemy can't afford any skills, it passes the turn.
            Debug.Log($"<color=orange>{enemyCombatant.characterSheet.name} has no affordable actions and passes its turn.</color>");
        }

        // A delay for the action/animation to play out.
        yield return new WaitForSeconds(1.5f);

        // ===================================
        // 3. END OF TURN PHASE
        // ===================================

        // All status effects (both buffs and debuffs) are ticked down at the end.
        enemyCombatant.TickDownStatusEffectsAtTurnEnd();
        
        // A short delay before transitioning.
        yield return new WaitForSeconds(0.5f);

        // ===================================
        // 4. STATE TRANSITION
        // ===================================

        // Check if the player was defeated by the enemy's action.
        if (playerCombatant.currentHealth <= 0)
        {
            state = CombatState.LOST;
            EndCombat();
        }
        else
        {
            // If the player is still alive, transition back to the player's turn.
            state = CombatState.PLAYERTURN;
            StartCoroutine(PlayerTurn());
        }
    }

    void EndCombat()
    {
        if (state == CombatState.WON)
        {
            Debug.Log("<color=green>You Won!</color>");
        }
        else if (state == CombatState.LOST)
        {
            Debug.Log("<color=red>You Lost.</color>");
        }
    }
    
    public void OnSkipTurnClicked()
    {
        if (state != CombatState.PLAYERTURN)
            return;

        StartCoroutine(SkipTurn());
    }

    // A new coroutine to handle the turn transition
    private IEnumerator SkipTurn()
    {
        combatUI.DisablePlayerActions();
        Debug.Log("Player skips their turn.");
        
        // --- TICK DOWN ALL EFFECTS AT THE END OF THE TURN ---
        playerCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);

        state = CombatState.ENEMYTURN;
        StartCoroutine(EnemyTurn());
    }

}