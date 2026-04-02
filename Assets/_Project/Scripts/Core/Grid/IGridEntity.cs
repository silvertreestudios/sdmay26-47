using UnityEngine;

namespace PathfinderTactics.Grid
{
    /// <summary>
    /// Common grid contract for units, doors, hazards, and interactables.
    /// </summary>
    public interface IGridEntity
    {
        Vector3Int CurrentPosition { get; }
        bool BlocksMovement { get; }
        CoverType CoverType { get; }
    }
}
