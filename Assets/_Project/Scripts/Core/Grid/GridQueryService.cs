using System.Collections.Generic;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.Grid
{
    /// <summary>
    /// Query layer for layered grid lookups.
    /// Phase 0: method contracts only.
    /// </summary>
    public static class GridQueryService
    {
        private static readonly Vector2Int[] HorizontalNeighborOffsets =
        {
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, -1),
            new Vector2Int(0, 1),
            new Vector2Int(1, -1),
            new Vector2Int(1, 0),
            new Vector2Int(1, 1),
        };

        public static List<GridNode> GetNeighbors(GridNode node)
        {
            List<GridNode> neighbors = new List<GridNode>();
            if (node == null)
                return neighbors;

            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            if (gridSystem == null)
                return neighbors;

            Vector2Int currentColumn = new Vector2Int(node.Coordinates.x, node.Coordinates.z);
            for (int i = 0; i < HorizontalNeighborOffsets.Length; i++)
            {
                Vector2Int neighborColumn = currentColumn + HorizontalNeighborOffsets[i];
                GridNode resolved = ResolveSurface(
                    neighborColumn,
                    node.Coordinates.y,
                    MovementTag.Normal
                );
                if (resolved != null)
                {
                    neighbors.Add(resolved);
                }
            }

            return neighbors;
        }

        public static GridNode ResolveSurface(
            List<GridNode> column,
            int currentY,
            MovementTag movementMode = MovementTag.Normal
        )
        {
            if (column == null || column.Count == 0)
                return null;

            GridNode best = null;
            int bestDistance = int.MaxValue;
            bool bestIsDownward = false;

            foreach (GridNode candidate in column)
            {
                if (candidate == null)
                    continue;

                int deltaY = candidate.Coordinates.y - currentY;
                if (!IsReachableByMovementMode(deltaY, movementMode))
                    continue;

                int absDelta = Mathf.Abs(deltaY);
                bool isDownwardOrLevel = deltaY <= 0;

                if (
                    best == null
                    || absDelta < bestDistance
                    || (absDelta == bestDistance && isDownwardOrLevel && !bestIsDownward)
                )
                {
                    best = candidate;
                    bestDistance = absDelta;
                    bestIsDownward = isDownwardOrLevel;
                }
            }

            return best;
        }

        /// <summary>
        /// Returns ALL nodes in the column reachable from currentY under the given
        /// movement mode. Used by pathfinding to explore every valid layer
        /// (e.g. ground AND bridge at the same x,z).
        /// </summary>
        public static List<GridNode> GetReachableSurfaces(
            List<GridNode> column,
            int currentY,
            MovementTag movementMode = MovementTag.Normal
        )
        {
            List<GridNode> reachable = new List<GridNode>();
            if (column == null || column.Count == 0)
                return reachable;

            foreach (GridNode candidate in column)
            {
                if (candidate == null)
                    continue;

                int deltaY = candidate.Coordinates.y - currentY;
                if (IsReachableByMovementMode(deltaY, movementMode))
                    reachable.Add(candidate);
            }

            return reachable;
        }

        /// <summary>
        /// Overload that takes a column key and resolves against the GridSystem.
        /// </summary>
        public static List<GridNode> GetReachableSurfaces(
            Vector2Int columnKey,
            int currentY,
            MovementTag movementMode = MovementTag.Normal
        )
        {
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            if (gridSystem == null)
                return new List<GridNode>();

            List<GridNode> column = gridSystem.GetColumn(columnKey);
            return GetReachableSurfaces(column, currentY, movementMode);
        }

        public static GridNode ResolveSurface(
            Vector2Int columnKey,
            int currentY,
            MovementTag movementMode = MovementTag.Normal
        )
        {
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            if (gridSystem == null)
                return null;

            List<GridNode> column = gridSystem.GetColumn(columnKey);
            return ResolveSurface(column, currentY, movementMode);
        }

        public static List<IGridEntity> GetEntitiesAt(Vector3Int position)
        {
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            if (gridSystem == null)
                return new List<IGridEntity>();

            GridNode node = gridSystem.GetNode(position);
            if (node == null || node.Entities == null || node.Entities.Count == 0)
                return new List<IGridEntity>();

            // Return a copy so callers cannot accidentally mutate node state.
            return new List<IGridEntity>(node.Entities);
        }

        private static bool IsReachableByMovementMode(int deltaY, MovementTag movementMode)
        {
            switch (movementMode)
            {
                case MovementTag.Normal:
                    return Mathf.Abs(deltaY) <= 1;
                case MovementTag.Climb:
                    return true;
                case MovementTag.Jump:
                    return Mathf.Abs(deltaY) <= 2;
                case MovementTag.Fly:
                    return true;
                default:
                    return false;
            }
        }
    }
}
