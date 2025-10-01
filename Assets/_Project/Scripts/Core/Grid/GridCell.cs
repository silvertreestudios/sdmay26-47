using PathfinderTactics.Characters;
using UnityEngine;

namespace PathfinderTactics.Grid
{
    /// <summary>
    /// Contains all data for a single cell in the grid.
    /// </summary>
    public class GridCell
    {
        public GridPosition GridPosition { get; }
        public Vector3 WorldPosition { get; }

        public Unit occupyingUnit;
        public bool isWalkable = true;

        // TODO: Future data will go here:
        // public TerrainType terrainType;

        public GridCell(GridPosition gridPosition, Vector3 worldPosition)
        {
            this.GridPosition = gridPosition;
            this.WorldPosition = worldPosition;
        }
    }
}
