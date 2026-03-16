using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Data.PF2e;
using PathfinderTactics.Grid;

namespace PathfinderTactics.Reactions
{
    // TODO: add step movement
    public abstract class GameEvent
    {
        public Unit SourceUnit { get; }
        public bool IsCancelled { get; set; } = false;

        protected GameEvent(Unit sourceUnit)
        {
            SourceUnit = sourceUnit;
        }
    }

    // Fired right before leaving a square. (Reactive Strike happens here)
    public class BeforeMoveEvent : GameEvent
    {
        public Unit MovingUnit { get; }
        public GridPosition StartPos { get; }
        public GridPosition TargetPos { get; }
        public bool IsStep { get; }

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
        public GridPosition TargetPosition { get; }

        public BeforeSpellEvent(Unit caster, SpellSO spell, GridPosition targetPos)
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
}
