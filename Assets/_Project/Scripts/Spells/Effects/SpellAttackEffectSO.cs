using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Data.TacticsRuleset;
using UnityEngine;

namespace TacticsGame.Spells.Effects
{
    /// <summary>
    /// Roll Phase: Performs a spell attack roll against each target's AC.
    /// Results are stored in context.RollResults for downstream effects (Damage, Condition).
    /// </summary>
    [CreateAssetMenu(menuName = "TacticsRuleset/Spell Effects/Spell Attack")]
    public class SpellAttackEffectSO : SpellEffectSO
    {
        private void OnEnable()
        {
            Phase = SpellEffectPhase.Roll;
        }

        public override void Apply(SpellCastContext context)
        {
            // Retrieve the unit's actual spell attack modifier
            int attackMod = context.Caster.GetSpellAttackModifier();

            foreach (var target in context.AffectedUnits)
            {
                int targetAC = target.GetArmorClass();
                int d20 = Random.Range(1, 21);
                int total = d20 + attackMod;
                Degree degree = TacticsRuleset_Core.CheckResult(d20, attackMod, targetAC);

                context.RollResults[target] = new RollResult(d20, total, degree);

                string color = degree >= Degree.Success ? "green" : "red";
                Debug.Log(
                    $"<color=cyan>[SPELL ATTACK]</color> {context.Caster.name} vs {target.name}: "
                        + $"{d20} + {attackMod} = {total} vs AC {targetAC} -> "
                        + $"<color={color}>{degree}</color>"
                );
            }
        }

        public override string GetEditorSummary()
        {
            return "Spell Attack Roll";
        }
    }
}
