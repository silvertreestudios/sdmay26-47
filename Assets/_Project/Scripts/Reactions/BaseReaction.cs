using System;
using TacticsGame.Characters;
using UnityEngine;

namespace TacticsGame.Reactions
{
    public abstract class BaseReaction : MonoBehaviour
    {
        protected Unit unit;

        [Header("Reaction Settings")]
        [SerializeField]
        private string reactionName;

        // Defaults to Prompt, but AI units or players can change this later
        public ReactionMode CurrentMode = ReactionMode.Prompt;

        protected virtual void Awake()
        {
            unit = GetComponent<Unit>();
        }

        public string GetReactionName() => reactionName;

        // Evaluates if this reaction even cares about this event
        public abstract bool CanTrigger(GameEvent gameEvent);

        // The actual logic of the reaction (e.g., swinging the sword, casting shield)
        public abstract void Execute(ReactionIntent intent, Action onReactionComplete);

        // Higher number = resolves first. (e.g., Counterspell might be 100, Reactive Strike 50)
        public virtual int GetPriority() => 0;

        // If the player sets the reaction to "Conditional", this runs to see if it should auto-fire
        public virtual bool ShouldAutoTrigger(GameEvent gameEvent)
        {
            return true; // Default behavior
        }
    }
}
