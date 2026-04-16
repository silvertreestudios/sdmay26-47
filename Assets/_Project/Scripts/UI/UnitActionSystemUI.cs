using System;
using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Characters;
using PathfinderTactics.Combat;
using PathfinderTactics.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PathfinderTactics.UI
{
    public class UnitActionSystemUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private GameObject actionMenuContainer;

        [SerializeField]
        private Transform actionButtonContainer;

        [SerializeField]
        private GameObject actionButtonPrefab;

        [SerializeField]
        private TextMeshProUGUI actionPointsText;

        private List<ActionButtonUI> actionButtonPool = new List<ActionButtonUI>();
        private List<ActionButtonUI> activeButtons = new List<ActionButtonUI>();

        private void Start()
        {
            // Subscribe to the State Change Event
            ServiceLocator.Get<UnitActionSystem>().OnSelectedUnitChanged +=
                UnitActionSystem_OnStateChanged;
            ServiceLocator.Get<UnitActionSystem>().OnActionCompleted +=
                UnitActionSystem_OnDataChanged;

            // Force menu off at start
            if (actionMenuContainer != null)
                actionMenuContainer.SetActive(false);

            UpdateVisuals();
        }

        private void UnitActionSystem_OnStateChanged(object sender, EventArgs e)
        {
            var currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;
            bool shouldShowMenu = (currentPhase == GamePhase.ActionSelection);

            // Debug.Log($"[UI MANAGER] State Change Detected: {currentPhase}. Menu Should Show: {shouldShowMenu}");

            // Update menu visibility
            if (actionMenuContainer.activeSelf != shouldShowMenu)
            {
                actionMenuContainer.SetActive(shouldShowMenu);
            }

            // Create/refresh buttons when entering ActionSelection
            if (shouldShowMenu)
            {
                Debug.Log("[UI MANAGER] Menu opened! Building buttons...");
                CreateUnitActionButtons();
            }
            else
            {
                // Clean up buttons when leaving ActionSelection
                ClearActionButtons();
            }

            UpdateVisuals(); // Update AP text too
        }

        private void ClearActionButtons()
        {
            foreach (ActionButtonUI button in activeButtons)
            {
                button.gameObject.SetActive(false);
                actionButtonPool.Add(button);
            }
            activeButtons.Clear();
        }

        private void CreateUnitActionButtons()
        {
            // Disable active buttons and send to pool
            ClearActionButtons();

            Unit selectedUnit = ServiceLocator.Get<UnitActionSystem>().SelectedUnit;
            if (selectedUnit == null)
            {
                Debug.LogWarning(
                    "[UI MANAGER] No unit selected when trying to create action buttons!"
                );
                return;
            }

            if (actionButtonPrefab == null)
            {
                Debug.LogError("[UI MANAGER] Action button prefab is not assigned!");
                return;
            }

            BaseAction[] actions = selectedUnit.GetBaseActionArray();
            Debug.Log($"[UI MANAGER] Creating {actions.Length} action buttons");

            foreach (BaseAction baseAction in actions)
            {
                if (!baseAction.isActiveAndEnabled)
                    continue;

                ActionButtonUI actionButtonUI;

                if (actionButtonPool.Count > 0)
                {
                    // Pull from pool
                    actionButtonUI = actionButtonPool[0];
                    actionButtonPool.RemoveAt(0);
                    actionButtonUI.gameObject.SetActive(true);
                    // Ensure it stays in the container if it was moved
                    actionButtonUI.transform.SetAsLastSibling();
                }
                else
                {
                    // Create new
                    GameObject buttonObj = Instantiate(actionButtonPrefab, actionButtonContainer);
                    actionButtonUI = buttonObj.GetComponent<ActionButtonUI>();
                }

                if (actionButtonUI == null)
                {
                    Debug.LogError(
                        $"[UI MANAGER] Button prefab or pooled object missing ActionButtonUI!"
                    );
                    continue;
                }

                actionButtonUI.SetBaseAction(baseAction);
                activeButtons.Add(actionButtonUI);
            }

            // Select first button if available
            if (activeButtons.Count > 0)
            {
                Button firstButton = activeButtons[0].GetComponent<Button>();
                if (firstButton != null)
                {
                    Debug.Log($"[UI MANAGER] Selecting: {firstButton.name}");
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
                }
            }
            else
            {
                Debug.LogWarning("[UI MANAGER] No action buttons were created!");
            }
        }

        private void UnitActionSystem_OnDataChanged(object sender, EventArgs e)
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            Unit selectedUnit = ServiceLocator.Get<UnitActionSystem>().SelectedUnit;
            if (selectedUnit != null && actionPointsText != null)
            {
                actionPointsText.text = $"AP: {selectedUnit.GetActionPointsRemaining()}";
            }
        }
    }
}
