using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Data.PF2e;
using UnityEngine;

namespace PathfinderTactics.Spells.Effects
{
    /// <summary>
    /// Roll Phase: Each affected target rolls a saving throw vs the caster's spell DC.
    /// Results are stored in context.RollResults for downstream effects (Damage, Condition).
    /// </summary>
    [CreateAssetMenu(menuName = "PF2e/Spell Effects/Saving Throw")]
    public class SavingThrowEffectSO : SpellEffectSO
    {
        [Header("Save Configuration")]
        [Tooltip("Which save the targets roll (Fortitude, Reflex, or Will).")]
        public SavingThrowType SaveType;

        private void OnEnable()
        {
            Phase = SpellEffectPhase.Roll;
        }

        public override void Apply(SpellCastContext context)
        {
            // Calculate the caster's spell DC
            // For now: DC = 10 + level + proficiency + casting stat mod
            // TODO: Pull casting stat and proficiency from unit's class data
            int casterLevel = 1;
            int castingStatMod = 3; // Placeholder - will pull from stats later
            Proficiency spellProf = Proficiency.Trained;
            int spellDC = PF2E_Core.CalculateModifier(casterLevel, spellProf, castingStatMod) + 10;

            foreach (var target in context.AffectedUnits)
            {
                // Target's save modifier
                // TODO: Pull save proficiency from target's class/stats
                int saveMod = GetSaveModifier(target);

                int d20 = Random.Range(1, 21);
                int total = d20 + saveMod;
                Degree degree = PF2E_Core.CheckResult(d20, saveMod, spellDC);

                context.RollResults[target] = new RollResult(d20, total, degree);

                string color = degree >= Degree.Success ? "green" : "red";
                Debug.Log(
                    $"<color=cyan>[SPELL SAVE]</color> {target.name} rolls {SaveType}: "
                        + $"{d20} + {saveMod} = {total} vs DC {spellDC} -> "
                        + $"<color={color}>{degree}</color>"
                );
            }
        }

        private int GetSaveModifier(Unit target)
        {
            // Simplified: use ability modifier based on save type
            // TODO: Full implementation should use target's save proficiency
            var stats = target.GetStats();
            if (stats == null)
                return 0;

            int abilityScore;
            switch (SaveType)
            {
                case SavingThrowType.Fortitude:
                    abilityScore = stats.constitution;
                    break;
                case SavingThrowType.Reflex:
                    abilityScore = stats.dexterity;
                    break;
                case SavingThrowType.Will:
                    abilityScore = stats.wisdom;
                    break;
                default:
                    return 0;
            }

            int abilityMod = (abilityScore - 10) / 2;
            return PF2E_Core.CalculateModifier(1, Proficiency.Trained, abilityMod);
        }

        public override string GetEditorSummary()
        {
            return $"Save: {SaveType}";
        }
    }
}
