using System.Collections.Generic;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.UI
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
                Destroy(this);
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
            bool posChanged = (
                gridPos.x != currentGridPosition.x || gridPos.z != currentGridPosition.z
            );
            currentGridPosition = gridPos;

            // Refresh the list of layers at this X,Z
            RefreshAvailableLayers(forceRebuild: posChanged);

            UpdateVisualPosition();
        }

        /// <summary>
        /// Cycle through valid Y layers at the current (x,z). direction: +1 = up, -1 = down.
        /// Returns true if the layer changed.
        /// </summary>
        public bool CycleLayer(int direction)
        {
            RefreshAvailableLayers(forceRebuild: true);

            if (availableLayers.Count <= 1)
            {
                return false;
            }

            int oldIndex = layerIndex;
            int newIndex = layerIndex + direction;

            // Wrap around
            if (newIndex < 0)
                newIndex = availableLayers.Count - 1;
            else if (newIndex >= availableLayers.Count)
                newIndex = 0;

            if (newIndex == oldIndex)
            {
                return false;
            }

            layerIndex = newIndex;
            currentLayer = availableLayers[layerIndex];

            UpdateVisualPosition();
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

        private void UpdateVisualPosition()
        {
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
                return;

            Vector3 worldPos = grid.GetWorldPosition(
                new Vector3Int(currentGridPosition.x, currentLayer, currentGridPosition.z)
            );

            if (cursorSize > 1)
            {
                float halfCell = grid.CellSize * 0.5f;
                worldPos += new Vector3(halfCell, 0, halfCell);
            }

            transform.position = worldPos;
        }

        private void RefreshAvailableLayers(bool forceRebuild)
        {
            int oldY = currentLayer;
            if (!forceRebuild && availableLayers.Count > 0)
            {
                // If we're on the same tile and already have layers,
                // just make sure our index matches our current layer.
                if (availableLayers.Contains(currentLayer))
                {
                    layerIndex = availableLayers.IndexOf(currentLayer);
                    return;
                }
            }

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

            HashSet<int> uniqueLayers = new HashSet<int>();
            foreach (GridNode node in column)
            {
                // Skip the interior of solid walls when aiming
                if (node.IsSolidWall())
                    continue;
                uniqueLayers.Add(node.Coordinates.y);
            }

            foreach (int y in uniqueLayers)
                availableLayers.Add(y);

            if (availableLayers.Count == 0)
            {
                // If the whole column is solid, fallback to the top-most solid layer
                // (or first node) just so the cursor has somewhere to be.
                availableLayers.Add(column[column.Count - 1].Coordinates.y);
            }

            availableLayers.Sort();

            // Try to find the best match for our current height
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
