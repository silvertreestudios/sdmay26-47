using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.Spells.Services
{
    /// <summary>
    /// Converts grid cells into filtered unit lists.
    /// Separates unit querying from spatial math (AreaService).
    /// </summary>
    public static class UnitQueryService
    {
        /// <summary>
        /// Returns all units occupying the given cells, filtered by the provided TargetFilter.
        /// </summary>
        public static List<Unit> GetUnitsInCells(
            List<GridPosition> cells,
            TargetFilter filter,
            Unit caster
        )
        {
            List<Unit> result = new List<Unit>();
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();

            foreach (var cell in cells)
            {
                if (!gridSystem.IsValidGridPosition(cell))
                    continue;

                Unit unitAtCell = gridSystem.GetUnitAt(cell);
                if (unitAtCell == null)
                    continue;

                if (PassesFilter(unitAtCell, filter, caster))
                {
                    if (!result.Contains(unitAtCell))
                        result.Add(unitAtCell);
                }
            }

            return result;
        }

        /// <summary>
        /// 3D overload: checks each exact Vector3Int for a unit, supporting
        /// multi-layer AoE that can hit units on different Y levels.
        /// TODO: thoroughly test this.
        /// </summary>
        public static List<Unit> GetUnitsInCells(
            List<Vector3Int> cells,
            TargetFilter filter,
            Unit caster
        )
        {
            List<Unit> result = new List<Unit>();
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();

            foreach (Vector3Int cell in cells)
            {
                Unit unitAtCell = gridSystem.GetUnitAt(cell);
                if (unitAtCell == null)
                    continue;

                if (PassesFilter(unitAtCell, filter, caster))
                {
                    if (!result.Contains(unitAtCell))
                        result.Add(unitAtCell);
                }
            }

            return result;
        }

        /// <summary>
        /// Evaluates whether a unit passes the given target filter.
        /// </summary>
        public static bool PassesFilter(Unit target, TargetFilter filter, Unit caster)
        {
            switch (filter)
            {
                case TargetFilter.All:
                    return true;

                case TargetFilter.Enemies:
                    return target.GetFaction() != caster.GetFaction();

                case TargetFilter.Allies:
                    return target.GetFaction() == caster.GetFaction();

                case TargetFilter.ExcludeCaster:
                    return target != caster;

                case TargetFilter.LivingOnly:
                    var health = target.GetComponent<IDamageable>();
                    return health != null && !health.IsDead;

                default:
                    return true;
            }
        }
    }
}
