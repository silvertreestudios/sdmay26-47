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
        private int cursorSize = 1; // 1 = normal (1x1), 2 = burst mode (2x2)
        private Vector3 baseScale;

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
            Vector3 worldPos = ServiceLocator.Get<GridSystem>().GetWorldPosition(gridPos);

            // In 2x2 mode, offset to center on the intersection point
            if (cursorSize > 1)
            {
                float halfCell = ServiceLocator.Get<GridSystem>().CellSize * 0.5f;
                worldPos += new Vector3(halfCell, 0, halfCell);
            }

            transform.position = worldPos;
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

        /// <summary>
        /// Sets the cursor to cover sizeInTiles x sizeInTiles grid cells.
        /// Used for Burst spells where the origin is an intersection point (2x2).
        /// Scales the visual and offsets position to the intersection center.
        /// </summary>
        public void SetCursorSize(int sizeInTiles)
        {
            cursorSize = sizeInTiles;
            transform.localScale = baseScale * sizeInTiles;
            // Re-apply position with new offset
            SetPosition(currentGridPosition);
        }

        /// <summary>
        /// Resets cursor back to normal 1x1 size.
        /// </summary>
        public void ResetCursorSize()
        {
            if (cursorSize != 1)
            {
                cursorSize = 1;
                transform.localScale = baseScale;
            }
        }

        public int GetCursorSize() => cursorSize;
    }
}
