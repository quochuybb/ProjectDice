using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DiceRoller
{

    #region Unchanged Core Logic
    private class DieState
    {
        public HashSet<int> PossibleValues { get; set; }
        public DieState() { PossibleValues = new HashSet<int> { 1, 2, 3, 4, 5, 6 }; }
        public bool IsValueForced() => PossibleValues.Count == 1;
    }
    public static (bool possible, int die1, int die2) RollWithCards(List<CardData> activeCards)
    {
        if (activeCards.Count(c => c.tag == CardTag.ForceValue) > 2) { return (false, -1, -1); }
        var sortedCards = activeCards.OrderBy(c => GetPriority(c.tag)).ToList();
        var die1 = new DieState(); var die2 = new DieState();
        bool die1ValueAssigned = false;
        CardData patternCard = null;
        var sumCards = new List<CardData>();
        foreach (var card in sortedCards)
        {
            switch (card.tag)
            {
                case CardTag.ForceValue:
                    DieState targetDie = die1ValueAssigned ? die2 : die1;
                    targetDie.PossibleValues = new HashSet<int> { card.value };
                    if (!die1ValueAssigned) die1ValueAssigned = true;
                    break;
                case CardTag.ForceParity:
                    bool applied = ApplyParity(die1, card.parity, force: !die1.IsValueForced());
                    if (!applied) { ApplyParity(die2, card.parity, force: true); }
                    break;
                case CardTag.ForcePattern: patternCard = card; break;
                case CardTag.SumExact: case CardTag.SumConstraint: sumCards.Add(card); break;
            }
        }
        if (die1.PossibleValues.Count == 0 || die2.PossibleValues.Count == 0) { return (false, -1, -1); }
        if (patternCard != null)
        {
            if (patternCard.pattern == Pattern.Double)
            {
                if (die1.IsValueForced() && die2.IsValueForced() && die1.PossibleValues.First() != die2.PossibleValues.First()) { return (false, -1, -1); }
                var intersection = new HashSet<int>(die1.PossibleValues);
                intersection.IntersectWith(die2.PossibleValues);
                if (intersection.Count == 0) { return (false, -1, -1); }
                die1.PossibleValues = intersection; die2.PossibleValues = intersection;
            }
        }
        var possiblePairs = new List<(int, int)>();
        foreach (var v1 in die1.PossibleValues)
        {
            foreach (var v2 in die2.PossibleValues)
            {
                if (patternCard != null)
                {
                    if (patternCard.pattern == Pattern.Double && v1 != v2) continue;
                    if (patternCard.pattern == Pattern.Distinct && v1 == v2) continue;
                }
                if (patternCard?.pattern == Pattern.Distinct && die1.IsValueForced() && die2.IsValueForced() && die1.PossibleValues.First() == die2.PossibleValues.First()) { return (false, -1, -1); }
                possiblePairs.Add((v1, v2));
            }
        }
        foreach (var sumCard in sumCards) { possiblePairs = possiblePairs.Where(pair => IsSumValid(pair, sumCard)).ToList(); }
        if (possiblePairs.Count == 0) { return (false, -1, -1); }
        int randomIndex = Random.Range(0, possiblePairs.Count);
        return (true, possiblePairs[randomIndex].Item1, possiblePairs[randomIndex].Item2);
    }
    private static int GetPriority(CardTag tag)
    {
        switch (tag)
        {
            case CardTag.ForceValue: return 1; case CardTag.ForceParity: return 2;
            case CardTag.ForcePattern: return 3; case CardTag.SumExact: return 4;
            case CardTag.SumConstraint: return 4; default: return 5;
        }
    }
    private static bool ApplyParity(DieState die, Parity parity, bool force = false)
    {
        if (!force) return false;
        if (parity == Parity.Even) { die.PossibleValues.RemoveWhere(v => v % 2 != 0); }
        else { die.PossibleValues.RemoveWhere(v => v % 2 == 0); }
        return true;
    }
    private static bool IsSumValid((int die1, int die2) pair, CardData sumCard)
    {
        int total = pair.die1 + pair.die2;
        switch (sumCard.tag)
        {
            case CardTag.SumExact: return total == sumCard.value;
            case CardTag.SumConstraint:
                if (sumCard.sumConstraintType == SumConstraintType.Less) return total < sumCard.value;
                else return total > sumCard.value;
            default: return true;
        }
    }
    #endregion

    public static (bool, List<CardData>) CheckForImmediateConflicts(List<CardData> cards)
    {
        if (cards == null || cards.Count <= 1) return (false, null);

        var conflictingCards = new List<CardData>();

        var forceValueCards = cards.Where(c => c.tag == CardTag.ForceValue).ToList();
        var forceDoubleCard = cards.FirstOrDefault(c => c.tag == CardTag.ForcePattern && c.pattern == Pattern.Double);
        var sumExactCards = cards.Where(c => c.tag == CardTag.SumExact).ToList();
        var sumLessCard = cards.FirstOrDefault(c => c.tag == CardTag.SumConstraint && c.sumConstraintType == SumConstraintType.Less);
        var sumGreaterCard = cards.FirstOrDefault(c => c.tag == CardTag.SumConstraint && c.sumConstraintType == SumConstraintType.Greater);

        // --- INITIAL VALIDATION: Check for illegal card counts ---
        if (forceValueCards.Count > 2) return (true, forceValueCards);
        if (cards.Count(c => c.tag == CardTag.SumConstraint && c.sumConstraintType == SumConstraintType.Less) > 1)
            return (true, cards.Where(c => c.tag == CardTag.SumConstraint && c.sumConstraintType == SumConstraintType.Less).ToList());
        if (cards.Count(c => c.tag == CardTag.SumConstraint && c.sumConstraintType == SumConstraintType.Greater) > 1)
            return (true, cards.Where(c => c.tag == CardTag.SumConstraint && c.sumConstraintType == SumConstraintType.Greater).ToList());
        if (sumExactCards.Count > 1 && sumExactCards.Select(c => c.value).Distinct().Count() > 1)
            return (true, sumExactCards); 

        int minPossibleSum = 2;
        int maxPossibleSum = 12;

        if (forceValueCards.Count == 1)
        {
            minPossibleSum = forceValueCards[0].value + 1;
            maxPossibleSum = forceValueCards[0].value + 6;
        }
        else if (forceValueCards.Count == 2)
        {
            minPossibleSum = maxPossibleSum = forceValueCards[0].value + forceValueCards[1].value;
        }

        if (forceDoubleCard != null)
        {
            if (forceValueCards.Count == 2 && forceValueCards[0].value != forceValueCards[1].value)
            {
                conflictingCards.Add(forceDoubleCard);
                conflictingCards.AddRange(forceValueCards);
                return (true, conflictingCards);
            }
            if (forceValueCards.Count == 1)
            {
                minPossibleSum = maxPossibleSum = forceValueCards[0].value * 2;
            }
        }
        
        foreach (var card in sumExactCards)
        {
            if (card.value < minPossibleSum || card.value > maxPossibleSum)
            {
                conflictingCards.Add(card);
                conflictingCards.AddRange(forceValueCards);
                if(forceDoubleCard != null) conflictingCards.Add(forceDoubleCard);
                return (true, conflictingCards.Distinct().ToList());
            }
            minPossibleSum = maxPossibleSum = card.value;
        }

        if (sumLessCard != null)
        {

            if (minPossibleSum >= sumLessCard.value)
            {
                conflictingCards.Add(sumLessCard);
                conflictingCards.AddRange(forceValueCards);
                if(forceDoubleCard != null) conflictingCards.Add(forceDoubleCard);
                if(sumExactCards.Any()) conflictingCards.AddRange(sumExactCards);
                return (true, conflictingCards.Distinct().ToList());
            }
            maxPossibleSum = Mathf.Min(maxPossibleSum, sumLessCard.value - 1);
        }

        if (sumGreaterCard != null)
        {

            if (maxPossibleSum <= sumGreaterCard.value)
            {
                conflictingCards.Add(sumGreaterCard);
                conflictingCards.AddRange(forceValueCards);
                if(forceDoubleCard != null) conflictingCards.Add(forceDoubleCard);
                if(sumExactCards.Any()) conflictingCards.AddRange(sumExactCards);
                return (true, conflictingCards.Distinct().ToList());
            }
            minPossibleSum = Mathf.Max(minPossibleSum, sumGreaterCard.value + 1);
        }

        if (minPossibleSum > maxPossibleSum)
        {
            return (true, cards.Where(c => c.tag == CardTag.SumExact || c.tag == CardTag.SumConstraint).ToList());
        }

        return (false, null);
    }
}