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
        public GridPosition FromPos { get; }
        public GridPosition ToPos { get; }

        public BeforeMoveEvent(Unit source, GridPosition from, GridPosition to)
            : base(source)
        {
            FromPos = from;
            ToPos = to;
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
}
