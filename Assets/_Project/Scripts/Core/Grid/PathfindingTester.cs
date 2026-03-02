using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PathfinderTactics.Grid
{
    public class PathfindingTester : MonoBehaviour
    {
        [Header("Testing")]
        [SerializeField]
        private Transform startTransform;

        [SerializeField]
        private Transform endTransform;

        private List<GridPosition> currentPath;

        private void Update()
        {
#if UNITY_EDITOR
            // Do not run in edit mode
            if (!Application.isPlaying)
                return;
#endif

            if (startTransform == null || endTransform == null)
                return;

            GridPosition startPos = GridSystem.Instance.GetGridPosition(startTransform.position);
            GridPosition endPos = GridSystem.Instance.GetGridPosition(endTransform.position);

            currentPath = Pathfinding.FindPath(startPos, endPos);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            if (currentPath != null && currentPath.Count > 1)
            {
#if UNITY_EDITOR
                Handles.color = Color.blue;
                // Create an array of points for smoother drawing
                Vector3[] points = new Vector3[currentPath.Count];
                for (int i = 0; i < currentPath.Count; i++)
                {
                    // Lift the line slightly so it doesn't clip into the floor
                    points[i] =
                        GridSystem.Instance.GetWorldPosition(currentPath[i]) + Vector3.up * 0.1f;
                }

                // Draw the line (Thickness: 5.0f)
                Handles.DrawAAPolyLine(5.0f, points);
#else
                // Fallback for non-editor builds (standard thin Gizmos)
                Gizmos.color = Color.green;
                for (int i = 0; i < currentPath.Count - 1; i++)
                {
                    Vector3 from = GridSystem.Instance.GetWorldPosition(currentPath[i]);
                    Vector3 to = GridSystem.Instance.GetWorldPosition(currentPath[i + 1]);
                    Gizmos.DrawLine(from, to);
                }
#endif
            }
        }
    }
}
