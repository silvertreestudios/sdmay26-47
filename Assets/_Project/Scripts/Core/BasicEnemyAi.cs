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

            Unit target = GetClosestPlayerUnit(enemyUnit);

            if (target != null && !IsNextToPlayer(enemyUnit))
            {
                yield return MoveTowardTarget(enemyUnit, target);
            }

            if (IsNextToPlayer(enemyUnit))
            {
                ServiceLocator.Get<UnitActionSystem>().EndTurn();
            }
        }

        private IEnumerator MoveTowardTarget(Unit enemyUnit, Unit target)
        {
            GridSystem grid = ServiceLocator.Get<GridSystem>();

            Vector3Int start = enemyUnit.CurrentLayeredPosition;
            Vector3Int targetPos = target.CurrentLayeredPosition;

            int maxMoveCost = enemyUnit.GetMaxMoveCost();

            List<Vector3Int> reachablePositions =
                Pathfinding.GetReachablePositions(start, maxMoveCost);

            Vector3Int bestDestination = start;
            int bestDistance = int.MaxValue;

            foreach (Vector3Int position in reachablePositions)
            {
                if (position == start)
                    continue;

                if (grid.IsPositionOccupied(position))
                    continue;

                int distance = PF2E_Core.GetPF2eDistance3D(position, targetPos);

                if (distance < bestDistance)
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


            grid.MoveUnit(enemyUnit, start, bestDestination);
            enemyUnit.FinalizeMove(bestDestination);

            bool moveComplete = false;

            enemyUnit.MoveAlongPath(path, () =>
            {
                moveComplete = true;
            });

            yield return new WaitUntil(() => moveComplete);
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