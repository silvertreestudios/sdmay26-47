using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Spells.Effects
{
    /// <summary>
    /// Roll Phase: Shifts the degree of success for all affected targets.
    /// Used by PF2e spells like Phantasmal Killer (Failure -> Critical Failure)
    /// or the Incapacitation trait (upgraded degree for higher-level creatures).
    /// </summary>
    [CreateAssetMenu(menuName = "PF2e/Spell Effects/Degree Shift")]
    public class DegreeShiftEffectSO : SpellEffectSO
    {
        [Header("Degree Modification")]
        [Tooltip("The original degree to modify.")]
        public Degree FromDegree;

        [Tooltip("The new degree it becomes.")]
        public Degree ToDegree;

        private void OnEnable()
        {
            Phase = SpellEffectPhase.Roll;
        }

        public override void Apply(SpellCastContext context)
        {
            foreach (var target in context.AffectedUnits)
            {
                if (!context.RollResults.TryGetValue(target, out var result))
                    continue;

                if (result.Degree == FromDegree)
                {
                    result.Degree = ToDegree;
                    Debug.Log(
                        $"<color=yellow>[DEGREE SHIFT]</color> {target.name}: "
                            + $"{FromDegree} -> {ToDegree} ({context.SpellData.ElementName})"
                    );
                }
            }
        }

        public override string GetEditorSummary()
        {
            return $"Degree Shift: {FromDegree} -> {ToDegree}";
        }
    }
}
