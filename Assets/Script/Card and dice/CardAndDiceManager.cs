using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardAndDiceManager : MonoBehaviour
{
    [Header("Card Database")]
    public List<CardData> allPossibleCards;

    // --- Public Events ---
    // MODIFIED: Now also tells the UI if the current stack is impossible.
    public event Action<List<CardData>, List<CardData>, bool> OnStateUpdated;
    public event Action<List<CardData>> OnImpossibleSelection;
    public event Action<bool, int, int> OnRollCompleted;

    // --- Private State ---
    private List<CardData> currentHand = new List<CardData>();
    private List<CardData> selectedCards = new List<CardData>();
    private bool isCurrentSelectionImpossible = false; // NEW: Tracks validity
    private const int MAX_SELECTED_CARDS = 6;

    void Start()
    {
        DrawNewHand();
    }

    public void SelectCard(int cardIndex)
    {
        if (cardIndex >= currentHand.Count) return;

        CardData clickedCard = currentHand[cardIndex];

        if (selectedCards.Contains(clickedCard)) // Logic for DESELECTING
        {
            selectedCards.Remove(clickedCard);
            // Re-check the stack, as it might become valid again.
            var conflictCheck = DiceRoller.CheckForImmediateConflicts(selectedCards);
            isCurrentSelectionImpossible = conflictCheck.Item1;
            OnStateUpdated?.Invoke(currentHand, selectedCards, isCurrentSelectionImpossible);
        }
        else // Logic for SELECTING
        {
            if (selectedCards.Count >= MAX_SELECTED_CARDS)
            {
                OnImpossibleSelection?.Invoke(new List<CardData> { clickedCard });
                return;
            }

            var potentialSelection = new List<CardData>(selectedCards) { clickedCard };
            var conflictCheck = DiceRoller.CheckForImmediateConflicts(potentialSelection);

            if (conflictCheck.Item1) // Conflict found!
            {
                // Fire the event to flash the conflicting cards.
                OnImpossibleSelection?.Invoke(conflictCheck.Item2);
                // DO NOT add the card to the selection.
                // DO NOT change isCurrentSelectionImpossible flag. The *committed* selection is still valid.
            }
            else // No conflict, proceed.
            {
                selectedCards.Add(clickedCard);
                isCurrentSelectionImpossible = false; // This new stack is valid.
                OnStateUpdated?.Invoke(currentHand, selectedCards, isCurrentSelectionImpossible);
            }
        }
    }

    public void PerformRoll()
    {
        // SAFETY GUARD: Do not allow rolling if the stack is impossible.
        if (isCurrentSelectionImpossible)
        {
            Debug.LogError("Attempted to roll with an impossible stack. This should not happen if UI is correct.");
            return;
        }

        var result = DiceRoller.RollWithCards(selectedCards);
        OnRollCompleted?.Invoke(result.possible, result.die1, result.die2);

        // A final roll with a "possible" stack might still have no valid outcomes
        // due to sum rules. The result.possible handles this final check.
        
        DrawNewHand();
    }

    public void DrawNewHand()
    {
        selectedCards.Clear();
        currentHand.Clear();
        isCurrentSelectionImpossible = false; // Reset the flag for the new hand.

        if (allPossibleCards.Count < 6) return;

        var shuffledDeck = allPossibleCards.OrderBy(c => UnityEngine.Random.value).ToList();
        for (int i = 0; i < 6; i++) { currentHand.Add(shuffledDeck[i]); }
        
        // Notify UI of the new, empty, and valid state.
        OnStateUpdated?.Invoke(currentHand, selectedCards, isCurrentSelectionImpossible);
    }
}