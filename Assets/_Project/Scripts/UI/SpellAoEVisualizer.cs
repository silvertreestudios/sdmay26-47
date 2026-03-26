using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Data.PF2e;
using PathfinderTactics.Grid;
using PathfinderTactics.Spells;
using PathfinderTactics.Spells.Services;
using UnityEngine;

namespace PathfinderTactics.UI
{
    /// <summary>
    /// Visualizes the AoE footprint of a spell as the player moves the targeting cursor.
    /// Spawns highlight tile prefabs for every cell in the affected area.
    /// Updates every time the cursor moves to a new grid position.
    ///
    /// Supports PF2e area types: Burst, Cone, Line, Emanation.
    /// For single-target spells, the normal GridCursor handles visualization.
    /// </summary>
    public class SpellAoEVisualizer : MonoBehaviour
    {
        [Header("AoE Tile Prefab")]
        [Tooltip("Tile prefab used to highlight AoE-affected cells. Should be a distinct color.")]
        [SerializeField]
        private GameObject aoeTilePrefab;

        [Header("Settings")]
        [Tooltip("Y offset to prevent z-fighting with other tile overlays.")]
        [SerializeField]
        private float yOffset = 0.03f;

        private List<GameObject> activeTiles = new List<GameObject>();
        private Transform tileParent;
        private GridPosition lastCursorPos;
        private bool isActive = false;

        private void Awake()
        {
            ServiceLocator.Register(this);
            tileParent = new GameObject("AoEVisuals").transform;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<SpellAoEVisualizer>();
            if (tileParent != null)
                Destroy(tileParent.gameObject);
        }

        [Header("Off-Layer Fading")]
        [Tooltip("Material used for AoE tiles on layers other than the cursor's current Y.")]
        [SerializeField]
        private Material fadedAoEMaterial;

        [Tooltip("Alpha multiplier for faded off-layer tiles.")]
        [SerializeField]
        private float fadedAlpha = 0.35f;

        private int currentCursorY;

        /// <summary>
        /// Called by TargetingService whenever the cursor moves during spell targeting.
        /// Computes the 3D AoE footprint at the cursor position and spawns highlight tiles.
        /// </summary>
        public void UpdateAoEPreview(GridPosition cursorPos, CastSpellAction spellAction)
        {
            if (spellAction == null || spellAction.GetCurrentSpell() == null)
            {
                ClearTiles();
                return;
            }

            SpellSO spell = spellAction.GetCurrentSpell();

            if (spell.Area.Shape == AreaShape.None)
            {
                ClearTiles();
                return;
            }

            if (isActive && cursorPos.x == lastCursorPos.x && cursorPos.z == lastCursorPos.z)
                return;

            lastCursorPos = cursorPos;
            isActive = true;

            ClearTiles();

            Unit casterUnit = ServiceLocator.Get<UnitActionSystem>().SelectedUnit;
            Vector3Int casterPos3D =
                casterUnit != null
                    ? casterUnit.CurrentLayeredPosition
                    : new Vector3Int(cursorPos.x, 0, cursorPos.z);

            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            int cursorY = ResolveCursorY(gridSystem, cursorPos);
            currentCursorY = cursorY;

            Vector3Int origin3D = new Vector3Int(cursorPos.x, cursorY, cursorPos.z);

            List<Vector3Int> affectedCells = AreaService.GetAffectedCells3D(
                origin3D,
                spell.Area,
                casterPos3D,
                origin3D
            );

            SpawnAoETiles3D(affectedCells, gridSystem);
        }

        /// <summary>
        /// Shows a static AoE preview (used for initial targeting setup).
        /// </summary>
        public void ShowPreview(GridPosition cursorPos, SpellSO spell, GridPosition casterPos)
        {
            if (spell == null || spell.Area.Shape == AreaShape.None)
            {
                ClearTiles();
                return;
            }

            lastCursorPos = cursorPos;
            isActive = true;

            ClearTiles();

            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();

            int cursorY = ResolveCursorY(gridSystem, cursorPos);
            int casterY = ResolveCursorY(gridSystem, casterPos);
            currentCursorY = cursorY;

            Vector3Int origin3D = new Vector3Int(cursorPos.x, cursorY, cursorPos.z);
            Vector3Int caster3D = new Vector3Int(casterPos.x, casterY, casterPos.z);

            List<Vector3Int> affectedCells = AreaService.GetAffectedCells3D(
                origin3D,
                spell.Area,
                caster3D,
                origin3D
            );

            SpawnAoETiles3D(affectedCells, gridSystem);
        }

        /// <summary>
        /// Hides all AoE preview tiles.
        /// </summary>
        public void HidePreview()
        {
            ClearTiles();
            isActive = false;
        }

        private int ResolveCursorY(GridSystem grid, GridPosition pos)
        {
            List<GridNode> column = grid.GetColumn(new Vector2Int(pos.x, pos.z));
            if (column != null && column.Count > 0)
                return column[0].Coordinates.y;
            return 0;
        }

        private void SpawnAoETiles3D(List<Vector3Int> cells, GridSystem gridSystem)
        {
            if (aoeTilePrefab == null)
            {
                Debug.LogWarning("[SpellAoEVisualizer] No AoE tile prefab assigned!");
                return;
            }

            float tileScale = gridSystem.CellSize;

            foreach (Vector3Int cell in cells)
            {
                Vector3 worldPos = gridSystem.GetWorldPosition(cell);
                Vector3 visualPos = worldPos + new Vector3(0, yOffset, 0);

                GameObject tile = Instantiate(
                    aoeTilePrefab,
                    visualPos,
                    Quaternion.Euler(90, 0, 0),
                    tileParent
                );
                tile.transform.localScale = new Vector3(tileScale, tileScale, tileScale);

                if (cell.y != currentCursorY && fadedAoEMaterial != null)
                {
                    Renderer rend = tile.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        Material fadedInstance = new Material(fadedAoEMaterial);
                        Color c = fadedInstance.color;
                        c.a = fadedAlpha;
                        fadedInstance.color = c;
                        rend.material = fadedInstance;
                    }
                }

                activeTiles.Add(tile);
            }
        }

        private void ClearTiles()
        {
            foreach (var tile in activeTiles)
            {
                Destroy(tile);
            }
            activeTiles.Clear();
        }
    }
}
