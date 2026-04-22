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
        private ActionMenuUI actionMenu;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private TextMeshProUGUI actionPointsText;

        private bool isSubscribed = false;

        private void Awake()
        {
            Debug.Log("<color=cyan>[UI DEBUG]</color> UnitActionSystemUI Awake called.");
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            SetMenuVisibility(false);
        }

        private void Start()
        {
            Debug.Log("<color=cyan>[UI DEBUG]</color> UnitActionSystemUI Start called.");
            TryInitialize();
            UpdateVisuals();
        }

        private void Update()
        {
            if (!isSubscribed)
            {
                TryInitialize();
            }
        }

        private void TryInitialize()
        {
            if (isSubscribed)
                return;

            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas))
            {
                uas.OnSelectedUnitChanged += UnitActionSystem_OnStateChanged;
                uas.OnActionCompleted += UnitActionSystem_OnDataChanged;
                isSubscribed = true;
                Debug.Log(
                    "<color=cyan>[UI DEBUG]</color> UnitActionSystemUI successfully subscribed to UnitActionSystem events."
                );

                // Refresh state immediately in case we missed the first event
                UnitActionSystem_OnStateChanged(this, EventArgs.Empty);
            }
        }

        private void UnitActionSystem_OnStateChanged(object sender, EventArgs e)
        {
            var currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;
            bool shouldShowMenu = (currentPhase == GamePhase.ActionSelection);

            Debug.Log(
                $"<color=cyan>[UI DEBUG]</color> Phase: {currentPhase}, shouldShowMenu: {shouldShowMenu}"
            );

            // Update menu visibility
            SetMenuVisibility(shouldShowMenu);

            // Create/refresh buttons when entering ActionSelection
            if (shouldShowMenu)
            {
                Debug.Log(
                    $"<color=cyan>[UI DEBUG]</color> Menu opened! Attempting to build buttons for unit: {ServiceLocator.Get<UnitActionSystem>().SelectedUnit?.name ?? "NULL"}"
                );
                CreateUnitActionButtons();
            }
            else
            {
                // Clean up buttons when leaving ActionSelection
                if (actionMenu != null)
                {
                    actionMenu.ClearMenu();
                }
            }

            UpdateVisuals(); // Update AP text too
        }

        private void CreateUnitActionButtons()
        {
            if (actionMenu == null)
            {
                Debug.LogError("[UI MANAGER] ActionMenuUI reference is missing!");
                return;
            }

            Unit selectedUnit = ServiceLocator.Get<UnitActionSystem>().SelectedUnit;
            if (selectedUnit == null)
            {
                actionMenu.ClearMenu();
                return;
            }

            BaseAction[] actions = selectedUnit.GetBaseActionArray();
            Debug.Log(
                $"<color=cyan>[UI DEBUG]</color> Building menu for {selectedUnit.name}. Actions found: {actions?.Length ?? 0}"
            );
            actionMenu.PopulateMenu(actions);
        }

        private void UnitActionSystem_OnDataChanged(object sender, EventArgs e)
        {
            UpdateVisuals();
        }

        private void SetMenuVisibility(bool isVisible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = isVisible ? 1 : 0;
                canvasGroup.interactable = isVisible;
                canvasGroup.blocksRaycasts = isVisible;
            }
            else if (actionMenuContainer != null)
            {
                // Fallback to SetActive if no CanvasGroup is present
                if (actionMenuContainer == gameObject && !isVisible)
                {
                    Debug.LogWarning(
                        "<color=orange>[UI DEBUG]</color> Suicide Warning: Disabling the GameObject this script is attached to! Use a CanvasGroup instead."
                    );
                }
                actionMenuContainer.SetActive(isVisible);
            }
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
