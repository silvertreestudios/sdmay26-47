using TacticsGame.Characters;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.Spells.Effects
{
    /// <summary>
    /// Roll Phase: Marks all affected targets as auto-hit (Success) with no roll.
    /// Used by spells like Magic Missile that bypass attack rolls and saving throws.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticsRuleset/Spell Effects/Auto-Hit")]
    public class AutoHitEffectSO : SpellEffectSO
    {
        private void OnEnable()
        {
            Phase = SpellEffectPhase.Roll;
        }

        public override void Apply(SpellCastContext context)
        {
            foreach (var target in context.AffectedUnits)
            {
                if (!context.RollResults.ContainsKey(target))
                {
                    // Auto-hit = treat as Success (not CriticalSuccess - no double damage)
                    context.RollResults[target] = new RollResult(0, 0, Degree.Success);
                }
            }

            Debug.Log(
                $"<color=cyan>[SPELL]</color> {context.SpellData.ElementName}: "
                    + $"Auto-hit applied to {context.AffectedUnits.Count} targets."
            );
        }

        public override string GetEditorSummary()
        {
            return "Auto-Hit (no roll)";
        }
    }
}
