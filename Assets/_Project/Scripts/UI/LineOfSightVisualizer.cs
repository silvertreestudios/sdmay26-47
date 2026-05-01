using System.Collections.Generic;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.UI
{
    /// <summary>
    /// Attach this to an Empty GameObject in your scene to debug Line of Effect and Line of Sight in Play Mode.
    /// Provide it with two Transforms to watch the calculated 3D Bresenham path turn Green (Clear) or Red (Blocked) or Yellow (Standard Cover) or Orange (Greater Cover).
    /// </summary>
    public class LineOfSightVisualizer : MonoBehaviour
    {
        public Transform Origin;
        public Transform Target;

        [Header("Visualization Settings")]
        [Tooltip(
            "If true, checks Line of Sight (including Cover). If false, strictly checks Line of Effect (Solid terrain walls)."
        )]
        public bool CheckLineOfSight = false;

        public float VoxelScale = 0.9f;
        public Color ClearColor = new Color(0, 1, 0, 0.4f);
        public Color StandardCoverColor = new Color(1, 1, 0, 0.4f); // Yellow
        public Color GreaterCoverColor = new Color(1, 0.5f, 0, 0.5f); // Orange

        [Tooltip("Used for Total Cover and blocked Line of Effect.")]
        public Color BlockedColor = new Color(1, 0, 0, 0.6f);
        public Color OriginTargetColor = new Color(0, 0, 1, 0.5f);
        public Color WireColor = new Color(1, 1, 1, 0.2f);

        private void OnDrawGizmos()
        {
            if (Origin == null || Target == null)
                return;

            if (!Application.isPlaying)
                return;

            if (!ServiceLocator.TryGet<GridSystem>(out GridSystem grid))
                return;

            Vector3Int startVoxel = grid.GetLayeredGridPosition(Origin.position);
            Vector3Int endVoxel = grid.GetLayeredGridPosition(Target.position);

            List<Vector3Int> line = LineOfSightUtility.Get3DBresenhamLine(startVoxel, endVoxel);
            if (line == null || line.Count == 0)
                return;

            CoverType highestCover = CoverType.None;

            // Draw origin and target visually distinct
            Vector3 size = new Vector3(
                grid.CellSize * VoxelScale,
                grid.VerticalCellSize * VoxelScale,
                grid.CellSize * VoxelScale
            );
            Vector3 halfYOffset = new Vector3(0, grid.VerticalCellSize / 2f, 0);

            Gizmos.color = OriginTargetColor;
            Gizmos.DrawCube(grid.GetWorldPosition(startVoxel) + halfYOffset, size * 1.1f);
            Gizmos.DrawCube(grid.GetWorldPosition(endVoxel) + halfYOffset, size * 1.1f);

            // Step through each voxel in the Bresenham line and evaluate visibility from the origin
            for (int i = 0; i < line.Count; i++)
            {
                Vector3Int voxel = line[i];

                if (highestCover != CoverType.Total)
                {
                    if (CheckLineOfSight)
                    {
                        VisibilityResult result = LineOfSightUtility.Evaluate(startVoxel, voxel);
                        if (result.Cover > highestCover)
                        {
                            highestCover = result.Cover;
                        }
                    }
                    else
                    {
                        if (!LineOfSightUtility.HasLineOfEffect(startVoxel, voxel))
                        {
                            highestCover = CoverType.Total;
                        }
                    }
                }

                switch (highestCover)
                {
                    case CoverType.None:
                        Gizmos.color = ClearColor;
                        break;
                    case CoverType.Standard:
                        Gizmos.color = StandardCoverColor;
                        break;
                    case CoverType.Greater:
                        Gizmos.color = GreaterCoverColor;
                        break;
                    case CoverType.Total:
                    default:
                        Gizmos.color = BlockedColor;
                        break;
                }

                Vector3 worldPos = grid.GetWorldPosition(voxel) + halfYOffset;

                Gizmos.DrawCube(worldPos, size);
                Gizmos.color = WireColor;
                Gizmos.DrawWireCube(worldPos, size);
            }

            // Draw a direct line between the transforms so you can see where they are currently placed in space
            Gizmos.color = highestCover == CoverType.Total ? Color.red : Color.green;
            Gizmos.DrawLine(Origin.position, Target.position);
        }
    }
}
