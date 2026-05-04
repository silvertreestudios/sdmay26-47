using System;
using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Data.TacticsRuleset;
using TacticsGame.Grid;
using TacticsGame.InputSystem;
using TacticsGame.Reactions;
using TacticsGame.Spells.Services;
using UnityEngine;

namespace TacticsGame.Spells
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
        private List<Vector3Int> cachedRangePositions;
        private bool isWaitingForProjectile;
        private bool isAnimationFinished;

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            if (unit != null)
                unit.OnMoveConfirmed += HandleMoveConfirmed;
        }

        private void OnDestroy()
        {
            if (unit != null)
                unit.OnMoveConfirmed -= HandleMoveConfirmed;
        }

        private void HandleMoveConfirmed()
        {
            cachedRangePositions = null;
        }

        public override string GetActionName()
        {
            return currentSpell != null ? currentSpell.ElementName : "Cast Spell";
        }

        public override DamageType GetPrimaryDamageType()
        {
            return currentSpell != null ? currentSpell.ElementType : DamageType.Untyped;
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
            cachedRangePositions = null; // Invalidate cache
        }

        public SpellSO GetCurrentSpell() => currentSpell;

        public override bool IsUnitTargeted => false;

        // Targeting

        public override List<Vector3Int> GetActionRangeGridPositions()
        {
            if (currentSpell == null)
                return new List<Vector3Int>();

            if (cachedRangePositions != null)
                return new List<Vector3Int>(cachedRangePositions);

            List<Vector3Int> positions = new List<Vector3Int>();
            Vector3Int unitPos3D = unit.CurrentLayeredPosition;
            int range = GetRangeInTiles();

            if (currentSpell.Targeting == SpellTargetingType.Self)
            {
                positions.Add(unitPos3D);
                cachedRangePositions = new List<Vector3Int>(positions);
                return positions;
            }

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            HashSet<Vector3Int> added = new HashSet<Vector3Int>();

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
                        Vector3Int testPos3D = node.Coordinates;

                        if (
                            TacticsRuleset_Core.GetTacticsRulesetDistance3D(unitPos3D, testPos3D)
                            > range
                        )
                            continue;

                        if (currentSpell.RequiresLineOfEffect)
                        {
                            if (!LineOfSightUtility.HasLineOfEffect(unitPos3D, testPos3D))
                                continue;
                        }

                        if (added.Add(testPos3D))
                            positions.Add(testPos3D);
                    }
                }
            }

            cachedRangePositions = new List<Vector3Int>(positions);
            return positions;
        }

        public override List<Vector3Int> GetValidActionGridPositions()
        {
            List<Vector3Int> validPositions = new List<Vector3Int>();
            if (currentSpell == null)
                return validPositions;

            List<Vector3Int> rangePositions = GetActionRangeGridPositions();

            if (currentSpell.Targeting == SpellTargetingType.Self)
                return rangePositions;

            if (
                currentSpell.Targeting == SpellTargetingType.GroundTarget
                || currentSpell.Targeting == SpellTargetingType.Area
                || currentSpell.Targeting == SpellTargetingType.Line
                || currentSpell.Targeting == SpellTargetingType.Cone
            )
            {
                return rangePositions;
            }

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            foreach (Vector3Int testPos3D in rangePositions)
            {
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
                    validPositions.Add(testPos3D);
            }

            return validPositions;
        }

        public override bool IsValidActionGridPosition(Vector3Int targetPosition)
        {
            return GetValidActionGridPositions().Contains(targetPosition);
        }

        // Execution

        public override void TakeAction(Vector3Int targetPosition, Action onActionComplete)
        {
            if (currentSpell == null)
            {
                onActionComplete?.Invoke();
                return;
            }

            this.onActionComplete = onActionComplete;

            SpellCastContext context = new SpellCastContext
            {
                Caster = unit,
                SpellData = currentSpell,
                CastLevel = castLevel,
                TargetPosition = targetPosition,
                TargetingType = currentSpell.Targeting,
            };

            isWaitingForProjectile = false;
            isAnimationFinished = false;

            if (
                currentSpell.Targeting == SpellTargetingType.SingleTarget
                || currentSpell.Targeting == SpellTargetingType.Self
            )
            {
                Unit targetAtPos = ServiceLocator.Get<GridSystem>().GetUnitAt(targetPosition);
                if (targetAtPos != null)
                {
                    context.AffectedUnits.Add(targetAtPos);
                    context.AffectedCells.Add(targetPosition);
                }
            }

            BeforeSpellEvent spellEvent = new BeforeSpellEvent(unit, currentSpell, targetPosition);
            ServiceLocator
                .Get<ReactionManager>()
                .EvaluateEvent(
                    spellEvent,
                    (resolvedEvent) =>
                    {
                        if (resolvedEvent.IsCancelled)
                        {
                            FinishCasting();
                            return;
                        }

                        SpellEffectResolver.ResolvePhases(
                            context,
                            SpellEffectPhase.Targeting,
                            SpellEffectPhase.Roll
                        );
                        activeContext = context;

                        if (context.PendingMovements.Exists(m => m.IsInteractive))
                        {
                            StartCoroutine(ProcessInteractiveMovements());
                        }
                        else
                        {
                            StartCastingVisuals();
                        }
                    }
                );
        }

        private void StartCastingVisuals()
        {
            // Ensure camera is watching the caster during their animation
            ServiceLocator.Get<CameraController>().SetFollowTarget(unit.transform);

            var visuals = unit.GetComponentInChildren<UnitVisuals>();
            if (visuals != null)
            {
                visuals.OnCastSpell += HandleCastSpellFire;
                visuals.OnAnimationEnd += HandleCastAnimationEnd;
                visuals.TriggerCastSpellAction();
            }
            else
            {
                isAnimationFinished = true;
                ResolveSpellVisualsAndPayload();
            }
        }

        private System.Collections.IEnumerator ProcessInteractiveMovements()
        {
            var targeting = ServiceLocator.Get<TargetingService>();
            var input = ServiceLocator.Get<InputService>();
            var grid = ServiceLocator.Get<GridSystem>();
            var rangeVis = ServiceLocator.Get<MoveRangeVisualizer>();
            var cam = ServiceLocator.Get<CameraController>();

            yield return null;

            foreach (var req in activeContext.PendingMovements)
            {
                if (!req.IsInteractive)
                    continue;

                cam.EnterEagleEyeMode(req.Target.transform);
                ServiceLocator.Get<HapticService>()?.TriggerRumble(0.2f, 0.2f, 0.1f);

                DragTargetingHelper helper =
                    req.Target.gameObject.AddComponent<DragTargetingHelper>();
                helper.Initialize(req.Target.CurrentLayeredPosition, req.MaxTiles);

                List<Vector3Int> validTiles = helper.GetValidActionGridPositions();
                rangeVis.ShowCustomRange(validTiles, true);

                targeting.InitializeTargeting(
                    new GridPosition(
                        req.Target.CurrentLayeredPosition.x,
                        req.Target.CurrentLayeredPosition.z
                    ),
                    helper
                );

                bool confirmed = false;
                while (!confirmed)
                {
                    targeting.HandleCursorMovement(helper);

                    Vector3 cursorWorldPos = grid.GetWorldPosition(
                        targeting.CurrentTargetLayeredPosition
                    );
                    req.Target.transform.position = Vector3.Lerp(
                        req.Target.transform.position,
                        cursorWorldPos,
                        Time.deltaTime * 20f
                    );

                    if (input.IsConfirmJustPressed())
                    {
                        if (
                            helper.IsValidActionGridPosition(targeting.CurrentTargetLayeredPosition)
                        )
                        {
                            confirmed = true;
                        }
                        else
                        {
                            ServiceLocator.Get<HapticService>()?.TriggerRumble(0.15f, 0.15f, 0.1f);
                        }
                    }
                    yield return null;
                }

                Vector3Int targetCell = targeting.CurrentTargetLayeredPosition;
                req.Target.FinalizeMove(targetCell);
                req.Target.SnapToGrid(grid.GetWorldPosition(targetCell));

                rangeVis.ClearCustomRange();
                targeting.HideTargeting();

                cam.ExitEagleEyeMode();
                Destroy(helper);

                yield return new WaitForSeconds(0.15f);
            }

            StartCastingVisuals();
        }

        private void HandleCastSpellFire()
        {
            if (activeContext != null)
                ResolveSpellVisualsAndPayload();
        }

        private void ResolveSpellVisualsAndPayload()
        {
            var visuals = unit.GetComponentInChildren<UnitVisuals>();
            Transform handTransform = (visuals != null) ? visuals.GetHandTransform() : transform;
            Vector3 handPos = handTransform.position;
            var gridSystem = ServiceLocator.Get<GridSystem>();

            Vector3 targetPos = gridSystem.GetWorldPosition(activeContext.TargetPosition);

            if (activeContext.SpellData.CastVFXPrefab != null)
            {
                Instantiate(activeContext.SpellData.CastVFXPrefab, handPos, Quaternion.identity);
                if (unit.GetFaction() == Faction.Player)
                    ServiceLocator.Get<HapticService>()?.TriggerRumble(0.25f, 0.25f, 0.1f);
            }

            if (activeContext.SpellData.DeliveryType == SpellDelivery.Instant)
            {
                PlayHitVFXAndResolve();
            }
            else if (activeContext.SpellData.DeliveryType == SpellDelivery.Projectile)
            {
                Unit targetUnit = gridSystem.GetUnitAt(activeContext.TargetPosition);
                if (targetUnit != null)
                    targetPos = targetUnit.transform.position + Vector3.up;

                if (activeContext.SpellData.ProjectileVFXPrefab != null)
                {
                    isWaitingForProjectile = true;
                    GameObject projObj = Instantiate(
                        activeContext.SpellData.ProjectileVFXPrefab,
                        handPos,
                        Quaternion.identity
                    );
                    SpellProjectile projectile =
                        projObj.GetComponent<SpellProjectile>()
                        ?? projObj.AddComponent<SpellProjectile>();

                    projectile.Launch(
                        handPos,
                        targetPos,
                        activeContext.SpellData.ProjectileSpeed,
                        () =>
                        {
                            isWaitingForProjectile = false;
                            PlayHitVFXAndResolve();
                        }
                    );
                }
                else
                {
                    PlayHitVFXAndResolve();
                }
            }
        }

        private void PlayHitVFXAndResolve()
        {
            if (activeContext.SpellData.HitVFXPrefab != null)
            {
                var gridSystem = ServiceLocator.Get<GridSystem>();
                Vector3 hitPos = gridSystem.GetWorldPosition(activeContext.TargetPosition);
                Unit targetUnit = gridSystem.GetUnitAt(activeContext.TargetPosition);
                if (targetUnit != null)
                    hitPos = targetUnit.transform.position + Vector3.up;

                Instantiate(activeContext.SpellData.HitVFXPrefab, hitPos, Quaternion.identity);
            }

            ServiceLocator.Get<HapticService>()?.TriggerRumble(0.75f, 0.75f, 0.2f);
            SpellEffectResolver.ResolvePhases(
                activeContext,
                SpellEffectPhase.Resolution,
                SpellEffectPhase.Aftermath
            );
            FireAfterEventAndFinish(activeContext);
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
                        CheckForResolutionComplete();
                    }
                );
        }

        private void HandleCastAnimationEnd()
        {
            isAnimationFinished = true;
            var visuals = unit.GetComponentInChildren<UnitVisuals>();
            if (visuals != null)
            {
                visuals.OnCastSpell -= HandleCastSpellFire;
                visuals.OnAnimationEnd -= HandleCastAnimationEnd;
            }
            CheckForResolutionComplete();
        }

        private void CheckForResolutionComplete()
        {
            if (isAnimationFinished && !isWaitingForProjectile)
                FinishCasting();
        }

        private void FinishCasting()
        {
            if (isAnimationFinished && !isWaitingForProjectile)
            {
                // Return camera to caster so player can select next action
                ServiceLocator.Get<CameraController>().SetFollowTarget(unit.transform);

                onActionComplete?.Invoke();
            }
        }

        private int GetRangeInTiles()
        {
            if (currentSpell == null)
                return 1;
            if (currentSpell.Targeting == SpellTargetingType.Self)
                return 0;
            return Mathf.Max(1, currentSpell.Range / 5);
        }

        public override bool CanExecuteAction()
        {
            if (!base.CanExecuteAction())
                return false;
            return true;
        }
    }

    public class DragTargetingHelper : Actions.BaseAction
    {
        private Vector3Int startPos;
        private int maxTiles;
        private List<Vector3Int> cachedValidPositions;

        public void Initialize(Vector3Int startPos, int maxTiles)
        {
            this.startPos = startPos;
            this.maxTiles = maxTiles;
            this.cachedValidPositions = null;
        }

        public override string GetActionName() => "Dragging Unit";

        public override bool IsUnitTargeted => false;

        public override List<Vector3Int> GetValidActionGridPositions()
        {
            if (cachedValidPositions == null)
            {
                cachedValidPositions = Pathfinding.GetReachablePositions(startPos, maxTiles * 10);
            }
            return cachedValidPositions;
        }

        public override List<Vector3Int> GetActionRangeGridPositions() =>
            GetValidActionGridPositions();

        public override bool IsValidActionGridPosition(Vector3Int targetPosition)
        {
            return GetValidActionGridPositions().Contains(targetPosition);
        }

        public override void TakeAction(Vector3Int targetPosition, Action onActionComplete) { }
    }
}
