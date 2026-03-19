using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Combat;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Actions
{
    public class SneakAction : BaseAction
    {
        private const bool STEALTH_DEBUG = true;

        [Header("Sneak Noise")]
        [Tooltip("If true, the Sneak action applies the noise rule (Undetected -> Hidden).")]
        [SerializeField]
        private bool makesNoise = false;

        public bool MakesNoise => makesNoise;

        private GridPosition cachedStart;
        private List<GridPosition> cachedRange = null;

        public override string GetActionName() => "Sneak";

        public override int GetActionPointsCost() => 1;

        public override List<GridPosition> GetActionRangeGridPositions()
        {
            return GetCachedSneakRange();
        }

        public override List<GridPosition> GetValidActionGridPositions()
        {
            return GetCachedSneakRange();
        }

        private List<GridPosition> GetCachedSneakRange()
        {
            GridPosition start = unit.CurrentGridPosition;
            if (cachedRange != null && start == cachedStart)
                return cachedRange;

            cachedStart = start;

            UnitStatsSO stats = unit.GetStats();
            if (stats == null)
                cachedRange = new List<GridPosition>();
            else
            {
                int maxMoveCost = unit.GetMaxMoveCost();
                int halfMoveCost = Mathf.Max(0, maxMoveCost / 2);
                cachedRange = Pathfinding.GetReachableGridPositions(start, halfMoveCost);

                // The actor's own square isn't a move.
                cachedRange.Remove(start);
            }

            return cachedRange;
        }

        public override bool CanExecuteAction()
        {
            if (!base.CanExecuteAction())
                return false;

            UnitStealth actorStealth = unit.GetComponent<UnitStealth>();
            if (actorStealth == null)
                return false;

            // Must be Hidden or Undetected to at least one observer.
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            List<Unit> observers = grid.GetAllEnemies(unit.GetFaction());
            foreach (Unit observer in observers)
            {
                DetectionState state = actorStealth.GetDetectionState(observer);
                if (state == DetectionState.Hidden || state == DetectionState.Undetected)
                    return true;
            }

            return false;
        }

        public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
        {
            // Move as part of Sneak, then resolve stealth transitions at end.
            GridPosition startPos = unit.CurrentGridPosition;
            GridPosition endPos = gridPosition;

            if (STEALTH_DEBUG)
                Debug.Log(
                    $"<color=blue>[STEALTH]</color> SneakAction.TakeAction unit={unit.name} start={startPos} end={endPos} makesNoise={makesNoise}"
                );

            List<GridPosition> path = Pathfinding.FindPath(startPos, endPos);
            if (path == null || path.Count == 0)
            {
                onActionComplete?.Invoke();
                return;
            }

            GridSystem grid = ServiceLocator.Get<GridSystem>();

            // Suppress passive state degradation while the stealth action resolves.
            StealthResolver.SetPassiveEvaluationSuppressed(unit, true);
            grid.MoveUnit(unit, startPos, endPos);
            unit.FinalizeMove(endPos);

            unit.MoveAlongPath(
                path,
                () =>
                {
                    if (STEALTH_DEBUG)
                        Debug.Log(
                            $"<color=blue>[STEALTH]</color> SneakAction.ResolveSneak unit={unit.name} pathLen={path.Count}"
                        );
                    StealthResolver.ResolveSneak(unit, startPos, endPos, path, makesNoise);

                    StealthResolver.SetPassiveEvaluationSuppressed(unit, false);
                    onActionComplete?.Invoke();
                }
            );
        }
    }
}
