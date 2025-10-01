using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Automatically registers a unit placed in the scene with the GridSystem at startup.
    /// The goal is to allow for designer-friendly level setup, eventually leading to a
    /// map/custom level creator
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class UnitPlacer : MonoBehaviour
    {
        private void Start()
        {
            // Find the grid position corresponding to this unit's world position
            GridPosition gridPosition = GridSystem.Instance.GetGridPosition(transform.position);

            // Get the Unit component on this same GameObject
            Unit unit = GetComponent<Unit>();

            // Tell the GridSystem to add this unit to the grid
            GridSystem.Instance.AddUnitAt(unit, gridPosition);
        }
    }
}
