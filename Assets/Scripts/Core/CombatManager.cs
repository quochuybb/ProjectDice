using System.Collections;
using UnityEngine;

public enum CombatState { START, PLAYERTURN, ENEMYTURN, WON, LOST }

public class CombatManager : MonoBehaviour
{
    public CombatState state;

    public Combatant playerCombatant;
    public Combatant enemyCombatant;
    
    // We will hook up the UI later. For now, we'll use logs.
    // public CombatUI combatUI; 

    void Start()
    {
        state = CombatState.START;
        StartCoroutine(SetupCombat());
    }

    IEnumerator SetupCombat()
    {
        Debug.Log("Setting up combat...");
        // Any setup animations or introductions go here
        yield return new WaitForSeconds(1f);

        // Your GDD says Speed determines turn order. We'll start with player first for simplicity.
        state = CombatState.PLAYERTURN;
        PlayerTurn();
    }

    void PlayerTurn()
    {
        Debug.Log("Player's Turn. Select an action.");
        // The system will now wait for player input (e.g., clicking a UI button)
    }

    // This function will be called by a UI button
    public void OnPlayerSkillSelection(Skill skill)
    {
        if (state != CombatState.PLAYERTURN)
            return;

        StartCoroutine(PlayerAttack(skill));
    }
    
    IEnumerator PlayerAttack(Skill skill)
    {
        playerCombatant.UseSkill(skill, enemyCombatant);
        
        yield return new WaitForSeconds(1.5f); // Wait for animations/VFX

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
    
    IEnumerator EnemyTurn()
    {
        Debug.Log("Enemy's Turn.");
        yield return new WaitForSeconds(1f);

        // Basic AI: Use the first skill available
        Skill enemySkill = enemyCombatant.characterSheet.startingSkills[0];
        enemyCombatant.UseSkill(enemySkill, playerCombatant);
        
        yield return new WaitForSeconds(1.5f);

        if (playerCombatant.currentHealth <= 0)
        {
            state = CombatState.LOST;
            EndCombat();
        }
        else
        {
            state = CombatState.PLAYERTURN;
            PlayerTurn();
        }
    }

    void EndCombat()
    {
        if(state == CombatState.WON)
        {
            Debug.Log("You Won!");
            // Transition to reward screen or back to the board
        }
        else if (state == CombatState.LOST)
        {
            Debug.Log("You Lost.");
            // Transition to death/reset sequence
        }
    }
}