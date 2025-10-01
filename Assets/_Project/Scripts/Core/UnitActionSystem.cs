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

        [SerializeField]
        private LayerMask unitLayerMask;

        private PlayerInputActions playerInputActions;
        private Unit selectedUnit;
        private GamePhase currentPhase;

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
        }

        private void OnDisable()
        {
            playerInputActions.Player.Disable();
            playerInputActions.Player.Select.performed -= OnSelectPerformed;
            playerInputActions.Player.Confirm.performed -= OnConfirmPerformed;
            playerInputActions.Player.Cancel.performed -= OnCancelPerformed;
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
                Debug.Log("Movement Confirmed. Transitioning to Action Selection.");
                if (currentPhase == GamePhase.FreeMovement)
                {
                    // Find the final grid position based on the unit's current free-floating transform.
                    GridPosition finalGridPosition = GridSystem.Instance.GetGridPosition(
                        selectedUnit.transform.position
                    );

                    // Calculate the cost to this final position from the starting point.
                    List<GridPosition> pathToFinalPos = Pathfinding.FindPath(
                        selectedUnit.CurrentGridPosition,
                        finalGridPosition
                    );
                    // int moveCost = 0;
                    if (pathToFinalPos != null)
                    {
                        // A simple way to get path cost is to recalculate it. We can optimize later if needed.
                        // AKA its probably never going to get optimized lmao cuz if if works it works lol
                        PathNode finalNode = new PathNode(finalGridPosition); // Placeholder
                        // TODO: This part of the logic needs to be fleshed out, for now we assume it's valid.
                    }

                    // Snap the unit to the center of the final cell. Will probably animate this later
                    selectedUnit.transform.position = GridSystem.Instance.GetWorldPosition(
                        finalGridPosition
                    );

                    // Update the unit's data in the grid.
                    selectedUnit.FinalizeMove(finalGridPosition);

                    // Hide movement visuals and transition state.
                    validMovePositions?.Clear();
                    Debug.Log(
                        $"Movement Confirmed at {finalGridPosition}. Transitioning to Action Selection."
                    );
                    SetPhase(GamePhase.ActionSelection);
                }
                // TODO: hide the movement range visuals here, action selection stuff, figure out
                // All the transisions and whatnot.
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
            Debug.Log(
                $"SetSelectedUnit: Setting selected unit to '{unit.gameObject.name}'. Invoking OnSelectedUnitChanged event."
            );
            selectedUnit = unit;
            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);

            selectedUnit.StartMoveAction();
            CameraController.Instance.SetFollowTarget(unit.transform);

            // Calculate and store the entire movement area for this unit
            validMovePositions = Pathfinding.GetReachableGridPositions(
                selectedUnit.CurrentGridPosition,
                selectedUnit.GetMaxMoveCost()
            );
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            // If we are in a state where a unit is selected, cancel returns to the main selection state.
            if (currentPhase == GamePhase.FreeMovement || currentPhase == GamePhase.ActionSelection)
            {
                ClearSelectedUnit();
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
            Debug.Log($"HandleFreeMovement: In Free Movement state for '{selectedUnit.name}'.");
            // Read Input
            Vector2 inputMoveDir = playerInputActions.Player.Move.ReadValue<Vector2>();
            if (inputMoveDir == Vector2.zero)
                return;

            // Calculate World-Space Movement Vector (relative to camera)
            // TODO: maybe make more dynamic based on unit stats
            float moveSpeed = 5f;
            var cameraTransform = Camera.main.transform;
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = (forward * inputMoveDir.y + right * inputMoveDir.x).normalized;
            Vector3 moveVector = moveDirection * moveSpeed * Time.deltaTime;

            // Calculate the proposed next position
            Vector3 proposedPosition = selectedUnit.transform.position + moveVector;

            // Check if the proposed position is within the valid move area.
            GridPosition proposedGridPos = GridSystem.Instance.GetGridPosition(proposedPosition);

            if (validMovePositions.Contains(proposedGridPos))
            {
                // The move is valid.
                selectedUnit.transform.position = proposedPosition;

                // Make the unit face the direction of movement
                // Will debug when we have a better model than the bean lol
                if (moveDirection != Vector3.zero)
                {
                    selectedUnit.transform.forward = Vector3.Slerp(
                        selectedUnit.transform.forward,
                        moveDirection,
                        Time.deltaTime * 15f
                    );
                }
            }
            // If the proposed position is NOT in the valid area, we do nothing.
            // This creates a wall at the boundary of the movement zone.
        }

        public void SetPhase(GamePhase newPhase)
        {
            this.currentPhase = newPhase;
        }
    }
}
