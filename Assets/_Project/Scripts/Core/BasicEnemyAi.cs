using System;
using System.Collections;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
using UnityEngine;
using PathfinderTactics.Actions;

namespace PathfinderTactics.Core
{
    public class BasicEnemyAi : MonoBehaviour
    {
        [SerializeField]
        private bool aiEnabled = true;

        [SerializeField]
        private float turnStartDelay = 1f;

        [SerializeField]
        private float delayBetweenActions = 0.5f;

        [SerializeField]
        private int maxAttackDistanceTiles = 5;

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
            if (!aiEnabled)
                return;

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
    turnManager.CurrentUnit == enemyUnit
    && enemyUnit.GetActionPointsRemaining() > 0
    && safety < 5
)
            {
                safety++;

                if (IsNextToPlayer(enemyUnit) && WantsToUseRangedAttack(enemyUnit))
                {
                    yield return MoveAwayFromClosestPlayer(enemyUnit);

                    if (turnManager.CurrentUnit != enemyUnit)
                        yield break;

                    yield return new WaitForSeconds(delayBetweenActions);
                }

                int apBefore = enemyUnit.GetActionPointsRemaining();

                yield return TryAttackIfPossible(enemyUnit);

                if (turnManager.CurrentUnit != enemyUnit)
                    yield break;

                if (enemyUnit.GetActionPointsRemaining() < apBefore)
                {
                    yield return new WaitForSeconds(delayBetweenActions);
                    continue;
                }

                Unit target = GetClosestPlayerUnit(enemyUnit);
                if (target == null)
                {
                    ServiceLocator.Get<UnitActionSystem>().EndTurn();
                    yield break;
                }

                yield return MoveTowardTarget(enemyUnit, target);

                if (turnManager.CurrentUnit != enemyUnit)
                    yield break;

                yield return new WaitForSeconds(delayBetweenActions);
            }

            if (turnManager.CurrentUnit == enemyUnit && enemyUnit.GetActionPointsRemaining() > 0)
            {
                ServiceLocator.Get<UnitActionSystem>().EndTurn();
            }
        }

        private IEnumerator TryAttackIfPossible(Unit enemyUnit)
        {
            BaseAction attackAction = null;
            Unit target = null;

            // Prefer ranged if this enemy has a ranged weapon/action.
            RangedAction rangedAction = enemyUnit.GetComponent<RangedAction>();
            if (rangedAction != null && rangedAction.CanExecuteAction())
            {
                target = GetClosestValidTargetForAction(enemyUnit, rangedAction);
                if (target != null)
                    attackAction = rangedAction;
            }

            // Fall back to melee.
            if (attackAction == null)
            {
                MeleeAction meleeAction = enemyUnit.GetComponent<MeleeAction>();
                if (meleeAction != null && meleeAction.CanExecuteAction())
                {
                    target = GetClosestValidTargetForAction(enemyUnit, meleeAction);
                    if (target != null)
                        attackAction = meleeAction;
                }
            }

            if (attackAction == null || target == null)
                yield break;

            bool actionComplete = false;
            UnitActionSystem uas = ServiceLocator.Get<UnitActionSystem>();

            void HandleActionComplete(object sender, EventArgs e)
            {
                actionComplete = true;
            }

            uas.OnActionCompleted += HandleActionComplete;
            uas.AiExecuteAction(attackAction, target.CurrentLayeredPosition);

            yield return new WaitUntil(() => actionComplete);

            uas.OnActionCompleted -= HandleActionComplete;
        }

        private Unit GetClosestValidTargetForAction(Unit enemyUnit, BaseAction action)
        {
            List<Vector3Int> validPositions = action.GetValidActionGridPositions();

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

                if (!validPositions.Contains(unit.CurrentLayeredPosition))
                    continue;

                int distance = PF2E_Core.GetPF2eDistance3D(
                    enemyUnit.CurrentLayeredPosition,
                    unit.CurrentLayeredPosition
                );

                if (distance > maxAttackDistanceTiles)
                    continue;

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = unit;
                }
            }

            return closest;
        }

        private bool WantsToUseRangedAttack(Unit enemyUnit)
        {
            RangedAction rangedAction = enemyUnit.GetComponent<RangedAction>();

            if (rangedAction == null)
                return false;

            if (!rangedAction.CanExecuteAction())
                return false;

            Unit rangedTarget = GetClosestValidTargetForAction(enemyUnit, rangedAction);

            return rangedTarget != null;
        }

        private IEnumerator MoveAwayFromClosestPlayer(Unit enemyUnit)
        {
            Unit closestPlayer = GetClosestPlayerUnit(enemyUnit);
            if (closestPlayer == null)
                yield break;

            GridSystem grid = ServiceLocator.Get<GridSystem>();

            Vector3Int start = enemyUnit.CurrentLayeredPosition;
            Vector3Int playerPos = closestPlayer.CurrentLayeredPosition;

            int maxMoveCost = enemyUnit.GetMaxMoveCost();

            List<Vector3Int> reachablePositions =
                Pathfinding.GetReachablePositions(start, maxMoveCost);

            Vector3Int bestDestination = start;
            int bestDistance = PF2E_Core.GetPF2eDistance3D(start, playerPos);

            foreach (Vector3Int position in reachablePositions)
            {
                if (position == start)
                    continue;

                if (grid.IsPositionOccupied(position))
                    continue;

                int distance = PF2E_Core.GetPF2eDistance3D(position, playerPos);

                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestDestination = position;
                }
            }

            if (bestDestination == start)
                yield break;

            List<Vector3Int> path = Pathfinding.FindPath(start, bestDestination);

            if (path == null || path.Count < 2)
                yield break;

            bool visualMoveComplete = false;

            enemyUnit.MoveAlongPath(path, () =>
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

        private IEnumerator TryMeleeAttack(Unit enemyUnit)
        {
            Unit target = GetClosestPlayerUnit(enemyUnit);
            if (target == null)
                yield break;

            MeleeAction meleeAction = enemyUnit.GetComponent<MeleeAction>();
            if (meleeAction == null)
                yield break;

            if (!meleeAction.CanExecuteAction())
                yield break;

            Vector3Int targetPos = target.CurrentLayeredPosition;

            if (!meleeAction.GetValidActionGridPositions().Contains(targetPos))
                yield break;

            bool actionComplete = false;

            UnitActionSystem uas = ServiceLocator.Get<UnitActionSystem>();

            void HandleActionComplete(object sender, EventArgs e)
            {
                actionComplete = true;
            }

            uas.OnActionCompleted += HandleActionComplete;
            uas.AiExecuteAction(meleeAction, targetPos);

            yield return new WaitUntil(() => actionComplete);

            uas.OnActionCompleted -= HandleActionComplete;
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