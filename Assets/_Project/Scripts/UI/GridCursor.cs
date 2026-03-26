using System.Collections.Generic;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.UI
{
    public class GridCursor : MonoBehaviour
    {
        public static GridCursor Instance { get; private set; }

        [SerializeField]
        private MeshRenderer meshRenderer;

        [SerializeField]
        private Material validMaterial;

        [SerializeField]
        private Material invalidMaterial;

        private GridPosition currentGridPosition;
        private int currentLayer;
        private List<int> availableLayers = new List<int>();
        private int layerIndex;
        private int cursorSize = 1;
        private Vector3 baseScale;

        /// <summary>
        /// The current Y layer the cursor is targeting.
        /// </summary>
        public int CurrentLayer => currentLayer;

        /// <summary>
        /// Full 3D cursor position (x, currentLayer, z).
        /// </summary>
        public Vector3Int CurrentLayeredPosition =>
            new Vector3Int(currentGridPosition.x, currentLayer, currentGridPosition.z);

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            baseScale = transform.localScale;
        }

        public void Show(GridPosition startPos)
        {
            gameObject.SetActive(true);
            SetPosition(startPos);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            ResetCursorSize();
        }

        public void SetPosition(GridPosition gridPos)
        {
            currentGridPosition = gridPos;
            RefreshAvailableLayers();

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3 worldPos = grid.GetWorldPosition(
                new Vector3Int(gridPos.x, currentLayer, gridPos.z)
            );

            if (cursorSize > 1)
            {
                float halfCell = grid.CellSize * 0.5f;
                worldPos += new Vector3(halfCell, 0, halfCell);
            }

            transform.position = worldPos;
        }

        /// <summary>
        /// Cycle through valid Y layers at the current (x,z). direction: +1 = up, -1 = down.
        /// Returns true if the layer changed.
        /// </summary>
        public bool CycleLayer(int direction)
        {
            if (availableLayers.Count <= 1)
                return false;

            int newIndex = layerIndex + direction;
            if (newIndex < 0)
                newIndex = availableLayers.Count - 1;
            else if (newIndex >= availableLayers.Count)
                newIndex = 0;

            if (newIndex == layerIndex)
                return false;

            layerIndex = newIndex;
            currentLayer = availableLayers[layerIndex];
            SetPosition(currentGridPosition);
            return true;
        }

        public void Move(int dx, int dz)
        {
            GridPosition newPos = new GridPosition(
                currentGridPosition.x + dx,
                currentGridPosition.z + dz
            );

            if (ServiceLocator.Get<GridSystem>().IsValidGridPosition(newPos))
            {
                SetPosition(newPos);
            }
        }

        public GridPosition GetGridPosition() => currentGridPosition;

        public void SetValidState(bool isValid)
        {
            if (meshRenderer)
            {
                meshRenderer.material = isValid ? validMaterial : invalidMaterial;
            }
        }

        public void SetCursorSize(int sizeInTiles)
        {
            cursorSize = sizeInTiles;
            transform.localScale = baseScale * sizeInTiles;
            SetPosition(currentGridPosition);
        }

        public void ResetCursorSize()
        {
            if (cursorSize != 1)
            {
                cursorSize = 1;
                transform.localScale = baseScale;
            }
        }

        public int GetCursorSize() => cursorSize;

        private void RefreshAvailableLayers()
        {
            availableLayers.Clear();
            layerIndex = 0;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
            {
                availableLayers.Add(0);
                currentLayer = 0;
                return;
            }

            List<GridNode> column = grid.GetColumn(
                new Vector2Int(currentGridPosition.x, currentGridPosition.z)
            );

            if (column == null || column.Count == 0)
            {
                availableLayers.Add(0);
                currentLayer = 0;
                return;
            }

            foreach (GridNode node in column)
                availableLayers.Add(node.Coordinates.y);

            availableLayers.Sort();

            int bestIdx = 0;
            int bestDist = int.MaxValue;
            for (int i = 0; i < availableLayers.Count; i++)
            {
                int dist = Mathf.Abs(availableLayers[i] - currentLayer);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }
            layerIndex = bestIdx;
            currentLayer = availableLayers[layerIndex];
        }
    }
}
