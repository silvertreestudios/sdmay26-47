using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Grid
{
    public class MoveRangeVisualizer : MonoBehaviour
    {
        [SerializeField]
        private GameObject moveTilePrefab;

        private List<GameObject> activeVisuals = new List<GameObject>();
        private Transform visualParent;

        private void Start()
        {
            UnitActionSystem.Instance.OnSelectedUnitChanged +=
                UnitActionSystem_OnSelectedUnitChanged;
            visualParent = new GameObject("MoveRangeVisuals").transform;
        }

        private void OnDestroy()
        {
            UnitActionSystem.Instance.OnSelectedUnitChanged -=
                UnitActionSystem_OnSelectedUnitChanged;
        }

        private void UnitActionSystem_OnSelectedUnitChanged(object sender, System.EventArgs e)
        {
            ClearVisuals();

            Unit selectedUnit = UnitActionSystem.Instance.SelectedUnit;
            if (selectedUnit != null)
            {
                ShowVisualsForUnit(selectedUnit);
            }
        }

        private void ShowVisualsForUnit(Unit unit)
        {
            GridPosition startPos = unit.CurrentGridPosition;
            int maxMoveCost = unit.GetMaxMoveCost();
            List<GridPosition> reachablePositions = Pathfinding.GetReachableGridPositions(
                startPos,
                maxMoveCost
            );

            float cellSize = GridSystem.Instance.CellSize;
            float tileScale = cellSize; // Scale it to cell size.

            foreach (GridPosition pos in reachablePositions)
            {
                Vector3 worldPos = GridSystem.Instance.GetWorldPosition(pos);
                Vector3 visualPos = worldPos + new Vector3(0, 0.01f, 0);
                GameObject tile = Instantiate(
                    moveTilePrefab,
                    visualPos,
                    Quaternion.Euler(90, 0, 0),
                    visualParent
                );
                tile.transform.localScale = new Vector3(tileScale, tileScale, tileScale);
                tile.transform.SetParent(visualParent);
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
    }
}
