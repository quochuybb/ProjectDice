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
        playerCombatant.OnEnergyChanged += (current, max) =>
        {
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
        playerCombatant.OnStatusEffectsChanged += (effects) =>
        {
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
        // --- STUN/FREEZE CHECK ---
        if (playerCombatant.HasStatusEffect(StatusEffectType.Stun) || playerCombatant.HasStatusEffect(StatusEffectType.Freeze))
        {
            // If stunned, skip the entire turn process and go to the end-of-turn logic.
            yield return StartCoroutine(ProcessSkippedTurn(playerCombatant, CombatState.ENEMYTURN));
            // Using yield break to ensure no more of this coroutine runs.
            yield break;
        }

        // If not stunned, proceed as normal
        playerCombatant.ProcessDoTsAndHoTs();
        playerCombatant.TickDownCooldowns();
        playerCombatant.RegenerateEnergy();
        yield return new WaitForSeconds(0.5f);
        Debug.Log("Player's Turn. Select an action.");
        combatUI.EnablePlayerActions();
    }


    public void OnPlayerSkillSelection(Skill skill)
    {
        // The state check remains the first line of defense.
        if (state != CombatState.PLAYERTURN)
            return;

        // --- THE FIX: Change the state IMMEDIATELY ---
        state = CombatState.ENEMYTURN; // Or a new state like CombatState.PROCESSING_ACTION;

        StartCoroutine(PlayerAttack(skill));
    }

    IEnumerator PlayerAttack(Skill skill)
    {
        combatUI.DisablePlayerActions();

        playerCombatant.UseSkill(skill, enemyCombatant);
        yield return new WaitForSeconds(1.5f);

        playerCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);

        if (enemyCombatant.currentHealth <= 0)
        {
            state = CombatState.WON;
            EndCombat();
        }
        else
        {
            // The state is already ENEMYTURN, so we just start the coroutine.
            StartCoroutine(EnemyTurn());
        }
    }

    private IEnumerator EnemyTurn()
    {
        Debug.Log("Enemy's Turn.");

        // ===================================
        // 0. TURN-SKIPPING STATUS CHECK
        // ===================================
        // This is the first check. If the enemy is stunned or frozen,
        // we skip the entire turn by calling a helper and exiting this coroutine immediately.
        if (enemyCombatant.HasStatusEffect(StatusEffectType.Stun) || enemyCombatant.HasStatusEffect(StatusEffectType.Freeze))
        {
            yield return StartCoroutine(ProcessSkippedTurn(enemyCombatant, CombatState.PLAYERTURN));
            yield break; // Stop this coroutine from proceeding further.
        }

        // If not stunned, the normal turn proceeds.

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
            // The AI now correctly checks for both energy and cooldowns.
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
            // The state is set back to PLAYERTURN here,
            // allowing the player to take their next action and preventing input bugs.
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

        // --- THE FIX: Change the state IMMEDIATELY ---
        state = CombatState.ENEMYTURN;

        StartCoroutine(SkipTurn());
    }

    private IEnumerator SkipTurn()
    {
        combatUI.DisablePlayerActions();
        Debug.Log("Player skips their turn.");

        playerCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);

        // The state is already ENEMYTURN, so we just start the coroutine.
        StartCoroutine(EnemyTurn());
    }
    
    private IEnumerator ProcessSkippedTurn(Combatant stunnedCombatant, CombatState nextState)
    {
        string effectName = stunnedCombatant.HasStatusEffect(StatusEffectType.Freeze) ? "Frozen" : "Stunned";
        Debug.Log($"<color=orange>{stunnedCombatant.characterSheet.name} is {effectName} and skips their turn!</color>");

        // A skipped turn still needs to tick down effect durations.
        stunnedCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(1.5f); // Pause to let the player see what happened

        // Transition to the next turn
        state = nextState;
        if (nextState == CombatState.PLAYERTURN)
        {
            StartCoroutine(PlayerTurn());
        }
        else
        {
            StartCoroutine(EnemyTurn());
        }
    }


}