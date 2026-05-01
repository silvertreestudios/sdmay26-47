using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TacticsGame.Grid
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

        private int lastNodeCount = -1;
        private float lastCellSize = -1f;
        private Transform lastPrefab = null;

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                lastNodeCount = -1;
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
                gridSystem.NodeCount == lastNodeCount
                && Mathf.Approximately(gridSystem.CellSize, lastCellSize)
                && gridTilePrefab == lastPrefab
            )
            {
                return;
            }

            FindOrCreateVisualParent();
            DestroyGridVisuals();
            CreateGridVisuals();

            lastNodeCount = gridSystem.NodeCount;
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
            TerrainBlock[] terrainBlocks = FindObjectsByType<TerrainBlock>(
                FindObjectsSortMode.None
            );

            float planeScale = gridSystem.CellSize / 10f;

            foreach (TerrainBlock block in terrainBlocks)
            {
                if (block == null)
                    continue;

                Vector3Int gridPos = gridSystem.GetLayeredGridPosition(block.transform.position);
                Vector3 worldPos = gridSystem.GetWorldPosition(gridPos);

                Transform gridTile = Instantiate(
                    gridTilePrefab,
                    worldPos,
                    Quaternion.identity,
                    visualParent
                );
                gridTile.localScale = new Vector3(planeScale, 1f, planeScale);
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
