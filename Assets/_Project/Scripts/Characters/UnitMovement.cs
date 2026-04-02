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
        private UnitVisuals unitVisuals;

        // State accessors for external systems
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsRising => verticalVelocity > 0.1f;
        public bool IsFalling => !IsGrounded && verticalVelocity < -0.1f;

        public event Action OnJumpStarted;
        public event Action OnLanded;

        private bool wasGroundedLastFrame;
        private Vector3 lastFramePosition; // Used to manually track velocity safely

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
            unitVisuals = GetComponentInChildren<UnitVisuals>();
        }

        private void Update()
        {
            if (isMoving)
            {
                TickPathMovement();
            }
            else
            {
                // Always apply gravity when idle so IsGrounded accurately detects the floor
                ApplyGravity();
                characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            }

            UpdateGroundedTracking();
            UpdateAnimator();

            lastFramePosition = transform.position;
        }

        private void ApplyGravity()
        {
            bool grounded = IsGrounded;

            // strong downward force keeps unit planted on slopes
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
            if (unitVisuals == null)
                return;

            // Compute velocity manually based on actual transform changes this frame.
            Vector3 worldVelocity = (transform.position - lastFramePosition) / Time.deltaTime;
            Vector3 planarVelocity = worldVelocity;
            planarVelocity.y = 0f;
            float speed = planarVelocity.magnitude;

            unitVisuals.SetSpeed(speed);
            unitVisuals.SetGrounded(IsGrounded);
            unitVisuals.SetVerticalSpeed(verticalVelocity);
        }

        // Path-following (AI)

        private void TickPathMovement()
        {
            Vector3 targetPosition = positionList[currentPositionIndex];

            // Move on XZ plane towards target
            Vector3 currentPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 targetPosXZ = new Vector3(targetPosition.x, 0, targetPosition.z);
            Vector3 moveDirectionXZ = (targetPosXZ - currentPosXZ).normalized;

            if (moveDirectionXZ != Vector3.zero)
            {
                transform.forward = Vector3.Lerp(
                    transform.forward,
                    moveDirectionXZ,
                    Time.deltaTime * rotateSpeed
                );
            }

            Vector3 stepXZ = Vector3.MoveTowards(
                currentPosXZ,
                targetPosXZ,
                moveSpeed * Time.deltaTime
            );
            Vector3 moveDelta = stepXZ - currentPosXZ;

            ApplyGravity();
            characterController.Move(moveDelta + (Vector3.up * verticalVelocity * Time.deltaTime));

            // Planar reach check
            if (Vector3.Distance(currentPosXZ, targetPosXZ) < 0.1f)
            {
                currentPositionIndex++;
                if (currentPositionIndex >= positionList.Count)
                {
                    isMoving = false;
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
            ApplyGravity();

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

            if (unitVisuals != null)
                unitVisuals.TriggerJump();
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
