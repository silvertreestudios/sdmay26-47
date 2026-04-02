using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
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

    /// <summary>
    /// Data structure for reporting a detailed breakdown of AC modifiers.
    /// </summary>
    public struct ArmorClassBreakdown
    {
        public int totalAC;
        public int baseAC; // 10 + prof + dex + item
        public int statusPenalty;
        public string statusPenaltySources; // e.g. "Frightened 2"
        public int circumstanceMod;
        public string circumstanceModSources; // e.g. "Off-Guard (-2), Prone (+2 vs Ranged)"
    }

    public static class PF2E_Core
    {
        /// <summary>
        /// Calculates the modifier for a specific ability score (e.g., 18 -> +4).
        /// PF2e rule: (Score - 10) / 2, rounded down.
        /// </summary>
        public static int GetAbilityModifier(int score)
        {
            return Mathf.FloorToInt((score - 10) / 2f);
        }

        /// <summary>
        /// Calculates the grid distance between two positions using Chebyshev math (all 8 directions = 1 unit).
        /// PF2e uses 5-10-5 for diagonals in movement, but reach uses simple "adjacency" logic
        /// which maps to Chebyshev for 5-foot squares.
        /// </summary>
        public static int GetGridDistance(GridPosition a, GridPosition b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.z - b.z));
        }

        /// <summary>
        /// PF2e 3D distance in tiles using the 2-step 5-10-5 diagonal rule.
        /// Step 1: Merge dx and dz horizontally with the alternating diagonal rule.
        /// Step 2: Merge the horizontal result with dy using the same rule.
        /// </summary>
        public static int GetPF2eDistance3D(Vector3Int a, Vector3Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            int dz = Mathf.Abs(a.z - b.z);
            int dHorizontal = Pf2eMergeDiagonal(dx, dz);
            return Pf2eMergeDiagonal(dHorizontal, dy);
        }

        /// <summary>
        /// Returns 3D PF2e distance in feet (tiles * 5).
        /// </summary>
        public static int GetPF2eDistance3DInFeet(Vector3Int a, Vector3Int b)
        {
            return GetPF2eDistance3D(a, b) * 5;
        }

        /// <summary>
        /// Merges two axis distances using PF2e's 5-10-5 alternating diagonal rule.
        /// Result = max(a,b) + floor(min(a,b) / 2).
        /// </summary>
        private static int Pf2eMergeDiagonal(int a, int b)
        {
            return Mathf.Max(a, b) + Mathf.FloorToInt(Mathf.Min(a, b) / 2f);
        }

        /// <summary>
        /// Calculates the total modifier for a check.
        /// Result = Level + ProficiencyBonus + AbilityMod + ItemBonus (ignored for now).
        /// PF2e rule: Untrained gives 0, others give Level + Constant.
        /// </summary>
        public static int CalculateModifier(int level, Proficiency proficiency, int abilityMod)
        {
            int profBonus = (proficiency == Proficiency.Untrained) ? 0 : (level + (int)proficiency);
            return profBonus + abilityMod;
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
        public static int GetStatusPenalty(
            UnitConditions conditions,
            AbilityScore ability,
            out string sourceCondition
        )
        {
            sourceCondition = "";
            if (conditions == null)
                return 0;

            int frightened = conditions.GetConditionValue(ConditionType.Frightened);
            int sickened = conditions.GetConditionValue(ConditionType.Sickened);
            int specific = 0;
            ConditionType specificType = ConditionType.Frightened;

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
                sourceCondition = $"{culprit} {worstPenalty}";
            }

            return -worstPenalty;
        }

        // Keep old overload for backward compatibility if needed, though internal now
        public static int GetStatusPenalty(UnitConditions conditions, AbilityScore ability)
        {
            return GetStatusPenalty(conditions, ability, out _);
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
            if (conditions.HasCondition(ConditionType.Prone) && attackType != AttackType.Melee)
            {
                circumstancePenalty = -2;
            }

            return baseMod + statusPenalty + circumstancePenalty;
        }

        /// <summary>
        /// Calculates the Spell DC for a caster.
        /// PF2e: DC = 10 + Level + Proficiency + Casting Ability Mod.
        /// Applies Stupefied penalty (reduces spell DCs and attack rolls).
        /// </summary>
        public static int CalculateSpellDC(
            Unit caster,
            int level,
            Proficiency spellProf,
            int castingStatMod
        )
        {
            int dc = 10 + CalculateModifier(level, spellProf, castingStatMod);

            UnitConditions conditions = caster.GetComponent<UnitConditions>();
            if (conditions != null)
            {
                // Stupefied penalizes spell DCs
                int stupefied = conditions.GetConditionValue(ConditionType.Stupefied);
                if (stupefied > 0)
                {
                    dc -= stupefied;
                    Debug.Log(
                        $"<color=orange>[PF2E CORE]</color> Spell DC reduced by {stupefied} from Stupefied."
                    );
                }
            }

            return dc;
        }

        /// <summary>
        /// Calculates a spell attack roll modifier.
        /// PF2e: Level + Proficiency + Casting Ability Mod - penalties.
        /// </summary>
        public static int CalculateSpellAttackModifier(
            Unit caster,
            AbilityScore castingAbility,
            int castingStatMod,
            int level,
            Proficiency spellProf
        )
        {
            int baseMod = CalculateModifier(level, spellProf, castingStatMod);

            UnitConditions conditions = caster.GetComponent<UnitConditions>();
            if (conditions == null)
                return baseMod;

            // Status penalties (includes Stupefied for mental stats)
            int statusPenalty = GetStatusPenalty(conditions, castingAbility);

            return baseMod + statusPenalty;
        }
    }
}
