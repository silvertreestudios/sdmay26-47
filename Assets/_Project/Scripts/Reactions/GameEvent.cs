using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Data.TacticsRuleset;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.Reactions
{
    public abstract class GameEvent
    {
        public Unit SourceUnit { get; }
        public bool IsCancelled { get; set; } = false;

        protected GameEvent(Unit sourceUnit)
        {
            SourceUnit = sourceUnit;
        }
    }

    public class BeforeMoveEvent : GameEvent
    {
        public Unit MovingUnit { get; }
        public GridPosition StartPos { get; }
        public GridPosition TargetPos { get; }
        public bool IsStep { get; }

        /// <summary>
        /// Full 3D layered position at move start. Used for accurate 3D range checks.
        /// </summary>
        public Vector3Int StartLayeredPos { get; }

        /// <summary>
        /// Full 3D layered position at move destination.
        /// </summary>
        public Vector3Int TargetLayeredPos { get; }

        public BeforeMoveEvent(
            Unit movingUnit,
            GridPosition startPos,
            GridPosition targetPos,
            bool isStep = false
        )
            : base(movingUnit)
        {
            MovingUnit = movingUnit;
            StartPos = startPos;
            TargetPos = targetPos;
            IsStep = isStep;
            StartLayeredPos = movingUnit.CurrentLayeredPosition;
            TargetLayeredPos = new Vector3Int(
                targetPos.x,
                movingUnit.CurrentLayeredPosition.y,
                targetPos.z
            );
        }

        public BeforeMoveEvent(
            Unit movingUnit,
            GridPosition startPos,
            GridPosition targetPos,
            Vector3Int startLayered,
            Vector3Int targetLayered,
            bool isStep = false
        )
            : base(movingUnit)
        {
            MovingUnit = movingUnit;
            StartPos = startPos;
            TargetPos = targetPos;
            IsStep = isStep;
            StartLayeredPos = startLayered;
            TargetLayeredPos = targetLayered;
        }
    }

    // Fired after arriving in a new square.
    public class AfterMoveEvent : GameEvent
    {
        public GridPosition CurrentPos { get; }

        public AfterMoveEvent(Unit source, GridPosition currentPos)
            : base(source)
        {
            CurrentPos = currentPos;
        }
    }

    public class BeforeDamageEvent : GameEvent
    {
        public Unit TargetUnit { get; }

        // This is mutable. Reactions can reduce or increase this before it resolves.
        public int DamageAmount { get; set; }

        public bool IsCriticalHit { get; }

        /// <summary>
        /// The type of damage being dealt. Used for resistance/weakness calculations.
        /// </summary>
        public DamageType DamageElement { get; }

        public BeforeDamageEvent(
            Unit source,
            Unit target,
            int damage,
            bool isCrit,
            DamageType element = DamageType.Untyped
        )
            : base(source)
        {
            TargetUnit = target;
            DamageAmount = damage;
            IsCriticalHit = isCrit;
            DamageElement = element;
        }
    }

    public class AfterDamageEvent : GameEvent
    {
        public Unit TargetUnit { get; }
        public int FinalDamageTaken { get; }

        public AfterDamageEvent(Unit source, Unit target, int finalDamage)
            : base(source)
        {
            TargetUnit = target;
            FinalDamageTaken = finalDamage;
        }
    }

    // Spell Events

    /// <summary>
    /// Fired before a spell resolves. Enables Counterspell and other reactions.
    /// Setting IsCancelled = true causes the spell to fizzle.
    /// </summary>
    public class BeforeSpellEvent : GameEvent
    {
        public SpellSO Spell { get; }
        public Vector3Int TargetPosition { get; }

        public BeforeSpellEvent(Unit caster, SpellSO spell, Vector3Int targetPos)
            : base(caster)
        {
            Spell = spell;
            TargetPosition = targetPos;
        }
    }

    /// <summary>
    /// Fired after a spell has fully resolved. For triggered effects and logging.
    /// </summary>
    public class AfterSpellEvent : GameEvent
    {
        public SpellSO Spell { get; }
        public List<Unit> AffectedUnits { get; }

        public AfterSpellEvent(Unit caster, SpellSO spell, List<Unit> affectedUnits)
            : base(caster)
        {
            Spell = spell;
            AffectedUnits = affectedUnits;
        }
    }

    /// <summary>
    /// Fired when a unit performs a manipulate action (Interacting, Casting with somatic components, etc.)
    /// </summary>
    public class ManipulateEvent : GameEvent
    {
        public string ActionName { get; }

        public ManipulateEvent(Unit source, string actionName)
            : base(source)
        {
            ActionName = actionName;
        }
    }

    /// <summary>
    /// Fired when a unit makes a ranged attack.
    /// </summary>
    public class RangedAttackEvent : GameEvent
    {
        public Unit Target { get; }

        public RangedAttackEvent(Unit source, Unit target)
            : base(source)
        {
            Target = target;
        }
    }

    /// <summary>
    /// Fired when a unit uses a move action that doesn't necessarily involve tile-to-tile movement (e.g., Stand Up).
    /// </summary>
    public class MoveActionEvent : GameEvent
    {
        public string ActionName { get; }

        public MoveActionEvent(Unit source, string actionName)
            : base(source)
        {
            ActionName = actionName;
        }
    }
}
