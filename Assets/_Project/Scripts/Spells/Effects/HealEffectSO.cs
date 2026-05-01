using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Data.TacticsRuleset;
using UnityEngine;

namespace TacticsGame.Spells.Effects
{
    /// <summary>
    /// Resolution Phase: Heals affected targets.
    /// Routes through UnitHealth.ApplyHealing (handles wake-up from Dying, etc.)
    /// Supports heightening for scaling heal amounts.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticsRuleset/Spell Effects/Heal")]
    public class HealEffectSO : SpellEffectSO
    {
        [Header("Heal Configuration")]
        public DiceFormula BaseHeal;
        public DiceFormula HeightenScaling;

        private void OnEnable()
        {
            Phase = SpellEffectPhase.Resolution;
        }

        public override void Apply(SpellCastContext context)
        {
            DiceFormula formula = GetScaledHeal(context);

            foreach (var target in context.AffectedUnits)
            {
                int healAmount = RollDice(formula);

                if (healAmount <= 0)
                    continue;

                var health = target.GetComponent<UnitHealth>();
                if (health == null)
                    continue;

                health.ApplyHealing(healAmount);

                Debug.Log(
                    $"<color=green>[SPELL HEAL]</color> {target.name} healed for {healAmount} "
                        + $"({context.SpellData.ElementName}). [{formula}]"
                );
            }
        }

        private DiceFormula GetScaledHeal(SpellCastContext context)
        {
            DiceFormula formula = BaseHeal;
            SpellSO spell = context.SpellData;

            if (HeightenScaling.DiceCount > 0 && context.CastLevel > spell.Level)
            {
                // Parse heighten interval from rules (e.g., "+1" means every level, "+2" every 2 levels)
                int interval = 1;
                if (!string.IsNullOrEmpty(spell.HeightenRules))
                {
                    string cleaned = spell.HeightenRules.Replace("+", "").Trim();
                    int.TryParse(cleaned, out interval);
                    if (interval < 1)
                        interval = 1;
                }

                int steps = (context.CastLevel - spell.Level) / interval;
                formula.DiceCount += HeightenScaling.DiceCount * steps;
                formula.Bonus += HeightenScaling.Bonus * steps;
            }

            return formula;
        }

        private int RollDice(DiceFormula formula)
        {
            int total = formula.Bonus;
            for (int i = 0; i < formula.DiceCount; i++)
            {
                total += Random.Range(1, formula.DiceSize + 1);
            }
            return Mathf.Max(0, total);
        }

        public override string GetEditorSummary()
        {
            return $"Heal: {BaseHeal}";
        }
    }
}
