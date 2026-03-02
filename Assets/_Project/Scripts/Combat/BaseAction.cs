using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Actions
{
    public abstract class BaseAction : MonoBehaviour
    {
        protected Unit unit;
        protected bool isActive;
        protected Action onActionComplete;

        protected virtual void Awake()
        {
            unit = GetComponent<Unit>();
        }

        /// <summary>
        /// Returns the name to display in the UI (e.g., "Strike").
        /// </summary>
        public abstract string GetActionName();

        /// <summary>
        /// How many actions (1-3) this consumes.
        /// </summary>
        public virtual int GetActionPointsCost()
        {
            return 1;
        }

        public abstract void TakeAction(GridPosition gridPosition, Action onActionComplete);

        public virtual bool IsValidActionGridPosition(GridPosition gridPosition)
        {
            return true;
        }

        // Returns a list of all valid grids (e.g., all enemies in range)
        public abstract System.Collections.Generic.List<Grid.GridPosition> GetValidActionGridPositions();

        public virtual List<GridPosition> GetActionRangeGridPositions()
        {
            // Default implementation just returns valid targets,
            // but child classes like MeleeAction will override this.
            return GetValidActionGridPositions();
        }
    }
}
