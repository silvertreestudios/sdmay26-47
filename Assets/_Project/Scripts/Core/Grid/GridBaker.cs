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

                Renderer[] renderers = block.GetComponentsInChildren<Renderer>();
                Collider[] colliders = block.GetComponentsInChildren<Collider>();

                if (renderers.Length > 0 || colliders.Length > 0)
                {
                    Bounds bounds = new Bounds();
                    bool boundsInitialized = false;

                    // Prefer colliders as they represent physics interactions
                    if (colliders.Length > 0)
                    {
                        bounds = colliders[0].bounds;
                        for (int j = 1; j < colliders.Length; j++)
                            bounds.Encapsulate(colliders[j].bounds);
                        boundsInitialized = true;
                    }
                    else if (renderers.Length > 0)
                    {
                        bounds = renderers[0].bounds;
                        for (int j = 1; j < renderers.Length; j++)
                            bounds.Encapsulate(renderers[j].bounds);
                        boundsInitialized = true;
                    }

                    if (boundsInitialized)
                    {
                        // Shrink bounds slightly to avoid boundary precision edge cases (e.g., perfectly overlapping adjacent voxels)
                        Vector3 minWorld = bounds.min + Vector3.one * 0.01f;
                        Vector3 maxWorld = bounds.max - Vector3.one * 0.01f;

                        // Fallback if the object is impossibly thin
                        if (minWorld.x > maxWorld.x)
                            minWorld.x = maxWorld.x = bounds.center.x;
                        if (minWorld.y > maxWorld.y)
                            minWorld.y = maxWorld.y = bounds.center.y;
                        if (minWorld.z > maxWorld.z)
                            minWorld.z = maxWorld.z = bounds.center.z;

                        // X and Z are centered around the physical mesh coordinates, so we purely resolve their grid domain borders.
                        Vector3Int minGrid = new Vector3Int(
                            Mathf.RoundToInt(minWorld.x / cellSize),
                            0, // calculated below
                            Mathf.RoundToInt(minWorld.z / cellSize)
                        );
                        Vector3Int maxGrid = new Vector3Int(
                            Mathf.RoundToInt(maxWorld.x / cellSize),
                            0, // calculated below
                            Mathf.RoundToInt(maxWorld.z / cellSize)
                        );

                        // An obstacle fills geometric space upwards, constrained accurately so precisely ending edges (Y=2.0)
                        // do not spill over into the next distinct Voxel layer unless they significantly pierce it.
                        minGrid.y = Mathf.FloorToInt(minWorld.y / gridSystem.VerticalCellSize);
                        maxGrid.y = Mathf.FloorToInt(maxWorld.y / gridSystem.VerticalCellSize);

                        // Project the solid geometric body of the mesh
                        for (int x = minGrid.x; x <= maxGrid.x; x++)
                        {
                            for (int y = minGrid.y; y <= maxGrid.y; y++)
                            {
                                for (int z = minGrid.z; z <= maxGrid.z; z++)
                                {
                                    gridSystem.RegisterLayeredNode(
                                        new Vector3Int(x, y, z),
                                        block.Terrain
                                    );
                                    bakedCount++;
                                }
                            }
                        }

                        // Project the pure Walkable Surface
                        // If this block is walkable, its physical top edge inherently establishes a floor.
                        if (block.Terrain.IsWalkable)
                        {
                            int surfaceLayer = Mathf.RoundToInt(
                                bounds.max.y / gridSystem.VerticalCellSize
                            );

                            // If the surface layer rises above the geometric body (e.g., [0, 2] -> body maps to 0, surface maps to 1),
                            // OR if the block was originally designed as a pure horizontal floor (CoverType.None),
                            // we inject a synthetic surface proxy isolated from any bulk obstructive Cover.
                            if (
                                surfaceLayer > maxGrid.y
                                || block.Terrain.CoverType == CoverType.None
                            )
                            {
                                TerrainDef floorProxy = new TerrainDef
                                {
                                    MovementCost = block.Terrain.MovementCost,
                                    IsWalkable = true,
                                    CoverType = CoverType.None,
                                    BlocksLineOfEffect = false, // Floor surface doesn't block horizontally
                                    AllowVerticalLineOfEffect = block
                                        .Terrain
                                        .AllowVerticalLineOfEffect,
                                };

                                for (int x = minGrid.x; x <= maxGrid.x; x++)
                                {
                                    for (int z = minGrid.z; z <= maxGrid.z; z++)
                                    {
                                        gridSystem.RegisterLayeredNode(
                                            new Vector3Int(x, surfaceLayer, z),
                                            floorProxy
                                        );
                                        // Count doesn't increment here to avoid double-counting the same block, but the node is registered.
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Fallback to pivot-based single node mapping if no geometry
                    Vector3Int gridPosition = WorldToGridPosition(
                        gridSystem,
                        block.transform.position
                    );
                    if (
                        !IsGridSnapped(gridSystem, block.transform.position, gridPosition, cellSize)
                    )
                    {
                        Debug.LogWarning(
                            $"[GridBaker] TerrainBlock '{block.name}' is off-grid. Rounded to {gridPosition}."
                        );
                    }

                    gridSystem.RegisterLayeredNode(gridPosition, block.Terrain);
                    bakedCount++;
                }
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
