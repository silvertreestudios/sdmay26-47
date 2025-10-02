using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PathfinderTactics.UI
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

        private void Start()
        {
            // Subscribe to game events
            UnitActionSystem.Instance.OnSelectedUnitChanged +=
                UnitActionSystem_OnSelectedUnitChanged;

            // Set up button listener
            endTurnButton.onClick.AddListener(() =>
            {
                // When clicked, tell the action system to end the current unit's turn
                UnitActionSystem.Instance.EndTurn();
            });

            UpdateActionUI(); // Initial update
        }

        private void OnDestroy()
        {
            UnitActionSystem.Instance.OnSelectedUnitChanged -=
                UnitActionSystem_OnSelectedUnitChanged;
        }

        private void UnitActionSystem_OnSelectedUnitChanged(object sender, System.EventArgs e)
        {
            UpdateActionUI();
        }

        private void UpdateActionUI()
        {
            Unit selectedUnit = UnitActionSystem.Instance.SelectedUnit;

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
    }
}
