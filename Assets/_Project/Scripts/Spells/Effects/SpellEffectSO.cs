using UnityEngine;

namespace PathfinderTactics.Spells
{
    /// <summary>
    /// Abstract base for all composable spell effects.
    /// Each effect declares its execution Phase and optional TargetFilter.
    /// Subclasses implement Apply() with their specific logic.
    /// </summary>
    public abstract class SpellEffectSO : ScriptableObject
    {
        [Header("Effect Ordering")]
        [Tooltip(
            "Which phase this effect runs in. Resolver iterates: Targeting -> Roll -> Resolution -> Aftermath."
        )]
        public SpellEffectPhase Phase;

        [Header("Target Filtering")]
        [Tooltip("Which units this effect applies to within the affected area.")]
        public TargetFilter Filter = TargetFilter.All;

        /// <summary>
        /// Execute this effect's logic using the shared spell context.
        /// Effects read from and write to the context as needed.
        /// </summary>
        public abstract void Apply(SpellCastContext context);

        /// <summary>
        /// Inspector-friendly summary for designers.
        /// Override to show a human-readable description in the SpellSO inspector.
        /// </summary>
        public virtual string GetEditorSummary()
        {
            return $"{GetType().Name} ({Phase})";
        }
    }
}
