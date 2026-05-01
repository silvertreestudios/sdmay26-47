using TacticsGame.Characters;
using TacticsGame.Core;
using TMPro;
using UnityEngine;

namespace TacticsGame.UI
{
    /// <summary>
    /// Displays the Armor Class (AC) of the currently selected unit.
    /// Updates dynamically when the selection changes or when unit conditions change.
    /// </summary>
    public class UnitACUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TextMeshProUGUI acText;

        private Unit trackedUnit;

        private void Start()
        {
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas))
            {
                uas.OnSelectedUnitChanged += UnitActionSystem_OnSelectedUnitChanged;
                SetTrackedUnit(uas.SelectedUnit);
            }
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas))
            {
                uas.OnSelectedUnitChanged -= UnitActionSystem_OnSelectedUnitChanged;
            }

            UnsubscribeFromTrackedUnit();
        }

        private void UnitActionSystem_OnSelectedUnitChanged(object sender, System.EventArgs e)
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
                var conditions = trackedUnit.GetComponent<UnitConditions>();
                if (conditions != null)
                {
                    conditions.OnConditionsChanged += HandleConditionsChanged;
                }
            }

            UpdateVisuals();
        }

        private void UnsubscribeFromTrackedUnit()
        {
            if (trackedUnit != null)
            {
                var conditions = trackedUnit.GetComponent<UnitConditions>();
                if (conditions != null)
                {
                    conditions.OnConditionsChanged -= HandleConditionsChanged;
                }
            }
        }

        private void HandleConditionsChanged()
        {
            UpdateVisuals();
        }

        private void Update()
        {
            if (trackedUnit != null)
            {
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            if (acText == null)
                return;

            if (trackedUnit == null)
            {
                acText.text = "";
                return;
            }

            Unit currentActiveUnit = null;
            if (ServiceLocator.TryGet<TurnManager>(out var turnManager))
            {
                currentActiveUnit = turnManager.CurrentUnit;
            }

            int currentAC = trackedUnit.GetArmorClass(currentActiveUnit);

            if (GridMathHelper.IsAnyFlankingVisual(trackedUnit))
            {
                if (
                    currentActiveUnit == null
                    || !GridMathHelper.IsFlanking(currentActiveUnit, trackedUnit)
                )
                {
                    currentAC -= 2;
                }
            }

            acText.text = $"AC: {currentAC}";
        }
    }
}
