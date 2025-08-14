using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardAndDiceManager : MonoBehaviour
{
    [Header("Card Database")]
    public List<CardData> allPossibleCards;

    public event Action<List<CardData>, List<CardData>, bool> OnStateUpdated;
    public event Action<List<CardData>> OnImpossibleSelection;
    public event Action<bool, int, int> OnRollCompleted;

    private List<CardData> currentHand = new List<CardData>();
    private List<CardData> selectedCards = new List<CardData>();
    private bool isCurrentSelectionImpossible = false;
    private const int MAX_SELECTED_CARDS = 6;

    void Start()
    {
        DrawNewHand();
    }

    public void SelectCard(int cardIndex)
    {
        if (cardIndex >= currentHand.Count) return;

        CardData clickedCard = currentHand[cardIndex];

        if (selectedCards.Contains(clickedCard)) 
        {
            selectedCards.Remove(clickedCard);
            var conflictCheck = DiceRoller.CheckForImmediateConflicts(selectedCards);
            isCurrentSelectionImpossible = conflictCheck.Item1;
            OnStateUpdated?.Invoke(currentHand, selectedCards, isCurrentSelectionImpossible);
        }
        else 
        {
            if (selectedCards.Count >= MAX_SELECTED_CARDS)
            {
                OnImpossibleSelection?.Invoke(new List<CardData> { clickedCard });
                return;
            }

            var potentialSelection = new List<CardData>(selectedCards) { clickedCard };
            var conflictCheck = DiceRoller.CheckForImmediateConflicts(potentialSelection);

            if (conflictCheck.Item1) 
            {
                OnImpossibleSelection?.Invoke(conflictCheck.Item2);
            }
            else 
            {
                selectedCards.Add(clickedCard);
                isCurrentSelectionImpossible = false; 
                OnStateUpdated?.Invoke(currentHand, selectedCards, isCurrentSelectionImpossible);
            }
        }
    }

    public void PerformRoll()
    {
        if (isCurrentSelectionImpossible)
        {
            Debug.LogError("Attempted to roll with an impossible stack. This should not happen if UI is correct.");
            return;
        }

        var result = DiceRoller.RollWithCards(selectedCards);
        OnRollCompleted?.Invoke(result.possible, result.die1, result.die2);

        
        DrawNewHand();
    }

    public void DrawNewHand()
    {
        selectedCards.Clear();
        currentHand.Clear();
        isCurrentSelectionImpossible = false; 

        if (allPossibleCards.Count < 6) return;

        var shuffledDeck = allPossibleCards.OrderBy(c => UnityEngine.Random.value).ToList();
        for (int i = 0; i < 6; i++) { currentHand.Add(shuffledDeck[i]); }
        
        OnStateUpdated?.Invoke(currentHand, selectedCards, isCurrentSelectionImpossible);
    }
}