using UnityEngine;

namespace TacticsGame.Grid
{
    /// <summary>
    /// Component used by GridBaker to create layered nodes from scene objects.
    /// </summary>
    [SelectionBase]
    public class TerrainBlock : MonoBehaviour
    {
        public TerrainDef Terrain;
    }
}
