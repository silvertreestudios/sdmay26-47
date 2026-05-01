using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.Grid
{
    /// <summary>
    /// Manages the layered game grid built from scene-authored TerrainBlocks.
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField]
        private float cellSize = 2f;

        public float CellSize => cellSize;

        public float VerticalCellSize => cellSize;
        public int NodeCount => nodes.Count;
        public int ColumnCount => columns.Count;

        private Dictionary<Vector3Int, GridNode> nodes = new Dictionary<Vector3Int, GridNode>();
        private Dictionary<Vector2Int, List<GridNode>> columns =
            new Dictionary<Vector2Int, List<GridNode>>();
        private Dictionary<Vector3Int, Unit> occupancy = new Dictionary<Vector3Int, Unit>();

        private void Awake()
        {
            ServiceLocator.Register(this);
            BakeGrid();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<GridSystem>();
        }

        private void BakeGrid()
        {
            nodes.Clear();
            columns.Clear();
            occupancy.Clear();

            int bakedCount = GridBaker.BakeScene(this, cellSize, replaceExisting: true);

            if (bakedCount > 0)
            {
                Debug.Log(
                    $"[GridSystem] Baked {bakedCount} TerrainBlocks ({NodeCount} nodes, {ColumnCount} columns)."
                );
            }
            else
            {
                Debug.LogWarning(
                    "[GridSystem] No TerrainBlocks found. Place TerrainBlock prefabs in the scene to define the playable area."
                );
            }
        }

        // World / Grid conversion

        public Vector3 GetWorldPosition(GridPosition gridPosition)
        {
            return new Vector3(gridPosition.x, 0, gridPosition.z) * cellSize;
        }

        public Vector3 GetWorldPosition(Vector3Int layeredPosition)
        {
            return new Vector3(
                layeredPosition.x * cellSize,
                layeredPosition.y * VerticalCellSize,
                layeredPosition.z * cellSize
            );
        }

        public Vector3 GetWorldPosition(
            GridPosition gridPosition,
            int referenceY,
            MovementTag movementMode = MovementTag.Normal
        )
        {
            Vector3Int layered = ResolveLayeredPosition(gridPosition, referenceY, movementMode);
            return GetWorldPosition(layered);
        }

        public GridPosition GetGridPosition(Vector3 worldPosition)
        {
            return new GridPosition(
                Mathf.RoundToInt(worldPosition.x / cellSize),
                Mathf.RoundToInt(worldPosition.z / cellSize)
            );
        }

        public Vector3Int GetLayeredGridPosition(Vector3 worldPosition)
        {
            return new Vector3Int(
                Mathf.RoundToInt(worldPosition.x / cellSize),
                Mathf.FloorToInt(worldPosition.y / VerticalCellSize),
                Mathf.RoundToInt(worldPosition.z / cellSize)
            );
        }

        // Layered resolution

        public Vector3Int ResolveLayeredPosition(
            GridPosition gridPosition,
            int referenceY,
            MovementTag movementMode = MovementTag.Normal
        )
        {
            GridNode node = GridQueryService.ResolveSurface(
                new Vector2Int(gridPosition.x, gridPosition.z),
                referenceY,
                movementMode
            );

            if (node != null)
                return node.Coordinates;

            return new Vector3Int(gridPosition.x, referenceY, gridPosition.z);
        }

        public Vector3Int ResolveClosestLayeredPosition(GridPosition gridPosition, int referenceY)
        {
            List<GridNode> column = GetColumn(new Vector2Int(gridPosition.x, gridPosition.z));
            if (column == null || column.Count == 0)
                return new Vector3Int(gridPosition.x, referenceY, gridPosition.z);

            GridNode best = null;
            int bestDistance = int.MaxValue;
            foreach (GridNode candidate in column)
            {
                if (candidate == null)
                    continue;

                int distance = Mathf.Abs(candidate.Coordinates.y - referenceY);
                if (
                    best == null
                    || distance < bestDistance
                    || (distance == bestDistance && candidate.Coordinates.y < best.Coordinates.y)
                )
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best != null
                ? best.Coordinates
                : new Vector3Int(gridPosition.x, referenceY, gridPosition.z);
        }

        public Vector3 GetClosestWorldPosition(GridPosition gridPosition, int referenceY)
        {
            return GetWorldPosition(ResolveClosestLayeredPosition(gridPosition, referenceY));
        }

        // Node / Column access

        public GridNode GetNode(Vector3Int position)
        {
            nodes.TryGetValue(position, out GridNode node);
            return node;
        }

        public List<GridNode> GetColumn(Vector2Int columnPosition)
        {
            if (columns.TryGetValue(columnPosition, out List<GridNode> column))
                return column;

            return null;
        }

        /// <summary>
        /// A position is valid if the baked grid contains at least one node in that column.
        /// </summary>
        public bool IsValidGridPosition(GridPosition gridPosition)
        {
            return columns.ContainsKey(new Vector2Int(gridPosition.x, gridPosition.z));
        }

        // Node registration (used by GridBaker)

        public void ClearLayeredData()
        {
            nodes.Clear();
            columns.Clear();
        }

        public void RegisterLayeredNode(Vector3Int position, TerrainDef terrain)
        {
            TerrainDef resolvedTerrain = terrain ?? new TerrainDef();
            GridNode newNode = new GridNode(position, resolvedTerrain);

            if (nodes.TryGetValue(position, out GridNode existingNode))
            {
                TerrainDef existingTerrain = existingNode.Terrain;
                int existingScore =
                    (existingTerrain != null && existingTerrain.BlocksLineOfEffect ? 10 : 0)
                    + (existingTerrain != null ? (int)existingTerrain.CoverType : 0);
                int newScore =
                    (resolvedTerrain.BlocksLineOfEffect ? 10 : 0) + (int)resolvedTerrain.CoverType;

                // If the existing node provides more or equal cover/blocking, do not overwrite it
                // This ensures solid pillars aren't accidentally hollowed out by intersecting walkable flat tiles.
                if (existingScore > newScore)
                {
                    return;
                }

                Vector2Int existingColumnKey = new Vector2Int(
                    existingNode.Coordinates.x,
                    existingNode.Coordinates.z
                );

                if (columns.TryGetValue(existingColumnKey, out List<GridNode> existingColumn))
                {
                    existingColumn.Remove(existingNode);
                }
            }

            nodes[position] = newNode;

            Vector2Int columnKey = new Vector2Int(position.x, position.z);
            if (!columns.TryGetValue(columnKey, out List<GridNode> column))
            {
                column = new List<GridNode>();
                columns[columnKey] = column;
            }

            column.Add(newNode);
            column.Sort((a, b) => a.Coordinates.y.CompareTo(b.Coordinates.y));
        }

        // Unit occupancy

        public void AddUnitAt(Unit unit, Vector3Int layeredPosition)
        {
            occupancy[layeredPosition] = unit;
        }

        public void RemoveUnitAt(Vector3Int layeredPosition)
        {
            occupancy.Remove(layeredPosition);
        }

        public void MoveUnit(Unit unit, Vector3Int fromPos, Vector3Int toPos)
        {
            RemoveUnitAt(fromPos);
            AddUnitAt(unit, toPos);
        }

        public Unit GetUnitAt(Vector3Int layeredPosition)
        {
            occupancy.TryGetValue(layeredPosition, out Unit unit);
            return unit;
        }

        public bool IsPositionOccupied(Vector3Int layeredPosition)
        {
            return occupancy.ContainsKey(layeredPosition);
        }

        public void AddUnitAt(Unit unit, GridPosition gridPosition)
        {
            Vector3Int resolved = ResolveClosestLayeredPosition(gridPosition, 0);
            AddUnitAt(unit, resolved);
        }

        public void RemoveUnitAt(GridPosition gridPosition)
        {
            Vector3Int? pos = FindOccupiedLayerInColumn(gridPosition);
            if (pos.HasValue)
                occupancy.Remove(pos.Value);
        }

        public void MoveUnit(Unit unit, GridPosition fromPos, GridPosition toPos)
        {
            Vector3Int? from = FindOccupiedLayerInColumn(fromPos);
            if (from.HasValue)
                occupancy.Remove(from.Value);

            Vector3Int resolved = ResolveClosestLayeredPosition(toPos, 0);
            occupancy[resolved] = unit;
        }

        public Unit GetUnitAt(GridPosition gridPosition)
        {
            List<GridNode> column = GetColumn(new Vector2Int(gridPosition.x, gridPosition.z));
            if (column == null)
                return null;

            foreach (GridNode node in column)
            {
                if (occupancy.TryGetValue(node.Coordinates, out Unit unit))
                    return unit;
            }
            return null;
        }

        public bool IsPositionOccupied(GridPosition gridPosition)
        {
            return GetUnitAt(gridPosition) != null;
        }

        /// <summary>
        /// Returns the layered position of the first occupied node in the column, or null.
        /// </summary>
        private Vector3Int? FindOccupiedLayerInColumn(GridPosition gridPosition)
        {
            List<GridNode> column = GetColumn(new Vector2Int(gridPosition.x, gridPosition.z));
            if (column == null)
                return null;

            foreach (GridNode node in column)
            {
                if (occupancy.ContainsKey(node.Coordinates))
                    return node.Coordinates;
            }
            return null;
        }

        // Queries

        public List<Unit> GetAllEnemies(Faction friendlyFaction)
        {
            List<Unit> enemyList = new List<Unit>();

            Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

            foreach (Unit testUnit in allUnits)
            {
                if (testUnit.GetFaction() != friendlyFaction)
                {
                    var conditions = testUnit.GetComponent<UnitConditions>();
                    if (conditions != null && conditions.IsDead())
                        continue;

                    enemyList.Add(testUnit);
                }
            }

            return enemyList;
        }

        public List<Unit> GetUnitsInRadius(GridPosition center, int radiusTiles)
        {
            Vector3Int center3D = new Vector3Int(center.x, 0, center.z);
            List<GridNode> col = GetColumn(new Vector2Int(center.x, center.z));
            if (col != null && col.Count > 0)
                center3D = col[0].Coordinates;

            return GetUnitsInRadius(center3D, radiusTiles);
        }

        /// <summary>
        /// Returns all units within TacticsRuleset 3D distance of the given position.
        /// </summary>
        public List<Unit> GetUnitsInRadius(Vector3Int center, int radiusTiles)
        {
            List<Unit> unitsInRange = new List<Unit>();

            foreach (var kvp in occupancy)
            {
                int dist = Core.TacticsRuleset_Core.GetTacticsRulesetDistance3D(center, kvp.Key);
                if (dist <= radiusTiles)
                    unitsInRange.Add(kvp.Value);
            }

            return unitsInRange;
        }
    }
}
