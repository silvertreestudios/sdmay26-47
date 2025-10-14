using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using UnityEngine;
using TMPro;

namespace PathfinderTactics.UI
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
        private UnitHealth currentUnitHealth;

        private void Start()
        {
            if (UnitActionSystem.Instance != null)
            {
                UnitActionSystem.Instance.OnSelectedUnitChanged += UnitActionSystem_OnSelectedUnitChanged;
            }

            UpdateVisibility();
        }

        private void OnDestroy()
        {
            if (UnitActionSystem.Instance != null)
            {
                UnitActionSystem.Instance.OnSelectedUnitChanged -= UnitActionSystem_OnSelectedUnitChanged;
            }

            UnsubscribeFromUnitHealth();
        }

        private void UnitActionSystem_OnSelectedUnitChanged(object sender, System.EventArgs e)
        {
            UpdateForSelectedUnit(UnitActionSystem.Instance?.SelectedUnit);
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
            currentUnitHealth = currentSelectedUnit.GetComponent<UnitHealth>();
            if (currentUnitHealth != null)
            {
                currentUnitHealth.OnHpChanged += UnitHealth_OnHpChanged;
            }

            // Update HP display immediately
            RefreshHpText();

            UpdateVisibility();
        }

        private void UnsubscribeFromUnitHealth()
        {
            if (currentUnitHealth != null)
            {
                currentUnitHealth.OnHpChanged -= UnitHealth_OnHpChanged;
                currentUnitHealth = null;
            }
        }

        private void UnitHealth_OnHpChanged(object sender, System.EventArgs e)
        {
            RefreshHpText();
        }

        private void RefreshHpText()
        {
            if (hpText == null) return;

            if (currentSelectedUnit == null)
            {
                hpText.text = "";
                return;
            }

            var health = currentSelectedUnit.GetComponent<UnitHealth>();
            if (health == null)
            {
                // Fall back to using stats TotalHP if no UnitHealth component exists
                int total = currentSelectedUnit.getTotalHP();
                hpText.text = total.ToString();
            }
            else
            {
                hpText.text = $"{health.GetCurrentHP()} / {health.GetMaxHP()}";
            }
        }

        private void UpdateVisibility()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(currentSelectedUnit != null);
        }
    }
}
