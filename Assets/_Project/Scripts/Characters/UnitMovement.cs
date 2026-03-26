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

        [Header("Jump Tuning")]
        [SerializeField]
        [Tooltip("Peak height in Unity units.")]
        private float jumpHeight = 2.4f;

        [SerializeField]
        [Tooltip("Gravity magnitude during ascent. Higher = snappier takeoff.")]
        private float riseGravity = 35f;

        [SerializeField]
        [Tooltip("Multiplier applied to riseGravity during descent. >1 = faster fall.")]
        private float fallGravityMultiplier = 1.6f;

        [SerializeField]
        [Tooltip("Maximum downward speed to prevent runaway velocity.")]
        private float maxFallSpeed = 30f;

        [SerializeField]
        [Tooltip("Downward force applied while grounded to keep the unit planted on slopes.")]
        private float groundStickForce = 10f;

        [SerializeField]
        [Tooltip("Seconds after leaving ground where a jump input is still accepted.")]
        private float coyoteTime = 0.08f;

        [SerializeField]
        [Tooltip("Seconds a jump press is buffered before the unit lands.")]
        private float jumpBufferTime = 0.1f;

        private float verticalVelocity;
        private float lastGroundedTime = -999f;
        private float lastJumpRequestTime = -999f;
        private bool jumpedThisAirTime;

        // Animator integration
        private Animator animator;
        private static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int AnimVerticalSpeed = Animator.StringToHash("VerticalSpeed");
        private static readonly int AnimJumpTrigger = Animator.StringToHash("Jump");

        // State accessors for external systems
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsRising => verticalVelocity > 0.1f;
        public bool IsFalling => !IsGrounded && verticalVelocity < -0.1f;

        public event Action OnJumpStarted;
        public event Action OnLanded;

        private bool wasGroundedLastFrame;

        // Path-following state
        private List<Vector3> positionList;
        private int currentPositionIndex;
        private Action onMoveComplete;
        private bool isMoving = false;

        private int movementBudgetRemaining;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            unit = GetComponent<Unit>();
            animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (isMoving)
            {
                TickPathMovement();
                return;
            }

            UpdateGroundedTracking();
            UpdateAnimator();
        }

        private void UpdateGroundedTracking()
        {
            bool grounded = IsGrounded;

            if (grounded)
                lastGroundedTime = Time.time;

            if (grounded && !wasGroundedLastFrame && verticalVelocity <= 0f)
            {
                OnLanded?.Invoke();
                jumpedThisAirTime = false;
            }

            wasGroundedLastFrame = grounded;
        }

        private void UpdateAnimator()
        {
            if (animator == null)
                return;

            animator.SetBool(AnimIsGrounded, IsGrounded);
            animator.SetFloat(AnimVerticalSpeed, verticalVelocity);
        }

        // Path-following (AI)

        private void TickPathMovement()
        {
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

        public void MoveAlongPath(List<Vector3Int> path, Action onComplete)
        {
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            positionList = new List<Vector3>();
            foreach (Vector3Int pos in path)
            {
                positionList.Add(grid.GetWorldPosition(pos));
            }

            currentPositionIndex = 0;
            onMoveComplete = onComplete;
            isMoving = true;
        }

        // Free movement (player-controlled)

        public void StartMoveAction()
        {
            if (unit == null)
                unit = GetComponent<Unit>();

            if (unit == null)
                return;

            movementBudgetRemaining = unit.GetMaxMoveCost();
        }

        public void SpendMovement(int amount)
        {
            movementBudgetRemaining -= amount;
        }

        public void HandleMovement(Vector3 moveDirection)
        {
            bool grounded = IsGrounded;

            // strong downward force keeps unit planted on slopes
            // and prevents micro-bouncing after landing.
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -groundStickForce;
            }

            // Asymmetric gravity: heavier on the way down.
            float currentGravity =
                verticalVelocity > 0f ? riseGravity : riseGravity * fallGravityMultiplier;

            verticalVelocity -= currentGravity * Time.deltaTime;

            // Terminal velocity clamp
            if (verticalVelocity < -maxFallSpeed)
                verticalVelocity = -maxFallSpeed;

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

        // Jump

        public void HandleJump()
        {
            lastJumpRequestTime = Time.time;
            TryExecuteJump();
        }

        private void TryExecuteJump()
        {
            if (jumpedThisAirTime)
                return;

            bool withinCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
            bool withinBuffer = (Time.time - lastJumpRequestTime) <= jumpBufferTime;
            bool canJump = (IsGrounded || withinCoyote) && withinBuffer;

            if (!canJump)
                return;

            // v = sqrt(2 * g * h)
            verticalVelocity = Mathf.Sqrt(2f * riseGravity * jumpHeight);
            jumpedThisAirTime = true;
            lastJumpRequestTime = -999f;

            OnJumpStarted?.Invoke();

            if (animator != null)
                animator.SetTrigger(AnimJumpTrigger);
        }

        // Called from HandleMovement's ground tracking via Update loop.
        // If the player pressed jump just before landing, the buffer catches it.
        private void LateUpdate()
        {
            if (!isMoving && IsGrounded && !jumpedThisAirTime)
            {
                if ((Time.time - lastJumpRequestTime) <= jumpBufferTime)
                    TryExecuteJump();
            }
        }

        // Utility

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

            verticalVelocity = 0f;
            jumpedThisAirTime = false;
        }
    }
}
