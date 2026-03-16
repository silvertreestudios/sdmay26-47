using System.Collections.Generic;
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

            // Diagonal cost: alternating 5, 10, 5, 10...
            int diagCost = 0;
            for (int i = 0; i < diagonal; i++)
            {
                diagCost += (i % 2 == 0) ? 5 : 10;
            }

            return diagCost + straight * 5;
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
    }
}
