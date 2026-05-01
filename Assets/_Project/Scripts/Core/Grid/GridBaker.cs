using UnityEngine;

namespace TacticsGame.Grid
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
                        // Shrink bounds slightly to avoid boundary precision edge cases
                        Vector3 minWorld = bounds.min + Vector3.one * 0.01f;
                        Vector3 maxWorld = bounds.max - Vector3.one * 0.01f;

                        // Fallback if the object is impossibly thin
                        if (minWorld.x > maxWorld.x)
                            minWorld.x = maxWorld.x = bounds.center.x;
                        if (minWorld.y > maxWorld.y)
                            minWorld.y = maxWorld.y = bounds.center.y;
                        if (minWorld.z > maxWorld.z)
                            minWorld.z = maxWorld.z = bounds.center.z;

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

                        // SIZE-BASED LAYER COUNTING:
                        // Prevent thin assets from bleeding into multiple layers by explicitly determining
                        // how many layers their physical height justifies.
                        int numLayersToFill = Mathf.Max(
                            1,
                            Mathf.RoundToInt(bounds.size.y / gridSystem.VerticalCellSize)
                        );

                        // Anchoring the body to the top of the asset ensures it occupies the
                        // intended layer, even if it spans a boundary slightly.
                        maxGrid.y = Mathf.FloorToInt(maxWorld.y / gridSystem.VerticalCellSize);
                        minGrid.y = maxGrid.y - (numLayersToFill - 1);

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
                                }
                            }
                        }
                        bakedCount++;

                        // Project the pure Walkable Surface
                        if (block.Terrain.IsWalkable)
                        {
                            // Surface is the layer the unit "stands in" (floor = layer index)
                            int surfaceLayer = Mathf.FloorToInt(
                                bounds.max.y / gridSystem.VerticalCellSize
                            );

                            // If the surface layer rises above the geometric body,
                            // OR if the block is a pure floor (CoverType.None), inject a surface proxy.
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
                                    BlocksLineOfEffect = false,
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
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Fallback to pivot-based single node mapping
                    Vector3Int gridPosition = WorldToGridPosition(
                        gridSystem,
                        block.transform.position
                    );
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
