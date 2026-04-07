using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Combat;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Actions
{
    public class SeekAction : BaseAction
    {
        private const bool STEALTH_DEBUG = true;

        [Header("Seek Area")]
        [Tooltip(
            "Radius in tiles around your space that Seek checks. Minimum viable: 3 tiles (~15 ft)."
        )]
        [SerializeField]
        private int seekRadiusTiles = 3;

        public override string GetActionName() => "Seek";

        public override int GetActionPointsCost() => 1;

        public override List<Vector3Int> GetActionRangeGridPositions()
        {
            return new List<Vector3Int> { unit.CurrentLayeredPosition };
        }

        public override List<Vector3Int> GetValidActionGridPositions()
        {
            return new List<Vector3Int> { unit.CurrentLayeredPosition };
        }

        public override bool CanExecuteAction()
        {
            if (!base.CanExecuteAction())
                return false;

            UnitStealth seekerStealth = unit.GetComponent<UnitStealth>();
            if (seekerStealth == null)
                return false;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            List<Unit> nearby = grid.GetUnitsInRadius(unit.CurrentGridPosition, seekRadiusTiles);

            foreach (Unit target in nearby)
            {
                if (target == null || target.GetFaction() == unit.GetFaction())
                    continue;

                UnitStealth targetStealth = target.GetComponent<UnitStealth>();
                if (targetStealth == null)
                    continue;

                DetectionState state = targetStealth.GetDetectionState(unit);
                if (state == DetectionState.Hidden || state == DetectionState.Undetected)
                    return true;
            }

            return false;
        }

        public override void TakeAction(Vector3Int targetPosition, Action onActionComplete)
        {
            GridSystem grid = ServiceLocator.Get<GridSystem>();

            if (STEALTH_DEBUG && unit != null)
            {
                Debug.Log(
                    $"<color=blue>[STEALTH]</color> SeekAction.TakeAction seeker={unit.name} from={unit.CurrentGridPosition}"
                );
            }

            List<Unit> nearby = grid.GetUnitsInRadius(unit.CurrentGridPosition, seekRadiusTiles);
            List<Unit> targets = new List<Unit>();

            foreach (Unit target in nearby)
            {
                if (target == null)
                    continue;
                if (target.GetFaction() == unit.GetFaction())
                    continue;

                UnitStealth targetStealth = target.GetComponent<UnitStealth>();
                if (targetStealth == null)
                    continue;

                DetectionState state = targetStealth.GetDetectionState(unit);
                if (state == DetectionState.Hidden || state == DetectionState.Undetected)
                    targets.Add(target);
            }

            StealthResolver.ResolveSeek(unit, targets);
            if (STEALTH_DEBUG)
                Debug.Log(
                    $"<color=blue>[STEALTH]</color> SeekAction.ResolveSeek complete seeker={unit.name} targets={targets.Count}"
                );
            onActionComplete?.Invoke();
        }
    }
}
