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
    public List<Combatant> GetAllValidEnemyTargets()
    {
        return enemies.Where(e => e != null && e.currentHealth > 0).ToList();
    }
    public List<Combatant> GetValidEnemyTargets(Combatant primaryTarget)
    {
        return enemies.Where(e => e != null && e.currentHealth > 0 && e != primaryTarget).ToList();
    }
    private Combatant previouslyActiveCombatant;
    private Combatant currentTarget;
    private Skill selectedSkill;
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
            enemy.OnPrimeStatusChanged += () => { if (currentTarget == enemy) combatUI.UpdateTargetHUD(enemy); };
        }

        yield return new WaitForSeconds(0.5f);
        StartNextTurn();
    }

    void StartNextTurn()
    {
        if (CheckGameState()) return;

        // --- HIDE the previous indicator ---
        if (previouslyActiveCombatant != null && previouslyActiveCombatant.turnIndicator != null)
        {
            previouslyActiveCombatant.turnIndicator.SetActive(false);
        }

        Combatant currentCombatant = turnOrder[currentTurnIndex];

        // --- SHOW the new indicator ---
        if (currentCombatant.turnIndicator != null)
        {
            currentCombatant.turnIndicator.SetActive(true);
        }
        // --- STORE the current combatant for the next turn ---
        previouslyActiveCombatant = currentCombatant;

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

        combatUI.DisablePlayerActions();

        SetCurrentTarget(currentTarget); // Refresh target indicator if needed.

        playerCombatant.ProcessCleansingEffectsAtTurnStart();
        if (CheckGameState()) yield break;

        playerCombatant.ProcessDoTsAndHoTs();
        if (CheckGameState()) yield break;

        if (playerCombatant.HasStatusEffect(StatusEffectType.Ethereal) || playerCombatant.HasStatusEffect(StatusEffectType.Stun) || playerCombatant.HasStatusEffect(StatusEffectType.Freeze))
        {
            // If the turn is skipped, the controls remain disabled.
            playerCombatant.TickDownCooldowns();
            yield return StartCoroutine(ProcessSkippedTurn(playerCombatant));
            yield break; // Exit after the skipped turn is processed.
        }

        playerCombatant.TickDownCooldowns();
        playerCombatant.RegenerateEnergy();

        // This line is now correctly guarded. It will only be reached if the player can act.
        combatUI.EnablePlayerActions();
    }

    private IEnumerator EnemyTurn(Combatant currentEnemy)
    {
        Debug.Log($"--- {currentEnemy.characterSheet.name}'s TURN ---");
        combatUI.UpdateTargetHUD(currentEnemy);
        yield return new WaitForSeconds(0.5f);
        currentEnemy.ProcessCleansingEffectsAtTurnStart();
        if (CheckGameState()) yield break;

        currentEnemy.ProcessDoTsAndHoTs();
        if (CheckGameState()) yield break;
        // 1. Check for Turn-Skipping Effects
        if (currentEnemy.HasStatusEffect(StatusEffectType.Stun) || currentEnemy.HasStatusEffect(StatusEffectType.Freeze))
        {
            yield return StartCoroutine(ProcessSkippedTurn(currentEnemy));
            yield break;
        }

        // 2. Start of Turn Phase


        currentEnemy.TickDownCooldowns();
        currentEnemy.RegenerateEnergy();
        yield return new WaitForSeconds(0.5f);

        // 3. Action Phase (AI Logic)
        var affordableSkills = currentEnemy.characterSheet.startingSkills
            .Where(s => s.energyCost <= currentEnemy.currentEnergy && !currentEnemy.IsSkillOnCooldown(s)).ToList();

        if (affordableSkills.Count > 0)
        {
            Skill skillToUse = affordableSkills[Random.Range(0, affordableSkills.Count)];

            Combatant primaryTarget;

            // --- NEW, SIMPLIFIED AI TARGETING ---
            // Random skills don't need a primary target.
            if (skillToUse.randomHits > 0)
            {
                primaryTarget = null;
            }
            // Friendly skills (Healing or Self-targeted buffs) should target an ally or self.
            else if (skillToUse.effectType == SkillEffectType.Healing || skillToUse.targetType == TargetType.Self)
            {
                // For now, the AI will just target itself with friendly skills.
                // This is simple, predictable, and prevents healing the player.
                primaryTarget = currentEnemy;
            }
            else // It's a hostile, single-target skill.
            {
                // Target the player.
                primaryTarget = playerCombatant;
            }

            // Use the skill on the correctly determined primary target.
            currentEnemy.UseSkill(skillToUse, primaryTarget);
        }
        else
        {
            Debug.Log($"<color=orange>{currentEnemy.characterSheet.name} has no affordable actions and passes its turn.</color>");
        }

        yield return new WaitForSeconds(0.5f);

        // 4. End of Turn Phase
        if (CheckGameState()) yield break;

        currentEnemy.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);

        // 5. Advance to the next turn in the order
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
        if (state != CombatState.PLAYERTURN && state != CombatState.TARGETING) return;

        // --- NEW LOGIC FOR RANDOM SKILLS ---
        if (skill.randomHits > 0)
        {
            // Random skills don't need a target. Execute immediately.
            state = CombatState.PROCESSING;
            // We can pass 'null' for the target as it won't be used.
            StartCoroutine(PlayerAttack(skill, null));
            return;
        }

        // --- EXISTING LOGIC FOR OTHER SKILLS ---
        if (state == CombatState.TARGETING)
        {
            Debug.Log($"Cancelled targeting with {selectedSkill.name}.");
        }

        if (skill.targetType == TargetType.Self)
        {
            state = CombatState.PROCESSING;
            StartCoroutine(PlayerAttack(skill, playerCombatant));
        }
        else
        {
            state = CombatState.TARGETING;
            selectedSkill = skill;
            Debug.Log($"Selected skill: {selectedSkill.name}. Please choose a target.");
            combatUI.ShowTargetingPrompt(true, skill.skillName);
        }
    }

    public void OnTargetSelected(Combatant target)
    {
        // You can only select a target when in the TARGETING state.
        if (state != CombatState.TARGETING) return;

        // We have a skill and a target, proceed with the attack.
        state = CombatState.PROCESSING;
        combatUI.ShowTargetingPrompt(false); // Hide the prompt
        StartCoroutine(PlayerAttack(selectedSkill, target));
    }

    IEnumerator PlayerAttack(Skill skill, Combatant target) // New parameter
    {
        combatUI.DisablePlayerActions();

        // The target is now passed in directly.
        playerCombatant.UseSkill(skill, target);

        yield return new WaitForSeconds(0.5f);

        if (target != null && target.currentHealth <= 0)
        {
            // If the target of our attack died, deselect it.
            if (currentTarget == target)
            {
                SetCurrentTarget(null);
            }
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

        // --- ADD THIS to clean up the UI ---
        if (previouslyActiveCombatant != null && previouslyActiveCombatant.turnIndicator != null)
        {
            previouslyActiveCombatant.turnIndicator.SetActive(false);
        }

        if (state == CombatState.WON) Debug.Log("<color=green>You Won!</color>");
        else if (state == CombatState.LOST) Debug.Log("<color=red>You Lost.</color>");
    }

    public void OnSkipTurnClicked()
    {
        if (state != CombatState.PLAYERTURN && state != CombatState.TARGETING) return;

        // If we were targeting, cancel it.
        if (state == CombatState.TARGETING)
        {
            selectedSkill = null;
            combatUI.ShowTargetingPrompt(false);
        }

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
        }
        else
        {
            Debug.Log($"<color=grey>{skippedCombatant.characterSheet.name} is Ethereal and cannot act this turn.</color>");
        }

        skippedCombatant.TickDownStatusEffectsAtTurnEnd();
        yield return new WaitForSeconds(0.5f);
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
            // Pass the playerCombatant to the UI methods
            combatUI.UpdatePlayerStatusEffectsUI(effects, playerCombatant);
            combatUI.UpdatePlayerStats(playerCombatant);
            combatUI.UpdateSkillButtons(playerCombatant);
        };
        playerCombatant.OnCooldownsChanged += () => combatUI.UpdateSkillButtons(playerCombatant);
        playerCombatant.OnPrimeStatusChanged += () => combatUI.UpdatePlayerStatusEffectsUI(playerCombatant.activeStatusEffects, playerCombatant);
    }

    public List<Combatant> GetValidAllyTargets(Combatant self)
    {
        if (self.isPlayer)
        {
            // Player's only ally is themself, so return an empty list.
            return new List<Combatant>();
        }
        else // It's an enemy
        {
            // Enemy's allies are all other living enemies.
            return enemies.Where(e => e != null && e.currentHealth > 0 && e != self).ToList();
        }
    }

    // Gets a list of hostile targets for a given combatant.
    public List<Combatant> GetHostileTargets(Combatant self)
    {
        if (self.isPlayer)
        {
            // Player's hostiles are all living enemies.
            return enemies.Where(e => e != null && e.currentHealth > 0).ToList();
        }
        else // It's an enemy
        {
            // Enemy's only hostile is the player.
            return new List<Combatant> { playerCombatant };
        }
    }

    public void TriggerCombo(ElementType detonatorElement, ElementType primeElement, Combatant caster, Combatant target)
    {
        Debug.Log($"<color=yellow>COMBO! Detonator: {detonatorElement}, Prime: {primeElement}</color>");

        // --- VOLCANO COMBO ---
        if (primeElement == ElementType.Inferno && detonatorElement == ElementType.Quake)
        {
            Debug.Log("VOLCANO!");

            // GDD: Deals moderate AoE damage to all enemies.
            int aoeDamage = (int)caster.Stats.Might.Value;
            List<Combatant> allEnemies = GetAllValidEnemyTargets();
            foreach (Combatant enemy in allEnemies)
            {
                // We can reuse the TakeDamage method for this
                enemy.TakeDamage(aoeDamage);
            }

            // GDD: Applies Stun to the Primed target.
            var stunEffect = new StatusEffect(StatusEffectType.Stun, 1, EffectClassification.Debuff);
            // The original caster of the detonator skill is the source of the stun.
            target.ApplyStatusEffect(stunEffect, caster, null);
        }
        // --- FLASH STEAM COMBO ---
        else if (primeElement == ElementType.Inferno && detonatorElement == ElementType.Tide)
        {
            Debug.Log("FLASH STEAM!");

            int comboDamage = Mathf.RoundToInt(caster.Stats.Might.Value * 1.5f);
            target.TakeDamage(comboDamage);

            var armorDownEffect = new StatusEffect(
                StatusEffectType.StatDown,      // The effect type
                2,                              // Duration
                EffectClassification.Debuff,    // It's a debuff
                StatType.Armor,                 // The stat to target
                StatModType.Percent,            // It's a percentage reduction
                0.50f                           // The value (50%)
            );

            // Apply the effect to the target, with the caster as the source.
            target.ApplyStatusEffect(armorDownEffect, caster, null);
        }
        // --- HEAT WAVE COMBO ---
        else if (primeElement == ElementType.Inferno && detonatorElement == ElementType.Cyclone)
        {
            Debug.Log("HEAT WAVE!");

            var burnEffectTemplate = new StatusEffect(
                StatusEffectType.Burn,          // The effect type
                3,                              // Duration
                EffectClassification.Debuff     // It's a debuff
            );

            burnEffectTemplate.TickValue = Mathf.RoundToInt(caster.Stats.Intelligence.Value * 1.0f);

            List<Combatant> allEnemies = GetHostileTargets(caster);

            Debug.Log($"Applying a {burnEffectTemplate.TickValue}/turn Burn to {allEnemies.Count} enemies.");

            foreach (Combatant enemy in allEnemies)
            {
                enemy.ApplyStatusEffect(burnEffectTemplate, caster, null);
            }
        }
        // --- MUDSLIDE COMBO ---
        else if (primeElement == ElementType.Quake && detonatorElement == ElementType.Tide)
        {
            Debug.Log("MUDSLIDE");

            int aoeDamage = Mathf.RoundToInt(caster.Stats.Might.Value * 0.5f);
            List<Combatant> allEnemies = GetHostileTargets(caster);

            Debug.Log($"Dealing {aoeDamage} AoE damage to {allEnemies.Count} enemies.");
            foreach (Combatant enemy in allEnemies)
            {
                enemy.TakeDamage(aoeDamage);
            }

            var speedDownEffect = new StatusEffect(
                StatusEffectType.StatDown,      // The effect type
                2,                              // Duration
                EffectClassification.Debuff,    // It's a debuff
                StatType.Speed,                 // The stat to target
                StatModType.Percent,            // It's a percentage reduction
                0.50f                           // The value (50%)
            );

            Debug.Log($"Applying a 50% Speed debuff to {allEnemies.Count} enemies.");
            foreach (Combatant enemy in allEnemies)
            {
                enemy.ApplyStatusEffect(speedDownEffect, caster, null);
            }
        }
        // --- OVERGROWTH COMBO ---
        else if (primeElement == ElementType.Quake && detonatorElement == ElementType.Verdant)
        {
            Debug.Log("OVERGROWTH");
            
            int aoeDamage = Mathf.RoundToInt(caster.Stats.Might.Value * 0.5f);
            List<Combatant> allEnemies = GetHostileTargets(caster);

            foreach (Combatant enemy in allEnemies)
            {
                enemy.TakeDamage(aoeDamage);
            }

            var mortalWoundEffect = new StatusEffect(StatusEffectType.MortalWound, 3, EffectClassification.Debuff);
            foreach (Combatant enemy in allEnemies)
            {
                enemy.ApplyStatusEffect(mortalWoundEffect, caster, null);
            }
        }

        // --- MAELSTROM COMBO ---
        else if (primeElement == ElementType.Tide && detonatorElement == ElementType.Cyclone)
        {
            Debug.Log("MAELSTROM");
            
            int comboDamage = Mathf.RoundToInt(caster.Stats.Might.Value * 1.5f);
            target.TakeDamage(comboDamage);

            var vulnerableEffect = new StatusEffect(StatusEffectType.Vulnerable, 2, EffectClassification.Debuff);
            target.ApplyStatusEffect(vulnerableEffect, caster, null);
        }
        
        // --- TOXIC BLOOM COMBO ---
        else if (primeElement == ElementType.Tide && detonatorElement == ElementType.Verdant)
        {
            Debug.Log("TOXIC BLOOM");

            var poisonEffectTemplate = new StatusEffect(StatusEffectType.Poison, 3, EffectClassification.Debuff);
            poisonEffectTemplate.TickValue = Mathf.RoundToInt(caster.Stats.Intelligence.Value * 0.7f);

            List<Combatant> allEnemies = GetHostileTargets(caster);
            foreach (Combatant enemy in allEnemies)
            {
                enemy.ApplyStatusEffect(poisonEffectTemplate, caster, null);
            }
        }

        // --- UPROOT COMBO ---
        else if (primeElement == ElementType.Cyclone && detonatorElement == ElementType.Verdant)
        {
            Debug.Log("UPROOT");
            
            int aoeDamage = Mathf.RoundToInt(caster.Stats.Might.Value * 0.5f);
            List<Combatant> allEnemies = GetHostileTargets(caster);

            foreach (Combatant enemy in allEnemies)
            {
                enemy.TakeDamage(aoeDamage);
            }

            var woundEffect = new StatusEffect(StatusEffectType.Wound, 99, EffectClassification.Debuff);
            woundEffect.Stacks = 3; // The effect itself will have 3 stacks.
            
            Skill comboWoundSkill = ScriptableObject.CreateInstance<Skill>();
            comboWoundSkill.stacksToApply = 3;

            foreach (Combatant enemy in allEnemies)
            {
                enemy.ApplyStatusEffect(woundEffect, caster, comboWoundSkill);
            }
        }

    }
}