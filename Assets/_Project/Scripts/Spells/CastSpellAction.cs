using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Data.PF2e;
using PathfinderTactics.Grid;
using PathfinderTactics.Reactions;
using PathfinderTactics.Spells.Services;
using UnityEngine;

namespace PathfinderTactics.Spells
{
    /// <summary>
    /// Runtime action component attached to units that can cast spells.
    /// Extends BaseAction so it integrates with the existing action economy,
    /// targeting system, phase manager, and UI action buttons.
    ///
    /// Flow:
    /// 1. SetSpell() configures which spell + cast level
    /// 2. Player selects target via TargetingService
    /// 3. TakeAction() fires BeforeSpellEvent -> reactions (Counterspell window)
    /// 4. If not cancelled -> SpellEffectResolver resolves effect chain
    /// 5. AfterSpellEvent fires -> turn continues
    /// </summary>
    public class CastSpellAction : Actions.BaseAction
    {
        [Header("Spell Configuration")]
        [SerializeField]
        private SpellSO currentSpell;

        [SerializeField]
        private int castLevel = 1;

        private SpellCastContext activeContext;

        // BaseAction Interface

        public override string GetActionName()
        {
            return currentSpell != null ? currentSpell.ElementName : "Cast Spell";
        }

        public override int GetActionPointsCost()
        {
            if (currentSpell == null)
                return 2;

            switch (currentSpell.Cost)
            {
                case ActionCost.Free:
                    return 0;
                case ActionCost.Reaction:
                    return 0;
                case ActionCost.One:
                    return 1;
                case ActionCost.Two:
                    return 2;
                case ActionCost.Three:
                    return 3;
                default:
                    return 2;
            }
        }

        /// <summary>
        /// Configures which spell to cast and at what level.
        /// Called by UI or AI before selecting this action.
        /// </summary>
        public void SetSpell(SpellSO spell, int level = -1)
        {
            currentSpell = spell;
            castLevel = level > 0 ? level : spell.Level;
        }

        public SpellSO GetCurrentSpell() => currentSpell;

        // Targeting

        public override List<GridPosition> GetActionRangeGridPositions()
        {
            List<GridPosition> positions = new List<GridPosition>();
            if (currentSpell == null)
                return positions;

            GridPosition unitPos = unit.CurrentGridPosition;
            int range = GetRangeInTiles();

            if (currentSpell.Targeting == SpellTargetingType.Self)
            {
                positions.Add(unitPos);
                return positions;
            }

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3Int unitPos3D = unit.CurrentLayeredPosition;

            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    Vector2Int colKey = new Vector2Int(unitPos3D.x + x, unitPos3D.z + z);
                    List<GridNode> column = grid.GetColumn(colKey);
                    if (column == null || column.Count == 0)
                        continue;

                    foreach (GridNode node in column)
                    {
                        if (PF2E_Core.GetPF2eDistance3D(unitPos3D, node.Coordinates) <= range)
                        {
                            positions.Add(new GridPosition(colKey.x, colKey.y));
                            break;
                        }
                    }
                }
            }

