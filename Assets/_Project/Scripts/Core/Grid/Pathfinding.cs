using System.Collections.Generic;
using UnityEngine;

namespace PathfinderTactics.Grid
{
    public static class Pathfinding
    {
        public const int MOVE_STRAIGHT_COST = 10;
        public const int MOVE_DIAGONAL_COST = 14;

        public static List<GridPosition> FindPath(
            GridPosition startPosition,
            GridPosition endPosition
        )
        {
            GridSystem gridSystem = GridSystem.Instance;
            int width = gridSystem.Width;
            int height = gridSystem.Height;

            PathNode[,] pathNodeGrid = new PathNode[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    pathNodeGrid[x, z] = new PathNode(new GridPosition(x, z));
                    pathNodeGrid[x, z].isWalkable = gridSystem
                        .GetCell(new GridPosition(x, z))
                        .isWalkable;
                }
            }

            List<PathNode> openList = new List<PathNode>();
            HashSet<PathNode> closedList = new HashSet<PathNode>();

            PathNode startNode = pathNodeGrid[startPosition.x, startPosition.z];
            PathNode endNode = pathNodeGrid[endPosition.x, endPosition.z];

            openList.Add(startNode);

            startNode.gCost = 0;
            startNode.hCost = CalculateDistance(startPosition, endPosition);
            startNode.CalculateFCost();

            while (openList.Count > 0)
            {
                PathNode currentNode = GetLowestFCostNode(openList);

                if (currentNode == endNode)
                {
                    return CalculatePath(endNode);
                }

                openList.Remove(currentNode);
                closedList.Add(currentNode);

                foreach (PathNode neighbourNode in GetNeighbourList(currentNode, pathNodeGrid))
                {
                    if (closedList.Contains(neighbourNode))
                        continue;

                    if (!neighbourNode.isWalkable)
                    {
                        closedList.Add(neighbourNode);
                        continue;
                    }

                    // TODO: Check if the neighbour is walkable.

                    // Calculate the cost based on whether the move is straight or diagonal.
                    int tentativeGCost =
                        currentNode.gCost
                        + CalculateDistance(currentNode.GridPosition, neighbourNode.GridPosition);

                    if (tentativeGCost < neighbourNode.gCost || neighbourNode.gCost == 0)
                    {
                        neighbourNode.cameFromNode = currentNode;
                        neighbourNode.gCost = tentativeGCost;
                        neighbourNode.hCost = CalculateDistance(
                            neighbourNode.GridPosition,
                            endPosition
                        );
                        neighbourNode.CalculateFCost();

                        if (!openList.Contains(neighbourNode))
                        {
                            openList.Add(neighbourNode);
                        }
                    }
                }
            }

            return null;
        }

        private static List<GridPosition> CalculatePath(PathNode endNode)
        {
            List<GridPosition> path = new List<GridPosition>();
            path.Add(endNode.GridPosition);
            PathNode currentNode = endNode;
            while (currentNode.cameFromNode != null)
            {
                currentNode = currentNode.cameFromNode;
                path.Add(currentNode.GridPosition);
            }
            path.Reverse();
            return path;
        }

        public static int CalculateDistance(GridPosition a, GridPosition b)
        {
            // This calculates the cost between two adjacent cells OR
            // the heuristic distance for the entire path
            int xDistance = Mathf.Abs(a.x - b.x);
            int zDistance = Mathf.Abs(a.z - b.z);
            int remaining = Mathf.Abs(xDistance - zDistance);
            return MOVE_DIAGONAL_COST * Mathf.Min(xDistance, zDistance)
                + MOVE_STRAIGHT_COST * remaining;
        }

        private static PathNode GetLowestFCostNode(List<PathNode> pathNodeList)
        {
            PathNode lowestFCostNode = pathNodeList[0];
            for (int i = 1; i < pathNodeList.Count; i++)
            {
                if (pathNodeList[i].fCost < lowestFCostNode.fCost)
                {
                    lowestFCostNode = pathNodeList[i];
                }
            }
            return lowestFCostNode;
        }

