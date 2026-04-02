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


        private List<Button> actionButtons = new List<Button>();

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
            foreach (Transform buttonTransform in actionButtonContainer)
            {
                Destroy(buttonTransform.gameObject);
            }
            actionButtons.Clear();
        }

        private void CreateUnitActionButtons()
        {
            // destroy old buttons
            foreach (Transform buttonTransform in actionButtonContainer)
            {
                DestroyImmediate(buttonTransform.gameObject);
            }
            actionButtons.Clear();

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
                GameObject buttonObj = Instantiate(actionButtonPrefab, actionButtonContainer);
                ActionButtonUI actionButtonUI = buttonObj.GetComponent<ActionButtonUI>();

                if (actionButtonUI == null)
                {
                    Debug.LogError($"[UI MANAGER] Button prefab missing ActionButtonUI component!");
                    continue;
                }

                actionButtonUI.SetBaseAction(baseAction);
                Button button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    actionButtons.Add(button);
                }
            }

            // Select first button if available
            if (actionButtons.Count > 0)
            {
                Debug.Log($"[UI MANAGER] Forcing EventSystem to select: {actionButtons[0].name}");

                // Clear and re-select to force the UI highlight
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(actionButtons[0].gameObject);
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