            return positions;
        }

        public override List<GridPosition> GetValidActionGridPositions()
        {
            List<GridPosition> validPositions = new List<GridPosition>();
            if (currentSpell == null)
                return validPositions;

            GridPosition unitPos = unit.CurrentGridPosition;
            int range = GetRangeInTiles();

            // Self-targeting
            if (currentSpell.Targeting == SpellTargetingType.Self)
            {
                validPositions.Add(unitPos);
                return validPositions;
            }

            // Ground/Area targeting - any tile in range is valid
            if (
                currentSpell.Targeting == SpellTargetingType.GroundTarget
                || currentSpell.Targeting == SpellTargetingType.Area
                || currentSpell.Targeting == SpellTargetingType.Line
                || currentSpell.Targeting == SpellTargetingType.Cone
            )
            {
                return GetActionRangeGridPositions();
            }

            // SingleTarget - must have a valid unit
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3Int unitPos3D = unit.CurrentLayeredPosition;

            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    Vector2Int colKey = new Vector2Int(unitPos.x + x, unitPos.z + z);
                    List<GridNode> column = grid.GetColumn(colKey);
                    if (column == null || column.Count == 0)
                        continue;

                    bool foundInColumn = false;
                    foreach (GridNode node in column)
                    {
                        if (foundInColumn)
                            break;

                        Vector3Int testPos3D = node.Coordinates;

                        if (PF2E_Core.GetPF2eDistance3D(unitPos3D, testPos3D) > range)
                            continue;

                        Unit targetUnit = grid.GetUnitAt(testPos3D);
                        if (targetUnit == null)
                            continue;

                        bool valid = false;
                        switch (currentSpell.Target)
                        {
                            case TargetType.Enemy:
                                valid = targetUnit.GetFaction() != unit.GetFaction();
                                break;
                            case TargetType.Ally:
                                valid = targetUnit.GetFaction() == unit.GetFaction();
                                break;
                            case TargetType.Creature:
                                valid = true;
                                break;
                            case TargetType.Self:
                                valid = targetUnit == unit;
                                break;
                            default:
                                valid = true;
                                break;
                        }

                        if (valid)
                        {
                            validPositions.Add(new GridPosition(testPos3D.x, testPos3D.z));
                            foundInColumn = true;
                        }
                    }
                }
            }

            return validPositions;
        }

        public override bool IsValidActionGridPosition(GridPosition gridPosition)
        {
            return GetValidActionGridPositions().Contains(gridPosition);
        }

        // Execution

        public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
        {
            if (!CanExecuteAction())
            {
                onActionComplete?.Invoke();
                return;
            }

            if (currentSpell == null)
            {
                Debug.LogError("[CastSpellAction] No spell configured!");
                onActionComplete?.Invoke();
                return;
            }

            this.onActionComplete = onActionComplete;

            // Build context
            SpellCastContext context = new SpellCastContext
            {
                Caster = unit,
                SpellData = currentSpell,
                CastLevel = castLevel,
                TargetPosition = gridPosition,
                TargetingType = currentSpell.Targeting,
            };

            // For single-target spells without AoE effects, pre-populate the target
            if (
                currentSpell.Targeting == SpellTargetingType.SingleTarget
                || currentSpell.Targeting == SpellTargetingType.Self
            )
            {
                Unit targetAtPos = ServiceLocator.Get<GridSystem>().GetUnitAt(gridPosition);
                if (targetAtPos != null)
                {
                    context.AffectedUnits.Add(targetAtPos);
                    context.AffectedCells.Add(gridPosition);
                }
            }

            Debug.Log(
                $"<b><color=magenta>[SPELL CAST]</color></b> {unit.name} casts "
                    + $"{currentSpell.ElementName} (Level {castLevel}) at {gridPosition}!"
            );

            // Fire BeforeSpellEvent -> Counterspell window
            BeforeSpellEvent spellEvent = new BeforeSpellEvent(unit, currentSpell, gridPosition);

            ServiceLocator
                .Get<ReactionManager>()
                .EvaluateEvent(
                    spellEvent,
                    (resolvedEvent) =>
                    {
                        if (resolvedEvent.IsCancelled)
                        {
                            Debug.Log(
                                $"<color=red>[SPELL]</color> {currentSpell.ElementName} was counterspelled!"
                            );
                            FinishCasting();
                            return;
                        }

                        var visuals = unit.GetComponentInChildren<UnitVisuals>();
                        if (visuals != null)
                        {
                            activeContext = context;
                            visuals.OnCastSpell += HandleCastSpellFire;
                            visuals.OnAnimationEnd += HandleCastAnimationEnd;
                            visuals.TriggerCastSpellAction();
                        }
                        else
                        {
                            // Fallback
                            SpellEffectResolver.Resolve(context);
                            FireAfterEventAndFinish(context);
                        }
                    }
                );
        }

        private void HandleCastSpellFire()
        {
            if (activeContext != null)
            {
                SpellEffectResolver.Resolve(activeContext);
                FireAfterEventAndFinish(activeContext);
            }
        }

        private void FireAfterEventAndFinish(SpellCastContext ctx)
        {
            AfterSpellEvent afterEvent = new AfterSpellEvent(unit, currentSpell, ctx.AffectedUnits);
            ServiceLocator
                .Get<ReactionManager>()
                .EvaluateEvent(
                    afterEvent,
                    (_) =>
                    {
                        var visuals = unit.GetComponentInChildren<UnitVisuals>();
                        if (visuals == null)
                        {
                            FinishCasting();
                        }
                    }
                );
        }

        private void HandleCastAnimationEnd()
        {
            var visuals = unit.GetComponentInChildren<UnitVisuals>();
            if (visuals != null)
            {
                visuals.OnCastSpell -= HandleCastSpellFire;
                visuals.OnAnimationEnd -= HandleCastAnimationEnd;
            }
            FinishCasting();
        }

        private void FinishCasting()
        {
            onActionComplete?.Invoke();
        }

        // Helpers

        /// <summary>
        /// Converts the SpellSO's range (in feet) to grid tiles (1 tile = 5ft).
        /// Returns at least 1 for non-self spells so cursor can move.
        /// </summary>
        private int GetRangeInTiles()
        {
            if (currentSpell == null)
                return 1;
            if (currentSpell.Targeting == SpellTargetingType.Self)
                return 0;

            int rangeInTiles = currentSpell.Range / 5;
            return Mathf.Max(1, rangeInTiles);
        }

        /// <summary>
        /// Override condition check to also block casting when Stupefied
        /// or when silenced (for spells with verbal components).
        /// </summary>
        public override bool CanExecuteAction()
        {
            if (!base.CanExecuteAction())
                return false;

            // TODO: Add Silenced check for verbal component spells
            // TODO: Add Stupefied flat check for spell failure

            return true;
        }
    }
}
