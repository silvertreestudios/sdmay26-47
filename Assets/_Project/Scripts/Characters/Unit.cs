using System;
using System.Collections.Generic;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class Unit : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private UnitStatsSO stats;

        // Public Properties
        public GridPosition CurrentGridPosition { get; private set; }

        // Physics & Movement State
        private CharacterController characterController;
        private float verticalVelocity;
        private float gravity = -9.81f;
        private float jumpHeight = 1.5f;

        // Budget is used to track how far a unit can move
        private int movementBudgetRemaining;

        // 3 actions per turn. Here is where it begins to get messy :P
        private int actionPointsRemaining;

        // Honestly theres no way we need this to be anything other than 3 but
        // Useful for debugging
        private int totalActionPointsPerTurn = 3;

        #region Action Economy
        public void StartTurn()
        {
            actionPointsRemaining = totalActionPointsPerTurn;
        }

        public void SpendActionPoint()
        {
            actionPointsRemaining--;
        }

        public void SpendActionPoints(int amount)
        {
            actionPointsRemaining -= amount;
        }

        public int GetActionPointsRemaining()
        {
            return actionPointsRemaining;
        }

        #endregion

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        #region Movement Budget
        // Called when a unit's turn begins or when it's selected for movement.
        public void StartMoveAction()
        {
            // Reset the budget to the maximum allowed for this unit.
            movementBudgetRemaining = GetMaxMoveCost();
        }

        // Call this to spend budget when moving.
        public void SpendMovement(int amount)
        {
            movementBudgetRemaining -= amount;
        }

        public int GetMovementBudgetRemaining()
        {
            return movementBudgetRemaining;
        }
        #endregion

        #region Movement Execution
        // This method is called every frame from the UnitActionSystem during FreeMovement
        public void HandleMovement(Vector3 moveDirection)
        {
            // Gravity and Grounding
            if (characterController.isGrounded && verticalVelocity < 0)
            {
                // Small downward force to keep the character stuck to the ground
                verticalVelocity = -2f;
            }

            // Apply gravity over time
            verticalVelocity += gravity * Time.deltaTime;

            // Combine horizontal and vertical motion
            Vector3 finalMoveVector = moveDirection + (Vector3.up * verticalVelocity);

            characterController.Move(finalMoveVector * Time.deltaTime);

            // Update Facing Direction
            if (moveDirection != Vector3.zero)
            {
                transform.forward = Vector3.Slerp(
                    transform.forward,
                    moveDirection,
                    Time.deltaTime * 15f
                );
            }
        }

        public void HandleJump()
        {
            // Only allow jumping if the character is on the ground
            if (characterController.isGrounded)
            {
                // Calculate the upward velocity needed to reach a specific height
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        #endregion

        #region State Management
        public int GetMoveDistanceInCells()
        {
            if (stats == null)
                return 0;
            return stats.speedInFeet / 5;
        }

        public int GetMaxMoveCost()
        {
            return GetMoveDistanceInCells() * Pathfinding.MOVE_STRAIGHT_COST;
        }

        public void SetInitialPosition(GridPosition gridPosition)
        {
            CurrentGridPosition = gridPosition;
            // The CharacterController must be temporarily disabled to teleport it.
            characterController.enabled = false;
            transform.position = GridSystem.Instance.GetWorldPosition(gridPosition);
            characterController.enabled = true;
        }

        public void FinalizeMove(GridPosition finalPosition)
        {
            GridSystem.Instance.MoveUnit(this, finalPosition);
            CurrentGridPosition = finalPosition;
        }

        #endregion

        public float GetUnitRadius()
        {
            if (characterController != null)
                return characterController.radius;
            return 0.25f;
        }

        public void SnapToGrid(Vector3 newPosition)
        {
            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = newPosition;
                characterController.enabled = true;
            }
            else
            {
                transform.position = newPosition;
            }
        }

        public int getArmorClass()
        {
            if (stats == null)
                return 10; // Default AC

            return stats.armorClass;
        }

        public int getTotalHP()
        {
            if (stats == null)
                return 0;

            return stats.TotalHP;
        }
    }
}
