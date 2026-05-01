using TacticsGame.Characters;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.Spells.Effects
{
    /// <summary>
    /// Resolution Phase: Applies a condition to affected targets based on degree of success.
    /// PF2e pattern: different condition values at different degrees.
    /// Example: Fear -> CritFail=Frightened 3, Fail=Frightened 2, Success=Frightened 1, CritSuccess=nothing.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticsRuleset/Spell Effects/Condition")]
    public class ConditionEffectSO : SpellEffectSO
    {
        [Header("Condition Configuration")]
        public ConditionType Condition;

        [Tooltip(
            "Condition value on Critical Failure (save spells) or Critical Success (attack spells)."
        )]
        public int ValueOnWorstOutcome = 0;

        [Tooltip("Condition value on Failure (save) or Success (attack).")]
        public int ValueOnBadOutcome = 0;

        [Tooltip("Condition value on Success (save) or nothing on attack.")]
        public int ValueOnOkOutcome = 0;

        [Tooltip("Condition value on Critical Success (save) - usually 0 (no effect).")]
        public int ValueOnBestOutcome = 0;

        [Tooltip("If true, uses save-spell degree mapping. If false, uses attack-spell mapping.")]
        public bool IsSaveSpell = true;

        private void OnEnable()
        {
            Phase = SpellEffectPhase.Resolution;
        }

        public override void Apply(SpellCastContext context)
        {
            foreach (var target in context.AffectedUnits)
            {
                if (!context.RollResults.TryGetValue(target, out var result))
                    continue;

                int conditionValue = GetConditionValue(result.Degree);

                if (conditionValue <= 0)
                    continue;

                var conditions = target.GetComponent<UnitConditions>();
                if (conditions == null)
                    continue;

                conditions.ApplyCondition(Condition, conditionValue, context.Caster);

                Debug.Log(
                    $"<color=orange>[SPELL CONDITION]</color> {target.name} gains "
                        + $"{Condition} {conditionValue} ({result.Degree})."
                );
            }
        }

        private int GetConditionValue(Degree degree)
        {
            if (IsSaveSpell)
            {
                // Save spells: worse save = worse condition
                switch (degree)
                {
                    case Degree.CriticalFailure:
                        return ValueOnWorstOutcome;
                    case Degree.Failure:
                        return ValueOnBadOutcome;
                    case Degree.Success:
                        return ValueOnOkOutcome;
                    case Degree.CriticalSuccess:
                        return ValueOnBestOutcome;
                }
            }
            else
            {
                // Attack spells: better attack = worse for target
                switch (degree)
                {
                    case Degree.CriticalSuccess:
                        return ValueOnWorstOutcome;
                    case Degree.Success:
                        return ValueOnBadOutcome;
                    case Degree.Failure:
                        return ValueOnOkOutcome;
                    case Degree.CriticalFailure:
                        return ValueOnBestOutcome;
                }
            }
            return 0;
        }

        public override string GetEditorSummary()
        {
            return $"Condition: {Condition} ({ValueOnWorstOutcome}/{ValueOnBadOutcome}/{ValueOnOkOutcome}/{ValueOnBestOutcome})";
        }
    }
}
