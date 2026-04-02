using System.Collections.Generic;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Grid
{
    public static class Pathfinding
    {
        public const int MOVE_STRAIGHT_COST = 10;
        public const int MOVE_DIAGONAL_COST = 14;
        private const bool DEBUG_REACHABILITY = false;
        private static readonly GridPosition[] NeighborOffsets =
        {
            new GridPosition(-1, -1),
            new GridPosition(-1, 0),
            new GridPosition(-1, 1),
            new GridPosition(0, -1),
            new GridPosition(0, 1),
            new GridPosition(1, -1),
            new GridPosition(1, 0),
            new GridPosition(1, 1),
        };
        private static readonly HashSet<string> loggedReachabilityKeys = new HashSet<string>();

        // Public path APIs

        public static List<Vector3Int> FindPath(Vector3Int startPosition, Vector3Int endPosition)
        {
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            GridNode startNode = gridSystem.GetNode(startPosition);
            GridNode endNode = gridSystem.GetNode(endPosition);
            if (startNode == null || endNode == null)
                return null;

            List<PathNode> openList = new List<PathNode>();
            HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>();
            Dictionary<Vector3Int, PathNode> pathNodeMap = new Dictionary<Vector3Int, PathNode>();

            PathNode start = GetOrCreatePathNode(startPosition, pathNodeMap);
            start.gCost = 0;
            start.hCost = CalculateHeuristic(startPosition, endPosition);
            start.CalculateFCost();
            openList.Add(start);

            while (openList.Count > 0)
            {
                PathNode currentNode = GetLowestFCostNode(openList);

                if (currentNode.LayeredPosition == endPosition)
                    return BuildPath(currentNode);

                openList.Remove(currentNode);
                closedSet.Add(currentNode.LayeredPosition);

                foreach (
                    PathNode neighbour in GetNeighbourList(currentNode, pathNodeMap, gridSystem)
                )
                {
                    if (closedSet.Contains(neighbour.LayeredPosition))
                        continue;

                    if (!neighbour.isWalkable)
                    {
                        closedSet.Add(neighbour.LayeredPosition);
                        continue;
                    }

                    int tentativeGCost =
                        currentNode.gCost + GetStepCost(gridSystem, currentNode, neighbour);

                    if (tentativeGCost < neighbour.gCost)
                    {
                        neighbour.cameFromNode = currentNode;
                        neighbour.gCost = tentativeGCost;
                        neighbour.hCost = CalculateHeuristic(
                            neighbour.LayeredPosition,
                            endPosition
                        );
                        neighbour.CalculateFCost();

                        if (!openList.Contains(neighbour))
                            openList.Add(neighbour);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves 2D positions to the best layered node in each column before running the full 3D A*.
        /// </summary>
        public static List<Vector3Int> FindPath(
            GridPosition startPosition,
            GridPosition endPosition
        )
        {
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            GridNode startNode = GetDefaultColumnNode(gridSystem, startPosition);
            GridNode endNode = GetDefaultColumnNode(gridSystem, endPosition);
            if (startNode == null || endNode == null)
                return null;

            return FindPath(startNode.Coordinates, endNode.Coordinates);
        }

        // Reachability

        public static List<Vector3Int> GetReachablePositions(
            Vector3Int startPosition,
            int maxMoveCost
        )
        {
            List<Vector3Int> reachable = new List<Vector3Int>();
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            GridNode startNode = gridSystem.GetNode(startPosition);

            if (startNode == null)
                return reachable;

            Dictionary<Vector3Int, PathNode> pathNodeMap = new Dictionary<Vector3Int, PathNode>();
            List<PathNode> openList = new List<PathNode>();

            PathNode start = new PathNode(
                new GridPosition(startPosition.x, startPosition.z),
                startPosition.y
            );
            start.gCost = 0;
            openList.Add(start);
            pathNodeMap[start.LayeredPosition] = start;

            while (openList.Count > 0)
            {
                PathNode currentNode = GetLowestGCostNode(openList);
                openList.Remove(currentNode);

                if (!reachable.Contains(currentNode.LayeredPosition))
                    reachable.Add(currentNode.LayeredPosition);

                foreach (
                    PathNode neighbour in GetNeighbourList(currentNode, pathNodeMap, gridSystem)
                )
                {
                    if (!neighbour.isWalkable)
                        continue;

                    if (gridSystem.IsPositionOccupied(neighbour.LayeredPosition))
                    {
                        if (neighbour.LayeredPosition != startPosition)
                            continue;
                    }

                    int tentativeGCost =
                        currentNode.gCost + GetStepCost(gridSystem, currentNode, neighbour);

                    if (tentativeGCost <= maxMoveCost)
                    {
                        if (
                            !pathNodeMap.ContainsKey(neighbour.LayeredPosition)
                            || tentativeGCost < neighbour.gCost
                        )
                        {
                            neighbour.cameFromNode = currentNode;
                            neighbour.gCost = tentativeGCost;
                            pathNodeMap[neighbour.LayeredPosition] = neighbour;

                            if (!openList.Contains(neighbour))
                                openList.Add(neighbour);
                        }
                    }
                }
            }

            if (DEBUG_REACHABILITY && reachable.Count <= 1)
            {
                GridPosition gp = new GridPosition(startPosition.x, startPosition.z);
                LogReachabilityDiagnostics(gridSystem, gp, maxMoveCost, reachable.Count);
            }

            return reachable;
        }

        /// <summary>
        /// Resolves the 2D start position to a layered node,
        /// then runs full 3D reachability search.
        /// </summary>
        public static List<Vector3Int> GetReachablePositions(
            GridPosition startPosition,
            int maxMoveCost
        )
        {
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            GridNode startNode = GetDefaultColumnNode(gridSystem, startPosition);
            if (startNode == null)
                return new List<Vector3Int>();

            return GetReachablePositions(startNode.Coordinates, maxMoveCost);
        }

        /// <summary>
        /// Projects a list of layered positions to unique 2D GridPositions.
        /// Useful for action range / targeting systems that work in column space.
        /// </summary>
        public static List<GridPosition> ProjectToGridPositions(List<Vector3Int> layered)
        {
            List<GridPosition> result = new List<GridPosition>();
            if (layered == null)
                return result;

            HashSet<GridPosition> seen = new HashSet<GridPosition>();
            foreach (Vector3Int v in layered)
            {
                GridPosition gp = new GridPosition(v.x, v.z);
                if (seen.Add(gp))
                    result.Add(gp);
            }
            return result;
        }

        // distance

        public static int CalculateDistance(GridPosition a, GridPosition b)
        {
            int xDistance = Mathf.Abs(a.x - b.x);
            int zDistance = Mathf.Abs(a.z - b.z);
            int remaining = Mathf.Abs(xDistance - zDistance);
            return MOVE_DIAGONAL_COST * Mathf.Min(xDistance, zDistance)
                + MOVE_STRAIGHT_COST * remaining;
        }

        public static int CalculateHeuristic(Vector3Int a, Vector3Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dz = Mathf.Abs(a.z - b.z);
            int dy = Mathf.Abs(a.y - b.y);
            int remaining = Mathf.Abs(dx - dz);
            return MOVE_DIAGONAL_COST * Mathf.Min(dx, dz)
                + MOVE_STRAIGHT_COST * remaining
                + MOVE_STRAIGHT_COST * dy;
        }

        // Internal helpers

        private static List<Vector3Int> BuildPath(PathNode endNode)
        {
            List<Vector3Int> path = new List<Vector3Int>();
            path.Add(endNode.LayeredPosition);

            PathNode current = endNode;
            while (current.cameFromNode != null)
            {
                current = current.cameFromNode;
                path.Add(current.LayeredPosition);
            }
            path.Reverse();
            return path;
        }

        private static PathNode GetLowestFCostNode(List<PathNode> list)
        {
            PathNode lowest = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].fCost < lowest.fCost)
                    lowest = list[i];
            }
            return lowest;
        }

        private static PathNode GetLowestGCostNode(List<PathNode> list)
        {
            PathNode lowest = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].gCost < lowest.gCost)
                    lowest = list[i];
            }
            return lowest;
        }

        /// <summary>
        /// Enumerates all reachable surfaces in each neighboring column,
        /// creating a PathNode for every valid layer. This is what enables
        /// bridge/multi-floor pathfinding.
        /// </summary>
        private static List<PathNode> GetNeighbourList(
            PathNode currentNode,
            Dictionary<Vector3Int, PathNode> pathNodeMap,
            GridSystem gridSystem
        )
        {
            List<PathNode> neighbourList = new List<PathNode>();
            GridPosition pos = currentNode.GridPosition;

            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                GridPosition offset = NeighborOffsets[i];
                GridPosition neighbourPos = pos + offset;
                Vector2Int neighbourColumn = new Vector2Int(neighbourPos.x, neighbourPos.z);

                List<GridNode> reachableSurfaces = GridQueryService.GetReachableSurfaces(
                    neighbourColumn,
                    currentNode.ElevationY,
                    MovementTag.Normal
                );

                foreach (GridNode surface in reachableSurfaces)
                {
                    if (offset.x != 0 && offset.z != 0)
                    {
                        if (
                            !IsValidDiagonalBridge(
                                currentNode.ElevationY,
                                surface.Coordinates.y,
                                gridSystem,
                                new Vector2Int(neighbourPos.x, pos.z),
                                new Vector2Int(pos.x, neighbourPos.z)
                            )
                        )
                            continue;
                    }

                    PathNode neighbourNode = GetOrCreatePathNode(surface.Coordinates, pathNodeMap);
                    neighbourNode.isWalkable = surface.IsWalkable();
                    neighbourList.Add(neighbourNode);
                }
            }

            // Same-column vertical neighbors (ladders, trapdoors, stairwells
            // that land on the same X,Z tile).
            Vector2Int currentColumn = new Vector2Int(pos.x, pos.z);
            List<GridNode> verticalSurfaces = GridQueryService.GetReachableSurfaces(
                currentColumn,
                currentNode.ElevationY,
                MovementTag.Normal
            );

            foreach (GridNode surface in verticalSurfaces)
            {
                if (surface.Coordinates.y == currentNode.ElevationY)
                    continue;

                PathNode neighbourNode = GetOrCreatePathNode(surface.Coordinates, pathNodeMap);
                neighbourNode.isWalkable = surface.IsWalkable();
                neighbourList.Add(neighbourNode);
            }

            return neighbourList;
        }

        private static PathNode GetOrCreatePathNode(
            Vector3Int position,
            Dictionary<Vector3Int, PathNode> pathNodeMap
        )
        {
            if (pathNodeMap.TryGetValue(position, out PathNode existing))
                return existing;

            PathNode created = new PathNode(new GridPosition(position.x, position.z), position.y);
            created.gCost = int.MaxValue;
            pathNodeMap[position] = created;
            return created;
        }

        private static GridNode GetDefaultColumnNode(GridSystem gridSystem, GridPosition position)
        {
            List<GridNode> column = gridSystem.GetColumn(new Vector2Int(position.x, position.z));
            if (column == null || column.Count == 0)
                return null;

            for (int i = 0; i < column.Count; i++)
            {
                if (column[i] != null && column[i].IsWalkable())
                    return column[i];
            }

            return column[0];
        }

        /// <summary>
        /// Checks if ANY walkable node in either intermediate column bridges
        /// the source and destination elevations (both within +1 or -1).
        /// Probably has an edge case or two that it doesn't account for
        /// but it works for now.
        /// </summary>
        private static bool IsValidDiagonalBridge(
            int currentY,
            int destinationY,
            GridSystem gridSystem,
            Vector2Int intermediateCol1,
            Vector2Int intermediateCol2
        )
        {
            return HasBridgingNode(currentY, destinationY, gridSystem, intermediateCol1)
                || HasBridgingNode(currentY, destinationY, gridSystem, intermediateCol2);
        }

        private static bool HasBridgingNode(
            int currentY,
            int destinationY,
            GridSystem gridSystem,
            Vector2Int columnKey
        )
        {
            List<GridNode> column = gridSystem.GetColumn(columnKey);
            if (column == null)
                return false;

            foreach (GridNode node in column)
            {
                if (node == null || !node.IsWalkable())
                    continue;

                int y = node.Coordinates.y;
                if (Mathf.Abs(y - currentY) <= 1 && Mathf.Abs(y - destinationY) <= 1)
                    return true;
            }
            return false;
        }

        private static int GetStepCost(GridSystem gridSystem, PathNode fromNode, PathNode toNode)
        {
            int baseCost = CalculateDistance(fromNode.GridPosition, toNode.GridPosition);
            GridNode destinationNode = gridSystem.GetNode(toNode.LayeredPosition);
            if (destinationNode?.Terrain == null)
                return baseCost;

            int extraTileCost = Mathf.Max(0, destinationNode.Terrain.MovementCost - 1);
            return baseCost + (extraTileCost * MOVE_STRAIGHT_COST);
        }

        private static void LogReachabilityDiagnostics(
            GridSystem gridSystem,
            GridPosition startPosition,
            int maxMoveCost,
            int reachableCount
        )
        {
            string key = $"{startPosition.x},{startPosition.z}|{maxMoveCost}";
            if (!loggedReachabilityKeys.Add(key))
                return;

            GridNode startNode = GetDefaultColumnNode(gridSystem, startPosition);
            Debug.LogWarning(
                $"[Pathfinding][Debug] Low reachability from {startPosition}. "
                    + $"reachableCount={reachableCount}, maxMoveCost={maxMoveCost}, "
                    + $"startNodeExists={startNode != null}, "
                    + $"startWalkable={startNode != null && startNode.IsWalkable()}, "
                    + $"startY={(startNode != null ? startNode.Coordinates.y : -999)}"
            );

            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                GridPosition checkPos = startPosition + NeighborOffsets[i];
                List<GridNode> column = gridSystem.GetColumn(
                    new Vector2Int(checkPos.x, checkPos.z)
                );
                int startY = startNode != null ? startNode.Coordinates.y : 0;
                GridNode selectedNode = GridQueryService.ResolveSurface(
                    new Vector2Int(checkPos.x, checkPos.z),
                    startY,
                    MovementTag.Normal
                );
                bool occupied = gridSystem.IsPositionOccupied(
                    new GridPosition(checkPos.x, checkPos.z)
                );
                bool walkable = selectedNode != null && selectedNode.IsWalkable();
                int columnCount = column != null ? column.Count : 0;
                int selectedY = selectedNode != null ? selectedNode.Coordinates.y : -999;

                Debug.LogWarning(
                    $"[Pathfinding][Debug] Neighbor {checkPos}: "
                        + $"columnCount={columnCount}, selectedY={selectedY}, "
                        + $"walkable={walkable}, occupied={occupied}"
                );
            }
        }
    }
}
