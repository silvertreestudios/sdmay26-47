using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Visualizes the tile the unit is currently occupying on the grid in real-time.
    /// </summary>
    public class UnitTileVisualizer : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        private GameObject tilePrefab;

        [SerializeField]
        private Color highlightColor = new Color(1, 1, 1, 0.4f);

        [SerializeField]
        private Color flankedColor = new Color(0.8f, 0.1f, 0.1f, 0.6f);

        [SerializeField]
        private float yOffset = 0.01f;

        private Unit unit;
        private GridSystem gridSystem;
        private GameObject highlightInstance;
        private GridPosition lastPosition;
        private AuraTile auraTile;

        private void Awake()
        {
            unit = GetComponent<Unit>();
        }

        private void Start()
        {
            gridSystem = ServiceLocator.Get<GridSystem>();
            InitializeHighlight();
        }

        private void Update()
        {
            UpdateHighlightPosition();
            UpdateVisualState();
        }

        private void InitializeHighlight()
        {
            if (tilePrefab == null)
            {
                Debug.LogWarning($"[UnitTileVisualizer] Tile prefab not assigned on {unit.name}");
                return;
            }

            highlightInstance = Instantiate(
                tilePrefab,
                transform.position,
                Quaternion.Euler(90, 0, 0)
            );
            highlightInstance.name = $"{unit.name}_TileHighlight";

            float scale = gridSystem.CellSize;
            highlightInstance.transform.localScale = new Vector3(scale, scale, scale);

            auraTile = highlightInstance.GetComponent<AuraTile>();
            if (auraTile == null)
                auraTile = highlightInstance.AddComponent<AuraTile>();

            auraTile.SetColor(highlightColor);

            lastPosition = gridSystem.GetGridPosition(transform.position);
            UpdatePosition();
        }

        private void UpdateHighlightPosition()
        {
            if (highlightInstance == null)
                return;

            GridPosition currentPos = gridSystem.GetGridPosition(transform.position);
            if (currentPos != lastPosition)
            {
                lastPosition = currentPos;
                UpdatePosition();
            }
        }

        private void UpdateVisualState()
        {
            if (auraTile == null)
                return;

            // Check if this unit is currently being flanked
            bool isFlanked = GridMathHelper.IsAnyFlankingVisual(unit);

            if (isFlanked)
            {
                auraTile.SetColor(flankedColor);
            }
            else
            {
                auraTile.SetColor(highlightColor);
            }
        }

        private void UpdatePosition()
        {
            Vector3 worldPos = gridSystem.GetWorldPosition(lastPosition);
            highlightInstance.transform.position = worldPos + new Vector3(0, yOffset, 0);
        }

        private void OnDisable()
        {
            if (highlightInstance != null)
                highlightInstance.SetActive(false);
        }

        private void OnEnable()
        {
            if (highlightInstance != null)
                highlightInstance.SetActive(true);
        }

        private void OnDestroy()
        {
            if (highlightInstance != null)
                Destroy(highlightInstance);
        }
    }
}
