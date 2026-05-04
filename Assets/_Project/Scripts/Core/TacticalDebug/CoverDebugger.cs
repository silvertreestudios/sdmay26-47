using System.Collections.Generic;
using TacticsGame.Grid;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TacticsGame.Core.TacticalDebug
{
    public class CoverDebugger : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        private KeyCode toggleKey = KeyCode.F4;

        [SerializeField]
        private bool isVisible = false;

        [SerializeField]
        private float cubeScale = 0.8f;

        [Header("Colors")]
        [SerializeField]
        private Color standardColor = new Color(1f, 1f, 0f, 0.4f); // Yellow

        [SerializeField]
        private Color greaterColor = new Color(1f, 0.5f, 0f, 0.5f); // Orange

        [SerializeField]
        private Color totalColor = new Color(1f, 0f, 0f, 0.6f); // Red

        private void Start()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<CoverDebugger>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                isVisible = !isVisible;
                Debug.Log(
                    $"<color=cyan>[DEBUG]</color> Cover Visualization: {(isVisible ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>")}"
                );
            }
        }

        private void OnDrawGizmos()
        {
            if (!isVisible)
                return;

            GridSystem grid = ServiceLocator.TryGet<GridSystem>(out var system) ? system : null;
            if (grid == null)
                return;

            float cellSize = grid.CellSize;
            Vector3 size = Vector3.one * cellSize * cubeScale;

            foreach (GridNode node in grid.AllNodes)
            {
                CoverType cover = node.GetCoverType();
                if (cover == CoverType.None)
                    continue;

                Color color = Color.clear;
                string label = "";

                switch (cover)
                {
                    case CoverType.Standard:
                        color = standardColor;
                        label = "Standard";
                        break;
                    case CoverType.Greater:
                        color = greaterColor;
                        label = "Greater";
                        break;
                    case CoverType.Total:
                        color = totalColor;
                        label = "Total";
                        break;
                }

                if (color != Color.clear)
                {
                    Vector3 worldPos = grid.GetWorldPosition(node.Coordinates);

                    // Shift up slightly to be in the middle of the cell volume
                    worldPos.y += grid.VerticalCellSize * 0.5f;

                    Gizmos.color = color;
                    Gizmos.DrawCube(worldPos, size);

                    Gizmos.color = new Color(color.r, color.g, color.b, 1f);
                    Gizmos.DrawWireCube(worldPos, size);

#if UNITY_EDITOR
                    if (
                        Selection.activeGameObject == gameObject
                        || Vector3.Distance(
                            SceneView.lastActiveSceneView.camera.transform.position,
                            worldPos
                        ) < 15f
                    )
                    {
                        GUIStyle style = new GUIStyle();
                        style.normal.textColor = Color.white;
                        style.alignment = TextAnchor.MiddleCenter;
                        style.fontSize = 10;
                        Handles.Label(worldPos, label, style);
                    }
#endif
                }
            }
        }
    }
}
