using TacticsGame.Combat;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.Characters
{
    public class UnitGridObject : MonoBehaviour
    {
        private const bool STEALTH_DEBUG = true;

        private Unit unit;

        public Vector3Int CurrentLayeredPosition { get; private set; }

        public GridPosition CurrentGridPosition =>
            new GridPosition(CurrentLayeredPosition.x, CurrentLayeredPosition.z);

        private void Awake()
        {
            unit = GetComponent<Unit>();
        }

        private void Start()
        {
            var grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
                return;

            if (unit == null)
                unit = GetComponent<Unit>();
            if (unit == null)
                return;

            GridPosition gp = grid.GetGridPosition(transform.position);
            int referenceY = Mathf.RoundToInt(transform.position.y / grid.VerticalCellSize);
            CurrentLayeredPosition = grid.ResolveClosestLayeredPosition(gp, referenceY);

            grid.AddUnitAt(unit, CurrentLayeredPosition);
            unit.SnapToGrid(grid.GetWorldPosition(CurrentLayeredPosition));
        }

        public void SetInitialPosition(GridPosition gridPosition)
        {
            if (unit == null)
                unit = GetComponent<Unit>();
            if (unit == null)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            int referenceY = Mathf.RoundToInt(transform.position.y / grid.VerticalCellSize);
            CurrentLayeredPosition = grid.ResolveClosestLayeredPosition(gridPosition, referenceY);

            unit.SnapToGrid(grid.GetWorldPosition(CurrentLayeredPosition));
        }

        public void FinalizeMove(Vector3Int finalLayeredPosition)
        {
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            grid.MoveUnit(unit, CurrentLayeredPosition, finalLayeredPosition);
            CurrentLayeredPosition = finalLayeredPosition;

            if (STEALTH_DEBUG && unit != null)
            {
                Debug.Log(
                    $"<color=yellow>[STEALTH]</color> FinalizeMove unit={unit.name} -> {finalLayeredPosition}. Evaluating passive stealth."
                );
            }
            StealthResolver.EvaluatePassiveStateChanges(unit);
        }

        public void FinalizeMove(GridPosition finalPosition)
        {
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            int referenceY = CurrentLayeredPosition.y;
            Vector3Int resolved = grid.ResolveClosestLayeredPosition(finalPosition, referenceY);
            FinalizeMove(resolved);
        }
    }
}
