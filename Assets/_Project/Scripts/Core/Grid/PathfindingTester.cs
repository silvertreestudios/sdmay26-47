using System.Collections.Generic;
using PathfinderTactics.Core;
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

        private List<Vector3Int> currentPath;

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif

            if (startTransform == null || endTransform == null)
                return;

            GridPosition startPos = ServiceLocator
                .Get<GridSystem>()
                .GetGridPosition(startTransform.position);
            GridPosition endPos = ServiceLocator
                .Get<GridSystem>()
                .GetGridPosition(endTransform.position);

            currentPath = Pathfinding.FindPath(startPos, endPos);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            if (currentPath != null && currentPath.Count > 1)
            {
                GridSystem grid = ServiceLocator.Get<GridSystem>();
#if UNITY_EDITOR
                Handles.color = Color.blue;
                Vector3[] points = new Vector3[currentPath.Count];
                for (int i = 0; i < currentPath.Count; i++)
                {
                    points[i] = grid.GetWorldPosition(currentPath[i]) + Vector3.up * 0.1f;
                }
                Handles.DrawAAPolyLine(5.0f, points);
#else
                Gizmos.color = Color.green;
                for (int i = 0; i < currentPath.Count - 1; i++)
                {
                    Vector3 from = grid.GetWorldPosition(currentPath[i]);
                    Vector3 to = grid.GetWorldPosition(currentPath[i + 1]);
                    Gizmos.DrawLine(from, to);
                }
#endif
            }
        }
    }
}
