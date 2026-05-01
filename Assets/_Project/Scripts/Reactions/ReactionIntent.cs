using TacticsGame.Characters;

namespace TacticsGame.Reactions
{
    public class ReactionIntent
    {
        public BaseReaction Reaction { get; }
        public Unit ReactingUnit { get; }
        public GameEvent TriggeringEvent { get; }

        public ReactionIntent(BaseReaction reaction, Unit reactingUnit, GameEvent triggeringEvent)
        {
            Reaction = reaction;
            ReactingUnit = reactingUnit;
            TriggeringEvent = triggeringEvent;
        }
    }
}
