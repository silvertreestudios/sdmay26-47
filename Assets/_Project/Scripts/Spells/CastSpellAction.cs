using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Data.PF2e;
using PathfinderTactics.Grid;
using PathfinderTactics.InputSystem;
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

                        // Ensure the node exists
                        if (node == null)
                            continue;

                        // Range Check
                        if (PF2E_Core.GetPF2eDistance3D(unitPos3D, testPos3D) > range)
                            continue;

                        // Line of Effect Check
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

            // Use the filtered range positions as the base set
            List<Vector3Int> rangePositions = GetActionRangeGridPositions();

            // Self-targeting
            if (currentSpell.Targeting == SpellTargetingType.Self)
            {
                return rangePositions;
            }

            // Ground/Area targeting - any tile in range is valid
            if (
                currentSpell.Targeting == SpellTargetingType.GroundTarget
                || currentSpell.Targeting == SpellTargetingType.Area
                || currentSpell.Targeting == SpellTargetingType.Line
                || currentSpell.Targeting == SpellTargetingType.Cone
            )
            {
                return rangePositions;
            }

            // SingleTarget - must have a valid unit within the already-filtered range
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
                {
                    validPositions.Add(testPos3D);
                }
            }

            Debug.Log(
                $"[SPELL TARGETING] Found {validPositions.Count} valid target tiles for {currentSpell.ElementName}. Range used: {GetRangeInTiles()} tiles."
            );
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
                TargetPosition = targetPosition,
                TargetingType = currentSpell.Targeting,
            };

            isWaitingForProjectile = false;
            isAnimationFinished = false;

            // For single-target spells without AoE effects, pre-populate the target
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

            Debug.Log(
                $"<b><color=magenta>[SPELL CAST]</color></b> {unit.name} casts "
                    + $"{currentSpell.ElementName} (Level {castLevel}) at {targetPosition}!"
            );

            // Fire BeforeSpellEvent -> Counterspell window
            BeforeSpellEvent spellEvent = new BeforeSpellEvent(unit, currentSpell, targetPosition);

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
                            activeContext = context;
                            isAnimationFinished = true;
                            ResolveSpellVisualsAndPayload();
                        }
                    }
                );
        }

        private void HandleCastSpellFire()
        {
            if (activeContext != null)
            {
                ResolveSpellVisualsAndPayload();
            }
        }

        private void ResolveSpellVisualsAndPayload()
        {
            var visuals = unit.GetComponentInChildren<UnitVisuals>();
            Transform handTransform = (visuals != null) ? visuals.GetHandTransform() : transform;
            Vector3 handPos = handTransform.position;
            Vector3 targetPos = activeContext.TargetPosition; // Default to cell center

            // If we have a unit target, aim for center
            Unit targetUnit = ServiceLocator
                .Get<GridSystem>()
                .GetUnitAt(activeContext.TargetPosition);
            if (targetUnit != null)
            {
                targetPos = targetUnit.transform.position + Vector3.up;
            }

            // Cast VFX
            if (activeContext.SpellData.CastVFXPrefab != null)
            {
                Instantiate(activeContext.SpellData.CastVFXPrefab, handPos, Quaternion.identity);

                if (unit.GetFaction() == Faction.Player)
                {
                    ServiceLocator.Get<HapticService>()?.TriggerRumble(0.25f, 0.25f, 0.1f);
                }
            }

            // Delivery Branch
            if (activeContext.SpellData.DeliveryType == SpellDelivery.Instant)
            {
                PlayHitVFXAndResolve();
            }
            else if (activeContext.SpellData.DeliveryType == SpellDelivery.Projectile)
            {
                if (activeContext.SpellData.ProjectileVFXPrefab != null)
                {
                    isWaitingForProjectile = true;
                    GameObject projObj = Instantiate(
                        activeContext.SpellData.ProjectileVFXPrefab,
                        handPos,
                        Quaternion.identity
                    );
                    SpellProjectile projectile = projObj.GetComponent<SpellProjectile>();

                    if (projectile == null)
                        projectile = projObj.AddComponent<SpellProjectile>();

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
                    // Fallback if prefab missing
                    PlayHitVFXAndResolve();
                }
            }
        }

        private void PlayHitVFXAndResolve()
        {
            // Hit VFX
            if (activeContext.SpellData.HitVFXPrefab != null)
            {
                Vector3 hitPos = activeContext.TargetPosition;
                Unit targetUnit = ServiceLocator
                    .Get<GridSystem>()
                    .GetUnitAt(activeContext.TargetPosition);
                if (targetUnit != null)
                    hitPos = targetUnit.transform.position + Vector3.up;

                Instantiate(activeContext.SpellData.HitVFXPrefab, hitPos, Quaternion.identity);
            }

            // Rumble on impact
            ServiceLocator.Get<HapticService>()?.TriggerRumble(0.75f, 0.75f, 0.2f);

            // Logic Resolution
            SpellEffectResolver.Resolve(activeContext);
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
            // wait for both the animation to end and the projectile (if any) to hit.
            if (isAnimationFinished && !isWaitingForProjectile)
            {
                FinishCasting();
            }
        }

        private void FinishCasting()
        {
            // This is called either directly (instant) or via the projectile callback + animation end check.
            if (isAnimationFinished && !isWaitingForProjectile)
            {
                onActionComplete?.Invoke();
            }
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
