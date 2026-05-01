using System.Collections;
using System.Collections.Generic;
using TacticsGame.Actions;
using TacticsGame.Core;
using TacticsGame.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TacticsGame.UI
{
    public class ActionMenuUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private ActionButtonUI actionButtonPrefab;

        [SerializeField]
        private Transform buttonContainer;

        [SerializeField]
        private ScrollRect scrollRect;

        [SerializeField]
        private UIDataConfig uiDataConfig;

        [Header("AP Cost Sprites")]
        [SerializeField]
        private Sprite apCostOne;

        [SerializeField]
        private Sprite apCostTwo;

        [SerializeField]
        private Sprite apCostThree;

        private List<ActionButtonUI> spawnedButtons = new List<ActionButtonUI>();

        public void PopulateMenu(BaseAction[] availableActions)
        {
            Debug.Log(
                $"<color=green>[MENU DEBUG]</color> PopulateMenu called with {availableActions?.Length ?? 0} actions."
            );

            // Clear existing buttons
            ClearMenu();

            if (availableActions == null)
                return;

            foreach (BaseAction action in availableActions)
            {
                if (action == null || !action.isActiveAndEnabled)
                    continue;

                // Determine the icon
                Sprite iconToUse = null;
                if (action.actionData != null)
                {
                    iconToUse = action.actionData.abilityIcon; // Default fallback
                }

                // If the action has a specific damage type, override the default icon
                var actionDamageType = action.GetPrimaryDamageType();
                if (
                    actionDamageType != TacticsGame.Characters.DamageType.Untyped
                    && uiDataConfig != null
                )
                {
                    Sprite typeIcon = uiDataConfig.GetDamageIcon(actionDamageType);
                    if (typeIcon != null)
                    {
                        iconToUse = typeIcon;
                    }
                }

                // Determine AP cost icon
                int cost = action.GetActionPointsCost();
                Sprite costIcon = GetAPCostSprite(cost);

                // Instantiate and setup
                if (actionButtonPrefab == null)
                {
                    Debug.LogError(
                        "<color=red>[MENU DEBUG]</color> actionButtonPrefab is NULL! Cannot spawn buttons."
                    );
                    continue;
                }

                ActionButtonUI newButton = Instantiate(actionButtonPrefab, buttonContainer);
                Debug.Log(
                    $"<color=green>[MENU DEBUG]</color> Spawned button for: {action.GetActionName()}"
                );
                newButton.Setup(
                    action.GetActionName(),
                    iconToUse,
                    costIcon,
                    () => OnActionButtonClicked(action, newButton)
                );

                // Dim the button if the action cannot be afforded or executed
                newButton.SetInteractable(action.CanExecuteAction());

                spawnedButtons.Add(newButton);
            }

            // Auto-select the first button for keyboard/joystick navigation with a frame delay
            if (gameObject.activeInHierarchy && spawnedButtons.Count > 0)
            {
                StartCoroutine(SelectFirstButtonDelayed());
            }
        }

        private IEnumerator SelectFirstButtonDelayed()
        {
            yield return null; // Wait for one frame
            if (spawnedButtons.Count > 0 && spawnedButtons[0] != null)
            {
                var btn = spawnedButtons[0].GetComponent<Button>();
                if (btn != null)
                    btn.Select();
            }
        }

        public void ClearMenu()
        {
            foreach (var btn in spawnedButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            spawnedButtons.Clear();
        }

        private Sprite GetAPCostSprite(int cost)
        {
            switch (cost)
            {
                case 1:
                    return apCostOne;
                case 2:
                    return apCostTwo;
                case 3:
                    return apCostThree;
                default:
                    return null;
            }
        }

        private void OnActionButtonClicked(BaseAction action, ActionButtonUI clickedButton)
        {
            // Execute logic
            ServiceLocator.Get<UnitActionSystem>().SetSelectedAction(action);
            Debug.Log(
                $"[ActionMenu] Action Selected: {action.GetActionName()}. Cost: {action.GetActionPointsCost()}"
            );
        }

        /// <summary>
        /// Called via SendMessage from ActionButtonUI when it gains focus.
        /// Keeps the selected button within the visible area of the ScrollRect.
        /// </summary>
        public void OnButtonSelected(RectTransform buttonTransform)
        {
            if (scrollRect == null || buttonContainer == null)
                return;

            float containerHeight = ((RectTransform)buttonContainer).rect.height;
            float viewportHeight =
                scrollRect.viewport != null
                    ? scrollRect.viewport.rect.height
                    : ((RectTransform)scrollRect.transform).rect.height;

            if (containerHeight <= viewportHeight)
                return;

            // Current scroll position in pixels (0 is top)
            float scrollOffset =
                (1f - scrollRect.verticalNormalizedPosition) * (containerHeight - viewportHeight);

            // Button bounds relative to the top of the container
            float buttonTop =
                -buttonTransform.anchoredPosition.y - (buttonTransform.rect.height * 0.5f);
            float buttonBottom = buttonTop + buttonTransform.rect.height;

            // Check if button is out of bounds
            if (buttonTop < scrollOffset)
            {
                // Scroll UP to show the top of this button
                float targetNormalized = 1f - (buttonTop / (containerHeight - viewportHeight));
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(targetNormalized);
            }
            else if (buttonBottom > scrollOffset + viewportHeight)
            {
                // Scroll DOWN to show the bottom of this button
                float targetNormalized =
                    1f - ((buttonBottom - viewportHeight) / (containerHeight - viewportHeight));
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(targetNormalized);
            }
        }
    }
}
