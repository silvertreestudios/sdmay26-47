using System;
using System.Collections;
using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Core
{
    public class EnemyAIManager : MonoBehaviour
    {
        public static EnemyAIManager Instance { get; private set; }

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
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            state = State.WaitingForTurn;
        }

        private void Start()
        {
            TurnManager.Instance.OnTurnChanged += TurnManager_OnTurnChanged;
            if (!TurnManager.Instance.IsPlayerTurn())
            {
                state = State.TakingTurn;
                timer = 1.0f;
            }
        }

        private void Update()
        {
            if (TurnManager.Instance.IsPlayerTurn())
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
                        TakeEnemyAction(UnitActionSystem.Instance.SelectedUnit);
                    }
                    break;

                case State.Busy:
                    // Just wait for the callback from the action to set us back to TakingTurn
                    break;
            }
        }

        private void TurnManager_OnTurnChanged(object sender, EventArgs e)
        {
            if (!TurnManager.Instance.IsPlayerTurn())
            {
                // Enemy's turn. Give them a brief pause to "think" before moving and to slow the game down a bit.
                state = State.TakingTurn;
                timer = 1.0f;
            }
        }

        private void TakeEnemyAction(Unit enemyUnit)
        {
            if (enemyUnit == null || enemyUnit.GetActionPointsRemaining() <= 0)
            {
                // Out of AP, end the turn
                UnitActionSystem.Instance.EndTurn();
                state = State.WaitingForTurn;
                return;
            }

            // Find the closest target
            Unit target = GetClosestPlayerUnit(enemyUnit);
            if (target == null)
            {
                // No players left alive? End turn.
                UnitActionSystem.Instance.EndTurn();
                state = State.WaitingForTurn;
                return;
            }

            // Are we in Melee Range?
            MeleeAction meleeAction = enemyUnit.GetComponent<MeleeAction>();
            if (
                meleeAction != null
                && meleeAction.GetValidActionGridPositions().Contains(target.CurrentGridPosition)
            )
            {
                // Attack
                Debug.Log($"[ENEMY AI] {enemyUnit.name} is attacking {target.name}!");
                enemyUnit.SpendActionPoints(meleeAction.GetActionPointsCost());

                meleeAction.TakeAction(
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
            UnitActionSystem.Instance.EndTurn();
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

                Reactions.ReactionManager.Instance.EvaluateEvent(
                    moveEvent,
                    (resolvedEvent) =>
                    {
                        if (resolvedEvent.IsCancelled)
                        {
                            // Player's Reactive Strike killed or stopped the enemy
                            enemyUnit.SnapToGrid(
                                GridSystem.Instance.GetWorldPosition(enemyUnit.CurrentGridPosition)
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
                            GridSystem.Instance.MoveUnit(
                                enemyUnit,
                                enemyUnit.CurrentGridPosition,
                                bestMove
                            );
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
                    var health = playerUnit.GetComponent<UnitHealth>();
                    if (health != null && (health.IsDead || health.IsUnconscious))
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
