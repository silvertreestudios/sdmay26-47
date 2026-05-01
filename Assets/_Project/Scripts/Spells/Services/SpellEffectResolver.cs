using System.Collections.Generic;
using System.Linq;
using TacticsGame.Spells.Effects;
using UnityEngine;

namespace TacticsGame.Spells.Services
{
    /// <summary>
    /// Iterates a spell's effects by phase.
    /// Each effect handles its own logic, this class just enforces execution order.
    /// </summary>
    public static class SpellEffectResolver
    {
        /// <summary>
        /// Resolves all effects on the given SpellSO in phase order.
        /// Stops early if context.IsCancelled becomes true.
        /// </summary>
        public static void Resolve(SpellCastContext context)
        {
            if (context.SpellData.Effects == null || context.SpellData.Effects.Count == 0)
            {
                Debug.LogWarning(
                    $"[SpellEffectResolver] {context.SpellData.ElementName} has no effects to resolve!"
                );
                return;
            }

            // Group effects by phase
            var effectsByPhase = context
                .SpellData.Effects.Where(e => e != null)
                .OrderBy(e => (int)e.Phase)
                .GroupBy(e => e.Phase);

            foreach (var phaseGroup in effectsByPhase)
            {
                if (context.IsCancelled)
                {
                    Debug.Log(
                        $"<color=grey>[SPELL]</color> {context.SpellData.ElementName} was "
                            + $"cancelled during {phaseGroup.Key} phase."
                    );
                    return;
                }

                foreach (var effect in phaseGroup)
                {
                    if (context.IsCancelled)
                        return;

                    effect.Apply(context);
                }
            }

            Debug.Log(
                $"<color=green>[SPELL]</color> {context.SpellData.ElementName} fully resolved. "
                    + $"Affected {context.AffectedUnits.Count} units."
            );
        }
    }
}
