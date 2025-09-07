using UnityEngine;

[RequireComponent(typeof(Combatant), typeof(BoxCollider2D))]
public class Targetable : MonoBehaviour
{
    private Combatant combatant;
    private CombatManager combatManager;

    void Awake()
    {
        combatant = GetComponent<Combatant>();
    }

    void Start()
    {
        // Find the single instance of the managers
        combatManager = FindFirstObjectByType<CombatManager>();
    }

    private void OnMouseDown()
    {
        // The click now informs the CombatManager that a target has been chosen.
        if (combatManager != null)
        {
            // We no longer set the target here, we *confirm* the target.
            combatManager.OnTargetSelected(combatant);
        }
    }

    private void OnMouseEnter()
    {
        if (combatManager != null)
        {
            combatManager.SetCurrentTarget(combatant);
        }
    }

    private void OnMouseExit()
    {
        // When the mouse leaves, we revert the HUD to show no one.
        // The "selected" target concept is now gone from the hover logic.
        if (combatManager != null)
        {
            combatManager.SetCurrentTarget(null);
        }
    }
}