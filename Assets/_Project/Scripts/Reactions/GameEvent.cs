using PathfinderTactics.Characters;
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

        public BeforeDamageEvent(Unit source, Unit target, int damage, bool isCrit)
            : base(source)
        {
            TargetUnit = target;
            DamageAmount = damage;
            IsCriticalHit = isCrit;
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
}
