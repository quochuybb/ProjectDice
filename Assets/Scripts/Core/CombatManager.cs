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

        combatUI.SetupPlayerUI(playerCombatant);
        combatUI.SetupEnemyUI(enemyCombatant);
        combatUI.CreatePlayerSkillButtons(playerCombatant, this);

        // --- Event Subscriptions ---
        playerCombatant.OnHealthChanged += combatUI.UpdatePlayerHealth;
        playerCombatant.OnEnergyChanged += (current, max) =>
        {
            combatUI.UpdatePlayerStats(playerCombatant);
            combatUI.UpdateSkillButtons(playerCombatant);
        };
        playerCombatant.GetComponent<InventoryComponent>().OnInventoryChanged += (itemDict) =>
        {
            combatUI.UpdateInventoryUI(itemDict);
            combatUI.UpdatePlayerStats(playerCombatant);
        };
        playerCombatant.OnStatusEffectsChanged += (effects) =>
        {
            combatUI.UpdatePlayerStatusEffectsUI(effects);
            combatUI.UpdatePlayerStats(playerCombatant);
            combatUI.UpdateSkillButtons(playerCombatant);
        };
        playerCombatant.OnCooldownsChanged += () => combatUI.UpdateSkillButtons(playerCombatant);

        enemyCombatant.OnHealthChanged += combatUI.UpdateEnemyHealth;
        enemyCombatant.OnEnergyChanged += (current, max) => combatUI.UpdateEnemyStats(enemyCombatant);
        enemyCombatant.OnStatusEffectsChanged += combatUI.UpdateEnemyStatusEffectsUI;

        yield return new WaitForSeconds(1f);
        state = CombatState.PLAYERTURN;
        StartCoroutine(PlayerTurn());
    }

    // ===================================================================
    // CORE TURN FLOW
    // ===================================================================

    IEnumerator PlayerTurn()
    {
        if (playerCombatant.HasStatusEffect(StatusEffectType.Ethereal))
        {
            // We use the existing ProcessSkippedTurn helper, which handles the end-of-turn logic.
            // We need a specific log message for this case.
            Debug.Log($"<color=grey>{playerCombatant.characterSheet.name} is Ethereal and cannot act this turn.</color>");
            yield return StartCoroutine(ProcessSkippedTurn(playerCombatant));
            yield break;
        }
        if (playerCombatant.HasStatusEffect(StatusEffectType.Stun) || playerCombatant.HasStatusEffect(StatusEffectType.Freeze))
        {
            yield return StartCoroutine(ProcessSkippedTurn(playerCombatant));
            yield break;
        }
        playerCombatant.ProcessCleansingEffectsAtTurnStart();
        if (CheckGameState()) yield break; 

        playerCombatant.ProcessDoTsAndHoTs();
        // --- NEW: Check game state after DoTs ---
        if (CheckGameState()) yield break;

        playerCombatant.TickDownCooldowns();
        playerCombatant.RegenerateEnergy();
        yield return new WaitForSeconds(0.5f);
        Debug.Log("Player's Turn. Select an action.");
        combatUI.EnablePlayerActions();
    }

    private IEnumerator EnemyTurn()
    {
        Debug.Log("Enemy's Turn.");

        if (enemyCombatant.HasStatusEffect(StatusEffectType.Stun) || enemyCombatant.HasStatusEffect(StatusEffectType.Freeze))
        {
            yield return StartCoroutine(ProcessSkippedTurn(enemyCombatant));
            yield break;
        }
        enemyCombatant.ProcessCleansingEffectsAtTurnStart();
        if (CheckGameState()) yield break;
        enemyCombatant.ProcessDoTsAndHoTs();
        // --- NEW: Check game state after DoTs ---
        if (CheckGameState()) yield break;

        enemyCombatant.TickDownCooldowns();
        enemyCombatant.RegenerateEnergy();
        yield return new WaitForSeconds(0.5f);

        // --- AI Action Phase ---
        var allSkills = enemyCombatant.characterSheet.startingSkills;
        var affordableSkills = new List<Skill>();
        foreach (var skill in allSkills)
        {
            if (skill.energyCost <= enemyCombatant.currentEnergy && !enemyCombatant.IsSkillOnCooldown(skill))
            {
                affordableSkills.Add(skill);
            }
        }

        if (affordableSkills.Count > 0)
        {
            int randomIndex = Random.Range(0, affordableSkills.Count);
            enemyCombatant.UseSkill(affordableSkills[randomIndex], playerCombatant);
        }
        else
        {
            Debug.Log($"<color=orange>{enemyCombatant.characterSheet.name} has no affordable actions and passes its turn.</color>");
        }

        yield return new WaitForSeconds(1f);
        // --- NEW: Check game state after enemy action ---
        if (CheckGameState()) yield break;

        enemyCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);

        state = CombatState.PLAYERTURN;
        StartCoroutine(PlayerTurn());
    }

    // ===================================================================
    // PLAYER ACTIONS
    // ===================================================================

    public void OnPlayerSkillSelection(Skill skill)
    {
        if (state != CombatState.PLAYERTURN) return;
        state = CombatState.PROCESSING;
        StartCoroutine(PlayerAttack(skill));
    }

    public void OnSkipTurnClicked()
    {
        if (state != CombatState.PLAYERTURN) return;
        state = CombatState.PROCESSING;
        StartCoroutine(SkipTurn());
    }

    IEnumerator PlayerAttack(Skill skill)
    {
        combatUI.DisablePlayerActions();
        playerCombatant.UseSkill(skill, enemyCombatant);
        yield return new WaitForSeconds(1.5f);
        
        // --- NEW: Check game state after player action ---
        if (CheckGameState()) yield break;

        playerCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);

        state = CombatState.ENEMYTURN;
        StartCoroutine(EnemyTurn());
    }

    private IEnumerator SkipTurn()
    {
        combatUI.DisablePlayerActions();
        Debug.Log("Player skips their turn.");
        playerCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);
        state = CombatState.ENEMYTURN;
        StartCoroutine(EnemyTurn());
    }

    // ===================================================================
    // GAME STATE & HELPER METHODS
    // ===================================================================

    private IEnumerator ProcessSkippedTurn(Combatant skippedCombatant)
    {
        // Check for Ethereal FIRST to give it a unique, non-alarming message.
        // We check this by seeing if the combatant is the player.
        if (skippedCombatant == playerCombatant && skippedCombatant.HasStatusEffect(StatusEffectType.Ethereal))
        {
            // This log is handled in the PlayerTurn coroutine before this method is called.
            // We can add a secondary log here if needed, but it's optional.
        }
        else // If not Ethereal, it must be Stun or Freeze.
        {
            string effectName = skippedCombatant.HasStatusEffect(StatusEffectType.Freeze) ? "Frozen" : "Stunned";
            Debug.Log($"<color=orange>{skippedCombatant.characterSheet.name} is {effectName} and skips their turn!</color>");
        }

        // A skipped turn still needs to tick down all status effects at the end.
        skippedCombatant.TickDownStatusEffectsAtTurnEnd();
        
        // Pause to let the player see what happened.
        yield return new WaitForSeconds(1.5f);

        // Determine the next state based on who was skipped.
        if (skippedCombatant == playerCombatant)
        {
            state = CombatState.ENEMYTURN;
            StartCoroutine(EnemyTurn());
        }
        else // It was the enemy
        {
            state = CombatState.PLAYERTURN;
            StartCoroutine(PlayerTurn());
        }
    }

    // --- NEW CENTRALIZED GAME STATE CHECKER ---
    private bool CheckGameState()
    {
        if (enemyCombatant.currentHealth <= 0)
        {
            state = CombatState.WON;
            EndCombat();
            return true; // Game is over
        }
        if (playerCombatant.currentHealth <= 0)
        {
            state = CombatState.LOST;
            EndCombat();
            return true; // Game is over
        }
        return false; // Game continues
    }

    void EndCombat()
    {
        // Disable all actions permanently
        combatUI.DisablePlayerActions();

        if (state == CombatState.WON)
        {
            Debug.Log("<color=green>You Won!</color>");
        }
        else if (state == CombatState.LOST)
        {
            Debug.Log("<color=red>You Lost.</color>");
        }
    }
}