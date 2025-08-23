using System.Collections.Generic;
using System.Collections.ObjectModel; // To provide a read-only list externally
using UnityEngine;

[System.Serializable]
public class Stat
{
    public float baseValue;
    
    // The public property that everyone will access to get the final, calculated value.
    public float Value { get { if (isDirty) { _value = CalculateFinalValue(); isDirty = false; } return _value; } }

    private bool isDirty = true; // A flag to recalculate only when needed.
    private float _value;

    private readonly List<StatModifier> statModifiers;
    public readonly ReadOnlyCollection<StatModifier> StatModifiers;

    public Stat(float baseValue)
    {
        this.baseValue = baseValue;
        statModifiers = new List<StatModifier>();
        StatModifiers = statModifiers.AsReadOnly();
    }
    
    public void AddModifier(StatModifier mod)
    {
        isDirty = true;
        statModifiers.Add(mod);
    }

    public bool RemoveModifier(StatModifier mod)
    {
        if (statModifiers.Remove(mod))
        {
            isDirty = true;
            return true;
        }
        return false;
    }

    // The versatile removal method!
    public bool RemoveAllModifiersFromSource(object source)
    {
        int numRemovals = statModifiers.RemoveAll(mod => mod.Source == source);

        if (numRemovals > 0)
        {
            isDirty = true;
            return true;
        }
        return false;
    }

    private float CalculateFinalValue()
    {
        float finalValue = baseValue;
        float sumPercentAdd = 0;

        for (int i = 0; i < statModifiers.Count; i++)
        {
            StatModifier mod = statModifiers[i];

            if (mod.Type == StatModType.Flat)
            {
                finalValue += mod.Value;
            }
            else if (mod.Type == StatModType.Percent)
            {
                sumPercentAdd += mod.Value;
            }
        }
        
        // Final formula from GDD
        finalValue *= (1 + sumPercentAdd);

        return (float)Mathf.Ceil(finalValue);
    }
    
}