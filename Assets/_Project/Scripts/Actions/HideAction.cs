using System;
using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Combat;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.Actions
{
    public class HideAction : BaseAction
    {
        private const bool STEALTH_DEBUG = true;

        public override List<Vector3Int> GetActionRangeGridPositions()
        {
            return new List<Vector3Int> { unit.CurrentLayeredPosition };
        }

        public override List<Vector3Int> GetValidActionGridPositions()
        {
            return new List<Vector3Int> { unit.CurrentLayeredPosition };
        }

        public override bool IsUnitTargeted => false;

        public override bool CanExecuteAction()
        {
            if (!base.CanExecuteAction())
                return false;

            UnitStealth actorStealth = unit.GetComponent<UnitStealth>();
            if (actorStealth == null)
                return false;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            List<Unit> observers = grid.GetAllEnemies(unit.GetFaction());
            GridPosition actorPos = unit.CurrentGridPosition;

            foreach (Unit observer in observers)
            {
                if (actorStealth.GetDetectionState(observer) != DetectionState.Observed)
                    continue;

                if (StealthResolver.HasCoverOrConcealmentAt(actorPos, observer))
                    return true;
            }

            return false;
        }

        public override void TakeAction(Vector3Int targetPosition, Action onActionComplete)
        {
            // Hide doesn't depend on target square (we only allow current square in UI anyway).
            if (STEALTH_DEBUG && unit != null)
                Debug.Log(
                    $"<color=blue>[STEALTH]</color> HideAction.TakeAction unit={unit.name} pos={unit.CurrentGridPosition}"
                );
            StealthResolver.ResolveHide(unit);
            onActionComplete?.Invoke();
        }
    }
}
