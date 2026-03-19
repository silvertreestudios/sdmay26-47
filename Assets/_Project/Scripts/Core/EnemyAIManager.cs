using System;
using System.Collections;
using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using PathfinderTactics.Reactions;
using UnityEngine;

namespace PathfinderTactics.Core
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

        public EnemyControlMode ControlMode => controlMode;

        private enum State
        {
            WaitingForTurn,
            TakingTurn,
            Busy, // Waiting for an action/animation to finish
        }

        private State state;
        private float timer;

        private void Awake()
        {
            ServiceLocator.Register(this);
            state = State.WaitingForTurn;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<EnemyAIManager>();
        }

        private void Start()
        {
            ServiceLocator.Get<TurnManager>().OnTurnChanged += TurnManager_OnTurnChanged;
            if (!ServiceLocator.Get<TurnManager>().IsPlayerTurn())
            {
                state = State.TakingTurn;
                timer = 1.0f;
            }
        }

        private void Update()
        {
            // Press 'P' to cycle enemy control mode:
            // AI enabled -> AI disabled -> Player controls enemy -> ...
            if (Input.GetKeyDown(KeyCode.P))
            {
                controlMode = controlMode switch
                {
                    EnemyControlMode.AiEnabled => EnemyControlMode.AiDisabled,
                    EnemyControlMode.AiDisabled => EnemyControlMode.PlayerControlsEnemy,
                    _ => EnemyControlMode.AiEnabled,
                };

                Debug.Log($"[ENEMY AI] Enemy control mode: {controlMode}");
            }

            // If it's a player turn (or we're giving player control), do nothing here.
            if (ServiceLocator.Get<TurnManager>().IsPlayerTurn())
                return;

            // If AI is disabled, immediately end enemy turns to avoid stalling combat.
            if (controlMode == EnemyControlMode.AiDisabled)
            {
                ServiceLocator.Get<UnitActionSystem>().EndTurn();
                state = State.WaitingForTurn;
                return;
            }

            // If the player controls enemy units, do not run AI.
            if (controlMode == EnemyControlMode.PlayerControlsEnemy)
                return;

            switch (state)
            {
                case State.WaitingForTurn:
                    break;

                case State.TakingTurn:
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        state = State.Busy;
                        TakeEnemyAction(ServiceLocator.Get<UnitActionSystem>().SelectedUnit);
                    }
                    break;

                case State.Busy:
                    // Just wait for the callback from the action to set us back to TakingTurn
                    break;
            }
        }

        private void TurnManager_OnTurnChanged(object sender, EventArgs e)
        {
            if (!ServiceLocator.Get<TurnManager>().IsPlayerTurn())
            {
                // Enemy's turn.
                // If AI is enabled, pause briefly to "think". Otherwise player-control/disabled modes
                // are handled in Update().
                if (controlMode == EnemyControlMode.AiEnabled)
                {
                    state = State.TakingTurn;
                    timer = 1.0f;
                }
            }
        }

        private void TakeEnemyAction(Unit enemyUnit)
        {
            if (enemyUnit == null || enemyUnit.GetActionPointsRemaining() <= 0)
            {
                // Out of AP, end the turn
                ServiceLocator.Get<UnitActionSystem>().EndTurn();
                state = State.WaitingForTurn;
                return;
            }

            // Find the closest target
            Unit target = GetClosestPlayerUnit(enemyUnit);
            if (target == null)
            {
                // No players left alive? End turn.
                ServiceLocator.Get<UnitActionSystem>().EndTurn();
                state = State.WaitingForTurn;
                return;
            }

            // Get all possible actions the enemy can take
            BaseAction[] availableActions = enemyUnit.GetComponents<BaseAction>();

            // Filter to actions that have valid targets
            List<BaseAction> validActions = new List<BaseAction>();
            foreach (var action in availableActions)
            {
                if (action.GetValidActionGridPositions().Contains(target.CurrentGridPosition))
                {
                    validActions.Add(action);
                }
            }

            if (validActions.Count > 0)
            {
                // Attack with a random valid action
                BaseAction chosenAction = validActions[
                    UnityEngine.Random.Range(0, validActions.Count)
                ];

                Debug.Log(
                    $"[ENEMY AI] {enemyUnit.name} is using {chosenAction.GetActionName()} on {target.name}!"
                );
                enemyUnit.SpendActionPoints(chosenAction.GetActionPointsCost());

                chosenAction.TakeAction(
                    target.CurrentGridPosition,
                    () =>
                    {
                        // Callback when attack finishes
                        state = State.TakingTurn;
                        timer = 0.5f; // Short pause before next action
                    }
                );
                return;
            }

            // Not in range? Try to move closer.
            if (TryMoveTowardsTarget(enemyUnit, target))
            {
                return; // Move action started
            }

            // If we can't hit them and can't move, just end turn.
            ServiceLocator.Get<UnitActionSystem>().EndTurn();
            state = State.WaitingForTurn;
        }

        private bool TryMoveTowardsTarget(Unit enemyUnit, Unit target)
        {
            // Get all valid moves for this enemy
            List<GridPosition> validMoves = Pathfinding.GetReachableGridPositions(
                enemyUnit.CurrentGridPosition,
                enemyUnit.GetMaxMoveCost()
            );

            if (validMoves.Count == 0)
                return false;

            GridPosition bestMove = enemyUnit.CurrentGridPosition;
            int closestDistance = int.MaxValue;

            // Find the tile that gets us physically closest to the target
            foreach (GridPosition movePos in validMoves)
            {
                int dist = Pathfinding.CalculateDistance(movePos, target.CurrentGridPosition);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    bestMove = movePos;
                }
            }

            if (bestMove != enemyUnit.CurrentGridPosition)
            {
                Debug.Log($"[ENEMY AI] {enemyUnit.name} is moving towards {target.name}!");

                // We must fire the BeforeMoveEvent manually for the AI, so player Reactions trigger
                var moveEvent = new Reactions.BeforeMoveEvent(
                    enemyUnit,
                    enemyUnit.CurrentGridPosition,
                    bestMove
                );

                ServiceLocator
                    .Get<ReactionManager>()
                    .EvaluateEvent(
                        moveEvent,
                        (resolvedEvent) =>
                        {
                            if (resolvedEvent.IsCancelled)
                            {
                                // Player's Reactive Strike killed or stopped the enemy
                                enemyUnit.SnapToGrid(
                                    ServiceLocator
                                        .Get<GridSystem>()
                                        .GetWorldPosition(enemyUnit.CurrentGridPosition)
                                );
                                state = State.TakingTurn;
                                timer = 0.5f;
                            }
                            else
                            {
                                // Get the path before updating logical position
                                List<GridPosition> path = Pathfinding.FindPath(
                                    enemyUnit.CurrentGridPosition,
                                    bestMove
                                );

                                // Update the Logical Grid
                                enemyUnit.SpendActionPoints(1);
                                ServiceLocator
                                    .Get<GridSystem>()
                                    .MoveUnit(enemyUnit, enemyUnit.CurrentGridPosition, bestMove);
                                enemyUnit.FinalizeMove(bestMove);

                                // Animate the Physical Movement
                                enemyUnit.MoveAlongPath(
                                    path,
                                    () =>
                                    {
                                        // This callback runs when the walking finishes
                                        state = State.TakingTurn;
                                        timer = 0.5f;
                                    }
                                );
                            }
                        }
                    );
                return true;
            }

            return false;
        }

        private Unit GetClosestPlayerUnit(Unit enemyUnit)
        {
            Unit closest = null;
            int closestDist = int.MaxValue;

            foreach (Unit playerUnit in UnitManager.AllUnits)
            {
                if (playerUnit.GetFaction() == Faction.Player)
                {
                    var health = playerUnit.GetComponent<IDamageable>();
                    if (health != null && health.IsDead)
                        continue; // Ignore downed players

                    int dist = Pathfinding.CalculateDistance(
                        enemyUnit.CurrentGridPosition,
                        playerUnit.CurrentGridPosition
                    );
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = playerUnit;
                    }
                }
            }
            return closest;
        }
    }
}
