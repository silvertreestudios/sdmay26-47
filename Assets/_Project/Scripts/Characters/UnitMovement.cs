using System;
using System.Collections.Generic;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class UnitMovement : MonoBehaviour
    {
        private CharacterController characterController;
        private Unit unit;

        [Header("Movement Settings")]
        [SerializeField]
        private float moveSpeed = 7f;

        [SerializeField]
        private float rotateSpeed = 15f;

        [SerializeField]
        private float jumpHeight = 1.5f;

        private float verticalVelocity;
        private float gravity = -9.81f;

        private List<Vector3> positionList;
        private int currentPositionIndex;
        private Action onMoveComplete;
        private bool isMoving = false;

        private int movementBudgetRemaining;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            unit = GetComponent<Unit>();
        }

        private void Update()
        {
            if (!isMoving)
                return;

            Vector3 targetPosition = positionList[currentPositionIndex];
            Vector3 moveDirection = (targetPosition - transform.position).normalized;

            if (moveDirection != Vector3.zero)
            {
                transform.forward = Vector3.Lerp(
                    transform.forward,
                    moveDirection,
                    Time.deltaTime * rotateSpeed
                );
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                currentPositionIndex++;
                if (currentPositionIndex >= positionList.Count)
                {
                    isMoving = false;
                    transform.position = targetPosition;
                    onMoveComplete?.Invoke();
                }
            }
        }

        public void MoveAlongPath(List<GridPosition> path, Action onComplete)
        {
            positionList = new List<Vector3>();
            foreach (GridPosition pos in path)
            {
                positionList.Add(ServiceLocator.Get<GridSystem>().GetWorldPosition(pos));
            }

            currentPositionIndex = 0;
            onMoveComplete = onComplete;
            isMoving = true;
        }

        public void StartMoveAction()
        {
            movementBudgetRemaining = unit.GetMaxMoveCost();
        }

        public void SpendMovement(int amount)
        {
            movementBudgetRemaining -= amount;
        }

        public void HandleMovement(Vector3 moveDirection)
        {
            if (characterController.isGrounded && verticalVelocity < 0)
            {
                verticalVelocity = -5f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            Vector3 finalMoveVector = moveDirection + (Vector3.up * verticalVelocity);

            characterController.Move(finalMoveVector * Time.deltaTime);

            if (moveDirection != Vector3.zero)
            {
                transform.forward = Vector3.Slerp(
                    transform.forward,
                    moveDirection,
                    Time.deltaTime * rotateSpeed
                );
            }
        }

        public void HandleJump()
        {
            if (characterController.isGrounded)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

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
    }
}
