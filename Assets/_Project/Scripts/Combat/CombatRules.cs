using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Combat
{
    /// <summary>
    /// Static class for resolving high-level PF2e combat rules.
    /// This layer sits above the spatial math (GridMathHelper) and below the Unit/Action layer.
    /// </summary>
    public static class CombatRules
    {
        /// <summary>
        /// Determines if a target is Off-Guard relative to an attacker.
        /// </summary>
        public static bool IsOffGuard(
            Unit attacker,
            Unit target,
            AttackType attackType = AttackType.Melee
        )
        {
            if (target == null)
                return false;

            // General conditions apply to all attacks
            if (IsOffGuardFromConditions(target))
                return true;

            // Flanking ONLY applies to melee attacks
            if (attackType == AttackType.Melee)
            {
                if (IsOffGuardFromFlanking(attacker, target))
                    return true;
            }

            // Stealth: target is Off-Guard against attacks from Hidden or Undetected attackers.
            // Rule: "the creature remains off-guard against that attack".
            if (IsOffGuardFromStealth(attacker, target))
                return true;

            return false;
        }

        /// <summary>
        /// Target is Off-Guard against an attacker who is Hidden or Undetected relative to them.
        /// </summary>
        public static bool IsOffGuardFromStealth(Unit attacker, Unit target)
        {
            if (attacker == null || target == null)
                return false;

            var attackerStealth = attacker.GetComponent<UnitStealth>();
            if (attackerStealth == null)
                return false;

            DetectionState stateVsTarget = attackerStealth.GetDetectionState(target);
            return stateVsTarget == DetectionState.Hidden
                || stateVsTarget == DetectionState.Undetected;
        }

        /// <summary>
        /// Checks if the target has general conditions that make it Off-Guard (Prone, Grabbed, etc.).
        /// </summary>
        public static bool IsOffGuardFromConditions(Unit target)
        {
            var conditions = target.GetComponent<UnitConditions>();
            if (conditions == null)
                return false;

            return conditions.IsOffGuard();
        }

        /// <summary>
        /// Specifically checks if the target is Off-Guard due to being flanked by the attacker and their allies.
        /// </summary>
        public static bool IsOffGuardFromFlanking(Unit attacker, Unit target)
        {
            if (attacker == null || target == null)
                return false;

            // All-Around Vision: This creature is fundamentally immune to flanking Off-Guard.
            if (target.HasAllAroundVision)
                return false;

            // Pure Positional Check: Are they geometrically flanked?
            if (!GridMathHelper.IsFlanking(attacker, target))
                return false;

            // Deny Advantage: Rogue/special ability that ignores flanking from equal/lower level enemies.
            if (target.HasDenyAdvantage && attacker.Level <= target.Level)
            {
                return false;
            }

            return true;
        }
    }
}
