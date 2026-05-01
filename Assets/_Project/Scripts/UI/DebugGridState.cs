using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.UI
{
    public class DebugGridState : MonoBehaviour
    {
        public bool trigger = false;
        public Transform testTransform;

        void Update()
        {
            if (trigger)
            {
                trigger = false;
                var grid = ServiceLocator.Get<GridSystem>();
                var pos = grid.GetLayeredGridPosition(testTransform.position);
                GridNode node = grid.GetNode(pos);
                Debug.Log(
                    $"Transform {testTransform.name} (World {testTransform.position}) -> Grid Pos: {pos}"
                );
                if (node != null)
                {
                    string info =
                        node.Terrain != null
                            ? $"CoverType: {node.Terrain.CoverType}, Walkable: {node.Terrain.IsWalkable}, BlocksLoE: {node.Terrain.BlocksLineOfEffect}"
                            : "No TerrainDef";
                    Debug.Log($"Node at {pos} Terrain Info: {info}");
                }
                else
                {
                    Debug.Log($"Node at {pos} is NULL");
                }
            }
        }
    }
}
