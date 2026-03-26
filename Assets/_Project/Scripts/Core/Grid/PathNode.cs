using UnityEngine;

namespace PathfinderTactics.Grid
{
    /// <summary>
    /// A helper class for the Pathfinding system.
    //  It represents a single node on the grid and stores data needed for A* calculations.
    /// </summary>
    public class PathNode
    {
        public GridPosition GridPosition { get; }
        public int ElevationY { get; }
        public Vector3Int LayeredPosition =>
            new Vector3Int(GridPosition.x, ElevationY, GridPosition.z);

        // G-cost: Walking distance from the start node
        public int gCost;

        // H-cost: Heuristic distance to the end node (Manhattan distance)
        public int hCost;

        // F-cost: The sum of G-cost and H-cost
        public int fCost;

        // A reference to the node that led to this one, used to reconstruct the path
        public PathNode cameFromNode;

        // If a node is walkable
        public bool isWalkable = true;

        public PathNode(GridPosition gridPosition, int elevationY = 0)
        {
            this.GridPosition = gridPosition;
            this.ElevationY = elevationY;
        }

        public void CalculateFCost()
        {
            fCost = gCost + hCost;
        }
    }
}
