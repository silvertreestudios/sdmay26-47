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

        /// <summary>
        /// Called by TargetingService whenever the cursor moves during spell targeting.
        /// Computes the AoE footprint at the cursor position and spawns highlight tiles.
        /// </summary>
        public void UpdateAoEPreview(GridPosition cursorPos, CastSpellAction spellAction)
        {
            if (spellAction == null || spellAction.GetCurrentSpell() == null)
            {
                ClearTiles();
                return;
            }

            SpellSO spell = spellAction.GetCurrentSpell();

            // Only show AoE preview for spells with an actual area shape
            if (spell.Area.Shape == AreaShape.None)
            {
                ClearTiles();
                return;
            }

            // Skip rebuild if cursor hasn't moved
            if (isActive && cursorPos.x == lastCursorPos.x && cursorPos.z == lastCursorPos.z)
                return;

            lastCursorPos = cursorPos;
            isActive = true;

            ClearTiles();

            // Get the caster's position for emanation/cone/line origin
            Unit casterUnit = ServiceLocator.Get<UnitActionSystem>().SelectedUnit;
            GridPosition casterPos =
                casterUnit != null ? casterUnit.CurrentGridPosition : cursorPos;

            // Compute affected cells using the existing AreaService
            List<GridPosition> affectedCells = AreaService.GetAffectedCells(
                cursorPos,
                spell.Area,
                casterPos,
                cursorPos
            );

            // Filter to valid grid positions
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            affectedCells.RemoveAll(c => !gridSystem.IsValidGridPosition(c));

            // Spawn tiles
            SpawnAoETiles(affectedCells, gridSystem);
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

            List<GridPosition> affectedCells = AreaService.GetAffectedCells(
                cursorPos,
                spell.Area,
                casterPos,
                cursorPos
            );

            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            affectedCells.RemoveAll(c => !gridSystem.IsValidGridPosition(c));

            SpawnAoETiles(affectedCells, gridSystem);
        }

        /// <summary>
        /// Hides all AoE preview tiles.
        /// </summary>
        public void HidePreview()
        {
            ClearTiles();
            isActive = false;
        }

        private void SpawnAoETiles(List<GridPosition> cells, GridSystem gridSystem)
        {
            if (aoeTilePrefab == null)
            {
                Debug.LogWarning("[SpellAoEVisualizer] No AoE tile prefab assigned!");
                return;
            }

            float tileScale = gridSystem.CellSize;

            foreach (var cell in cells)
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
