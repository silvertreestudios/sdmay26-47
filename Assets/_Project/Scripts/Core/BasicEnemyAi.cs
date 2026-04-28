using System;
using System.Collections;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Core
{
    public class BasicEnemyAi : MonoBehaviour
    {
        [SerializeField]
        private float turnStartDelay = 1f;

        private TurnManager turnManager;

        private void Start()
        {
            turnManager = ServiceLocator.Get<TurnManager>();
            turnManager.OnTurnChanged += TurnManager_OnTurnChanged;
        }

        private void OnDestroy()
        {
            if (turnManager != null)
                turnManager.OnTurnChanged -= TurnManager_OnTurnChanged;
        }

        private void TurnManager_OnTurnChanged(object sender, EventArgs e)
        {
            Unit currentUnit = turnManager.CurrentUnit;

            if (currentUnit == null)
                return;

            if (currentUnit.GetFaction() != Faction.Enemy)
                return;

            StartCoroutine(RunEnemyTurnRoutine(currentUnit));
        }

        private IEnumerator RunEnemyTurnRoutine(Unit enemyUnit)
        {
            yield return null;

            if (turnManager.CurrentUnit != enemyUnit)
                yield break;

            yield return new WaitForSeconds(turnStartDelay);

            int safety = 0;

            while (
                !IsNextToPlayer(enemyUnit)
                && enemyUnit.GetActionPointsRemaining() > 0
                && safety < 5
            )
            {
                int apBefore = enemyUnit.GetActionPointsRemaining();
                Vector3Int posBefore = enemyUnit.CurrentLayeredPosition;

                yield return MoveTowardTarget(enemyUnit, GetClosestPlayerUnit(enemyUnit));

                safety++;

                // If nothing changed, stop so we do not loop forever.
                if (
                    enemyUnit.GetActionPointsRemaining() == apBefore
                    && enemyUnit.CurrentLayeredPosition == posBefore
                )
                {
                    break;
                }
            }

            if (IsNextToPlayer(enemyUnit) && enemyUnit.GetActionPointsRemaining() > 0)
            {
                ServiceLocator.Get<UnitActionSystem>().EndTurn();
            }

            // If AP reached 0, UnitActionSystem.CheckTurnEnd() should already end the turn.
        }

        private IEnumerator MoveTowardTarget(Unit enemyUnit, Unit target)
        {
            if (target == null)
                yield break;

            GridSystem grid = ServiceLocator.Get<GridSystem>();

            Vector3Int start = enemyUnit.CurrentLayeredPosition;
            Vector3Int targetPos = target.CurrentLayeredPosition;

            List<Vector3Int> fullPath = Pathfinding.FindPath(
                start,
                targetPos,
                targetPos // allow pathing to the occupied target square
            );

            if (fullPath == null || fullPath.Count < 2)
                yield break;

            int maxMoveCost = enemyUnit.GetMaxMoveCost();

            Vector3Int bestDestination = start;
            int usedCost = 0;

            for (int i = 1; i < fullPath.Count; i++)
            {
                Vector3Int previous = fullPath[i - 1];
                Vector3Int next = fullPath[i];

                if (grid.IsPositionOccupied(next) && next != targetPos)
                    break;

                if (next == targetPos)
                    break; // do not move into the player's occupied tile

                int stepCost = Pathfinding.CalculatePathCost(
                    new List<Vector3Int> { previous, next }
                );

                if (usedCost + stepCost > maxMoveCost)
                    break;

                usedCost += stepCost;
                bestDestination = next;
            }

            if (bestDestination == start)
                yield break;

            List<Vector3Int> movePath = Pathfinding.FindPath(start, bestDestination);

            if (movePath == null || movePath.Count < 2)
                yield break;

            bool visualMoveComplete = false;

            enemyUnit.MoveAlongPath(movePath, () =>
            {
                visualMoveComplete = true;
            });

            yield return new WaitUntil(() => visualMoveComplete);

            bool actionCommitComplete = false;

            ServiceLocator.Get<UnitActionSystem>().AiCommitMoveAction(() =>
            {
                actionCommitComplete = true;
            });

            yield return new WaitUntil(() => actionCommitComplete);
        }

        private IEnumerator MoveOneStepTowardTarget(Unit enemyUnit, Unit target)
        {
            GridSystem grid = ServiceLocator.Get<GridSystem>();

            Vector3Int start = enemyUnit.CurrentLayeredPosition;
            Vector3Int targetPos = target.CurrentLayeredPosition;

            Vector3Int direction = Vector3Int.zero;

            int xDelta = targetPos.x - start.x;
            int zDelta = targetPos.z - start.z;

            if (Mathf.Abs(xDelta) >= Mathf.Abs(zDelta))
                direction.x = Math.Sign(xDelta);
            else
                direction.z = Math.Sign(zDelta);

            Vector3Int destination = start + direction;

            if (destination == start)
                yield break;

            if (grid.GetNode(destination) == null)
                yield break;

            if (grid.IsPositionOccupied(destination))
                yield break;

            List<Vector3Int> path = new List<Vector3Int>
            {
                start,
                destination
            };

            enemyUnit.SpendActionPoints(1);
            grid.MoveUnit(enemyUnit, start, destination);
            enemyUnit.FinalizeMove(destination);

            bool moveComplete = false;

            enemyUnit.MoveAlongPath(path, () =>
            {
                moveComplete = true;
            });

            yield return new WaitUntil(() => moveComplete);
        }

        private Unit GetClosestPlayerUnit(Unit enemyUnit)
        {
            Unit closest = null;
            int closestDistance = int.MaxValue;

            foreach (Unit unit in UnitManager.AllUnits)
            {
                if (unit == null)
                    continue;

                if (unit.GetFaction() != Faction.Player)
                    continue;

                var health = unit.GetComponent<IDamageable>();
                if (health != null && health.IsDead)
                    continue;

                int distance = PF2E_Core.GetPF2eDistance3D(
                    enemyUnit.CurrentLayeredPosition,
                    unit.CurrentLayeredPosition
                );

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = unit;
                }
            }

            return closest;
        }
        private bool IsNextToPlayer(Unit enemyUnit)
        {
            foreach (Unit unit in UnitManager.AllUnits)
            {
                if (unit == null)
                    continue;

                if (unit.GetFaction() != Faction.Player)
                    continue;

                var health = unit.GetComponent<IDamageable>();
                if (health != null && health.IsDead)
                    continue;

                int distance = PF2E_Core.GetPF2eDistance3D(
                    enemyUnit.CurrentLayeredPosition,
                    unit.CurrentLayeredPosition
                );

                if (distance <= 1)
                    return true;
            }

            return false;
        }
    }
}