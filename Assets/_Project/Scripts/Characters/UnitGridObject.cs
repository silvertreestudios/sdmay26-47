using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    public class UnitGridObject : MonoBehaviour
    {
        private Unit unit;
        public GridPosition CurrentGridPosition { get; private set; }

        private void Awake()
        {
            unit = GetComponent<Unit>();
        }

        private void Start()
        {
            // Register self on grid at start
            var grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
                return;

            if (unit == null)
                unit = GetComponent<Unit>();
            if (unit == null)
                return;

            CurrentGridPosition = grid.GetGridPosition(transform.position);
            grid.AddUnitAt(unit, CurrentGridPosition);

            // Snap to ensure alignment
            unit.SnapToGrid(grid.GetWorldPosition(CurrentGridPosition));
        }

        public void SetInitialPosition(GridPosition gridPosition)
        {
            CurrentGridPosition = gridPosition;
            unit.SnapToGrid(ServiceLocator.Get<GridSystem>().GetWorldPosition(gridPosition));
        }

        public void FinalizeMove(GridPosition finalPosition)
        {
            ServiceLocator.Get<GridSystem>().MoveUnit(unit, CurrentGridPosition, finalPosition);
            CurrentGridPosition = finalPosition;
        }
    }
}
