using UnityEngine;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;

public class ActionButtonScript : MonoBehaviour
{
    private Unit currentSelectedUnit;
    private void Start()
    {
        if (UnitActionSystem.Instance != null)
        {
            UnitActionSystem.Instance.OnSelectedUnitChanged += UpdateVisibility;
        }
        UpdateVisibility(null, null);
    }

    private void UpdateVisibility(object sender, System.EventArgs e)
    {
        currentSelectedUnit = UnitActionSystem.Instance?.SelectedUnit;
        this.gameObject.SetActive(currentSelectedUnit != null);
    }

}
