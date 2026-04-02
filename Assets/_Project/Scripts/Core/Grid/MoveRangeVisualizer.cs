using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Characters;
using PathfinderTactics.Combat;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Grid
{
    public class MoveRangeVisualizer : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField]
        private GameObject moveTilePrefab; // Blue

        [SerializeField]
        private GameObject attackRangeTilePrefab; // Soft Red (Bounds)

        [SerializeField]
        private GameObject attackTargetTilePrefab; // Bright Red (Valid Targets)

        private List<GameObject> activeVisuals = new List<GameObject>();
        private Transform visualParent;

        private void Start()
        {
            ServiceLocator.Get<UnitActionSystem>().OnSelectedUnitChanged +=
                UnitActionSystem_OnStateChanged;

            if (ServiceLocator.TryGet<PhaseManager>(out var phaseManager))
            {
                phaseManager.OnPhaseChanged += PhaseManager_OnPhaseChanged;
            }

            visualParent = new GameObject("ActionRangeVisuals").transform;

            // Initial update in case Start() order caused us to miss the first selection
            UpdateVisuals();
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<UnitActionSystem>(out var unitActionSystem))
            {
                unitActionSystem.OnSelectedUnitChanged -= UnitActionSystem_OnStateChanged;
            }
            if (ServiceLocator.TryGet<PhaseManager>(out var phaseManager))
            {
                phaseManager.OnPhaseChanged -= PhaseManager_OnPhaseChanged;
            }
        }

        private void PhaseManager_OnPhaseChanged(object sender, GamePhase newPhase)
        {
            UpdateVisuals();
        }

        private void UnitActionSystem_OnStateChanged(object sender, System.EventArgs e)
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            ClearVisuals();

            Unit selectedUnit = ServiceLocator.Get<UnitActionSystem>().SelectedUnit;
            if (selectedUnit == null)
                return;

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;

            // If moving, Show Blue Tiles
            if (currentPhase == GamePhase.FreeMovement || currentPhase == GamePhase.ActionSelection)
            {
                ShowMoveRange(selectedUnit);
            }
            // If targeting, Show Red Tiles
            else if (currentPhase == GamePhase.ActionTargeting)
            {
                ShowActionRange();
            }
        }

        private void ShowMoveRange(Unit unit)
        {
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas) && uas != null)
            {
                List<Vector3Int> positions = uas.GetValidMovePositions();
                if (positions != null)
                {
                    SpawnLayeredTiles(positions, moveTilePrefab);
                    return;
                }
            }

            int maxMoveCost = unit.GetMaxMoveCost();
            List<Vector3Int> reachable = Pathfinding.GetReachablePositions(
                unit.CurrentLayeredPosition,
                maxMoveCost
            );
            SpawnLayeredTiles(reachable, moveTilePrefab);
        }

        private void ShowActionRange()
        {
            BaseAction selectedAction = ServiceLocator.Get<UnitActionSystem>().GetSelectedAction();
            if (selectedAction == null)
                return;

            List<GridPosition> rangeBounds = new List<GridPosition>(
                selectedAction.GetActionRangeGridPositions()
            );
            List<GridPosition> validTargets = new List<GridPosition>(
                selectedAction.GetValidActionGridPositions()
            );

            // Prevent Z-fighting by removing valid targets from the general range list
            foreach (var target in validTargets)
            {
                rangeBounds.Remove(target);
            }

            // Spawn soft red for empty reachable tiles
            SpawnTiles(
                rangeBounds,
                attackRangeTilePrefab,
                ServiceLocator.Get<UnitActionSystem>().SelectedUnit
            );

            // Spawn bright red for tiles containing enemies
            SpawnTiles(
                validTargets,
                attackTargetTilePrefab,
                ServiceLocator.Get<UnitActionSystem>().SelectedUnit
            );
        }

        private void SpawnLayeredTiles(List<Vector3Int> positions, GameObject prefab)
        {
            if (prefab == null)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            float tileScale = grid.CellSize;

            foreach (Vector3Int pos in positions)
            {
                Vector3 worldPos = grid.GetWorldPosition(pos);
                Vector3 visualPos = worldPos + new Vector3(0, 0.02f, 0);

                GameObject tile = Instantiate(
                    prefab,
                    visualPos,
                    Quaternion.Euler(90, 0, 0),
                    visualParent
                );
                DisableColliders(tile);
                tile.transform.localScale = new Vector3(tileScale, tileScale, tileScale);
                activeVisuals.Add(tile);
            }
        }

        private void SpawnTiles(List<GridPosition> positions, GameObject prefab, Unit referenceUnit)
        {
            if (prefab == null)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            float tileScale = grid.CellSize;

            foreach (GridPosition pos in positions)
            {
                Vector3 worldPos = grid.GetWorldPosition(pos);
                Vector3 visualPos = worldPos + new Vector3(0, 0.02f, 0);

                GameObject tile = Instantiate(
                    prefab,
                    visualPos,
                    Quaternion.Euler(90, 0, 0),
                    visualParent
                );
                DisableColliders(tile);
                tile.transform.localScale = new Vector3(tileScale, tileScale, tileScale);
                activeVisuals.Add(tile);
            }
        }

        private void ClearVisuals()
        {
            foreach (GameObject visual in activeVisuals)
            {
                Destroy(visual);
            }
            activeVisuals.Clear();
        }

        private static void DisableColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }
    }
}
