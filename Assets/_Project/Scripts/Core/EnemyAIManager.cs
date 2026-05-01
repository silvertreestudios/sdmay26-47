using System;
using System.Collections;
using System.Collections.Generic;
using TacticsGame.Actions;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Grid;
using TacticsGame.Reactions;
using UnityEngine;

namespace TacticsGame.Core
{
    public class EnemyAIManager : MonoBehaviour
    {
        public enum EnemyControlMode
        {
            AiEnabled,
            AiDisabled,
            PlayerControlsEnemy,
        }

        [Header("Enemy Control")]
        [SerializeField]
        private EnemyControlMode controlMode = EnemyControlMode.AiEnabled;

        [SerializeField]
        private float turnStartDelay = 1f;

        [SerializeField]
        private float delayBetweenActions = 0.5f;

        [SerializeField]
        private int maxAttackDistanceTiles = 6;

        [Header("Random Jumping")]
        [SerializeField]
        private bool randomJumpEnabled = true;

        [SerializeField]
        private float randomJumpChance = 0.25f;

        [SerializeField]
        private float jumpLeadTime = 0.08f;

        public EnemyControlMode ControlMode => controlMode;

        private TurnManager turnManager;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<EnemyAIManager>();
        }

        private void Start()
        {
            turnManager = ServiceLocator.Get<TurnManager>();
            turnManager.OnTurnChanged += TurnManager_OnTurnChanged;
        }

        private void Update()
        {
            // Press 'P' to cycle enemy control mode
            if (Input.GetKeyDown(KeyCode.P))
            {
                controlMode = controlMode switch
                {
                    EnemyControlMode.AiEnabled => EnemyControlMode.AiDisabled,
                    EnemyControlMode.AiDisabled => EnemyControlMode.PlayerControlsEnemy,
                    _ => EnemyControlMode.AiEnabled,
                };

                Debug.Log($"<color=orange>[ENEMY AI]</color> Mode: {controlMode}");
            }
        }

        private void TurnManager_OnTurnChanged(object sender, EventArgs e)
        {
            if (controlMode == EnemyControlMode.AiDisabled)
            {
                if (
                    turnManager.CurrentUnit != null
                    && turnManager.CurrentUnit.GetFaction() == Faction.Enemy
                )
                {
                    ServiceLocator.Get<UnitActionSystem>().EndTurn();
                }
                return;
            }

            if (controlMode != EnemyControlMode.AiEnabled)
                return;

            Unit currentUnit = turnManager.CurrentUnit;
            if (currentUnit == null || currentUnit.GetFaction() != Faction.Enemy)
                return;

            StartCoroutine(RunEnemyTurnRoutine(currentUnit));
        }

        private IEnumerator RunEnemyTurnRoutine(Unit enemyUnit)
        {
            yield return null;

            if (turnManager.CurrentUnit != enemyUnit)
                yield break;

            yield return new WaitForSeconds(turnStartDelay);

            UnitConditions conditions = enemyUnit.GetComponent<UnitConditions>();
            if (conditions != null && conditions.HasCondition(ConditionType.Unconscious))
            {
                Debug.Log($"[AI] {enemyUnit.name} is unconscious. Skipping AI turn.");
                ServiceLocator.Get<UnitActionSystem>().EndTurn();
                yield break;
            }

            int safety = 0;
            while (
                turnManager.CurrentUnit == enemyUnit
                && enemyUnit.GetActionPointsRemaining() > 0
                && safety < 5
            )
            {
                safety++;

                // Respect real-time mode changes during turn
                if (controlMode != EnemyControlMode.AiEnabled)
                    yield break;

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

            RangedAction rangedAction = enemyUnit.GetComponent<RangedAction>();
            if (rangedAction != null && rangedAction.CanExecuteAction())
            {
                target = GetClosestValidTargetForAction(enemyUnit, rangedAction);
                if (target != null)
                    attackAction = rangedAction;
            }

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

            void HandleActionComplete(object sender, EventArgs e) => actionComplete = true;

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
                if (unit == null || unit.GetFaction() != Faction.Player)
                    continue;

                var health = unit.GetComponent<IDamageable>();
                if (health != null && health.IsDead)
                    continue;

                if (!validPositions.Contains(unit.CurrentLayeredPosition))
                    continue;

                int distance = TacticsRuleset_Core.GetTacticsRulesetDistance3D(
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
            if (rangedAction == null || !rangedAction.CanExecuteAction())
                return false;
            return GetClosestValidTargetForAction(enemyUnit, rangedAction) != null;
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

            List<Vector3Int> reachablePositions = Pathfinding.GetReachablePositions(
                start,
                maxMoveCost
            );
            Vector3Int bestDestination = start;
            int bestDistance = TacticsRuleset_Core.GetTacticsRulesetDistance3D(start, playerPos);

            foreach (Vector3Int position in reachablePositions)
            {
                if (position == start || grid.IsPositionOccupied(position))
                    continue;

                int distance = TacticsRuleset_Core.GetTacticsRulesetDistance3D(position, playerPos);
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
            enemyUnit.MoveAlongPath(path, () => visualMoveComplete = true);
            yield return new WaitUntil(() => visualMoveComplete);

            bool actionCommitComplete = false;
            ServiceLocator
                .Get<UnitActionSystem>()
                .AiCommitMoveAction(() => actionCommitComplete = true);
            yield return new WaitUntil(() => actionCommitComplete);
        }

        private IEnumerator MoveTowardTarget(Unit enemyUnit, Unit target)
        {
            if (target == null)
                yield break;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3Int start = enemyUnit.CurrentLayeredPosition;
            Vector3Int targetPos = target.CurrentLayeredPosition;

            List<Vector3Int> fullPath = Pathfinding.FindPath(start, targetPos, targetPos);
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
                    break;

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
            enemyUnit.MoveAlongPath(movePath, () => visualMoveComplete = true);
            yield return new WaitUntil(() => visualMoveComplete);

            bool actionCommitComplete = false;
            ServiceLocator
                .Get<UnitActionSystem>()
                .AiCommitMoveAction(() => actionCommitComplete = true);
            yield return new WaitUntil(() => actionCommitComplete);
        }

        private Unit GetClosestPlayerUnit(Unit enemyUnit)
        {
            Unit closest = null;
            int closestDistance = int.MaxValue;

            foreach (Unit unit in UnitManager.AllUnits)
            {
                if (unit == null || unit.GetFaction() != Faction.Player)
                    continue;

                var health = unit.GetComponent<IDamageable>();
                if (health != null && health.IsDead)
                    continue;

                int distance = TacticsRuleset_Core.GetTacticsRulesetDistance3D(
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
                if (unit == null || unit.GetFaction() != Faction.Player)
                    continue;

                var health = unit.GetComponent<IDamageable>();
                if (health != null && health.IsDead)
                    continue;

                int distance = TacticsRuleset_Core.GetTacticsRulesetDistance3D(
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
