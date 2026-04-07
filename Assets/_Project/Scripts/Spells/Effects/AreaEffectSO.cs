using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Data.PF2e;
using PathfinderTactics.Grid;
using PathfinderTactics.Spells.Services;
using UnityEngine;

namespace PathfinderTactics.Spells.Effects
{
    /// <summary>
    /// Targeting Phase: Computes affected grid cells from an AreaDefinition,
    /// then populates context.AffectedUnits using UnitQueryService.
    /// </summary>
    [CreateAssetMenu(menuName = "PF2e/Spell Effects/Area Effect")]
    public class AreaEffectSO : SpellEffectSO
    {
        private void OnEnable()
        {
            Phase = SpellEffectPhase.Targeting;
        }

        public override void Apply(SpellCastContext context)
        {
            AreaDefinition area = context.SpellData.Area;

            if (area.Shape == AreaShape.None)
            {
                // Single-target: just the target position
                context.AffectedCells.Add(context.TargetPosition);
            }
            else
            {
                // Compute AoE cells using 3D logic
                context.AffectedCells = AreaService.GetAffectedCells3D(
                    context.TargetPosition,
                    area,
                    context.Caster.CurrentLayeredPosition,
                    context.TargetPosition
                );

                // Validate cells against grid bounds
                GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
                context.AffectedCells.RemoveAll(c => gridSystem.GetNode(c) == null);
            }

            // Populate affected units using this effect's filter
            context.AffectedUnits = UnitQueryService.GetUnitsInCells(
                context.AffectedCells,
                Filter,
                context.Caster
            );

            Debug.Log(
                $"<color=cyan>[SPELL AoE]</color> {context.SpellData.ElementName}: "
                    + $"{area.Shape} hits {context.AffectedCells.Count} cells, {context.AffectedUnits.Count} units."
            );
        }

        public override string GetEditorSummary()
        {
            return $"Area ({Filter})";
        }
    }
}
