using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    public class UnitSelectorVisual : MonoBehaviour
    {
        [SerializeField]
        private GameObject selectorVisual;
        private Unit unit;

        private void Awake()
        {
            unit = GetComponent<Unit>();
        }

        private void Start()
        {
            // Subscribe to the event
            UnitActionSystem.Instance.OnSelectedUnitChanged +=
                UnitActionSystem_OnSelectedUnitChanged;

            // Run an initial update to ensure it's hidden at the start
            UpdateVisual();
        }

        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            if (UnitActionSystem.Instance != null)
            {
                UnitActionSystem.Instance.OnSelectedUnitChanged -=
                    UnitActionSystem_OnSelectedUnitChanged;
            }
        }

        private void UnitActionSystem_OnSelectedUnitChanged(object sender, System.EventArgs e)
        {
            Debug.Log(
                $"{gameObject.name}'s visual script RECEIVED the OnSelectedUnitChanged event."
            );
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (selectorVisual == null)
            {
                Debug.LogError(
                    "'" + gameObject.name + "' is missing its 'selectorVisual' reference!",
                    this
                );
                return;
            }
            if (unit == null)
            {
                Debug.LogError(
                    "'" + gameObject.name + "' could not find its 'Unit' component in Awake()!",
                    this
                );
                return;
            }

            bool shouldBeActive = (UnitActionSystem.Instance.SelectedUnit == unit);

            Debug.Log(
                gameObject.name + " - UpdateVisual() called. Should I be active? " + shouldBeActive
            );

            selectorVisual.SetActive(shouldBeActive);
        }
    }
}
