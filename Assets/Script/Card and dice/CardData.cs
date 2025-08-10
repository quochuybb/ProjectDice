using UnityEngine;

public enum CardTag { ForceValue, ForceParity, ForcePattern, SumExact, SumConstraint }
public enum Parity { Odd, Even }
public enum Pattern { Double, Distinct }
public enum SumConstraintType { Less, Greater }

[CreateAssetMenu(fileName = "New Card", menuName = "Card")]
public class CardData : ScriptableObject
{
    [Header("Card Info")]
    public string cardName;
    [TextArea] public string description;

    [Header("Card Logic")]
    public CardTag tag;

    [Tooltip("Value for ForceValue, SumExact, or SumConstraint")]
    public int value;


    [Tooltip("Used only for ForceParity tags")]
    public Parity parity; 

    [Tooltip("Used only for ForcePattern tags")]
    public Pattern pattern; 

    [Tooltip("Used only for SumConstraint tags")]
    public SumConstraintType sumConstraintType; 
}