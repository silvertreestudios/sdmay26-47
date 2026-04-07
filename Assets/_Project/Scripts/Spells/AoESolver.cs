using System.Collections.Generic;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Combat.Spells
{
    public static class AoESolver
    {
        /// <summary>
        /// Generates a 3D Burst footprint.
        /// Origin is treated as the bottom-left grid intersection of the 2x2 core.
        /// </summary>
        public static List<Vector3Int> GetBurstVoxels(
            Vector3Int origin,
            int radiusFeet,
            bool requireLineOfEffect = true
        )
        {
            int radiusTiles = radiusFeet / 5;
            List<Vector3Int> validVoxels = new List<Vector3Int>();

            // +1 accounts for the intersection split
            for (int x = -radiusTiles; x <= radiusTiles + 1; x++)
            {
                for (int y = -radiusTiles; y <= radiusTiles; y++) // Y remains voxel-centered
                {
                    for (int z = -radiusTiles; z <= radiusTiles + 1; z++)
                    {
                        Vector3Int targetVoxel = origin + new Vector3Int(x, y, z);

                        // Calculate distance from the intersection (0.5 offset)
                        float dxOffset = targetVoxel.x - origin.x - 0.5f;
                        float dzOffset = targetVoxel.z - origin.z - 0.5f;

                        // Snaps to -2, -1, 1, 2 (No zero, creating the 2-cell wide core)
                        int abs_dx = Mathf.Abs(
                            dxOffset > 0 ? Mathf.CeilToInt(dxOffset) : Mathf.FloorToInt(dxOffset)
                        );
                        int abs_dz = Mathf.Abs(
                            dzOffset > 0 ? Mathf.CeilToInt(dzOffset) : Mathf.FloorToInt(dzOffset)
                        );
                        int abs_dy = Mathf.Abs(targetVoxel.y - origin.y);

                        // Standard PF2e 5-10-5 Distance
                        int distHorizontal =
                            Mathf.Max(abs_dx, abs_dz)
                            + Mathf.FloorToInt(Mathf.Min(abs_dx, abs_dz) / 2f);
                        int dist3D =
                            Mathf.Max(distHorizontal, abs_dy)
                            + Mathf.FloorToInt(Mathf.Min(distHorizontal, abs_dy) / 2f);

                        if (dist3D <= radiusTiles)
                        {
                            if (
                                !requireLineOfEffect
                                || LineOfSightUtility.HasLineOfEffect(origin, targetVoxel)
                            )
                            {
                                validVoxels.Add(targetVoxel);
                            }
                        }
                    }
                }
            }
            return validVoxels;
        }

        /// <summary>
        /// PF2e 1-2-1-2 distance metric extended to 3 axes.
        /// </summary>
        private static int GetDist3D(int diffX, int diffY, int diffZ)
        {
            int[] arr = { Mathf.Abs(diffX), Mathf.Abs(diffY), Mathf.Abs(diffZ) };
            System.Array.Sort(arr); // Sorts ascending

            // arr[2] is the max (largest distance), arr[1] is mid, arr[0] is min
            return arr[2] + Mathf.FloorToInt(arr[1] / 2f) + Mathf.FloorToInt(arr[0] / 2f);
        }

        /// <summary>
        /// Volumetric 3D Cone Algorithm. Lots of pain and suffering was endured to get this right.
        /// </summary>
        public static List<Vector3Int> GetConeVoxels(
            Vector3Int origin,
            int lengthFeet,
            Vector3Int directionTarget
        )
        {
            int rangeSquares = lengthFeet / 5;
            List<Vector3Int> validVoxels = new List<Vector3Int>();

            if (origin == directionTarget)
                return validVoxels;

            // Map the raw target voxel into a clean 3D direction vector (-1, 0, or 1 for each axis)
            Vector3 rawDir = new Vector3(
                directionTarget.x - origin.x,
                directionTarget.y - origin.y,
                directionTarget.z - origin.z
            );
            float maxAbs = Mathf.Max(
                Mathf.Abs(rawDir.x),
                Mathf.Max(Mathf.Abs(rawDir.y), Mathf.Abs(rawDir.z))
            );

            int vx = Mathf.RoundToInt(rawDir.x / maxAbs);
            int vy = Mathf.RoundToInt(rawDir.y / maxAbs);
            int vz = Mathf.RoundToInt(rawDir.z / maxAbs);

            int numNonZero = Mathf.Abs(vx) + Mathf.Abs(vy) + Mathf.Abs(vz);
            bool is15ftOrtho = (rangeSquares == 3 && numNonZero == 1);

            // Origin definitions: Corner (0.5) for standard, Face (0.0) for 15ft Ortho
            float ox = vx != 0 ? vx * 0.5f : (is15ftOrtho ? 0f : 0.5f);
            float oy = vy != 0 ? vy * 0.5f : (is15ftOrtho ? 0f : 0.5f);
            float oz = vz != 0 ? vz * 0.5f : (is15ftOrtho ? 0f : 0.5f);

            for (int x = -rangeSquares; x <= rangeSquares; x++)
            {
                for (int y = -rangeSquares; y <= rangeSquares; y++)
                {
                    for (int z = -rangeSquares; z <= rangeSquares; z++)
                    {
                        if (x == 0 && y == 0 && z == 0)
                            continue;

                        // Skip cells outside the directional quadrant
                        if (vx != 0 && System.Math.Sign(x) != System.Math.Sign(vx) && x != 0)
                            continue;
                        if (vy != 0 && System.Math.Sign(y) != System.Math.Sign(vy) && y != 0)
                            continue;
                        if (vz != 0 && System.Math.Sign(z) != System.Math.Sign(vz) && z != 0)
                            continue;

                        // PF2e cones must start moving forward
                        if (vx != 0 && x == 0)
                            continue;
                        if (vy != 0 && y == 0)
                            continue;
                        if (vz != 0 && z == 0)
                            continue;

                        // Symmetrically map grid coordinates to distance steps
                        int stepsX =
                            (is15ftOrtho && vx == 0)
                                ? Mathf.Abs(x)
                                : Mathf.CeilToInt(Mathf.Abs(x - ox));
                        int stepsY =
                            (is15ftOrtho && vy == 0)
                                ? Mathf.Abs(y)
                                : Mathf.CeilToInt(Mathf.Abs(y - oy));
                        int stepsZ =
                            (is15ftOrtho && vz == 0)
                                ? Mathf.Abs(z)
                                : Mathf.CeilToInt(Mathf.Abs(z - oz));

                        int activeSteps = Mathf.Max(
                            vx != 0 ? stepsX : 0,
                            Mathf.Max(vy != 0 ? stepsY : 0, vz != 0 ? stepsZ : 0)
                        );

                        // Frustum expansion limit
                        int limit = is15ftOrtho ? activeSteps - 1 : activeSteps;

                        if (vx == 0 && stepsX > limit)
                            continue;
                        if (vy == 0 && stepsY > limit)
                            continue;
                        if (vz == 0 && stepsZ > limit)
                            continue;

                        // Distance radius constraint (Shapes the rounded cap)
                        int dist = GetDist3D(stepsX, stepsY, stepsZ);
                        if (dist <= rangeSquares)
                        {
                            Vector3Int voxel = new Vector3Int(
                                origin.x + x,
                                origin.y + y,
                                origin.z + z
                            );

                            // Check for solid walls blocking the magic
                            if (LineOfSightUtility.HasLineOfEffect(origin, voxel))
                            {
                                validVoxels.Add(voxel);
                            }
                        }
                    }
                }
            }

            return validVoxels;
        }

        public static List<Vector3Int> GetEmanationVoxels(Vector3Int center, int radiusFeet)
        {
            // Emanations originate from the voxel center.
            int radiusTiles = radiusFeet / 5;
            List<Vector3Int> validVoxels = new List<Vector3Int>();

            for (int x = -radiusTiles; x <= radiusTiles; x++)
            {
                for (int y = -radiusTiles; y <= radiusTiles; y++)
                {
                    for (int z = -radiusTiles; z <= radiusTiles; z++)
                    {
                        Vector3Int targetVoxel = center + new Vector3Int(x, y, z);
                        if (PF2E_Core.GetPF2eDistance3D(center, targetVoxel) <= radiusTiles)
                        {
                            if (LineOfSightUtility.HasLineOfEffect(center, targetVoxel))
                                validVoxels.Add(targetVoxel);
                        }
                    }
                }
            }
            return validVoxels;
        }

        public static List<Vector3Int> GetLineVoxels(
            Vector3Int origin,
            int lengthFeet,
            Vector3Int directionTarget
        )
        {
            int lengthTiles = lengthFeet / 5;
            List<Vector3Int> validVoxels = new List<Vector3Int>();

            Vector3 originWorld = new Vector3(origin.x, origin.y, origin.z);
            Vector3 targetWorld = new Vector3(
                directionTarget.x,
                directionTarget.y,
                directionTarget.z
            );
            Vector3 aimDir = (targetWorld - originWorld).normalized;

            Vector3 extendedWorld = originWorld + (aimDir * (lengthTiles * 2));
            Vector3Int extendedVoxel = new Vector3Int(
                Mathf.RoundToInt(extendedWorld.x),
                Mathf.RoundToInt(extendedWorld.y),
                Mathf.RoundToInt(extendedWorld.z)
            );

            List<Vector3Int> fullLine = LineOfSightUtility.Get3DBresenhamLine(
                origin,
                extendedVoxel
            );

            foreach (Vector3Int voxel in fullLine)
            {
                if (voxel == origin)
                    continue;
                if (PF2E_Core.GetPF2eDistance3D(origin, voxel) > lengthTiles)
                    break;
                if (!LineOfSightUtility.HasLineOfEffect(origin, voxel))
                    break;

                validVoxels.Add(voxel);
            }
            return validVoxels;
        }
    }
}
