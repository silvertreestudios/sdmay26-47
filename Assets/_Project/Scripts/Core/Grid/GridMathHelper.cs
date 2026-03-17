using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
using PathfinderTactics.Items;
using UnityEngine;

namespace PathfinderTactics.Core
{
    /// <summary>
    /// Static helper class for PF2e-specific grid math, including flanking and threat detection.
    /// </summary>
    public static class GridMathHelper
    {
        /// <summary>
        /// A creature threatens a square if it can make a melee Strike into that square.
        /// </summary>
        public static bool IsThreatening(Unit attacker, Unit target)
        {
            if (attacker == null || target == null)
                return false;

            // Ability to act & attack: Uses capability flags from UnitConditions
            if (!attacker.CanMakeMeleeAttacks)
                return false;

            // Armed: Must have a melee weapon or unarmed strike
            var equipment = attacker.GetComponent<UnitEquipment>();
            if (equipment == null)
                return false;

            WeaponSO weapon = equipment.GetMainWeapon();

            // If still null or not a melee weapon, they don't threaten
            if (weapon == null || weapon.IsRangedWeapon())
                return false;

            // Reach: Grid distance (Chebyshev) must be <= weapon reach
            // PF2e: Reach 0 means they MUST share a square to threaten.
            // Doesnt exist in current system tho.
            int reachFeet = weapon.reachFeet;
            int reachInTiles = reachFeet / 5;

            int distance = PF2E_Core.GetGridDistance(
                attacker.CurrentGridPosition,
                target.CurrentGridPosition
            );

            if (distance > reachInTiles)
                return false;

            // Line of Effect: No solid walls blocking the path
            if (
                LineOfSightUtility.GetCoverBonus(
                    attacker.CurrentGridPosition,
                    target.CurrentGridPosition
                ) == -1
            )
                return false;

            return true;
        }

        /// <summary>
        /// Normalizes a grid delta to a direction vector (8-way: N, NE, E, SE, S, SW, W, NW).
        /// </summary>
        public static GridPosition NormalizeToGrid(GridPosition delta)
        {
            return new GridPosition(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.z, -1, 1));
        }

        /// <summary>
        /// Checks if two units are on opposite sides/corners of a target.
        /// Used for flanking detection.
        /// </summary>
        public static bool AreOpposite(Unit a, Unit b, Unit target)
        {
            GridPosition dirA = NormalizeToGrid(a.CurrentGridPosition - target.CurrentGridPosition);
            GridPosition dirB = NormalizeToGrid(b.CurrentGridPosition - target.CurrentGridPosition);

            // They are opposite if their normalized direction vectors are inverses
            return dirA == -dirB && dirA != new GridPosition(0, 0);
        }

        /// <summary>
        /// Determines if the target is flanked by the specific attacker.
        /// PF2e rule: You flank if you and an ally threaten the target and are on opposite sides/corners.
        /// </summary>
        public static bool IsFlanking(Unit attacker, Unit target)
        {
            if (attacker == null || target == null)
                return false;
            if (attacker.GetFaction() == target.GetFaction())
                return false;

            // Reach 0 creatures cannot flank
            // They can threaten, but cannot form opposite sides/corners reliably.
            var equipment = attacker.GetComponent<UnitEquipment>();
            if (equipment == null)
                return false;
            WeaponSO attackerWeapon = equipment.GetMainWeapon();
            if (attackerWeapon == null || attackerWeapon.reachFeet <= 0)
                return false;

            // Attacker must threaten the target
            if (!IsThreatening(attacker, target))
                return false;

            // Find all allies of the attacker
            List<Unit> allies = ServiceLocator.Get<GridSystem>().GetAllEnemies(target.GetFaction());

            foreach (Unit ally in allies)
            {
                if (ally == attacker)
                    continue;

                // Ally must also threaten the target
                if (IsThreatening(ally, target))
                {
                    // Check if they are on opposite sides
                    if (AreOpposite(attacker, ally, target))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns a list of all units that currently threaten the target.
        /// </summary>
        public static List<Unit> GetThreateningUnits(Unit target)
        {
            List<Unit> threateningUnits = new List<Unit>();
            if (target == null)
                return threateningUnits;

            // Get all potential enemies
            List<Unit> enemies = ServiceLocator
                .Get<GridSystem>()
                .GetAllEnemies(target.GetFaction());

            foreach (Unit enemy in enemies)
            {
                if (IsThreatening(enemy, target))
                {
                    threateningUnits.Add(enemy);
                }
            }

            return threateningUnits;
        }
    }
}
