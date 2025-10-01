using System;

namespace PathfinderTactics.Grid
{
    /// <summary>
    /// Represents a coordinate on the grid in (x, z) space.
    /// </summary>
    [System.Serializable]
    public struct GridPosition : IEquatable<GridPosition>
    {
        public int x;
        public int z;

        public GridPosition(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        // Standard overrides for equality, hashing, and string representation.
        // These are crucial for using GridPosition as a key in dictionaries or sets.
        public override bool Equals(object obj)
        {
            return obj is GridPosition position && Equals(position);
        }

        public bool Equals(GridPosition other)
        {
            return x == other.x && z == other.z;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, z);
        }

        public override string ToString()
        {
            return $"({x}, {z})";
        }

        public static bool operator ==(GridPosition a, GridPosition b)
        {
            return a.x == b.x && a.z == b.z;
        }

        public static bool operator !=(GridPosition a, GridPosition b)
        {
            return !(a == b);
        }
    }
}
