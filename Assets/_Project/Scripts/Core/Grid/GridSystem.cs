using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Grid
{
    /// <summary>
    /// Manages the game grid, including its creation, data storage, and utility functions.
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField]
        private int width = 20;

        [SerializeField]
        private int height = 20;

        [SerializeField]
        private float cellSize = 2f;

        [Header("Layer Masks")]
        [SerializeField]
        private LayerMask obstacleLayerMask;

        [SerializeField]
        private LayerMask halfCoverLayerMask;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;

        private GridCell[,] gridCells;

        private void Awake()
        {
            ServiceLocator.Register(this);
            CreateGrid();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<GridSystem>();
        }

        private void CreateGrid()
        {
            gridCells = new GridCell[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    GridPosition gridPos = new GridPosition(x, z);
                    Vector3 worldPos = GetWorldPosition(gridPos);
                    GridCell cell = new GridCell(gridPos, worldPos);

                    // Obstacle check (Walls and HalfCover)
                    LayerMask combinedLayerMask = obstacleLayerMask | halfCoverLayerMask;
                    bool isBlocked = Physics.CheckBox(
                        worldPos + Vector3.up * 0.5f,
                        new Vector3(cellSize, 1f, cellSize) * 0.4f,
                        Quaternion.identity,
                        combinedLayerMask
                    );
                    cell.isWalkable = !isBlocked;
                    gridCells[x, z] = cell;
                }
            }
        }

        // PUBLIC API

        public Vector3 GetWorldPosition(GridPosition gridPosition)
        {
            return new Vector3(gridPosition.x, 0, gridPosition.z) * cellSize;
        }

        public GridPosition GetGridPosition(Vector3 worldPosition)
        {
            return new GridPosition(
                Mathf.RoundToInt(worldPosition.x / cellSize),
                Mathf.RoundToInt(worldPosition.z / cellSize)
            );
        }

        public GridCell GetCell(GridPosition gridPosition)
        {
            if (!IsValidGridPosition(gridPosition))
                return null;
            return gridCells[gridPosition.x, gridPosition.z];
        }

        public bool IsValidGridPosition(GridPosition gridPosition)
        {
            return gridPosition.x >= 0
                && gridPosition.z >= 0
                && gridPosition.x < width
                && gridPosition.z < height;
        }

        // Unit registration

        public void AddUnitAt(Unit unit, GridPosition gridPosition)
        {
            GridCell cell = GetCell(gridPosition);
            if (cell != null)
            {
                cell.occupyingUnit = unit;
            }
        }

        public void RemoveUnitAt(GridPosition gridPosition)
        {
            GridCell cell = GetCell(gridPosition);
            if (cell != null)
            {
                cell.occupyingUnit = null;
            }
        }

        public void MoveUnit(Unit unit, GridPosition fromPos, GridPosition toPos)
        {
            RemoveUnitAt(fromPos);
            AddUnitAt(unit, toPos);
        }

        public Unit GetUnitAt(GridPosition gridPosition)
        {
            GridCell cell = GetCell(gridPosition);
            return cell?.occupyingUnit;
        }

        public bool IsPositionOccupied(GridPosition gridPosition)
        {
            GridCell cell = GetCell(gridPosition);
            return cell != null && cell.occupyingUnit != null;
        }

        /// <summary>
        /// Returns a list of all active units on the board that do not belong to the provided faction.
        /// </summary>
        public List<Unit> GetAllEnemies(Faction friendlyFaction)
        {
            List<Unit> enemyList = new List<Unit>();

            Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

            foreach (Unit testUnit in allUnits)
            {
                // Are they on a different team?
                if (testUnit.GetFaction() != friendlyFaction)
                {
                    // Are they actually alive?
                    var conditions = testUnit.GetComponent<UnitConditions>();
                    if (conditions != null && conditions.IsDead())
                    {
                        continue;
                    }

                    enemyList.Add(testUnit);
                }
            }

            return enemyList;
        }
    }
}
