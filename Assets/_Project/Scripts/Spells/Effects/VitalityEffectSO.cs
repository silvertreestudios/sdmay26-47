using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Data.PF2e;
using UnityEngine;

namespace PathfinderTactics.Spells.Effects
{
    /// <summary>
    /// Specialized effect for PF2e Vitality (Heal).
    /// Heals living creatures and deals Vitality damage to Undead.
    /// </summary>
    [CreateAssetMenu(menuName = "PF2e/Spell Effects/Vitality")]
    public class VitalityEffectSO : SpellEffectSO
    {
        [Header("Base Scaling")]
        public DiceFormula BaseDice = new DiceFormula(1, 8, 0);
        public DiceFormula HeightenDiceScaling = new DiceFormula(1, 8, 0);

        [Header("2-Action Bonus")]
        public int BaseFixedBonus = 0;
        public int HeightenFixedBonusScaling = 0;

        private void OnEnable()
        {
            Phase = SpellEffectPhase.Resolution;
        }

        public override void Apply(SpellCastContext context)
        {
            SpellSO spell = context.SpellData;
            int castLevel = context.CastLevel;
            int heightenSteps = Mathf.Max(0, castLevel - spell.Level);

            // Calculate total dice and bonus
            DiceFormula formula = BaseDice;
            formula.DiceCount += HeightenDiceScaling.DiceCount * heightenSteps;
            formula.Bonus += HeightenDiceScaling.Bonus * heightenSteps;

            int totalFixedBonus = BaseFixedBonus + (HeightenFixedBonusScaling * heightenSteps);

            foreach (Unit target in context.AffectedUnits)
            {
                bool isUndead = target.HasTrait("Undead");

                if (isUndead)
                {
                    // Undead: Deal damage with a basic Fortitude save
                    if (!context.RollResults.TryGetValue(target, out var result))
                    {
                        // Fallback: Roll a basic Fortitude save if the spell asset is missing the SaveEffect
                        int spellDC = context.Caster.GetSpellDC(
                            AbilityScore.INT,
                            Proficiency.Trained
                        );
                        int saveMod = target.GetSaveModifier(SavingThrowType.Fortitude);
                        int d20 = Random.Range(1, 21);
                        Degree degree = PF2E_Core.CheckResult(d20, saveMod, spellDC);
                        result = new RollResult(d20, d20 + saveMod, degree);
                        context.RollResults[target] = result;
                    }

                    int damage = RollDice(formula);
                    int finalDamage = ApplyBasicSave(damage, result.Degree);

                    var health = target.GetComponent<IDamageable>();
                    if (health != null)
                    {
                        bool isCrit = result.Degree == Degree.CriticalFailure;
                        health.ApplyDamage(
                            context.Caster,
                            finalDamage,
                            DamageType.Positive,
                            isCrit
                        );

                        Debug.Log(
                            $"<color=red>[VITALITY DMG]</color> {target.name} (Undead) took {finalDamage} vitality damage."
                        );
                    }
                }
                else
                {
                    // Living: Heal
                    int healAmount = RollDice(formula) + totalFixedBonus;

                    var health = target.GetComponent<UnitHealth>();
                    if (health != null)
                    {
                        health.ApplyHealing(healAmount);
                        Debug.Log(
                            $"<color=green>[VITALITY HEAL]</color> {target.name} healed for {healAmount}."
                        );
                    }
                }
            }
        }

        private int ApplyBasicSave(int damage, Degree degree)
        {
            switch (degree)
            {
                case Degree.CriticalSuccess:
                    return 0;
                case Degree.Success:
                    return damage / 2;
                case Degree.Failure:
                    return damage;
                case Degree.CriticalFailure:
                    return damage * 2;
                default:
                    return damage;
            }
        }

        private int RollDice(DiceFormula formula)
        {
            int total = formula.Bonus;
            for (int i = 0; i < formula.DiceCount; i++)
            {
                total += Random.Range(1, formula.DiceSize + 1);
            }
            return total;
        }

        public override string GetEditorSummary()
        {
            string summary = $"Vitality: {BaseDice}";
            if (BaseFixedBonus > 0)
                summary += $" + {BaseFixedBonus} (2A)";
            return summary;
        }
    }
}
