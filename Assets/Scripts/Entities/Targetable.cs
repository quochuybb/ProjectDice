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
        if (combatManager != null && combatManager.state == CombatState.PLAYERTURN)
        {
            combatManager.SetCurrentTarget(combatant);
        }
    }

    private void OnMouseEnter()
    {
        // Use the CombatUI singleton to update the hover information
        if (CombatUI.Instance != null)
        {
            CombatUI.Instance.UpdateTargetHUD(combatant);
        }
    }

    private void OnMouseExit()
    {
        // Revert the HUD to show the currently selected target, not the hovered one
        if (CombatUI.Instance != null && combatManager != null)
        {
            CombatUI.Instance.UpdateTargetHUD(combatManager.GetCurrentTarget());
        }
    }
}