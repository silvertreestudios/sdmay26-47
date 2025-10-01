using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PathfinderTactics.Grid
{
    [ExecuteAlways]
    public class GridVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Transform gridTilePrefab;

        [SerializeField]
        private GridSystem gridSystem;

        private Transform visualParent;
        private const string VISUAL_PARENT_NAME = "GridVisuals";

        private int lastWidth = -1;
        private int lastHeight = -1;
        private float lastCellSize = -1f;
        private Transform lastPrefab = null;

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                lastWidth = -1;
                UpdateGridVisuals();
            }
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;
                if (Application.isPlaying)
                    return;
                UpdateGridVisuals();
            };
        }
#endif

        private void UpdateGridVisuals()
        {
            if (gridSystem == null || gridTilePrefab == null)
            {
                DestroyGridVisuals();
                return;
            }

            if (
                gridSystem.Width == lastWidth
                && gridSystem.Height == lastHeight
                && Mathf.Approximately(gridSystem.CellSize, lastCellSize)
                && gridTilePrefab == lastPrefab
            )
            {
                return;
            }

            FindOrCreateVisualParent();
            DestroyGridVisuals();
            CreateGridVisuals();

            lastWidth = gridSystem.Width;
            lastHeight = gridSystem.Height;
            lastCellSize = gridSystem.CellSize;
            lastPrefab = gridTilePrefab;
        }

        private void FindOrCreateVisualParent()
        {
            visualParent = transform.Find(VISUAL_PARENT_NAME);
            if (visualParent == null)
            {
                visualParent = new GameObject(VISUAL_PARENT_NAME).transform;
                visualParent.SetParent(this.transform);
                visualParent.localPosition = Vector3.zero;
            }
        }

        private void CreateGridVisuals()
        {
            for (int x = 0; x < gridSystem.Width; x++)
            {
                for (int z = 0; z < gridSystem.Height; z++)
                {
                    GridPosition gridPosition = new GridPosition(x, z);
                    Vector3 worldPosition = gridSystem.GetWorldPosition(gridPosition);

                    Transform gridTile = Instantiate(
                        gridTilePrefab,
                        worldPosition,
                        Quaternion.identity,
                        visualParent
                    );

                    float planeScale = gridSystem.CellSize / 10f;
                    gridTile.localScale = new Vector3(planeScale, 1f, planeScale);
                }
            }
        }

        private void DestroyGridVisuals()
        {
            FindOrCreateVisualParent();

            for (int i = visualParent.childCount - 1; i >= 0; i--)
            {
                Transform child = visualParent.GetChild(i);

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
#if UNITY_EDITOR
                    DestroyImmediate(child.gameObject);
#endif
                }
            }
        }
    }
}
