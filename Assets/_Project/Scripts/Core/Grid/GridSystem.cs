using PathfinderTactics.Characters;
using UnityEngine;

namespace PathfinderTactics.Grid
{
    // TODO: add colisions and whatnot

    /// <summary>
    /// Manages the game grid, including its creation, data storage, and utility functions.
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        public static GridSystem Instance { get; private set; }

        [Header("Grid Settings")]
        [SerializeField]
        private int width = 20;

        [SerializeField]
        private int height = 20;

        [SerializeField]
        private float cellSize = 2f;
        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;

        [Header("Debug")]
        [SerializeField]
        private Transform debugTransform;

        private GridCell[,] gridCells;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Multiple instances of GridSystem found. Destroying this one.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateGrid();
        }

        private void Start() { }

        private void CreateGrid()
        {
            gridCells = new GridCell[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    GridPosition gridPosition = new GridPosition(x, z);
                    Vector3 worldPosition = GetWorldPosition(gridPosition);
                    gridCells[x, z] = new GridCell(gridPosition, worldPosition);
                }
            }
        }

        #region Public API

        public Vector3 GetWorldPosition(GridPosition gridPosition)
        {
            return new Vector3(gridPosition.x, 0, gridPosition.z) * cellSize;
        }

        public GridPosition GetGridPosition(Vector3 worldPosition)
        {
            int x = Mathf.RoundToInt(worldPosition.x / cellSize);
            int z = Mathf.RoundToInt(worldPosition.z / cellSize);
            return new GridPosition(x, z);
        }

        public bool IsValidGridPosition(GridPosition gridPosition)
        {
            return gridPosition.x >= 0
                && gridPosition.z >= 0
                && gridPosition.x < width
                && gridPosition.z < height;
        }

        public GridCell GetCell(GridPosition gridPosition)
        {
            if (!IsValidGridPosition(gridPosition))
            {
                Debug.LogError($"Invalid GridPosition requested: {gridPosition}");
                return null;
            }
            return gridCells[gridPosition.x, gridPosition.z];
        }

        public void AddUnitAt(Unit unit, GridPosition gridPosition)
        {
            if (IsValidGridPosition(gridPosition))
            {
                GridCell cell = GetCell(gridPosition);
                if (cell.occupyingUnit == null)
                {
                    cell.occupyingUnit = unit;
                    unit.SetInitialPosition(gridPosition);
                }
                else
                {
                    Debug.LogError(
                        $"Cell {gridPosition} is already occupied by {cell.occupyingUnit.name}!"
                    );
                }
            }
        }

        public void RemoveUnitAt(GridPosition gridPosition)
        {
            if (IsValidGridPosition(gridPosition))
            {
                var cell = GetCell(gridPosition);
                if (cell.occupyingUnit != null)
                {
                    cell.occupyingUnit = null;
                }
            }
        }

        public void MoveUnit(Unit unit, GridPosition newGridPosition)
        {
            RemoveUnitAt(unit.CurrentGridPosition);

            GridCell newCell = GetCell(newGridPosition);
            if (newCell != null && newCell.occupyingUnit == null)
            {
                newCell.occupyingUnit = unit;
            }
            else
            {
                Debug.LogError(
                    $"Cannot move unit to {newGridPosition}, cell might be invalid or already occupied!"
                );
            }
        }

        public Unit GetUnitAt(GridPosition gridPosition)
        {
            if (!IsValidGridPosition(gridPosition))
                return null;
            return GetCell(gridPosition).occupyingUnit;
        }

        #endregion

        private void OnDrawGizmos()
        {
            // This ensures the grid data exists for drawing in the editor.
            if (gridCells == null)
            {
                CreateGrid();
            }

            int segments = 24;
            float radius = cellSize * 0.15f;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    Vector3 center = GetWorldPosition(new GridPosition(x, z));

                    Gizmos.color = Color.white;
                    Gizmos.DrawWireCube(center, new Vector3(cellSize, 0, cellSize));

                    Gizmos.color = Color.red;
                    Vector3 previousPoint = center + new Vector3(radius, 0, 0);
                    for (int i = 1; i <= segments; i++)
                    {
                        float angle = i * 2 * Mathf.PI / segments;
                        Vector3 nextPoint =
                            center
                            + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                        Gizmos.DrawLine(previousPoint, nextPoint);
                        previousPoint = nextPoint;
                    }
                }
            }

            if (debugTransform != null)
            {
                GridPosition gridPosition = GetGridPosition(debugTransform.position);
                if (IsValidGridPosition(gridPosition))
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(
                        GetWorldPosition(gridPosition),
                        new Vector3(cellSize, 0.1f, cellSize) * 0.95f
                    );
                }
            }
        }
    }
}
