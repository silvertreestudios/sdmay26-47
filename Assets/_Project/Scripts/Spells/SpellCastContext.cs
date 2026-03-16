using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Data.PF2e;
using PathfinderTactics.Grid;

namespace PathfinderTactics.Spells
{
    /// <summary>
    /// Mutable context object that carries all state through the spell effect pipeline.
    /// Built by CastSpellAction, passed to each SpellEffectSO.Apply().
    /// Effects read from and write to this as they execute through phases.
    /// </summary>
    public class SpellCastContext
    {
        // Inputs (set by CastSpellAction before resolution)
        public Unit Caster { get; set; }
        public SpellSO SpellData { get; set; }
        public int CastLevel { get; set; }
        public GridPosition TargetPosition { get; set; }
        public SpellTargetingType TargetingType { get; set; }

        // Populated during Targeting phase
        public List<GridPosition> AffectedCells { get; set; } = new List<GridPosition>();
        public List<Unit> AffectedUnits { get; set; } = new List<Unit>();

        // Populated during Roll phase

        /// <summary>
        /// Stores the roll result per target unit. Populated by SavingThrowEffectSO
        /// or AttackRollEffectSO. Consumed by DamageEffectSO and ConditionEffectSO.
        /// </summary>
        public Dictionary<Unit, RollResult> RollResults { get; set; } =
            new Dictionary<Unit, RollResult>();

        // Flags
        public bool IsCancelled { get; set; } = false;
    }
}
