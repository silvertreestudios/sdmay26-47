using System.Collections.Generic;
using PathfinderTactics.Core;
using PathfinderTactics.Data.PF2e;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Spells.Services
{
    /// <summary>
    /// Pure spatial math - converts AreaDefinitions into grid cell lists.
    ///
    /// PF2e area rules:
    /// - 1 grid cell = 5 ft
    /// - Burst: Originates from an intersection point (corner of 4 tiles).
    ///   The cursor tile defines the intersection at (tile.x+0.5, tile.z+0.5).
    ///   Uses Euclidean distance from intersection to tile center.
    /// - Emanation: Circle centered on the caster's tile center.
    /// - Cone: 90 degree wedge locked to 8 directions (cardinal + diagonal).
    /// - Line: 1 cell wide path using Bresenham's algorithm for any angle.
    /// </summary>
    public static class AreaService
    {
        public static List<GridPosition> GetAffectedCells(
            GridPosition origin,
            AreaDefinition area,
            GridPosition casterPosition = default,
            GridPosition directionTarget = default
        )
        {
            List<GridPosition> cells = new List<GridPosition>();

            switch (area.Shape)
            {
                case AreaShape.Burst:
                    AddBurstCells(cells, origin, area.Radius);
                    break;
                case AreaShape.Emanation:
                    AddEmanationCells(cells, casterPosition, area.Radius);
                    break;
                case AreaShape.Line:
                    AddLineCells(cells, casterPosition, directionTarget, area.Radius);
                    break;
                case AreaShape.Cone:
                    AddConeCells(cells, casterPosition, directionTarget, area.Radius);
                    break;
                case AreaShape.None:
                default:
                    cells.Add(origin);
                    break;
            }

            return cells;
        }

        // BURST - Intersection-centered Euclidean circle

        /// <summary>
        /// PF2e burst originates from an intersection point (corner of 4 tiles).
        /// A tile is included if the PF2e grid distance from the intersection
        /// to the tile's nearest corner is within the burst radius.
        /// </summary>
        private static void AddBurstCells(List<GridPosition> cells, GridPosition origin, int radius)
        {
            int radiusInFeet = radius * 5; // Convert tiles to feet

            // Scan a bounding box based on the 2x2 center
            for (int tx = origin.x - radius + 1; tx <= origin.x + radius; tx++)
            {
                for (int tz = origin.z - radius + 1; tz <= origin.z + radius; tz++)
                {
                    // Compute distance in 5ft steps outward from the exact intersection.
                    // The intersection is between columns origin.x and origin.x + 1
                    // The 4 central tiles (tx=origin.x, tx=origin.x+1) are 1 step (5ft) away from the intersection.
                    int stepX = tx <= origin.x ? origin.x - tx + 1 : tx - origin.x;
                    int stepZ = tz <= origin.z ? origin.z - tz + 1 : tz - origin.z;

                    // Calculate PF2e distance using alternating diagonal rule
                    int dist = Pf2eGridDistance(stepX, stepZ);

                    if (dist <= radiusInFeet)
                    {
                        cells.Add(new GridPosition(tx, tz));
                    }
                }
            }
        }

        /// <summary>
        /// Calculates PF2e grid distance using the alternating diagonal rule.
        /// dx and dz are in grid steps (each step = 5ft).
        /// Diagonal steps alternate 5ft and 10ft: 1st=5, 2nd=10, 3rd=5, 4th=10...
        /// </summary>
        public static int Pf2eGridDistance(int dx, int dz)
        {
            int diagonal = Mathf.Min(dx, dz);
            int straight = Mathf.Max(dx, dz) - diagonal;

            int diagCost = 0;
            for (int i = 0; i < diagonal; i++)
            {
                diagCost += (i % 2 == 0) ? 5 : 10;
            }

            return diagCost + straight * 5;
        }

        /// <summary>
        /// 3D PF2e distance in feet using the 2-step merge rule.
        /// Step 1: merge dx and dz horizontally (tiles).
        /// Step 2: merge horizontal result with dy (tiles).
        /// dx, dy, dz are in grid steps (tiles).
        /// </summary>
        public static int Pf2eGridDistance3D(int dx, int dy, int dz)
        {
            int dHorizontalTiles = Mathf.Max(dx, dz) + Mathf.FloorToInt(Mathf.Min(dx, dz) / 2f);
            int totalTiles =
                Mathf.Max(dHorizontalTiles, dy)
                + Mathf.FloorToInt(Mathf.Min(dHorizontalTiles, dy) / 2f);
            return totalTiles * 5;
        }

        // EMANATION - Tile-centered Euclidean circle

        /// <summary>
        /// Emanation from the caster's tile center.
        /// Uses Euclidean distance from tile center to tile center.
        /// The +0.5 threshold includes tiles whose edge touches the radius boundary.
        /// </summary>
        private static void AddEmanationCells(
            List<GridPosition> cells,
            GridPosition center,
            int radius
        )
        {
            float radiusF = radius + 0.5f;
            float radiusSq = radiusF * radiusF;

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    float distSq = (float)(x * x + z * z);
                    if (distSq <= radiusSq)
                    {
                        cells.Add(new GridPosition(center.x + x, center.z + z));
                    }
                }
            }
        }

        // LINE - Bresenham's algorithm, exactly 1 cell wide

        /// <summary>
        /// A 1-cell-wide line from the caster toward the target.
        /// Uses Bresenham's algorithm for pixel-perfect 1-cell-wide output
        /// at any angle. Extends 'length' tiles from the caster.
        /// </summary>
        private static void AddLineCells(
            List<GridPosition> cells,
            GridPosition start,
            GridPosition end,
            int length
        )
        {
            if (start.x == end.x && start.z == end.z)
                return;

            // Compute the endpoint: start + normalized_direction * length
            float dx = end.x - start.x;
            float dz = end.z - start.z;
            float mag = Mathf.Sqrt(dx * dx + dz * dz);
            int targetX = Mathf.RoundToInt(start.x + (dx / mag) * length);
            int targetZ = Mathf.RoundToInt(start.z + (dz / mag) * length);

            // Bresenham's line from start to computed endpoint
            int x0 = start.x;
            int z0 = start.z;
            int x1 = targetX;
            int z1 = targetZ;

            int absDx = Mathf.Abs(x1 - x0);
            int absDz = Mathf.Abs(z1 - z0);
            int sx = x0 < x1 ? 1 : -1;
            int sz = z0 < z1 ? 1 : -1;
            int err = absDx - absDz;

            int cx = x0;
            int cz = z0;
            int cellCount = 0;

            while (cellCount <= length + 1) // Safety bound
            {
                // Skip the caster's tile
                if (cx != start.x || cz != start.z)
                {
                    cells.Add(new GridPosition(cx, cz));
                    cellCount++;
                    if (cellCount >= length)
                        break;
                }

                if (cx == x1 && cz == z1)
                    break;

                int e2 = 2 * err;
                if (e2 > -absDz)
                {
                    err -= absDz;
                    cx += sx;
                }
                if (e2 < absDx)
                {
                    err += absDx;
                    cz += sz;
                }
            }
        }

        // CONE - Exact PF2e Distance Based Generation

        /// <summary>
        /// PF2e grid templates for cones generated mathematically.
        /// Snaps to 8 directions (Cardinal and Diagonal).
        /// Diagonal cones originate from a corner, producing spherical boundaries.
        /// </summary>
        private static void AddConeCells(
            List<GridPosition> cells,
            GridPosition start,
            GridPosition end,
            int radius
        )
        {
            if (start.x == end.x && start.z == end.z)
                return;

            // Snap direction to nearest 45 degree angle
            float rawDx = end.x - start.x;
            float rawDz = end.z - start.z;
            float angle = Mathf.Atan2(rawDz, rawDx);
            float snappedAngle = Mathf.Round(angle / (Mathf.PI / 4f)) * (Mathf.PI / 4f);

            int dirX = Mathf.RoundToInt(Mathf.Cos(snappedAngle));
            int dirZ = Mathf.RoundToInt(Mathf.Sin(snappedAngle));

            bool isDiagonal = (Mathf.Abs(dirX) == 1 && Mathf.Abs(dirZ) == 1);
            int radiusFt = radius * 5;

            if (isDiagonal)
            {
                // Diagonal Cone Template
                // Originates from a corner. Generates spherical outer edge.
                for (int forward = 1; forward <= radius; forward++)
                {
                    for (int perp = 1; perp <= radius; perp++)
                    {
                        if (Pf2eGridDistance(forward, perp) <= radiusFt)
                        {
                            cells.Add(
                                new GridPosition(start.x + forward * dirX, start.z + perp * dirZ)
                            );
                        }
                    }
                }
            }
            else
            {
                // Cardinal Cone Template
                // 15ft cones (rad<=3) use edge-center origin (start 1-wide).
                // 30ft+ cones (rad>=4) use intersection origin (start 2-wide).
                bool useIntersectionOrigin = radius >= 4;

                for (int forward = 1; forward <= radius; forward++)
                {
                    for (int perp = -radius; perp <= radius; perp++)
                    {
                        int dist;
                        int gridOffsetPerp;

                        if (useIntersectionOrigin)
                        {
                            if (perp == 0)
                                continue; // Intersection means no center tile

                            int stepPerp = Mathf.Abs(perp);
                            if (stepPerp > forward)
                                continue; // Stay within 90-degree wedge

                            dist = Pf2eGridDistance(stepPerp, forward);

                            // Close the physical gap on the grid.
                            // The intersection sits strictly between two columns/rows.
                            // Maps perp {-2, -1, 1, 2} to grid offsets {-1, 0, 1, 2}
                            // Ensures the two halves are perfectly contiguous with zero gap.
                            gridOffsetPerp = perp > 0 ? perp : perp + 1;
                        }
                        else
                        {
                            // Edge-center means there is a center tile (perp=0).
                            int stepPerp = Mathf.Abs(perp);

                            // Standard 15ft width logic: 1, 3, 3...
                            if (stepPerp > forward / 2)
                                continue;

                            dist = Pf2eGridDistance(stepPerp, forward);
                            gridOffsetPerp = perp; // Exactly aligned
                        }

                        if (dist <= radiusFt)
                        {
                            int cellX = start.x;
                            int cellZ = start.z;

                            if (Mathf.Abs(dirX) == 1) // Pointing East/West
                            {
                                cellX += forward * dirX;
                                cellZ += gridOffsetPerp;
                            }
                            else // Pointing North/South
                            {
                                cellZ += forward * dirZ;
                                cellX += gridOffsetPerp;
                            }

                            cells.Add(new GridPosition(cellX, cellZ));
                        }
                    }
                }
            }
        }

        // 3D Area Methods

        /// <summary>
        /// Full 3D area calculation. Returns all affected voxels (Vector3Int) including
        /// correct Y layers, filtered by PF2e 3D distance and Line of Effect.
        /// </summary>
        public static List<Vector3Int> GetAffectedCells3D(
            Vector3Int origin,
            AreaDefinition area,
            Vector3Int casterPosition = default,
            Vector3Int directionTarget = default
        )
        {
            List<Vector3Int> cells = new List<Vector3Int>();
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
                return cells;

            switch (area.Shape)
            {
                case AreaShape.Burst:
                    AddBurstCells3D(cells, origin, area.Radius, grid);
                    break;
                case AreaShape.Emanation:
                    AddEmanationCells3D(cells, casterPosition, area.Radius, grid);
                    break;
                case AreaShape.Line:
                    AddLineCells3D(cells, casterPosition, directionTarget, area.Radius, grid);
                    break;
                case AreaShape.Cone:
                    AddConeCells3D(cells, casterPosition, directionTarget, area.Radius, grid);
                    break;
                case AreaShape.None:
                default:
                    if (grid.GetNode(origin) != null)
                        cells.Add(origin);
                    break;
            }

            return cells;
        }

        /// <summary>
        /// 3D burst centered on an intersection point. Uses 2-step PF2e distance
        /// from the intersection to each node, with LoE check per tile.
        /// </summary>
        private static void AddBurstCells3D(
            List<Vector3Int> cells,
            Vector3Int origin,
            int radius,
            GridSystem grid
        )
        {
            int radiusInFeet = radius * 5;

            for (int tx = origin.x - radius + 1; tx <= origin.x + radius; tx++)
            {
                for (int tz = origin.z - radius + 1; tz <= origin.z + radius; tz++)
                {
                    Vector2Int colKey = new Vector2Int(tx, tz);
                    List<GridNode> column = grid.GetColumn(colKey);
                    if (column == null || column.Count == 0)
                        continue;

                    int stepX = tx <= origin.x ? origin.x - tx + 1 : tx - origin.x;
                    int stepZ = tz <= origin.z ? origin.z - tz + 1 : tz - origin.z;

                    foreach (GridNode node in column)
                    {
                        int stepY = Mathf.Abs(node.Coordinates.y - origin.y);
                        int dist = Pf2eGridDistance3D(stepX, stepY, stepZ);

                        if (dist <= radiusInFeet)
                        {
                            Vector3Int nodePos = node.Coordinates;
                            if (LineOfSightUtility.HasLineOfEffect(origin, nodePos))
                                cells.Add(nodePos);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 3D emanation centered on the caster's position. Uses PF2e 3D distance
        /// from caster to each node, with LoE check per tile.
        /// </summary>
        private static void AddEmanationCells3D(
            List<Vector3Int> cells,
            Vector3Int center,
            int radius,
            GridSystem grid
        )
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    Vector2Int colKey = new Vector2Int(center.x + x, center.z + z);
                    List<GridNode> column = grid.GetColumn(colKey);
                    if (column == null || column.Count == 0)
                        continue;

                    foreach (GridNode node in column)
                    {
                        Vector3Int testPos = node.Coordinates;
                        int dist = PF2E_Core.GetPF2eDistance3D(center, testPos);
                        if (dist <= radius && LineOfSightUtility.HasLineOfEffect(center, testPos))
                        {
                            cells.Add(testPos);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 3D line using Bresenham from caster toward target direction.
        /// Checks each voxel along the line for LoE.
        /// </summary>
        private static void AddLineCells3D(
            List<Vector3Int> cells,
            Vector3Int start,
            Vector3Int end,
            int length,
            GridSystem grid
        )
        {
            if (start == end)
                return;

            float dx = end.x - start.x;
            float dy = end.y - start.y;
            float dz = end.z - start.z;
            float mag = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            if (mag < 0.001f)
                return;

            int targetX = Mathf.RoundToInt(start.x + (dx / mag) * length);
            int targetY = Mathf.RoundToInt(start.y + (dy / mag) * length);
            int targetZ = Mathf.RoundToInt(start.z + (dz / mag) * length);
            Vector3Int lineEnd = new Vector3Int(targetX, targetY, targetZ);

            int absDx = Mathf.Abs(lineEnd.x - start.x);
            int absDy = Mathf.Abs(lineEnd.y - start.y);
            int absDz = Mathf.Abs(lineEnd.z - start.z);
            int sx = start.x < lineEnd.x ? 1 : (start.x > lineEnd.x ? -1 : 0);
            int sy = start.y < lineEnd.y ? 1 : (start.y > lineEnd.y ? -1 : 0);
            int sz = start.z < lineEnd.z ? 1 : (start.z > lineEnd.z ? -1 : 0);

            int maxSteps = absDx + absDy + absDz;
            int cx = start.x,
                cy = start.y,
                cz = start.z;
            int cellCount = 0;

            int errXY = absDx - absDy;
            int errXZ = absDx - absDz;

            for (int step = 0; step <= maxSteps + 1 && cellCount < length; step++)
            {
                Vector3Int current = new Vector3Int(cx, cy, cz);
                if (current != start)
                {
                    GridNode node = grid.GetNode(current);
                    if (node != null && LineOfSightUtility.HasLineOfEffect(start, current))
                    {
                        cells.Add(current);
                    }
                    cellCount++;
                }

                if (cx == lineEnd.x && cy == lineEnd.y && cz == lineEnd.z)
                    break;

                int e2xy = 2 * errXY;
                int e2xz = 2 * errXZ;

                if (e2xy > -absDy)
                {
                    errXY -= absDy;
                    cx += sx;
                }
                if (e2xy < absDx)
                {
                    errXY += absDx;
                    cy += sy;
                }
                if (e2xz > -absDz)
                {
                    errXZ -= absDz;
                    cx += sx;
                }
                if (e2xz < absDx)
                {
                    errXZ += absDx;
                    cz += sz;
                }
            }
        }

        /// <summary>
        /// 3D cone using the existing 2D template math, extended through Y levels.
        /// For each (x,z) cell in the 2D cone, all Y nodes within PF2e 3D distance
        /// and LoE are included.
        /// </summary>
        private static void AddConeCells3D(
            List<Vector3Int> cells,
            Vector3Int start,
            Vector3Int end,
            int radius,
            GridSystem grid
        )
        {
            if (start.x == end.x && start.z == end.z)
                return;

            List<GridPosition> cone2D = new List<GridPosition>();
            GridPosition start2D = new GridPosition(start.x, start.z);
            GridPosition end2D = new GridPosition(end.x, end.z);
            AddConeCells(cone2D, start2D, end2D, radius);

            int radiusFt = radius * 5;
            foreach (GridPosition gp in cone2D)
            {
                Vector2Int colKey = new Vector2Int(gp.x, gp.z);
                List<GridNode> column = grid.GetColumn(colKey);
                if (column == null || column.Count == 0)
                    continue;

                foreach (GridNode node in column)
                {
                    Vector3Int testPos = node.Coordinates;
                    int dist = PF2E_Core.GetPF2eDistance3DInFeet(start, testPos);
                    if (dist <= radiusFt && LineOfSightUtility.HasLineOfEffect(start, testPos))
                    {
                        cells.Add(testPos);
                    }
                }
            }
        }
    }
}
