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
        /// Returns the grid position based on the unit's current world transform.
        /// Useful for real-time visualization and previews.
        /// </summary>
        public static GridPosition GetVisualGridPosition(Unit unit)
        {
            return ServiceLocator.Get<GridSystem>().GetGridPosition(unit.transform.position);
        }

        /// <summary>
        /// A creature threatens a square if it can make a melee Strike into that square.
        /// Uses 3D PF2e distance for reach calculations.
        /// </summary>
        public static bool IsThreatening(
            Unit attacker,
            Unit target,
            GridPosition? attackerPos = null,
            GridPosition? targetPos = null
        )
        {
            if (attacker == null || target == null)
                return false;

            if (!attacker.CanMakeMeleeAttacks)
                return false;

            var equipment = attacker.GetComponent<UnitEquipment>();
            WeaponSO weapon = equipment != null ? equipment.GetMainWeapon() : null;

            if (weapon != null && weapon.IsRangedWeapon())
                return false;

            int reachFeet = (weapon != null && weapon.reachFeet > 0) ? weapon.reachFeet : 5;
            int reachInTiles = reachFeet / 5;

            Vector3Int aPos3D = attackerPos.HasValue
                ? new Vector3Int(
                    attackerPos.Value.x,
                    attacker.CurrentLayeredPosition.y,
                    attackerPos.Value.z
                )
                : attacker.CurrentLayeredPosition;
            Vector3Int tPos3D = targetPos.HasValue
                ? new Vector3Int(
                    targetPos.Value.x,
                    target.CurrentLayeredPosition.y,
                    targetPos.Value.z
                )
                : target.CurrentLayeredPosition;

            int distance = PF2E_Core.GetPF2eDistance3D(aPos3D, tPos3D);
            if (distance > reachInTiles)
                return false;

            if (!LineOfSightUtility.HasLineOfEffect(aPos3D, tPos3D))
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
        /// </summary>
        public static bool AreOpposite(
            Unit a,
            GridPosition aPos,
            Unit b,
            GridPosition bPos,
            Unit target,
            GridPosition targetPos
        )
        {
            GridPosition dirA = NormalizeToGrid(aPos - targetPos);
            GridPosition dirB = NormalizeToGrid(bPos - targetPos);

            // They are opposite if their normalized direction vectors are inverses
            return dirA == -dirB && dirA != new GridPosition(0, 0);
        }

        /// <summary>
        /// Determines if the target is flanked by the specific attacker.
        /// </summary>
        public static bool IsFlanking(
            Unit attacker,
            Unit target,
            GridPosition? attackerOverride = null,
            GridPosition? targetOverride = null
        )
        {
            if (attacker == null || target == null)
                return false;
            if (attacker.GetFaction() == target.GetFaction())
                return false;

            GridPosition aPos = attackerOverride ?? attacker.CurrentGridPosition;
            GridPosition tPos = targetOverride ?? target.CurrentGridPosition;

            // Reach 0 creatures cannot flank
            var equipment = attacker.GetComponent<UnitEquipment>();
            if (equipment == null)
                return false;
            WeaponSO attackerWeapon = equipment.GetMainWeapon();
            if (attackerWeapon == null || attackerWeapon.reachFeet <= 0)
                return false;

            // Attacker must threaten the target
            if (!IsThreatening(attacker, target, aPos, tPos))
                return false;

            // Find all allies of the attacker
            List<Unit> allies = ServiceLocator.Get<GridSystem>().GetAllEnemies(target.GetFaction());

            foreach (Unit ally in allies)
            {
                if (ally == attacker)
                    continue;

                // Ally check uses CurrentGridPosition for standard rules
                if (IsThreatening(ally, target, null, tPos))
                {
                    if (AreOpposite(attacker, aPos, ally, ally.CurrentGridPosition, target, tPos))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Visual flanking check that uses transform-based positions for ALL units.
        /// Used for real-time highlights.
        /// </summary>
        public static bool IsAnyFlankingVisual(Unit target)
        {
            if (target == null)
                return false;

            GridPosition targetPos = GetVisualGridPosition(target);
            List<Unit> enemies = ServiceLocator
                .Get<GridSystem>()
                .GetAllEnemies(target.GetFaction());

            // Look for any pair of enemies that flank the target based on visual positions
            for (int i = 0; i < enemies.Count; i++)
            {
                Unit attackerA = enemies[i];
                GridPosition posA = GetVisualGridPosition(attackerA);

                if (!IsThreatening(attackerA, target, posA, targetPos))
                    continue;

                for (int j = i + 1; j < enemies.Count; j++)
                {
                    Unit attackerB = enemies[j];
                    GridPosition posB = GetVisualGridPosition(attackerB);

                    if (!IsThreatening(attackerB, target, posB, targetPos))
                        continue;

                    if (AreOpposite(attackerA, posA, attackerB, posB, target, targetPos))
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
