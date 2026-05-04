using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Data.TacticsRuleset;
using UnityEngine;

namespace TacticsGame.Spells
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
        public Vector3Int TargetPosition { get; set; }
        public SpellTargetingType TargetingType { get; set; }

        // Populated during Targeting phase
        public List<Vector3Int> AffectedCells { get; set; } = new List<Vector3Int>();
        public List<Unit> AffectedUnits { get; set; } = new List<Unit>();
        public List<IDamageable> AffectedDamageables { get; set; } = new List<IDamageable>();

        // Populated during Roll phase

        /// <summary>
        /// Stores the roll result per target unit. Populated by SavingThrowEffectSO
        /// or AttackRollEffectSO. Consumed by DamageEffectSO and ConditionEffectSO.
        /// </summary>
        public Dictionary<Unit, RollResult> RollResults { get; set; } =
            new Dictionary<Unit, RollResult>();

        // Flags
        public bool IsCancelled { get; set; } = false;

        // Interactive Requests (populated during Resolution, processed after)
        public List<ForcedMovementRequest> PendingMovements { get; set; } =
            new List<ForcedMovementRequest>();
    }

    public class ForcedMovementRequest
    {
        public Unit Target;
        public int MaxTiles;
        public bool IsInteractive;
    }
}
