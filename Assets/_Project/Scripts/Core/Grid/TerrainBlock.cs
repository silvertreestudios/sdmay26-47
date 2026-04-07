using UnityEngine;

namespace PathfinderTactics.Grid
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