        private static List<PathNode> GetNeighbourList(
            PathNode currentNode,
            PathNode[,] pathNodeGrid
        )
        {
            List<PathNode> neighbourList = new List<PathNode>();
            GridPosition pos = currentNode.GridPosition;

            int width = pathNodeGrid.GetLength(0);
            int height = pathNodeGrid.GetLength(1);

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    // Skip the current node itself
                    if (x == 0 && z == 0)
                        continue;

                    int checkX = pos.x + x;
                    int checkZ = pos.z + z;

                    if (checkX >= 0 && checkX < width && checkZ >= 0 && checkZ < height)
                    {
                        neighbourList.Add(pathNodeGrid[checkX, checkZ]);
                    }
                }
            }

            return neighbourList;
        }

        public static List<GridPosition> GetReachableGridPositions(
            GridPosition startPosition,
            int maxMoveCost
        )
        {
            List<GridPosition> reachablePositions = new List<GridPosition>();

            // Using a dictionary for fast lookups of nodes we've already processed
            Dictionary<GridPosition, PathNode> pathNodeMap =
                new Dictionary<GridPosition, PathNode>();
            List<PathNode> openList = new List<PathNode>();

            PathNode startNode = new PathNode(startPosition);
            startNode.gCost = 0;
            openList.Add(startNode);
            pathNodeMap[startPosition] = startNode;

            while (openList.Count > 0)
            {
                PathNode currentNode = GetLowestGCostNode(openList);
                openList.Remove(currentNode);
                // We've found a valid reachable node
                reachablePositions.Add(currentNode.GridPosition);

                foreach (
                    PathNode neighbourNode in GetNeighbourListForReachability(
                        currentNode,
                        pathNodeMap
                    )
                )
                {
                    // Wall Check
                    if (!GridSystem.Instance.GetCell(neighbourNode.GridPosition).isWalkable)
                        continue;

                    // Unit Check
                    // If the cell is occupied by someone else, we cannot walk there.
                    if (GridSystem.Instance.IsPositionOccupied(neighbourNode.GridPosition))
                    {
                        // If it's the unit itself (start pos), it's fine.
                        if (neighbourNode.GridPosition != startPosition)
                            continue;
                    }

                    int tentativeGCost =
                        currentNode.gCost
                        + CalculateDistance(currentNode.GridPosition, neighbourNode.GridPosition);

                    // If the path to this neighbor is within our movement range
                    if (tentativeGCost <= maxMoveCost)
                    {
                        // If we haven't processed this node before, or we've found a cheaper path to it
                        if (
                            !pathNodeMap.ContainsKey(neighbourNode.GridPosition)
                            || tentativeGCost < neighbourNode.gCost
                        )
                        {
                            neighbourNode.cameFromNode = currentNode;
                            neighbourNode.gCost = tentativeGCost;
                            pathNodeMap[neighbourNode.GridPosition] = neighbourNode;

                            if (!openList.Contains(neighbourNode))
                            {
                                openList.Add(neighbourNode);
                            }
                        }
                    }
                }
            }

            // TODO: make starting square different color.
            // reachablePositions.Remove(startPosition);

            return reachablePositions;
        }

        // Helper method for GetLowestGCostNode
        private static PathNode GetLowestGCostNode(List<PathNode> pathNodeList)
        {
            PathNode lowestGCostNode = pathNodeList[0];
            for (int i = 1; i < pathNodeList.Count; i++)
            {
                if (pathNodeList[i].gCost < lowestGCostNode.gCost)
                {
                    lowestGCostNode = pathNodeList[i];
                }
            }
            return lowestGCostNode;
        }

        // A modified GetNeighbourList that works with a dictionary instead of a full grid
        private static List<PathNode> GetNeighbourListForReachability(
            PathNode currentNode,
            Dictionary<GridPosition, PathNode> pathNodeMap
        )
        {
            List<PathNode> neighbourList = new List<PathNode>();
            GridPosition pos = currentNode.GridPosition;

            GridSystem gridSystem = GridSystem.Instance;
            int width = gridSystem.Width;
            int height = gridSystem.Height;

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0)
                        continue;

                    GridPosition neighbourPos = new GridPosition(pos.x + x, pos.z + z);
                    if (gridSystem.IsValidGridPosition(neighbourPos))
                    {
                        GridCell cell = gridSystem.GetCell(neighbourPos);

                        // If a cell isn't walkable, dont move to it.
                        if (!cell.isWalkable)
                            continue;

                        // If we already have a node for this position, use it. Otherwise, create a new one.
                        if (!pathNodeMap.TryGetValue(neighbourPos, out PathNode neighbourNode))
                        {
                            neighbourNode = new PathNode(neighbourPos);
                        }
                        neighbourList.Add(neighbourNode);
                    }
                }
            }
            return neighbourList;
        }
    }
}
