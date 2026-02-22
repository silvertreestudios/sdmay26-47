namespace PathfinderTactics.Core
{
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
    }
}
