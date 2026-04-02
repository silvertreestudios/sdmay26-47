using UnityEngine;

namespace PathfinderTactics.Grid
{
    /// <summary>
    /// Scene baker that converts TerrainBlock placements into layered grid nodes.
    /// </summary>
    public static class GridBaker
    {
        public static int BakeScene(
            GridSystem gridSystem,
            float cellSize,
            bool replaceExisting = true
        )
        {
            if (gridSystem == null)
                return 0;

            TerrainBlock[] terrainBlocks = Object.FindObjectsByType<TerrainBlock>(
                FindObjectsSortMode.None
            );

            // Do not clear baseline data when no TerrainBlocks exist yet.
            if (replaceExisting && terrainBlocks.Length > 0)
            {
                gridSystem.ClearLayeredData();
            }

            int bakedCount = 0;
            for (int i = 0; i < terrainBlocks.Length; i++)
            {
                TerrainBlock block = terrainBlocks[i];
                if (block == null)
                    continue;

                Vector3Int gridPosition = WorldToGridPosition(gridSystem, block.transform.position);
                if (!IsGridSnapped(gridSystem, block.transform.position, gridPosition, cellSize))
                {
                    Debug.LogWarning(
                        $"[GridBaker] TerrainBlock '{block.name}' is off-grid. Rounded to {gridPosition}."
                    );
                }

                gridSystem.RegisterLayeredNode(gridPosition, block.Terrain);
                bakedCount++;
            }

            return bakedCount;
        }

        private static Vector3Int WorldToGridPosition(GridSystem gridSystem, Vector3 worldPosition)
        {
            return gridSystem.GetLayeredGridPosition(worldPosition);
        }

        private static bool IsGridSnapped(
            GridSystem gridSystem,
            Vector3 worldPosition,
            Vector3Int gridPosition,
            float cellSize
        )
        {
            Vector3 snapped = new Vector3(
                gridPosition.x * cellSize,
                gridPosition.y * gridSystem.VerticalCellSize,
                gridPosition.z * cellSize
            );
            return (worldPosition - snapped).sqrMagnitude <= 0.0001f;
        }
    }
}
