using System;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.Reactions
{
    /// <summary>
    /// Reaction that triggers when an enemy casts a spell (BeforeSpellEvent).
    /// Sets event.IsCancelled = true to prevent the spell from resolving.
    ///
    /// PF2e Counterspell rules (simplified):
    /// - Must have the same spell prepared/known
    /// - Must be within 120 feet of the caster
    /// - Costs your reaction
    ///
    /// For this prototype, the range check is hardened at 120ft (24 tiles)
    /// and the "same spell" requirement is relaxed.
    ///
    /// TODO: Implement full PF2e counterspell rules
    /// </summary>
    public class Counterspell : BaseReaction
    {
        [Header("Counterspell Settings")]
        [SerializeField]
        private int counterRange = 24; // 120ft = 24 tiles

        public override int GetPriority() => 100; // Highest priority - resolves before other reactions

        public override bool CanTrigger(GameEvent gameEvent)
        {
            if (!(gameEvent is BeforeSpellEvent spellEvent))
                return false;

            // Don't counter your own spells
            if (spellEvent.SourceUnit == unit)
                return false;

            // Don't counter allies
            if (spellEvent.SourceUnit.GetFaction() == unit.GetFaction())
                return false;

            int distance = TacticsRuleset_Core.GetTacticsRulesetDistance3D(
                unit.CurrentLayeredPosition,
                spellEvent.SourceUnit.CurrentLayeredPosition
            );

            if (distance > counterRange)
                return false;

            // TODO: Full PF2e requires having the same spell prepared.
            // For prototype, any spellcaster can attempt to counter.
            return true;
        }

        public override void Execute(ReactionIntent intent, Action onReactionComplete)
        {
            BeforeSpellEvent spellEvent = intent.TriggeringEvent as BeforeSpellEvent;

            // Cancel the spell
            spellEvent.IsCancelled = true;

            Debug.Log(
                $"<color=magenta>[COUNTERSPELL]</color> {unit.name} counters "
                    + $"{spellEvent.SourceUnit.name}'s {spellEvent.Spell.ElementName}! "
                    + $"The spell fizzles!"
            );

            onReactionComplete?.Invoke();
        }

        public override bool ShouldAutoTrigger(GameEvent gameEvent)
        {
            // AI always counters when possible (for now)
            return true;
        }
    }
}
