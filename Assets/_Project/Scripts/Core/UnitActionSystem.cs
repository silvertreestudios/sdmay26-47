using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PathfinderTactics.Core
{
    public class UnitActionSystem : MonoBehaviour
    {
        public static UnitActionSystem Instance { get; private set; }

        public event EventHandler OnSelectedUnitChanged;
        public event EventHandler OnActionStarted;
        public event EventHandler OnActionCompleted;

        [SerializeField]
        private LayerMask unitLayerMask;

        [SerializeField]
        private LayerMask groundLayerMask;

        private PlayerInputActions playerInputActions;
        private Unit selectedUnit;
        public GamePhase currentPhase;

        // TODO: update valid move positions to actually work properly.
        private List<GridPosition> validMovePositions;

        public Unit SelectedUnit => selectedUnit;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            playerInputActions = new PlayerInputActions();
            currentPhase = GamePhase.UnitSelection;
        }

        private void OnEnable()
        {
            playerInputActions.Player.Enable();
            playerInputActions.Player.Select.performed += OnSelectPerformed;
            playerInputActions.Player.Confirm.performed += OnConfirmPerformed;
            playerInputActions.Player.Cancel.performed += OnCancelPerformed;
            playerInputActions.Player.Jump.performed += OnJumpPerformed;
            playerInputActions.Player.OpenMenu.performed += OnOpenMenuPerformed;
        }

        private void OnDisable()
        {
            playerInputActions.Player.Disable();
            playerInputActions.Player.Select.performed -= OnSelectPerformed;
            playerInputActions.Player.Confirm.performed -= OnConfirmPerformed;
            playerInputActions.Player.Cancel.performed -= OnCancelPerformed;
            playerInputActions.Player.Jump.performed -= OnJumpPerformed;
            playerInputActions.Player.OpenMenu.performed -= OnOpenMenuPerformed;
        }

        private void Update()
        {
            switch (currentPhase)
            {
                case GamePhase.UnitSelection:
                    // TODO: In the future, this could handle hovering over units to show info/stats.
                    break;
                case GamePhase.FreeMovement:
                    HandleFreeMovement();
                    break;
                case GamePhase.ActionSelection:
                    // TODO: Handle input for bringing up an action menu (ex: Attack, Skill, etc).
                    break;
                case GamePhase.Busy:
                    // Do nothing while an action is executing.
                    break;
            }
        }

        private void OnConfirmPerformed(InputAction.CallbackContext context)
        {
            if (currentPhase == GamePhase.FreeMovement)
            {
                // TODO:::: Big todo. Add terrain, climbing, other unit interactions, etc
                // Lock in the unit's position and move to the next phase
                // Debug.Log("Movement Confirmed. Transitioning to Action Selection.");

                // Check if we actually moved from the last point
                GridPosition currentPos = GridSystem.Instance.GetGridPosition(
                    selectedUnit.transform.position
                );

                // If we haven't moved, treat Confirm as open menu
                if (currentPos == selectedUnit.CurrentGridPosition)
                {
                    SetPhase(GamePhase.ActionSelection);
                    return;
                }

                // Otherwise, commit the move as an action
                CommitMoveAction();
            }
            // TODO: hide the movement range visuals here, action selection stuff, figure out
            // All the transisions and whatnot.
        }

        private void OnOpenMenuPerformed(InputAction.CallbackContext context)
        {
            if (selectedUnit == null)
                return;

            if (currentPhase == GamePhase.FreeMovement)
            {
                selectedUnit.SnapToGrid(
                    GridSystem.Instance.GetWorldPosition(selectedUnit.CurrentGridPosition)
                );

                // Stop moving, open menu
                SetPhase(GamePhase.ActionSelection);
            }
            else if (currentPhase == GamePhase.ActionSelection)
            {
                // Close menu, return to moving
                SetPhase(GamePhase.FreeMovement);
            }
        }

        public void EndTurn()
        {
            if (selectedUnit != null)
            {
                Debug.Log($"{selectedUnit.name} ends their turn.");
                ClearSelectedUnit(); // Deselect the unit and return to the UnitSelection phase
            }
        }

        private void OnSelectPerformed(InputAction.CallbackContext context)
        {
            // check if the player clicked on a unit.
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, unitLayerMask))
            {
                Unit unit = hit.transform.GetComponentInParent<Unit>();
                if (unit != null)
                {
                    // We clicked on a unit.

                    // If it's the same unit we already have selected, do nothing.
                    if (unit == selectedUnit)
                    {
                        return;
                    }

                    // If it's a different unit, select it.
                    SetSelectedUnit(unit);

                    // enter the FreeMovement phase for selected unit.
                    currentPhase = GamePhase.FreeMovement;
                    return;
                }
            }

            // If we reach this point, we did NOT click on a unit.
            // TODO: Handle other actions based on the current phase.
        }

        private void SetSelectedUnit(Unit unit)
        {
            // Debug.Log(
            //     $"SetSelectedUnit: Setting selected unit to '{unit.gameObject.name}'. Invoking OnSelectedUnitChanged event."
            // );
            selectedUnit = unit;
            selectedUnit.StartTurn();
            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);

            // selectedUnit.StartMoveAction();
            CameraController.Instance.SetFollowTarget(unit.transform);

            // Calculate and store the entire movement area for this unit
            validMovePositions = Pathfinding.GetReachableGridPositions(
                selectedUnit.CurrentGridPosition,
                selectedUnit.GetMaxMoveCost()
            );
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (currentPhase == GamePhase.ActionSelection)
            {
                // Go back to moving
                SetPhase(GamePhase.FreeMovement);
            }
            else if (currentPhase == GamePhase.FreeMovement)
            {
                // Deselect unit
                ClearSelectedUnit();
            }
        }

        private void CommitMoveAction()
        {
            SetPhase(GamePhase.Busy);

            // Finalize Position
            GridPosition finalGridPosition = GridSystem.Instance.GetGridPosition(
                selectedUnit.transform.position
            );

            foreach (Unit other in UnitManager.AllUnits)
            {
                if (other == this)
                    continue;

                if (other.CurrentGridPosition.x == finalGridPosition.x &&
                    other.CurrentGridPosition.z == finalGridPosition.z)
                {
                    SetPhase(GamePhase.FreeMovement);
                    return;
                }
            }


            selectedUnit.SnapToGrid(GridSystem.Instance.GetWorldPosition(finalGridPosition));

            selectedUnit.FinalizeMove(finalGridPosition);

            // Spend AP
            SpendActionAndContinue(1);
        }

        public void SpendActionAndContinue(int cost)
        {
            if (selectedUnit == null)
                return;

            selectedUnit.SpendActionPoints(cost);

            if (selectedUnit.GetActionPointsRemaining() > 0)
            {
                // The main turn loop
                // We still have AP. Recalculate range from new spot and go back to FreeMovement.
                validMovePositions = Pathfinding.GetReachableGridPositions(
                    selectedUnit.CurrentGridPosition,
                    selectedUnit.GetMaxMoveCost()
                );

                // UI updates, Range Visualizer updates
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);

                SetPhase(GamePhase.FreeMovement);
            }
            else
            {
                // Out of AP
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                EndTurn();
            }
        }

        private void ClearSelectedUnit()
        {
            // Tell the camera to unlock
            CameraController.Instance.ClearFollowTarget();

            // TODO: Clear the unit reference and notify listeners (like the selector visual)
            selectedUnit = null;
            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
            // Clear the stored move positions
            validMovePositions?.Clear();
            SetPhase(GamePhase.UnitSelection);
        }

        private void HandleFreeMovement()
        {
            // Read input and move based on camera
            Vector2 inputMoveDir = playerInputActions.Player.Move.ReadValue<Vector2>();

            // If no input, let physics update (gravity etc)
            if (inputMoveDir == Vector2.zero)
            {
                selectedUnit.HandleMovement(Vector3.zero);
                return;
            }

            float moveSpeed = 5f;
            var cameraTransform = Camera.main.transform;
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection =
                (forward * inputMoveDir.y + right * inputMoveDir.x).normalized * moveSpeed;
            Vector3 proposedPosition =
                selectedUnit.transform.position + moveDirection * Time.deltaTime;

            GridSystem gridSystem = GridSystem.Instance;
            GridPosition currentGridPos = gridSystem.GetGridPosition(
                selectedUnit.transform.position
            );
            Vector3 cellCenterWorld = gridSystem.GetWorldPosition(currentGridPos);
            float cellSize = gridSystem.CellSize;
            float unitRadius = selectedUnit.GetUnitRadius();

            GridPosition northPos = new GridPosition(currentGridPos.x, currentGridPos.z + 1);
            if (!IsValidMovePosition(northPos))
            {
                float maxZ = cellCenterWorld.z + (cellSize * 0.5f) - unitRadius;
                if (proposedPosition.z > maxZ)
                {
                    proposedPosition.z = maxZ;
                }
            }

            GridPosition southPos = new GridPosition(currentGridPos.x, currentGridPos.z - 1);
            if (!IsValidMovePosition(southPos))
            {
                float minZ = cellCenterWorld.z - (cellSize * 0.5f) + unitRadius;
                if (proposedPosition.z < minZ)
                {
                    proposedPosition.z = minZ;
                }
            }

            GridPosition eastPos = new GridPosition(currentGridPos.x + 1, currentGridPos.z);
            if (!IsValidMovePosition(eastPos))
            {
                float maxX = cellCenterWorld.x + (cellSize * 0.5f) - unitRadius;
                if (proposedPosition.x > maxX)
                {
                    proposedPosition.x = maxX;
                }
            }

            GridPosition westPos = new GridPosition(currentGridPos.x - 1, currentGridPos.z);
            if (!IsValidMovePosition(westPos))
            {
                float minX = cellCenterWorld.x - (cellSize * 0.5f) + unitRadius;
                if (proposedPosition.x < minX)
                {
                    proposedPosition.x = minX;
                }
            }

            GridPosition targetGridPos = gridSystem.GetGridPosition(proposedPosition);
            if (!validMovePositions.Contains(targetGridPos))
            {
                selectedUnit.HandleMovement(Vector3.zero);
                return;
            }

            // Execute Move
            Vector3 clampedMoveDir =
                (proposedPosition - selectedUnit.transform.position).normalized * moveSpeed;

            if (Vector3.Distance(proposedPosition, selectedUnit.transform.position) < 0.001f)
            {
                selectedUnit.HandleMovement(Vector3.zero);
            }
            else
            {
                Vector3 moveDelta = proposedPosition - selectedUnit.transform.position;
                Vector3 finalVelocity = moveDelta / Time.deltaTime;
                selectedUnit.HandleMovement(finalVelocity);
            }
        }

        // Helper method to keep code clean
        private bool IsValidMovePosition(GridPosition pos)
        {
            return validMovePositions != null && validMovePositions.Contains(pos);
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            if (currentPhase == GamePhase.FreeMovement)
            {
                selectedUnit.HandleJump();
            }
        }

        public void SetPhase(GamePhase newPhase)
        {
            this.currentPhase = newPhase;
        }
    }
}
