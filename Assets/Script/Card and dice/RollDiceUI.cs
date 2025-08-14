using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RollDiceUI : MonoBehaviour
{
    [Header("Manager Reference")]
    [SerializeField] private CardAndDiceManager cardManager;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI die1Text;
    [SerializeField] private TextMeshProUGUI die2Text;
    [SerializeField] private Button rollButton;
    [SerializeField] private List<Button> cardButtons;

    [Header("Visuals")]
    [SerializeField] private Color deselectedColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.6f, 1f, 0.6f);
    [SerializeField] private Color invalidFlashColor = new Color(1f, 0.5f, 0.5f);
    [SerializeField] private float flashDuration = 0.3f;

    private List<CardData> currentHandCache;

    void Start()
    {
        cardManager.OnStateUpdated += UpdateVisualState;
        cardManager.OnRollCompleted += DisplayRollResult;
        cardManager.OnImpossibleSelection += FlashConflictingButtons;

        rollButton.onClick.AddListener(() => cardManager.PerformRoll());

        for (int i = 0; i < cardButtons.Count; i++)
        {
            int buttonIndex = i;
            cardButtons[i].onClick.AddListener(() => cardManager.SelectCard(buttonIndex));
        }
    }

    private void OnDestroy()
    {
        if (cardManager != null)
        {
            cardManager.OnStateUpdated -= UpdateVisualState;
            cardManager.OnRollCompleted -= DisplayRollResult;
            cardManager.OnImpossibleSelection -= FlashConflictingButtons;
        }
    }

    private void UpdateVisualState(List<CardData> hand, List<CardData> selection, bool isImpossible)
    {
        currentHandCache = hand;


        rollButton.interactable = !isImpossible;

        for (int i = 0; i < cardButtons.Count; i++)
        {
            if (i < hand.Count)
            {
                Button button = cardButtons[i];
                CardData card = hand[i];
                button.GetComponentInChildren<TextMeshProUGUI>().text = card.cardName;
                Image buttonImage = button.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = selection.Contains(card) ? selectedColor : deselectedColor;
                }
            }
        }
    }
    
    private void DisplayRollResult(bool possible, int die1, int die2)
    {
        if (possible)
        {
            die1Text.text = die1.ToString();
            die2Text.text = die2.ToString();
        }
        else
        {
            die1Text.text = "X";
            die2Text.text = "X";
        }
    }

    private void FlashConflictingButtons(List<CardData> conflictingCards)
    {
        if (currentHandCache == null) return;
        
        for (int i = 0; i < currentHandCache.Count; i++)
        {
            if (conflictingCards.Contains(currentHandCache[i]))
            {
                StartCoroutine(FlashCoroutine(cardButtons[i]));
            }
        }
    }

    private IEnumerator FlashCoroutine(Button buttonToFlash)
    {
        Image buttonImage = buttonToFlash.GetComponent<Image>();
        if (buttonImage == null) yield break;

        Color originalColor = buttonImage.color;
        buttonImage.color = invalidFlashColor;
        yield return new WaitForSeconds(flashDuration);
        buttonImage.color = originalColor;
    }
}