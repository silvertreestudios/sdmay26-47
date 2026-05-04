namespace TacticsGame.Grid
{
    public enum CoverType
    {
        None,
        Standard,
        Greater,
        Total,
    }

    public enum MovementTag
    {
        Normal,
        Climb,
        Jump,
        Fly,
    }

    [System.Serializable]
    public class TerrainDef
    {
        public int MovementCost = 1;
        public bool IsWalkable = true;
        public CoverType CoverType = CoverType.None;
        public bool GrantsConcealment;
        public bool BlocksImpreciseSenses;
        public bool RequiresBalanceCheck;
        public bool IsSolidWall;

        /// <summary>
        /// When true, Line of Effect cannot pass through this tile's volume for
        /// same-column vertical shots (e.g. solid bridge deck / floor slab).
        /// Independent of <see cref="CoverType"/> so walkable floors can still block
        /// vertical LoE without counting as total cover for horizontal combat.
        /// </summary>
        public bool BlocksLineOfEffect;

        /// <summary>
        /// When true, a walkable surface does <b>not</b> block same-column vertical
        /// Line of Effect (open grating, ladder hole, etc.). Default false so
        /// bridge decks block shots to/from lower floors without extra setup.
        /// </summary>
        public bool AllowVerticalLineOfEffect;

        public TerrainDef Clone()
        {
            return new TerrainDef
            {
                MovementCost = this.MovementCost,
                IsWalkable = this.IsWalkable,
                CoverType = this.CoverType,
                GrantsConcealment = this.GrantsConcealment,
                BlocksImpreciseSenses = this.BlocksImpreciseSenses,
                RequiresBalanceCheck = this.RequiresBalanceCheck,
                IsSolidWall = this.IsSolidWall,
                BlocksLineOfEffect = this.BlocksLineOfEffect,
                AllowVerticalLineOfEffect = this.AllowVerticalLineOfEffect,
            };
        }
    }
}
