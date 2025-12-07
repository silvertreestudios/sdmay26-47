using System;
using PathfinderTactics.Core;
using PathfinderTactics.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PathfinderTactics.UI
{
    public class UnitActionSystemUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private GameObject actionMenuContainer;

        [SerializeField]
        private Button attackButton; // Placeholder for now

        [SerializeField]
        private Button waitButton;

        [SerializeField]
        private TextMeshProUGUI actionPointsText;

        private void Start()
        {
            UnitActionSystem.Instance.OnSelectedUnitChanged +=
                UnitActionSystem_OnSelectedUnitChanged;
            UnitActionSystem.Instance.OnActionStarted += UnitActionSystem_OnActionStarted;
            UnitActionSystem.Instance.OnActionCompleted += UnitActionSystem_OnActionCompleted;

            // Setup simple buttons
            attackButton.onClick.AddListener(() =>
            {
                
                var unit = UnitActionSystem.Instance.SelectedUnit;

                //Search for units in range
                foreach (Unit other in UnitManager.AllUnits)
                {
                    if (other == unit) continue;
                    //TODO: Make range equal to weapon range. Range is in tiles.
                    if (unit.IsUnitInRange(other, 1))
                    {
                        unit.Attack(other);
                        Debug.Log("Unit is within 1 tile range");
                    }
                }


                Debug.Log("Attack Selected!");


                UnitActionSystem.Instance.SpendActionAndContinue(1);
            });

            waitButton.onClick.AddListener(() =>
            {
                UnitActionSystem.Instance.EndTurn();
            });

            HideMenu();
        }

        private void Update()
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            var unit = UnitActionSystem.Instance.SelectedUnit;
            if (unit != null)
            {
                actionPointsText.text = $"AP: {unit.GetActionPointsRemaining()}";
            }
            else
            {
                actionPointsText.text = "";
            }

            // Show menu if we are in the ActionSelection phase
            if (UnitActionSystem.Instance.currentPhase == GamePhase.ActionSelection)
            {
                if (!actionMenuContainer.activeSelf)
                    actionMenuContainer.SetActive(true);
            }
            else
            {
                if (actionMenuContainer.activeSelf)
                    actionMenuContainer.SetActive(false);
            }
        }

        private void HideMenu()
        {
            actionMenuContainer.SetActive(false);
        }

        private void UnitActionSystem_OnSelectedUnitChanged(object sender, EventArgs e)
        {
            UpdateVisuals();
        }

        private void UnitActionSystem_OnActionStarted(object sender, EventArgs e)
        {
            UpdateVisuals();
        }

        private void UnitActionSystem_OnActionCompleted(object sender, EventArgs e)
        {
            UpdateVisuals();
        }
    }
}
