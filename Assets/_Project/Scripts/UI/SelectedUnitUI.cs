using TacticsGame.Characters;
using TacticsGame.Core;
using TMPro;
using UnityEngine;

namespace TacticsGame.UI
{
    /// <summary>
    /// Controls a small UI panel that displays the currently selected unit's name and HP.
    /// Place this script on a GameObject under a Canvas and assign the Text references.
    /// </summary>
    public class SelectedUnitUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        private TextMeshProUGUI nameText;

        [SerializeField]
        private TextMeshProUGUI hpText;

        [SerializeField]
        private GameObject panelRoot;

        private Unit currentSelectedUnit;
        private IDamageable currentUnitHealth;

        private void Start()
        {
            if (ServiceLocator.Get<UnitActionSystem>() != null)
            {
                ServiceLocator.Get<UnitActionSystem>().OnSelectedUnitChanged +=
                    UnitActionSystem_OnSelectedUnitChanged;
            }

            UpdateVisibility();
        }

        private void OnDestroy()
        {
            if (ServiceLocator.Get<UnitActionSystem>() != null)
            {
                ServiceLocator.Get<UnitActionSystem>().OnSelectedUnitChanged -=
                    UnitActionSystem_OnSelectedUnitChanged;
            }

            UnsubscribeFromUnitHealth();
        }

        private void UnitActionSystem_OnSelectedUnitChanged(object sender, System.EventArgs e)
        {
            UpdateForSelectedUnit(ServiceLocator.Get<UnitActionSystem>()?.SelectedUnit);
        }

        private void UpdateForSelectedUnit(Unit unit)
        {
            // Unsubscribe from previous unit health events
            UnsubscribeFromUnitHealth();

            currentSelectedUnit = unit;
            if (currentSelectedUnit == null)
            {
                UpdateVisibility();
                return;
            }

            // Update name
            if (nameText != null)
                nameText.text = currentSelectedUnit.name;

            // Subscribe to health component if present
            currentUnitHealth = currentSelectedUnit.GetComponent<IDamageable>();
            if (currentUnitHealth != null)
            {
                currentUnitHealth.OnHealthChanged += UnitHealth_OnHpChanged;
            }

            // Update HP display immediately
            RefreshHpText();

            UpdateVisibility();
        }

        private void UnsubscribeFromUnitHealth()
        {
            if (currentUnitHealth != null)
            {
                currentUnitHealth.OnHealthChanged -= UnitHealth_OnHpChanged;
                currentUnitHealth = null;
            }
        }

        private void UnitHealth_OnHpChanged(object sender, System.EventArgs e)
        {
            RefreshHpText();
        }

        private void RefreshHpText()
        {
            if (hpText == null)
                return;

            if (currentSelectedUnit == null)
            {
                hpText.text = "";
                return;
            }

            var health = currentSelectedUnit.GetComponent<IDamageable>();
            if (health == null)
            {
                // Fall back to using stats TotalHP if no UnitHealth component exists
                int total = currentSelectedUnit.getTotalHP();
                hpText.text = total.ToString();
            }
            else
            {
                hpText.text = $"{health.GetCurrentHealth()} / {health.GetMaxHealth()}";
            }
        }

        private void UpdateVisibility()
        {
            if (panelRoot == null)
                return;
            panelRoot.SetActive(currentSelectedUnit != null);
        }
    }
}
