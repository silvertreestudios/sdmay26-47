using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.DebugTools
{
    public class UnitPositionPrinter : MonoBehaviour
    {
        [ContextMenu("Print Unit Positions")]
        public void PrintUnits()
        {
            var grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
            {
                Debug.LogWarning("[UnitPositionPrinter] GridSystem not found.");
                return;
            }

            Debug.Log(
                "<color=cyan>[UNIT MAP REPORT]</color> Listing all units in playback system:"
            );
            foreach (var unit in UnitManager.AllUnits)
            {
                if (unit == null)
                    continue;

                Vector3 world = unit.transform.position;
                Vector3Int gridPos = unit.CurrentLayeredPosition;
                Vector3 gridToWorld = grid.GetWorldPosition(gridPos);

                Debug.Log(
                    $"<b>{unit.name}</b>\n"
                        + $"  - WorldPos: {world}\n"
                        + $"  - GridCoords: {gridPos}\n"
                        + $"  - GridToWorld: {gridToWorld}\n"
                        + $"  - Occupancy Check: {(grid.GetUnitAt(gridPos) == unit ? "<color=green>MATCH</color>" : "<color=red>MISMATCH</color>")}"
                );
            }
        }

        void Update()
        {
            // Press L to trigger report
            if (Input.GetKeyDown(KeyCode.L))
            {
                PrintUnits();
            }
        }
    }
}
