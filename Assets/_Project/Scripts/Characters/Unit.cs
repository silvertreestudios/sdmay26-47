using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    public class Unit : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private UnitStatsSO stats;

        public GridPosition CurrentGridPosition { get; private set; }

        // Budget is used to track how far a unit can move
        private int movementBudgetRemaining;

        /// <summary>
        /// Gets the unit's maximum movement distance in number of grid cells.
        /// </summary>
        public int GetMoveDistanceInCells()
        {
            if (stats == null)
                return 0;

            // Pathfinder 2e rule: Movement is in 5-foot increments.
            // Assuming our grid cells are 5x5 feet.
            return stats.speedInFeet / 5;
        }

        public void SetInitialPosition(GridPosition gridPosition)
        {
            CurrentGridPosition = gridPosition;
            transform.position = GridSystem.Instance.GetWorldPosition(gridPosition);
        }

        public void Move(GridPosition newPosition)
        {
            // TODO: The actual visual movement (walking, running, jumping, etc.) will be handled later.
            GridSystem.Instance.MoveUnit(this, newPosition);
            CurrentGridPosition = newPosition;

            // Teleport to the new position for now
            transform.position = GridSystem.Instance.GetWorldPosition(newPosition);
        }

        public int GetMaxMoveCost()
        {
            // A unit's move distance in cells * the cost of moving to one straight cell.
            return GetMoveDistanceInCells() * Pathfinding.MOVE_STRAIGHT_COST;
        }

        // Called when a unit's turn begins or when it's selected for movement.
        public void StartMoveAction()
        {
            // Reset the budget to the maximum allowed for this unit.
            movementBudgetRemaining = GetMaxMoveCost();
        }

        // Call this to spend budget when moving.
        public void SpendMovement(int amount)
        {
            movementBudgetRemaining -= amount;
        }

        public int GetMovementBudgetRemaining()
        {
            return movementBudgetRemaining;
        }

        public void FinalizeMove(GridPosition finalPosition)
        {
            GridSystem.Instance.MoveUnit(this, finalPosition);
            CurrentGridPosition = finalPosition;
        }
    }
}
