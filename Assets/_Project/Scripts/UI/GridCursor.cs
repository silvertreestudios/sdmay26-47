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

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Hide(); // Hide by default
        }

        public void Show(GridPosition startPos)
        {
            gameObject.SetActive(true);
            SetPosition(startPos);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetPosition(GridPosition gridPos)
        {
            currentGridPosition = gridPos;
            transform.position = GridSystem.Instance.GetWorldPosition(gridPos);

            // TODO: Add animations or particle effects here
        }

        public void Move(int dx, int dz)
        {
            GridPosition newPos = new GridPosition(
                currentGridPosition.x + dx,
                currentGridPosition.z + dz
            );

            if (GridSystem.Instance.IsValidGridPosition(newPos))
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
    }
}
