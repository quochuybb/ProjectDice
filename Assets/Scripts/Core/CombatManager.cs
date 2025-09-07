using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CombatManager : MonoBehaviour
{
    public CombatState state;

    [Header("References")]
    public Combatant playerCombatant;
    [SerializeField] private CombatSpawner combatSpawner;
    [SerializeField] private CombatUI combatUI;

    private List<Combatant> enemies = new List<Combatant>();
    private List<Combatant> turnOrder = new List<Combatant>();
    private Combatant currentTarget;
    private int currentTurnIndex = 0;

    void Start()
    {
        state = CombatState.START;
        StartCoroutine(SetupCombat());
    }

    IEnumerator SetupCombat()
    {
        Debug.Log("Spawning enemies...");
        enemies = combatSpawner.SpawnEnemies();
        if (enemies.Count == 0)
        {
            Debug.LogError("No enemies were spawned! Check CombatSpawner setup.");
            yield break;
        }

        turnOrder.Add(playerCombatant);
        turnOrder.AddRange(enemies);
        turnOrder = turnOrder.OrderByDescending(c => c.Stats.Speed.Value).ToList();

        combatUI.SetupPlayerUI(playerCombatant);
        combatUI.CreatePlayerSkillButtons(playerCombatant, this);
        combatUI.UpdateTargetHUD(null);

        SetupPlayerEventSubscriptions();
        foreach (var enemy in enemies)
        {
            enemy.OnHealthChanged += (current, max) => { if (currentTarget == enemy) combatUI.UpdateTargetHUD(enemy); };
            enemy.OnStatusEffectsChanged += (effects) => { if (currentTarget == enemy) combatUI.UpdateTargetHUD(enemy); };
        }

        yield return new WaitForSeconds(1.5f);
        StartNextTurn();
    }

    void StartNextTurn()
    {
        if (CheckGameState()) return;

        Combatant currentCombatant = turnOrder[currentTurnIndex];
        
        if (currentCombatant.currentHealth <= 0)
        {
            AdvanceTurn();
            return;
        }
        
        if (currentCombatant == playerCombatant)
        {
            state = CombatState.PLAYERTURN;
            StartCoroutine(PlayerTurn());
        }
        else
        {
            state = CombatState.ENEMYTURN;
            StartCoroutine(EnemyTurn(currentCombatant));
        }
    }

    IEnumerator PlayerTurn()
    {
        Debug.Log("--- PLAYER'S TURN ---");
        SetCurrentTarget(currentTarget);

        if (playerCombatant.HasStatusEffect(StatusEffectType.Ethereal) || playerCombatant.HasStatusEffect(StatusEffectType.Stun) || playerCombatant.HasStatusEffect(StatusEffectType.Freeze))
        {
            yield return StartCoroutine(ProcessSkippedTurn(playerCombatant));
            yield break;
        }
        
        playerCombatant.ProcessCleansingEffectsAtTurnStart();
        if (CheckGameState()) yield break;
        
        playerCombatant.ProcessDoTsAndHoTs();
        if (CheckGameState()) yield break;

        playerCombatant.TickDownCooldowns();
        playerCombatant.RegenerateEnergy();
        combatUI.EnablePlayerActions();
    }

    IEnumerator EnemyTurn(Combatant currentEnemy)
    {
        Debug.Log($"--- {currentEnemy.characterSheet.name}'s TURN ---");
        combatUI.UpdateTargetHUD(currentEnemy);
        yield return new WaitForSeconds(1f);

        if (currentEnemy.HasStatusEffect(StatusEffectType.Stun) || currentEnemy.HasStatusEffect(StatusEffectType.Freeze))
        {
            yield return StartCoroutine(ProcessSkippedTurn(currentEnemy));
            yield break;
        }
        
        currentEnemy.ProcessCleansingEffectsAtTurnStart();
        if (CheckGameState()) yield break;

        currentEnemy.ProcessDoTsAndHoTs();
        if (CheckGameState()) yield break;

        currentEnemy.TickDownCooldowns();
        currentEnemy.RegenerateEnergy();
        yield return new WaitForSeconds(1f);

        var affordableSkills = currentEnemy.characterSheet.startingSkills
            .Where(s => s.energyCost <= currentEnemy.currentEnergy && !currentEnemy.IsSkillOnCooldown(s)).ToList();

        if (affordableSkills.Count > 0)
        {
            Skill skillToUse = affordableSkills[Random.Range(0, affordableSkills.Count)];
            currentEnemy.UseSkill(skillToUse, playerCombatant);
        }
        else
        {
            Debug.Log($"<color=orange>{currentEnemy.characterSheet.name} has no affordable actions.</color>");
        }

        yield return new WaitForSeconds(1.5f);
        if (CheckGameState()) yield break;

        currentEnemy.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);

        AdvanceTurn();
    }

    void AdvanceTurn()
    {
        currentTurnIndex++;
        if (currentTurnIndex >= turnOrder.Count)
        {
            currentTurnIndex = 0;
            Debug.Log("--- New Round ---");
        }
        StartNextTurn();
    }

    public void OnPlayerSkillSelection(Skill skill)
    {
        if (state != CombatState.PLAYERTURN) return;
        if (skill.targetType == TargetType.Enemy && currentTarget == null)
        {
            Debug.LogWarning("No target selected for an enemy-targeted skill!");
            return;
        }

        state = CombatState.PROCESSING;
        StartCoroutine(PlayerAttack(skill));
    }

    IEnumerator PlayerAttack(Skill skill)
    {
        combatUI.DisablePlayerActions();
        Combatant finalTarget = (skill.targetType == TargetType.Enemy) ? currentTarget : playerCombatant;
        playerCombatant.UseSkill(skill, finalTarget);
        
        yield return new WaitForSeconds(1.5f);

        if (currentTarget != null && currentTarget.currentHealth <= 0)
        {
            SetCurrentTarget(null);
        }
        
        if (CheckGameState()) yield break;
        
        playerCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);

        AdvanceTurn();
    }
    
    public void SetCurrentTarget(Combatant target)
    {
        if (currentTarget != null && currentTarget.targetIndicator != null)
            currentTarget.targetIndicator.SetActive(false);
            
        currentTarget = target;
        
        if (currentTarget != null && currentTarget.targetIndicator != null)
            currentTarget.targetIndicator.SetActive(true);

        combatUI.UpdateTargetHUD(currentTarget);
    }

    public Combatant GetCurrentTarget() => currentTarget;

    private bool CheckGameState()
    {
        enemies.RemoveAll(e => e != null && e.currentHealth <= 0);
        if (enemies.Count == 0)
        {
            state = CombatState.WON;
            EndCombat();
            return true;
        }
        if (playerCombatant.currentHealth <= 0)
        {
            state = CombatState.LOST;
            EndCombat();
            return true;
        }
        return false;
    }

    void EndCombat()
    {
        combatUI.DisablePlayerActions();
        if (state == CombatState.WON) Debug.Log("<color=green>You Won!</color>");
        else if (state == CombatState.LOST) Debug.Log("<color=red>You Lost.</color>");
    }
    
    public void OnSkipTurnClicked()
    {
        if (state != CombatState.PLAYERTURN) return;
        state = CombatState.PROCESSING;
        StartCoroutine(SkipTurn());
    }

    private IEnumerator SkipTurn()
    {
        combatUI.DisablePlayerActions();
        Debug.Log("Player skips their turn.");
        playerCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);
        AdvanceTurn();
    }

    private IEnumerator ProcessSkippedTurn(Combatant skippedCombatant)
    {
        if (!skippedCombatant.HasStatusEffect(StatusEffectType.Ethereal))
        {
            string effectName = skippedCombatant.HasStatusEffect(StatusEffectType.Freeze) ? "Frozen" : "Stunned";
            Debug.Log($"<color=orange>{skippedCombatant.characterSheet.name} is {effectName} and skips their turn!</color>");
        } else {
             Debug.Log($"<color=grey>{skippedCombatant.characterSheet.name} is Ethereal and cannot act this turn.</color>");
        }
        
        skippedCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(1.5f);
        AdvanceTurn();
    }

    void SetupPlayerEventSubscriptions()
    {
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
    }
}