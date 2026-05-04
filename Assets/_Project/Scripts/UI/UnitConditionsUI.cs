using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PathfinderTactics.UI
{
    /// <summary>
    /// Displays active conditions for a unit as a row of icons.
    /// Can track the selected unit or a specific unit.
    /// </summary>
    public class UnitConditionsUI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        private bool trackSelectedUnit = true;

        [SerializeField]
        private Unit specificUnit;

        [Header("References")]
        [SerializeField]
        private GameObject iconPrefab;

        [SerializeField]
        private Transform iconContainer;

        [SerializeField]
        private ConditionIconDataSO iconData;

        private Unit trackedUnit;
        private List<ConditionIconUI> iconPool = new List<ConditionIconUI>();
        private int activeIconCount = 0;

        private void Start()
        {
            if (iconContainer == null)
                iconContainer = transform;

            if (trackSelectedUnit)
            {
                if (ServiceLocator.TryGet<UnitActionSystem>(out var uas))
                {
                    uas.OnSelectedUnitChanged += HandleSelectedUnitChanged;
                    SetTrackedUnit(uas.SelectedUnit);
                }
            }
            else if (specificUnit != null)
            {
                SetTrackedUnit(specificUnit);
            }
        }

        private void OnDestroy()
        {
            if (trackSelectedUnit && ServiceLocator.TryGet<UnitActionSystem>(out var uas))
            {
                uas.OnSelectedUnitChanged -= HandleSelectedUnitChanged;
            }

            UnsubscribeFromTrackedUnit();
        }

        private void HandleSelectedUnitChanged(object sender, System.EventArgs e)
        {
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas))
            {
                SetTrackedUnit(uas.SelectedUnit);
            }
        }

        private void SetTrackedUnit(Unit newUnit)
        {
            UnsubscribeFromTrackedUnit();

            trackedUnit = newUnit;

            if (trackedUnit != null)
            {
                if (trackedUnit.TryGetComponent<UnitConditions>(out var conditions))
                {
                    conditions.OnConditionsChanged += RefreshIcons;
                }
            }

            RefreshIcons();
        }

        private void UnsubscribeFromTrackedUnit()
        {
            if (trackedUnit != null)
            {
                if (trackedUnit.TryGetComponent<UnitConditions>(out var conditions))
                {
                    conditions.OnConditionsChanged -= RefreshIcons;
                }
            }
        }

        private void RefreshIcons()
        {
            activeIconCount = 0;

            if (trackedUnit == null || iconPrefab == null || iconData == null)
            {
                HideAllIcons();
                return;
            }

            if (trackedUnit.TryGetComponent<UnitConditions>(out var conditions))
            {
                foreach (var kvp in conditions.ActiveConditions)
                {
                    ConditionType type = kvp.Key;
                    int value = kvp.Value.Value;
                    Sprite sprite = iconData.GetIcon(type);

                    // Skip if no icon defined? Or use a fallback?
                    if (sprite == null)
                        continue;

                    ConditionIconUI iconUI = GetOrCreateIcon();
                    iconUI.SetCondition(type, value, sprite);
                    iconUI.gameObject.SetActive(true);
                    activeIconCount++;
                }
            }

            // Hide remaining icons in pool
            for (int i = activeIconCount; i < iconPool.Count; i++)
            {
                iconPool[i].gameObject.SetActive(false);
            }
        }

        private ConditionIconUI GetOrCreateIcon()
        {
            if (activeIconCount < iconPool.Count)
            {
                return iconPool[activeIconCount];
            }

            GameObject go = Instantiate(iconPrefab, iconContainer);
            ConditionIconUI iconUI = go.GetComponent<ConditionIconUI>();

            if (iconUI == null)
            {
                Debug.LogError(
                    "[UnitConditionsUI] Prefab is missing ConditionIconUI component!",
                    go
                );
            }

            iconPool.Add(iconUI);
            return iconUI;
        }

        private void HideAllIcons()
        {
            foreach (var icon in iconPool)
            {
                icon.gameObject.SetActive(false);
            }
            activeIconCount = 0;
        }
    }
}
