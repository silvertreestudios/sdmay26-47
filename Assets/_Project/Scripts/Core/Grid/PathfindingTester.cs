using System.Collections.Generic;
using UnityEngine;

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

            if (currentPath != null)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < currentPath.Count - 1; i++)
                {
                    Vector3 from = GridSystem.Instance.GetWorldPosition(currentPath[i]);
                    Vector3 to = GridSystem.Instance.GetWorldPosition(currentPath[i + 1]);
                    Gizmos.DrawLine(from, to);
                }
            }
        }
    }
}
