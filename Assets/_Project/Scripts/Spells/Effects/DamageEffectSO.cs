using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Data.TacticsRuleset;
using UnityEngine;

namespace TacticsGame.Spells.Effects
{
    /// <summary>
    /// Resolution Phase: Rolls damage dice and applies damage to affected targets.
    /// Supports PF2e basic save scaling: CritFail=double, Fail=full, Success=half, CritSuccess=zero.
    /// Routes through UnitHealth.ApplyDamage so existing reactions (Shield Block) still trigger.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticsRuleset/Spell Effects/Damage")]
    public class DamageEffectSO : SpellEffectSO
    {
        [Header("Damage Configuration")]
        [Tooltip("If true, use basic save damage scaling.")]
        public bool IsBasicSave;

        [Tooltip("If true, this is a spell attack (uses the attack roll degree for damage).")]
        public bool IsSpellAttack;

        [Header("Overrides (Optional)")]
        [Tooltip("If set, overrides the spell's base damage formula.")]
        public DiceFormula OverrideDamage;

        [Tooltip("If set, overrides the spell's damage type. Set to Untyped to use spell default.")]
        public DamageType OverrideDamageType = DamageType.Untyped;

        private void OnEnable()
        {
            Phase = SpellEffectPhase.Resolution;
        }

        public override void Apply(SpellCastContext context)
        {
            SpellSO spell = context.SpellData;
            DiceFormula damageFormula =
                OverrideDamage.DiceCount > 0
                    ? OverrideDamage
                    : GetScaledDamage(spell, context.CastLevel);

            DamageType finalType =
                OverrideDamageType != DamageType.Untyped ? OverrideDamageType : spell.ElementType;

            foreach (Unit target in context.AffectedUnits)
            {
                // If it's a save/attack spell, we must have a result to know how to scale.
                // If it's a guaranteed hit (no save, no attack), we treat it as a 'Failure' (full damage).
                Degree degree = Degree.Failure;

                if (IsBasicSave || IsSpellAttack)
                {
                    if (!context.RollResults.TryGetValue(target, out var result))
                        continue;
                    degree = result.Degree;
                }
                else if (context.RollResults.TryGetValue(target, out var result))
                {
                    degree = result.Degree;
                }

                // Roll base damage
                int baseDamage = RollDice(damageFormula);

                // Apply degree-based scaling
                int finalDamage = ApplyDegreeScaling(baseDamage, degree);

                if (finalDamage <= 0)
                {
                    Debug.Log(
                        $"<color=grey>[SPELL DMG]</color> {target.name} takes 0 damage "
                            + $"({degree})."
                    );
                    continue;
                }

                // Route through existing damage pipeline (triggers Shield Block, etc.)
                var health = target.GetComponent<IDamageable>();
                if (health != null)
                {
                    bool isCrit =
                        degree == Degree.CriticalSuccess && IsSpellAttack
                        || degree == Degree.CriticalFailure && IsBasicSave;

                    health.ApplyDamage(context.Caster, finalDamage, finalType, isCrit);

                    Debug.Log(
                        $"<color=red>[SPELL DMG]</color> {target.name} takes {finalDamage} "
                            + $"{finalType} damage ({degree}). "
                            + $"[{damageFormula}]"
                    );
                }
            }

            // Environmental Objects
            foreach (IDamageable target in context.AffectedDamageables)
            {
                // Objects usually don't roll saves/attacks, they just take full damage.
                // We treat them as 'Failure' (full damage) for environmental effects.
                int baseDamage = RollDice(damageFormula);

                // Route through the same ApplyDamage pipeline
                target.ApplyDamage(context.Caster, baseDamage, finalType, false);
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

            // Default: if it's not a save or attack roll, it just hits for full damage.
            return baseDamage;
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
