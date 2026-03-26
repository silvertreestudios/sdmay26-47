using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Grid
{
    // This file was debugged by AI to try to fix an edge case
    // where units were able to attack through walls.

    public struct VisibilityResult
    {
        public bool HasLineOfSight;
        public CoverType Cover;

        public static readonly VisibilityResult Blocked = new VisibilityResult
        {
            HasLineOfSight = false,
            Cover = CoverType.Total,
        };

        public static readonly VisibilityResult Clear = new VisibilityResult
        {
            HasLineOfSight = true,
            Cover = CoverType.None,
        };
    }

    public static class LineOfSightUtility
    {
        /// <summary>
        /// When true, <see cref="HasLineOfEffect"/> logs why each check passed or failed.
        /// Enable from TargetLockService debug options
        /// or set manually while diagnosing bridge / vertical LoE.
        /// </summary>
        public static bool DebugLineOfEffect { get; set; }

        /// <summary>
        /// Layer-aware LOS and cover evaluation using explicit 3D coordinates.
        /// Uses 3D voxel traversal to detect obstructions along the full line.
        /// NOTE: This is not PF2e accurate.
        /// </summary>
        public static VisibilityResult Evaluate(Vector3Int origin, Vector3Int target)
        {
            if (origin == target)
                return VisibilityResult.Clear;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
                return VisibilityResult.Blocked;

            return EvaluateInternal3D(grid, origin, target);
        }

        /// <summary>
        /// Backward-compatible 2D overload. Resolves Y for origin and target
        /// by checking occupancy first, then falling back to column data.
        /// </summary>
        public static VisibilityResult Evaluate(GridPosition origin, GridPosition target)
        {
            if (origin == target)
                return VisibilityResult.Clear;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
                return VisibilityResult.Blocked;

            int originY = GetSurfaceY(grid, origin);
            int targetY = GetSurfaceY(grid, target);

            return EvaluateInternal3D(
                grid,
                new Vector3Int(origin.x, originY, origin.z),
                new Vector3Int(target.x, targetY, target.z)
            );
        }

        /// <summary>
        /// Returns true if there is a clear Line of Effect between two positions.
        /// Blocked by total cover, explicit <see cref="TerrainDef.BlocksLineOfEffect"/>,
        /// same-column vertical rules, and bridge-underpass rules: any interior (X,Z) column
        /// where the 3D line passes strictly below a bridge-style deck in that column (see
        /// <see cref="TerrainDef.AllowVerticalLineOfEffect"/> for grates).
        /// </summary>
        public static bool HasLineOfEffect(Vector3Int origin, Vector3Int target)
        {
            if (origin == target)
            {
                if (DebugLineOfEffect)
                    Debug.Log($"[LoE] {origin} -> {target}: CLEAR (same cell)");
                return true;
            }

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
            {
                if (DebugLineOfEffect)
                    Debug.Log($"[LoE] {origin} -> {target}: BLOCK (GridSystem missing)");
                return false;
            }

            List<Vector3Int> line = Get3DBresenhamLine(origin, target);
            int yMin = Mathf.Min(origin.y, target.y);
            int yMax = Mathf.Max(origin.y, target.y);

            if (DebugLineOfEffect)
                Debug.Log(
                    $"[LoE] {origin} -> {target} | Bresenham voxels={line.Count} yRange=[{yMin},{yMax}]"
                );

            for (int i = 0; i < line.Count; i++)
            {
                Vector3Int voxel = line[i];
                if (voxel == origin || voxel == target)
                    continue;

                GridNode node = grid.GetNode(voxel);
                if (node == null)
                    continue;

                if (voxel.y > yMin && voxel.y < yMax && TerrainBlocksVerticalInteriorSlab(node))
                {
                    if (DebugLineOfEffect)
                        Debug.Log(
                            $"[LoE] {origin} -> {target}: BLOCK interior Y slab at {voxel} "
                                + $"(AllowVerticalLoE={node.Terrain?.AllowVerticalLineOfEffect})"
                        );
                    return false;
                }

                if (NodeBlocksLineOfEffect(node))
                {
                    if (DebugLineOfEffect)
                        Debug.Log(
                            $"[LoE] {origin} -> {target}: BLOCK at {voxel} "
                                + $"(total cover or Terrain.BlocksLineOfEffect)"
                        );
                    return false;
                }
            }

            if (SameColumnVerticalLoEBlockedByEndpoints(grid, origin, target))
                return false;

            if (InteriorColumnLoEBlockedByBridgeDeckAboveLine(grid, origin, target, line))
                return false;

            if (DebugLineOfEffect)
                Debug.Log($"[LoE] {origin} -> {target}: CLEAR");

            return true;
        }

        /// <summary>
        /// 3D Bresenham skips endpoint voxels and often misses bridge decks as interior cells.
        /// For every <b>interior</b> column (X,Z) along the line, take the max Y visited there;
        /// if a bridge-style walkable slab exists strictly above that height in the same column,
        /// the line passes under the deck (any horizontal span or diagonal climb). Origin and
        /// target columns are excluded so reaching a unit on the deck still works.
        /// </summary>
        private static bool InteriorColumnLoEBlockedByBridgeDeckAboveLine(
            GridSystem grid,
            Vector3Int origin,
            Vector3Int target,
            List<Vector3Int> line3d
        )
        {
            if (line3d == null || line3d.Count < 3)
                return false;

            Dictionary<Vector2Int, int> columnMaxY = new Dictionary<Vector2Int, int>();

            for (int i = 0; i < line3d.Count; i++)
            {
                Vector3Int v = line3d[i];
                if (v == origin || v == target)
                    continue;

                Vector2Int xz = new Vector2Int(v.x, v.z);
                if (!columnMaxY.TryGetValue(xz, out int prevY) || v.y > prevY)
                    columnMaxY[xz] = v.y;
            }

            foreach (KeyValuePair<Vector2Int, int> kvp in columnMaxY)
            {
                int gx = kvp.Key.x;
                int gz = kvp.Key.y;

                // Same-column vertical handled by SameColumnVerticalLoEBlockedByEndpoints
                if (gx == origin.x && gz == origin.z && gx == target.x && gz == target.z)
                    continue;

                // A bridge deck only blocks LoE if the line crosses through it,
                // i.e. the deck sits between origin.y and target.y in this column.
                // This correctly allows horizontal under-bridge shots (both at
                // same Y -> no bridge "between") while blocking cross-bridge shots
                // in either direction.
                if (BridgeDeckExistsBetweenYLevelsInColumn(grid, gx, gz, origin.y, target.y))
                {
                    if (DebugLineOfEffect)
                    {
                        Debug.Log(
                            $"[LoE][BridgeCross] {origin} -> {target}: BLOCK column=({gx},{gz}) "
                                + $"(bridge deck between origin.y={origin.y} and target.y={target.y})"
                        );
                    }
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True if a bridge-style walkable slab exists in this column at a Y level
        /// strictly above <paramref name="yLow"/> and at or below <paramref name="yHigh"/>
        /// (i.e. between the two levels). Used for endpoint columns to detect when
        /// the line of effect crosses through a bridge deck floor.
        /// </summary>
        private static bool BridgeDeckExistsBetweenYLevelsInColumn(
            GridSystem grid,
            int x,
            int z,
            int y1,
            int y2
        )
        {
            if (y1 == y2)
                return false;

            int lower = Mathf.Min(y1, y2);
            int upper = Mathf.Max(y1, y2);

            List<GridNode> col = grid.GetColumn(new Vector2Int(x, z));
            if (col == null)
                return false;

            foreach (GridNode n in col)
            {
                int y = n.Coordinates.y;
                if (y <= lower || y > upper)
                    continue;

                Vector3Int pos = new Vector3Int(x, y, z);
                if (!SameColumnBlocksShootingDownward(grid, pos))
                    continue;
                if (!HasSeparatedWalkableFloorBelowInColumn(grid, pos))
                    continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// True if this column has a walkable bridge-style slab at Y strictly greater than
        /// <paramref name="yMaxExclusive"/>. Uses solid deck + separated floor below so cliffs
        /// and simple floors do not behave like bridge underpasses.
        /// </summary>
        private static bool BridgeDeckExistsStrictlyAboveYInColumn(
            GridSystem grid,
            int x,
            int z,
            int yMaxExclusive
        )
        {
            List<GridNode> col = grid.GetColumn(new Vector2Int(x, z));
            if (col == null)
                return false;

            foreach (GridNode n in col)
            {
                int y = n.Coordinates.y;
                if (y <= yMaxExclusive)
                    continue;

                Vector3Int pos = new Vector3Int(x, y, z);
                if (!SameColumnBlocksShootingDownward(grid, pos))
                    continue;
                if (!HasSeparatedWalkableFloorBelowInColumn(grid, pos))
                    continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Any non-permeable terrain in a voxel strictly between origin and target Y blocks
        /// vertical LoE (floor slabs, etc.). Does not use <see cref="GridNode.IsWalkable"/>,
        /// which can be false due to entities on the tile.
        /// </summary>
        private static bool TerrainBlocksVerticalInteriorSlab(GridNode node)
        {
            if (node?.Terrain == null)
                return false;
            return !node.Terrain.AllowVerticalLineOfEffect;
        }

        /// <summary>
        /// 3D cover bonus evaluation. Returns the same int values the old system
        /// used: -1 = blocked, 0 = none, 2 = standard, 4 = greater.
        /// </summary>
        public static int GetCoverBonus(Vector3Int origin, Vector3Int target)
        {
            VisibilityResult result = Evaluate(origin, target);
            return CoverBonusFromResult(result);
        }

        /// <summary>
        /// Legacy compatibility API (2D). Returns the same int values the old raycast system
        /// used: -1 = blocked, 0 = none, 2 = standard, 4 = greater.
        /// </summary>
        public static int GetCoverBonus(GridPosition origin, GridPosition target)
        {
            VisibilityResult result = Evaluate(origin, target);
            return CoverBonusFromResult(result);
        }

        /// <summary>
        /// Simple LoS check - true if line of sight exists (not fully blocked).
        /// </summary>
        public static bool HasLineOfSight(GridPosition origin, GridPosition target)
        {
            return Evaluate(origin, target).HasLineOfSight;
        }

        /// <summary>
        /// Overload that ignores the layerMask parameter (legacy callers pass one).
        /// </summary>
        public static bool HasLineOfSight(
            GridPosition origin,
            GridPosition target,
            LayerMask ignoredLegacyMask
        )
        {
            return HasLineOfSight(origin, target);
        }

        // Internals

        private static int CoverBonusFromResult(VisibilityResult result)
        {
            if (!result.HasLineOfSight)
                return -1;

            switch (result.Cover)
            {
                case CoverType.Greater:
                    return 4;
                case CoverType.Standard:
                    return 2;
                case CoverType.None:
                default:
                    return 0;
            }
        }

        private static int GetSurfaceY(GridSystem grid, GridPosition pos)
        {
            Unit unit = grid.GetUnitAt(pos);
            if (unit != null)
                return unit.CurrentLayeredPosition.y;

            List<GridNode> column = grid.GetColumn(new Vector2Int(pos.x, pos.z));
            if (column != null && column.Count > 0)
                return column[0].Coordinates.y;

            return 0;
        }

        /// <summary>
        /// 3D LoS and cover evaluation using voxel traversal.
        /// Each voxel along the 3D Bresenham line is checked for obstructions.
        /// </summary>
        private static VisibilityResult EvaluateInternal3D(
            GridSystem grid,
            Vector3Int origin,
            Vector3Int target
        )
        {
            List<Vector3Int> line = Get3DBresenhamLine(origin, target);
            int yMin = Mathf.Min(origin.y, target.y);
            int yMax = Mathf.Max(origin.y, target.y);

            CoverType worstCover = CoverType.None;

            for (int i = 0; i < line.Count; i++)
            {
                Vector3Int voxel = line[i];
                if (voxel == origin || voxel == target)
                    continue;

                GridNode node = grid.GetNode(voxel);
                if (node == null)
                    continue;

                if (voxel.y > yMin && voxel.y < yMax && TerrainBlocksVerticalInteriorSlab(node))
                    return VisibilityResult.Blocked;

                CoverType raw = GetNodeCover(node);
                if (NodeBlocksLineOfEffect(node))
                    return VisibilityResult.Blocked;

                if (raw == CoverType.None)
                    continue;

                if (raw == CoverType.Total)
                    return VisibilityResult.Blocked;

                CoverType adjusted = ApplyElevationAdjustment(raw, origin.y, target.y, voxel.y);

                if (adjusted > worstCover)
                    worstCover = adjusted;

                if (worstCover == CoverType.Total)
                    return VisibilityResult.Blocked;
            }

            if (SameColumnVerticalLoSBlockedByEndpoints(grid, origin, target))
                return VisibilityResult.Blocked;

            if (InteriorColumnLoEBlockedByBridgeDeckAboveLine(grid, origin, target, line))
                return VisibilityResult.Blocked;

            return new VisibilityResult { HasLineOfSight = true, Cover = worstCover };
        }

        /// <summary>
        /// True if terrain explicitly blocks LoE (opaque slab), or node has total cover.
        /// </summary>
        private static bool NodeBlocksLineOfEffect(GridNode node)
        {
            if (node == null)
                return false;
            if (GetNodeCover(node) == CoverType.Total)
                return true;
            return node.Terrain != null && node.Terrain.BlocksLineOfEffect;
        }

        /// <summary>
        /// Same (X,Z) vertical LoE: Bresenham skips origin/target cells, so bridge decks
        /// are never interior voxels. Walkable slabs block by default; use
        /// <see cref="TerrainDef.AllowVerticalLineOfEffect"/> for grates. Shooting up to an
        /// elevated walkable surface is blocked when a separated lower walkable floor exists
        /// in the column (bridge over open air / ground), but not for adjacent stacks (pit rim).
        /// </summary>
        private static bool SameColumnVerticalLoEBlockedByEndpoints(
            GridSystem grid,
            Vector3Int origin,
            Vector3Int target
        )
        {
            if (origin.x != target.x || origin.z != target.z || origin.y == target.y)
                return false;

            if (origin.y > target.y)
            {
                bool block = SameColumnBlocksShootingDownward(grid, origin);
                if (DebugLineOfEffect)
                    Debug.Log(
                        $"[LoE][SameCol] shoot DOWN origin={origin} -> target={target} -> "
                            + $"BLOCK={block}"
                    );
                return block;
            }

            bool blockUp = SameColumnBlocksShootingUpward(grid, target);
            if (DebugLineOfEffect)
                Debug.Log(
                    $"[LoE][SameCol] shoot UP origin={origin} -> target={target} -> "
                        + $"BLOCK={blockUp} "
                        + $"(separatedFloorBelow={HasSeparatedWalkableFloorBelowInColumn(grid, target)})"
                );
            return blockUp;
        }

        private static bool SameColumnBlocksShootingDownward(GridSystem grid, Vector3Int origin)
        {
            GridNode node = grid.GetNode(origin);
            if (node?.Terrain == null)
            {
                if (DebugLineOfEffect)
                    Debug.Log(
                        $"[LoE][SameCol][Down] origin={origin} no terrain on node "
                            + $"(GetNode null? {node == null}) - do NOT block downward"
                    );
                return false;
            }

            if (node.Terrain.AllowVerticalLineOfEffect)
            {
                if (DebugLineOfEffect)
                    Debug.Log(
                        $"[LoE][SameCol][Down] origin={origin} AllowVerticalLineOfEffect=true - no block"
                    );
                return false;
            }

            if (NodeBlocksLineOfEffect(node))
            {
                if (DebugLineOfEffect)
                    Debug.Log(
                        $"[LoE][SameCol][Down] origin={origin} total cover / BlocksLineOfEffect - BLOCK"
                    );
                return true;
            }

            if (DebugLineOfEffect)
                Debug.Log(
                    $"[LoE][SameCol][Down] origin={origin} solid deck (default) - BLOCK. "
                        + $"Terrain.IsWalkable={node.Terrain.IsWalkable} GridNode.IsWalkable={node.IsWalkable()}"
                );
            // Do not use IsWalkable() - props/units with BlocksMovement on the bridge tile
            // made the deck "unwalkable" and accidentally allowed LoE straight down.
            return true;
        }

        private static bool SameColumnBlocksShootingUpward(GridSystem grid, Vector3Int target)
        {
            GridNode targetNode = grid.GetNode(target);
            if (targetNode?.Terrain == null)
            {
                if (DebugLineOfEffect)
                    Debug.Log($"[LoE][SameCol][Up] target={target} no terrain - no block");
                return false;
            }

            if (targetNode.Terrain.AllowVerticalLineOfEffect)
            {
                if (DebugLineOfEffect)
                    Debug.Log(
                        $"[LoE][SameCol][Up] target={target} AllowVerticalLineOfEffect=true - no block"
                    );
                return false;
            }

            if (NodeBlocksLineOfEffect(targetNode))
            {
                if (DebugLineOfEffect)
                    Debug.Log(
                        $"[LoE][SameCol][Up] target={target} total cover / BlocksLineOfEffect - BLOCK"
                    );
                return true;
            }

            if (!targetNode.Terrain.IsWalkable)
            {
                if (DebugLineOfEffect)
                    Debug.Log(
                        $"[LoE][SameCol][Up] target={target} Terrain.IsWalkable=false - no block"
                    );
                return false;
            }

            bool sep = HasSeparatedWalkableFloorBelowInColumn(grid, target);
            if (DebugLineOfEffect)
                Debug.Log(
                    $"[LoE][SameCol][Up] target={target} Terrain.IsWalkable=true "
                        + $"separatedFloorBelow≥2Y={sep}"
                );
            return sep;
        }

        /// <summary>
        /// True if there is a terrain floor in this column strictly below <paramref name="pos"/>
        /// with a gap of at least one integer Y (excludes adjacent pit rim vs ground).
        /// Uses <see cref="TerrainDef.IsWalkable"/> only, not <see cref="GridNode.IsWalkable"/>.
        /// </summary>
        private static bool HasSeparatedWalkableFloorBelowInColumn(GridSystem grid, Vector3Int pos)
        {
            List<GridNode> col = grid.GetColumn(new Vector2Int(pos.x, pos.z));
            if (col == null)
                return false;

            int bestBelowY = int.MinValue;
            foreach (GridNode n in col)
            {
                if (n.Coordinates.y >= pos.y)
                    continue;
                if (n.Terrain == null || !n.Terrain.IsWalkable)
                    continue;
                bestBelowY = Mathf.Max(bestBelowY, n.Coordinates.y);
            }

            if (bestBelowY == int.MinValue)
                return false;

            return pos.y - bestBelowY >= 2;
        }

        private static bool SameColumnVerticalLoSBlockedByEndpoints(
            GridSystem grid,
            Vector3Int origin,
            Vector3Int target
        )
        {
            return SameColumnVerticalLoEBlockedByEndpoints(grid, origin, target);
        }

        private static CoverType GetNodeCover(GridNode node)
        {
            CoverType terrainCover = node.Terrain != null ? node.Terrain.CoverType : CoverType.None;

            CoverType entityCover = CoverType.None;
            if (node.Entities != null)
            {
                foreach (IGridEntity entity in node.Entities)
                {
                    if (entity != null && entity.CoverType > entityCover)
                        entityCover = entity.CoverType;
                }
            }

            return terrainCover > entityCover ? terrainCover : entityCover;
        }

        /// <summary>
        /// PF2e elevation adjustments:
        ///   - Observer above obstacle: reduce cover by one step (looking over it)
        ///   - Both observer AND target below obstacle: increase cover by one step
        /// </summary>
        private static CoverType ApplyElevationAdjustment(
            CoverType baseCover,
            int originY,
            int targetY,
            int obstacleY
        )
        {
            if (baseCover == CoverType.None)
                return CoverType.None;

            bool observerAbove = originY > obstacleY;
            bool targetAbove = targetY > obstacleY;

            if (observerAbove)
                return StepCover(baseCover, -1);

            if (!observerAbove && !targetAbove)
                return StepCover(baseCover, +1);

            return baseCover;
        }

        private static CoverType StepCover(CoverType cover, int direction)
        {
            int stepped = (int)cover + direction;
            if (stepped < 0)
                return CoverType.None;
            if (stepped > (int)CoverType.Total)
                return CoverType.Total;
            return (CoverType)stepped;
        }

        /// <summary>
        /// 3D Bresenham line algorithm. Returns all voxels the line passes through
        /// from start to end (inclusive). The driving axis is the one with the
        /// largest absolute delta.
        /// </summary>
        private static List<Vector3Int> Get3DBresenhamLine(Vector3Int start, Vector3Int end)
        {
            List<Vector3Int> result = new List<Vector3Int>();

            int dx = Mathf.Abs(end.x - start.x);
            int dy = Mathf.Abs(end.y - start.y);
            int dz = Mathf.Abs(end.z - start.z);

            int xs = end.x > start.x ? 1 : (end.x < start.x ? -1 : 0);
            int ys = end.y > start.y ? 1 : (end.y < start.y ? -1 : 0);
            int zs = end.z > start.z ? 1 : (end.z < start.z ? -1 : 0);

            int x = start.x,
                y = start.y,
                z = start.z;

            if (dx >= dy && dx >= dz)
            {
                int ey = 2 * dy - dx;
                int ez = 2 * dz - dx;
                for (int i = 0; i <= dx; i++)
                {
                    result.Add(new Vector3Int(x, y, z));
                    if (ey >= 0)
                    {
                        y += ys;
                        ey -= 2 * dx;
                    }
                    if (ez >= 0)
                    {
                        z += zs;
                        ez -= 2 * dx;
                    }
                    ey += 2 * dy;
                    ez += 2 * dz;
                    x += xs;
                }
            }
            else if (dy >= dx && dy >= dz)
            {
                int ex = 2 * dx - dy;
                int ez = 2 * dz - dy;
                for (int i = 0; i <= dy; i++)
                {
                    result.Add(new Vector3Int(x, y, z));
                    if (ex >= 0)
                    {
                        x += xs;
                        ex -= 2 * dy;
                    }
                    if (ez >= 0)
                    {
                        z += zs;
                        ez -= 2 * dy;
                    }
                    ex += 2 * dx;
                    ez += 2 * dz;
                    y += ys;
                }
            }
            else
            {
                int ex = 2 * dx - dz;
                int ey = 2 * dy - dz;
                for (int i = 0; i <= dz; i++)
                {
                    result.Add(new Vector3Int(x, y, z));
                    if (ex >= 0)
                    {
                        x += xs;
                        ex -= 2 * dz;
                    }
                    if (ey >= 0)
                    {
                        y += ys;
                        ey -= 2 * dz;
                    }
                    ex += 2 * dx;
                    ey += 2 * dy;
                    z += zs;
                }
            }

            return result;
        }
    }
}
