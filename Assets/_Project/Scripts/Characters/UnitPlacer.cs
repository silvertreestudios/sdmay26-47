using PathfinderTactics.Core;
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
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3Int layeredPos = grid.GetLayeredGridPosition(transform.position);
            Vector3Int resolved = grid.ResolveClosestLayeredPosition(
                new GridPosition(layeredPos.x, layeredPos.z),
                layeredPos.y
            );

            Unit unit = GetComponent<Unit>();
            grid.AddUnitAt(unit, resolved);
        }
    }
}
