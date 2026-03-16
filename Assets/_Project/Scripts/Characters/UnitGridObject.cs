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
            CurrentGridPosition = ServiceLocator
                .Get<GridSystem>()
                .GetGridPosition(transform.position);
            ServiceLocator.Get<GridSystem>().AddUnitAt(unit, CurrentGridPosition);

            // Snap to ensure alignment
            unit.SnapToGrid(ServiceLocator.Get<GridSystem>().GetWorldPosition(CurrentGridPosition));
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
