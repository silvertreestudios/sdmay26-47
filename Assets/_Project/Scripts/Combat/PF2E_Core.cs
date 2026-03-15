using PathfinderTactics.Characters;
using UnityEngine;

namespace PathfinderTactics.Core
{
    public enum AbilityScore
    {
        STR,
        DEX,
        CON,
        INT,
        WIS,
        CHA,
        None,
    }

    public enum AttackType
    {
        Melee,
        Ranged,
        Spell,
    }

    public enum Proficiency
    {
        Untrained = 0,
        Trained = 2,
        Expert = 4,
        Master = 6,
        Legendary = 8,
    }

    public enum Degree
    {
        CriticalFailure = 0,
        Failure = 1,
        Success = 2,
        CriticalSuccess = 3,
    }

    public static class PF2E_Core
    {
        /// <summary>
        /// Calculates the modifier for a check.
        /// Result = Level + ProficiencyBonus + AbilityMod + ItemBonus (ignored for now).
        /// </summary>
        public static int CalculateModifier(int level, Proficiency proficiency, int abilityMod)
        {
            if (proficiency == Proficiency.Untrained)
                return abilityMod; // Untrained is just the ability mod (usually)

            return level + (int)proficiency + abilityMod;
        }

        /// <summary>
        /// Determines degree of success (Critical Success, Success, Failure, Critical Failure).
        /// PF2E: Beat DC by 10 = Crit. Fail by 10 = Crit Fail. Nat 20/1 upgrades/downgrades one step.
        /// </summary>
        public static Degree CheckResult(int roll, int totalBonus, int dc)
        {
            int total = roll + totalBonus;
            Degree degree; // 0: CritFail, 1: Fail, 2: Success, 3: CritSuccess

            if (total >= dc + 10)
                degree = Degree.CriticalSuccess;
            else if (total >= dc)
                degree = Degree.Success;
            else if (total > dc - 10)
                degree = Degree.Failure;
            else
                degree = Degree.CriticalFailure;

            // Nat 20 improves by 1, Nat 1 decreases by 1
            if (roll == 20 && degree < Degree.CriticalSuccess)
                degree++;
            else if (roll == 1 && degree > Degree.CriticalFailure)
                degree--;

            return degree;
        }

        /// <summary>
        /// Calculates the worst active Status Penalty for a specific ability score.
        /// </summary>
        public static int GetStatusPenalty(UnitConditions conditions, AbilityScore ability)
        {
            if (conditions == null)
                return 0;

            int frightened = conditions.GetConditionValue(ConditionType.Frightened);
            int sickened = conditions.GetConditionValue(ConditionType.Sickened);
            int specific = 0;
            ConditionType specificType = ConditionType.Frightened; // Default dummy

            switch (ability)
            {
                case AbilityScore.STR:
                    specific = conditions.GetConditionValue(ConditionType.Enfeebled);
                    specificType = ConditionType.Enfeebled;
                    break;
                case AbilityScore.DEX:
                    specific = conditions.GetConditionValue(ConditionType.Clumsy);
                    specificType = ConditionType.Clumsy;
                    break;
                case AbilityScore.CON:
                    specific = conditions.GetConditionValue(ConditionType.Drained);
                    specificType = ConditionType.Drained;
                    break;
                case AbilityScore.INT:

                case AbilityScore.WIS:

                case AbilityScore.CHA:
                    specific = conditions.GetConditionValue(ConditionType.Stupefied);
                    specificType = ConditionType.Stupefied;
                    break;
            }

            int worstPenalty = Mathf.Max(frightened, sickened, specific);

            if (worstPenalty > 0)
            {
                string culprit =
                    worstPenalty == frightened
                        ? "Frightened"
                        : (worstPenalty == sickened ? "Sickened" : specificType.ToString());
                Debug.Log(
                    $"<color=orange>[PF2E CORE]</color> Applying -{worstPenalty} Status Penalty from {culprit}."
                );
            }

            return -worstPenalty;
        }

        /// <summary>
        /// The master method for calculating an attack modifier.
        /// </summary>
        public static int CalculateAttackRollModifier(
            Unit attacker,
            AbilityScore ability,
            int baseStatMod,
            int level,
            Proficiency prof,
            AttackType attackType,
            int mapPenalty = 0
        )
        {
            // Base Math (Level + Proficiency + Stat)
            int baseMod = CalculateModifier(level, prof, baseStatMod) + mapPenalty;

            UnitConditions conditions = attacker.GetComponent<UnitConditions>();
            if (conditions == null)
                return baseMod;

            // Status Penalties
            int statusPenalty = GetStatusPenalty(conditions, ability);

            // Circumstance Penalties
            int circumstancePenalty = 0;
            if (attackType == AttackType.Melee && conditions.HasCondition(ConditionType.Prone))
            {
                circumstancePenalty = -2;
            }

            return baseMod + statusPenalty + circumstancePenalty;
        }
    }
}
