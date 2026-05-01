using TacticsGame.Characters;
using TacticsGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsGame.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TextMeshProUGUI apText;

        [SerializeField]
        private Button endTurnButton;

        [SerializeField]
        private GameObject actionPanel;

        [SerializeField]
        private GameObject statusPanel;

        private void Start()
        {
            // Subscribe to game events
            ServiceLocator.Get<UnitActionSystem>().OnSelectedUnitChanged +=
                UnitActionSystem_OnSelectedUnitChanged;

            ServiceLocator.Get<UnitActionSystem>().OnActionCompleted += onActionCompleted;

            // Set up button listener
            endTurnButton.onClick.AddListener(() =>
            {
                // When clicked, tell the action system to end the current unit's turn
                ServiceLocator.Get<UnitActionSystem>().EndTurn();
            });

            UpdateStatus();
            UpdateActionUI(); // Initial update
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<UnitActionSystem>(out var unitActionSystem))
            {
                unitActionSystem.OnSelectedUnitChanged -= UnitActionSystem_OnSelectedUnitChanged;
            }
        }

        private void UnitActionSystem_OnSelectedUnitChanged(object sender, System.EventArgs e)
        {
            UpdateActionUI();
        }

        private void onActionCompleted(object sender, System.EventArgs e)
        {
            UpdateStatus();
        }

        private void UpdateActionUI()
        {
            Unit selectedUnit = ServiceLocator.Get<UnitActionSystem>().SelectedUnit;

            if (selectedUnit != null)
            {
                // A unit is selected, show the UI
                actionPanel.SetActive(true);
                apText.text = $"Action Points: {selectedUnit.GetActionPointsRemaining()}";
            }
            else
            {
                // No unit selected, hide the UI
                actionPanel.SetActive(false);
            }
        }

        // TODO: Remove this as its kinda not needed and not scalable and replaced.
        // useful for quickly looking at health for now ig.
        private void UpdateStatus()
        {
            // Ensure units exist
            if (UnitManager.AllUnits.Count <= 0)
                return;

            // Player 1 (Index 0)
            if (UnitManager.AllUnits.Count > 0)
            {
                Unit unit1 = UnitManager.AllUnits[0];
                IDamageable health1 = unit1.GetComponent<IDamageable>();
                int p1Health = (health1 != null) ? health1.GetCurrentHealth() : 0;

                var p1Text = statusPanel.transform.Find("Player_one_health");
                if (p1Text != null)
                    p1Text.GetComponent<TextMeshProUGUI>().text =
                        $"Player one health \n {p1Health}";
            }

            // Player 2 (Index 1) - Only if we have at least 2 units
            if (UnitManager.AllUnits.Count > 1)
            {
                Unit unit2 = UnitManager.AllUnits[1];
                IDamageable health2 = unit2.GetComponent<IDamageable>();
                int p2Health = (health2 != null) ? health2.GetCurrentHealth() : 0;

                var p2Text = statusPanel.transform.Find("Player_two_health");
                if (p2Text != null)
                    p2Text.GetComponent<TextMeshProUGUI>().text =
                        $"Player two health \n {p2Health}";
            }
        }
    }
}
