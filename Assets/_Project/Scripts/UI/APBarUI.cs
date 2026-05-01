using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsGame.UI
{
    /// <summary>
    /// Manages the discrete Action Point (AP) slots in the HUD.
    /// Responds to GameEvents to stay in sync with the active unit.
    /// </summary>
    public class APBarUI : MonoBehaviour
    {
        // TODO: make it so that ap bar is correcly left aligned when quickened or slowed.

        [Header("References")]
        [SerializeField]
        private GameObject apSlotPrefab;

        [SerializeField]
        private Transform container;

        private class APSlot
        {
            public Image fill;
            public Image empty;
        }

        private List<APSlot> activeSlots = new List<APSlot>();
        private Unit trackedUnit;

        private void OnEnable()
        {
            GameEvents.OnTurnOrderChanged += HandleTurnOrderChanged;
            GameEvents.OnUnitAPChanged += HandleAPChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnTurnOrderChanged -= HandleTurnOrderChanged;
            GameEvents.OnUnitAPChanged -= HandleAPChanged;
        }

        private void HandleTurnOrderChanged(Unit current, List<Unit> turnOrder)
        {
            // The AP Bar always tracks the currently active unit
            trackedUnit = current;

            if (trackedUnit != null)
            {
                var economy = trackedUnit.GetComponent<UnitActionEconomy>();
                if (economy != null)
                {
                    // Use the unit's actual capacity instead of a hardcoded 3
                    UpdateAPDisplay(economy.ActionPointsRemaining, economy.MaxActionPoints);
                }
            }
        }

        private void HandleAPChanged(Unit unit, int current, int max)
        {
            if (unit == trackedUnit)
            {
                UpdateAPDisplay(current, max);
            }
        }

        private void UpdateAPDisplay(int currentAP, int maxAP)
        {
            if (maxAP <= 0)
                return;

            if (activeSlots.Count != maxAP)
            {
                RebuildSlots(maxAP);
            }

            // Sync visuals
            for (int i = 0; i < activeSlots.Count; i++)
            {
                bool shouldBeFull = (i < currentAP);

                if (activeSlots[i].fill != null)
                {
                    activeSlots[i].fill.enabled = shouldBeFull;
                }

                if (activeSlots[i].empty != null)
                {
                    activeSlots[i].empty.enabled = !shouldBeFull;
                }

                // string fillStatus =
                //     activeSlots[i].fill != null ? activeSlots[i].fill.enabled.ToString() : "NULL";
                // string emptyStatus =
                //     activeSlots[i].empty != null ? activeSlots[i].empty.enabled.ToString() : "NULL";
                // Debug.Log(
                //     $"<color=cyan>[APBarUI]</color> Slot {i} State: Fill={fillStatus}, Empty={emptyStatus} (ShouldBeFull={shouldBeFull})"
                // );
            }
        }

        private void RebuildSlots(int maxAP)
        {
            foreach (Transform child in container)
            {
                if (child != null)
                    Destroy(child.gameObject);
            }
            activeSlots.Clear();

            for (int i = 0; i < maxAP; i++)
            {
                GameObject slotObj = Instantiate(apSlotPrefab, container);
                APSlot slotData = new APSlot();

                Transform fillT = slotObj.transform.Find("AP_Fill");
                if (fillT != null)
                    slotData.fill = fillT.GetComponent<Image>();

                Transform emptyT = slotObj.transform.Find("AP_Empty");
                if (emptyT != null)
                    slotData.empty = emptyT.GetComponent<Image>();

                activeSlots.Add(slotData);
            }
        }
    }
}
