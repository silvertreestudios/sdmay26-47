using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Data.PF2e;
using UnityEngine;

namespace PathfinderTactics.Spells.Effects
{
    /// <summary>
    /// Resolution Phase: Rolls damage dice and applies damage to affected targets.
    /// Supports PF2e basic save scaling: CritFail=double, Fail=full, Success=half, CritSuccess=zero.
    /// Routes through UnitHealth.ApplyDamage so existing reactions (Shield Block) still trigger.
    /// </summary>
    [CreateAssetMenu(menuName = "PF2e/Spell Effects/Damage")]
    public class DamageEffectSO : SpellEffectSO
    {
        [Header("Damage Configuration")]
        [Tooltip("If true, uses PF2e basic save damage scaling.")]
        public bool IsBasicSave;

        [Tooltip("If true, this is a spell attack (uses the attack roll degree for damage).")]
        public bool IsSpellAttack;

        private void OnEnable()
        {
            Phase = SpellEffectPhase.Resolution;
        }

        public override void Apply(SpellCastContext context)
        {
            SpellSO spell = context.SpellData;
            DiceFormula damageFormula = GetScaledDamage(spell, context.CastLevel);

            foreach (var target in context.AffectedUnits)
            {
                if (!context.RollResults.TryGetValue(target, out var result))
                    continue;

                // Roll base damage
                int baseDamage = RollDice(damageFormula);

                // Apply degree-based scaling
                int finalDamage = ApplyDegreeScaling(baseDamage, result.Degree);

                if (finalDamage <= 0)
                {
                    Debug.Log(
                        $"<color=grey>[SPELL DMG]</color> {target.name} takes 0 damage "
                            + $"({result.Degree})."
                    );
                    continue;
                }

                // Route through existing damage pipeline (triggers Shield Block, etc.)
                var health = target.GetComponent<IDamageable>();
                if (health != null)
                {
                    bool isCrit =
                        result.Degree == Degree.CriticalSuccess && IsSpellAttack
                        || result.Degree == Degree.CriticalFailure && IsBasicSave;

                    health.ApplyDamage(context.Caster, finalDamage, spell.ElementType, isCrit);

                    Debug.Log(
                        $"<color=red>[SPELL DMG]</color> {target.name} takes {finalDamage} "
                            + $"{spell.ElementType} damage ({result.Degree}). "
                            + $"[{damageFormula}]"
                    );
                }
            }
        }

        private DiceFormula GetScaledDamage(SpellSO spell, int castLevel)
        {
            DiceFormula formula = spell.BaseDamage;

            // Heightening: if spell has heightening rules and is cast above base level
            if (spell.HeightenDamageScaling.DiceCount > 0 && castLevel > spell.Level)
            {
                int heightenSteps = 0;

                // Parse heighten interval from rules (e.g., "+1" means every level, "+2" every 2 levels)
                int interval = 1;
                if (!string.IsNullOrEmpty(spell.HeightenRules))
                {
                    string cleaned = spell.HeightenRules.Replace("+", "").Trim();
                    int.TryParse(cleaned, out interval);
                    if (interval < 1)
                        interval = 1;
                }

                heightenSteps = (castLevel - spell.Level) / interval;

                formula.DiceCount += spell.HeightenDamageScaling.DiceCount * heightenSteps;
                formula.Bonus += spell.HeightenDamageScaling.Bonus * heightenSteps;
            }

            return formula;
        }

        private int ApplyDegreeScaling(int baseDamage, Degree degree)
        {
            if (IsBasicSave)
            {
                // PF2e basic save: CritSuccess=0, Success=half, Failure=full, CritFailure=double
                switch (degree)
                {
                    case Degree.CriticalSuccess:
                        return 0;
                    case Degree.Success:
                        return Mathf.Max(1, baseDamage / 2);
                    case Degree.Failure:
                        return baseDamage;
                    case Degree.CriticalFailure:
                        return baseDamage * 2;
                }
            }
            else if (IsSpellAttack)
            {
                // Spell attack roll: Success=full, CritSuccess=double, Miss=0
                switch (degree)
                {
                    case Degree.CriticalSuccess:
                        return baseDamage * 2;
                    case Degree.Success:
                        return baseDamage;
                    default:
                        return 0;
                }
            }

            // Default: full damage on Success or better
            return degree >= Degree.Success ? baseDamage : 0;
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
            if (IsBasicSave)
                return "Damage (Basic Save)";
            if (IsSpellAttack)
                return "Damage (Spell Attack)";
            return "Damage";
        }
    }
}
