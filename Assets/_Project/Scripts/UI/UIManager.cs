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

        [SerializeField]
        private GameObject statusPanel;

        private void Start()
        {
            // Subscribe to game events
            UnitActionSystem.Instance.OnSelectedUnitChanged +=
                UnitActionSystem_OnSelectedUnitChanged;

            UnitActionSystem.Instance.OnActionCompleted += onActionCompleted;

            // Set up button listener
            endTurnButton.onClick.AddListener(() =>
            {
                // When clicked, tell the action system to end the current unit's turn
                UnitActionSystem.Instance.EndTurn();
            });

            UpdateStatus();
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
        private void onActionCompleted(object sender, System.EventArgs e)
        {
            UpdateStatus();
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

        private void UpdateStatus()
        {
            int p1Health = UnitManager.AllUnits[0].GetCurrentHP();
            int p2Health = UnitManager.AllUnits[1].GetCurrentHP();

            statusPanel.transform.Find("Player_one_health").GetComponent<TextMeshProUGUI>().text = $"Player one health \n {p1Health}";
            statusPanel.transform.Find("Player_two_health").GetComponent<TextMeshProUGUI>().text = $"Player two health \n {p2Health}";
        }

    }
}
