using TacticsGame.Characters;
using TacticsGame.Core;
using TMPro;
using UnityEngine;

namespace TacticsGame.UI
{
    /// <summary>
    /// Displays the current and maximum HP of the currently selected unit.
    /// Updates dynamically when the selection changes or when health changes.
    /// </summary>
    public class UnitHealthUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TextMeshProUGUI hpText;

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
                if (trackedUnit.TryGetComponent<UnitHealth>(out var health))
                {
                    health.OnHealthChanged += HandleHealthChanged;
                }
            }

            UpdateVisuals();
        }

        private void UnsubscribeFromTrackedUnit()
        {
            if (trackedUnit != null)
            {
                if (trackedUnit.TryGetComponent<UnitHealth>(out var health))
                {
                    health.OnHealthChanged -= HandleHealthChanged;
                }
            }
        }

        private void HandleHealthChanged(object sender, System.EventArgs e)
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (hpText == null)
                return;

            if (trackedUnit == null)
            {
                hpText.text = "";
                return;
            }

            if (trackedUnit.TryGetComponent<UnitHealth>(out var health))
            {
                int currentHP = health.GetCurrentHealth();
                int maxHP = health.GetMaxHealth();
                hpText.text = $"HP: {currentHP}/{maxHP}";
            }
            else
            {
                hpText.text = "HP: ???";
            }
        }
    }
}
