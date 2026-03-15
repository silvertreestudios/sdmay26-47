using System;
using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
using PathfinderTactics.Reactions;
using PathfinderTactics.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// TODO: CURRENT KNOWN BUGS:
// - PATHFINDING BREAKS WHEN UNIT IS BLOCKED IN ON 2 SIDES, ALLOWS DIAGONAL MOVEMENT
// - CAMERA GOES UPSIDE DOWN AND THROUGH WALLS (Not a bug but should add limits to camera movement)
// - RANGE DOES NOT ACCOUNT FOR DIAGONAL ATTACKS (need to test weapon lenght/ranges)
// - WHEN STARTING GAME, BLUE TILES SIGNIFYING MOVEMENT RANGE DONT APPEAR UNTIL ACTION POINT IS USED.

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
        private BaseAction selectedAction;

        // MOVEMENT STATE
        private List<GridPosition> validMovePositions;

        private GridPosition currentCursorGridPosition;

        public Unit SelectedUnit => selectedUnit;

        [Header("UI References")]
        [SerializeField]
        private Transform gridCursorVisual;
        private float cursorMoveTimer;

        private void Awake()
        {
            // Hide cursor initially
            if (gridCursorVisual != null)
                gridCursorVisual.gameObject.SetActive(false);
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            playerInputActions = new PlayerInputActions();
            SetPhase(GamePhase.UnitSelection);
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
            // Block if over UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (!TurnManager.Instance.IsPlayerTurn())
                return;

            switch (currentPhase)
            {
                case GamePhase.UnitSelection:
                    break;
                case GamePhase.FreeMovement:
                    HandleFreeMovement();
                    break;
                case GamePhase.ActionSelection:
                    break;
                case GamePhase.ActionTargeting:
                    HandleCursorMovement();
                    // TODO: Add target highlighting here later
                    break;
                case GamePhase.Busy:
                    break;
            }
        }

        // INPUTS

        public void ForceSelectUnit(Unit unit)
        {
            SetSelectedUnit(unit);

            if (unit.GetFaction() == Faction.Enemy)
            {
                // It's the AI's turn
                // Lock Player Input (Set phase to Busy or maybe an 'AI' phase)
                SetPhase(GamePhase.Busy);
                // TODO: Trigger AI (will implement this in next)
                Debug.Log("AI Turn Started. Player controls locked.");
                // For now, since there is no AI, just automatically end their turn
            }
            else
            {
                // Player's turn
                SetPhase(GamePhase.FreeMovement);
            }
        }

        private void OnSelectPerformed(InputAction.CallbackContext context)
        {
            // PREVENT UI CLICK-THROUGH CRASH
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // NOTE: removed the "ActionTargeting" check here.
            // Clicking (Select) now only selects units.
            // Pressing E (Confirm) executes attacks.

            if (currentPhase == GamePhase.UnitSelection)
            {
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, unitLayerMask))
                {
                    Unit unit = hit.transform.GetComponentInParent<Unit>();
                    if (unit != null)
                    {
                        if (unit == selectedUnit)
                            return;
                        SetSelectedUnit(unit);
                    }
                }
            }
        }

        public BaseAction GetSelectedAction() => selectedAction;

        public void SetSelectedAction(BaseAction action)
        {
            if (!action.CanExecuteAction())
            {
                Debug.Log(
                    $"<color=red>Action '{action.GetActionName()}' is currently blocked by a condition!</color>"
                );
                return;
            }

            selectedAction = action;
            SetPhase(GamePhase.ActionTargeting);

            // Initialize Cursor at Unit's feet
            currentCursorGridPosition = selectedUnit.CurrentGridPosition;

            if (gridCursorVisual != null)
            {
                gridCursorVisual.gameObject.SetActive(true);
                UpdateCursorVisual();

                // Lock camera to the cursor
                CameraController.Instance.SetFollowTarget(gridCursorVisual);
            }
        }

        private void OnConfirmPerformed(InputAction.CallbackContext context)
        {
            if (!TurnManager.Instance.IsPlayerTurn())
                return;
            // Case 1: Moving
            if (currentPhase == GamePhase.FreeMovement)
            {
                CommitMoveAction();
            }
            // Case 2: Action
            else if (currentPhase == GamePhase.ActionTargeting)
            {
                // use the Cursor Position instead of Mouse Position
                TryExecuteActionAtGridPos(currentCursorGridPosition);
            }
        }

        private void OnOpenMenuPerformed(InputAction.CallbackContext context)
        {
            if (!TurnManager.Instance.IsPlayerTurn())
                return;
            if (currentPhase == GamePhase.FreeMovement)
            {
                // Snap unit to the grid cell they're currently over
                if (selectedUnit != null)
                {
                    GridPosition cellTheyreOver = GridSystem.Instance.GetGridPosition(
                        selectedUnit.transform.position
                    );
                    selectedUnit.SnapToGrid(GridSystem.Instance.GetWorldPosition(cellTheyreOver));
                }

                // Commit the move, and IF they survive, open the menu!
                CommitMoveAction(() =>
                {
                    if (selectedUnit.GetActionPointsRemaining() > 0)
                    {
                        Debug.Log("Opening Menu...");
                        SetPhase(GamePhase.ActionSelection);
                    }
                    else
                    {
                        EndTurn();
                    }
                });
                return;
            }
            else if (currentPhase == GamePhase.ActionSelection)
            {
                Debug.Log("Closing Menu...");
                SetPhase(GamePhase.FreeMovement);
                return;
            }
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (currentPhase == GamePhase.ActionTargeting)
            {
                if (gridCursorVisual != null)
                    gridCursorVisual.gameObject.SetActive(false); // Hide cursor
                if (selectedUnit != null)
                    CameraController.Instance.SetFollowTarget(selectedUnit.transform);
                SetPhase(GamePhase.ActionSelection);
            }
            else if (currentPhase == GamePhase.ActionSelection)
                SetPhase(GamePhase.FreeMovement);
            else if (currentPhase == GamePhase.FreeMovement)
                ClearSelectedUnit();
        }

        private void HandleFreeMovement()
        {
            if (selectedUnit == null || !TurnManager.Instance.IsPlayerTurn())
                return;

            // Condition Blockers
            var conditions = selectedUnit.GetComponent<UnitConditions>();
            if (conditions != null)
            {
                // Are they completely out of action?
                if (
                    conditions.IsDead()
                    || conditions.HasCondition(ConditionType.Unconscious)
                    || conditions.GetConditionValue(ConditionType.Stunned) > 0
                )
                {
                    selectedUnit.HandleMovement(Vector3.zero);
                    return;
                }

                // Are they tied down or lying on the floor?
                if (!conditions.CanMove() || conditions.HasCondition(ConditionType.Prone))
                {
                    // They might be trying to press WASD, but we force their velocity to zero
                    selectedUnit.HandleMovement(Vector3.zero);
                    return;
                }
            }

            Vector2 inputMoveDir = playerInputActions.Player.Move.ReadValue<Vector2>();

            if (inputMoveDir == Vector2.zero)
            {
                selectedUnit.HandleMovement(Vector3.zero);
                return;
            }

            // TODO: Find best speed value
            float moveSpeed = 7f;
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
            // proposedPosition.y = 0;
            GridSystem gridSystem = GridSystem.Instance;
            GridPosition currentGridPos = gridSystem.GetGridPosition(
                selectedUnit.transform.position
            );
            Vector3 cellCenterWorld = gridSystem.GetWorldPosition(currentGridPos);
            float cellSize = gridSystem.CellSize;
            float unitRadius = selectedUnit.GetUnitRadius();

            // Boundary Checks
            GridPosition northPos = new GridPosition(currentGridPos.x, currentGridPos.z + 1);
            if (!IsValidMovePosition(northPos))
            {
                float maxZ = cellCenterWorld.z + (cellSize * 0.5f) - unitRadius;
                if (proposedPosition.z > maxZ)
                    proposedPosition.z = maxZ;
            }

            GridPosition southPos = new GridPosition(currentGridPos.x, currentGridPos.z - 1);
            if (!IsValidMovePosition(southPos))
            {
                float minZ = cellCenterWorld.z - (cellSize * 0.5f) + unitRadius;
                if (proposedPosition.z < minZ)
                    proposedPosition.z = minZ;
            }

            GridPosition eastPos = new GridPosition(currentGridPos.x + 1, currentGridPos.z);
            if (!IsValidMovePosition(eastPos))
            {
                float maxX = cellCenterWorld.x + (cellSize * 0.5f) - unitRadius;
                if (proposedPosition.x > maxX)
                    proposedPosition.x = maxX;
            }

            GridPosition westPos = new GridPosition(currentGridPos.x - 1, currentGridPos.z);
            if (!IsValidMovePosition(westPos))
            {
                float minX = cellCenterWorld.x - (cellSize * 0.5f) + unitRadius;
                if (proposedPosition.x < minX)
                    proposedPosition.x = minX;
            }

            GridPosition targetGridPos = gridSystem.GetGridPosition(proposedPosition);
            if (!validMovePositions.Contains(targetGridPos))
            {
                selectedUnit.HandleMovement(Vector3.zero);
                return;
            }

            // Execute Move
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

        private void HandleCursorMovement()
        {
            cursorMoveTimer -= Time.deltaTime;

            Vector2 input = playerInputActions.Player.Move.ReadValue<Vector2>();

            if (input != Vector2.zero && cursorMoveTimer <= 0f)
            {
                cursorMoveTimer = 0.15f; // Cooldown

                // Get Camera directions (flattened to the floor)
                Transform cameraTransform = Camera.main.transform;
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();

                // Calculate intended world direction based on input
                Vector3 moveDirWorld = (forward * input.y + right * input.x).normalized;

                // Snap that world direction to the closest Grid Axis (X or Z)
                int moveX = 0;
                int moveZ = 0;

                // Whichever axis is stronger pull gets the movement
                if (Mathf.Abs(moveDirWorld.x) > Mathf.Abs(moveDirWorld.z))
                {
                    moveX = moveDirWorld.x > 0 ? 1 : -1;
                }
                else
                {
                    moveZ = moveDirWorld.z > 0 ? 1 : -1;
                }

                GridPosition newPos = new GridPosition(
                    currentCursorGridPosition.x + moveX,
                    currentCursorGridPosition.z + moveZ
                );

                // Validate and Apply
                if (GridSystem.Instance.IsValidGridPosition(newPos))
                {
                    // Is it within the weapon's reach?
                    if (selectedAction.GetActionRangeGridPositions().Contains(newPos))
                    {
                        currentCursorGridPosition = newPos;
                        UpdateCursorVisual();
                    }
                    else
                    {
                        // TODO: Play error sound or something here because they hit the edge of their range
                    }
                }
            }
            else if (input == Vector2.zero)
            {
                cursorMoveTimer = 0f; // Reset immediately on key release
            }
        }

        private void UpdateCursorVisual()
        {
            if (gridCursorVisual != null)
            {
                gridCursorVisual.position = GridSystem.Instance.GetWorldPosition(
                    currentCursorGridPosition
                );

                // Tell the cursor prefab to change materials if hovering over an enemy
                GridCursor cursorScript = gridCursorVisual.GetComponent<GridCursor>();
                if (cursorScript != null && selectedAction != null)
                {
                    bool isValidTarget = selectedAction
                        .GetValidActionGridPositions()
                        .Contains(currentCursorGridPosition);
                    cursorScript.SetValidState(isValidTarget);
                }
                Unit unitAtCursor = GridSystem.Instance.GetUnitAt(currentCursorGridPosition);
                if (unitAtCursor != null)
                {
                    UnitTooltipUI.Instance.Show(unitAtCursor);
                }
                else
                {
                    UnitTooltipUI.Instance.Hide();
                }
            }
        }

        // Helper method to keep code clean
        private bool IsValidMovePosition(GridPosition pos)
        {
            return validMovePositions != null && validMovePositions.Contains(pos);
        }

        private void TryExecuteActionAtGridPos(GridPosition targetPos)
        {
            if (selectedAction == null)
                return;

            // Validation Check
            if (!selectedAction.GetValidActionGridPositions().Contains(targetPos))
            {
                Debug.Log("Invalid Target! Cannot attack here.");
                // TODO: Trigger a Buzzer UI sound or something here
                return;
            }

            // Lock State & Hide Visuals
            SetPhase(GamePhase.Busy);
            if (gridCursorVisual != null)
                gridCursorVisual.gameObject.SetActive(false);
            if (selectedUnit != null)
                CameraController.Instance.SetFollowTarget(selectedUnit.transform);

            // Consume AP
            selectedUnit.SpendActionPoints(selectedAction.GetActionPointsCost());

            // Trigger the Action (MeleeAction handles the math and damage)
            selectedAction.TakeAction(
                targetPos,
                () =>
                {
                    // This callback fires when MeleeAction's State.Cooloff finishes
                    OnActionCompleted?.Invoke(this, EventArgs.Empty);
                    CheckTurnEnd();
                }
            );
        }

        private void CommitMoveAction(Action onComplete = null)
        {
            if (selectedUnit == null)
                return;

            // Grid Commitment Blockers
            var conditions = selectedUnit.GetComponent<UnitConditions>();
            if (conditions != null)
            {
                if (!conditions.CanMove())
                {
                    Debug.Log(
                        $"<color=orange>{selectedUnit.name} cannot move! They are Immobilized/Grabbed/Restrained.</color>"
                    );
                    onComplete?.Invoke();
                    return;
                }

                if (conditions.HasCondition(ConditionType.Prone))
                {
                    Debug.Log(
                        $"<color=orange>{selectedUnit.name} cannot Stride while Prone. They must Stand first!</color>"
                    );
                    onComplete?.Invoke();
                    return;
                }
            }

            GridPosition currentPos = GridSystem.Instance.GetGridPosition(
                selectedUnit.transform.position
            );

            if (currentPos != selectedUnit.CurrentGridPosition)
            {
                if (selectedUnit.GetActionPointsRemaining() < 1)
                {
                    Debug.Log("Not enough AP to Stride!");
                    selectedUnit.SnapToGrid(
                        GridSystem.Instance.GetWorldPosition(selectedUnit.CurrentGridPosition)
                    );
                    return;
                }

                // Lock the game state
                SetPhase(GamePhase.Busy);

                int distanceX = Mathf.Abs(currentPos.x - selectedUnit.CurrentGridPosition.x);
                int distanceZ = Mathf.Abs(currentPos.z - selectedUnit.CurrentGridPosition.z);
                int totalDistance = Mathf.Max(distanceX, distanceZ);

                bool isAutoStep = totalDistance == 1;

                if (isAutoStep)
                {
                    Debug.Log(
                        "<color=yellow>Unit moved exactly 1 tile. Auto-converting Stride to Step.</color>"
                    );
                }

                BeforeMoveEvent moveEvent = new BeforeMoveEvent(
                    selectedUnit,
                    selectedUnit.CurrentGridPosition,
                    currentPos,
                    isAutoStep
                );

                // Hand it to the Reaction Manager
                ReactionManager.Instance.EvaluateEvent(
                    moveEvent,
                    (resolvedEvent) =>
                    {
                        // This block executes AFTER all reactions are totally finished

                        if (resolvedEvent.IsCancelled)
                        {
                            // A reaction killed the unit or rooted them in place. Snap them back
                            selectedUnit.SnapToGrid(
                                GridSystem.Instance.GetWorldPosition(
                                    selectedUnit.CurrentGridPosition
                                )
                            );
                        }
                        else
                        {
                            // Safe to move Apply the AP cost and finalize grid position.
                            selectedUnit.SpendActionPoints(1);
                            GridSystem.Instance.MoveUnit(
                                selectedUnit,
                                selectedUnit.CurrentGridPosition,
                                currentPos
                            );
                            selectedUnit.FinalizeMove(currentPos);
                            selectedUnit.SnapToGrid(
                                GridSystem.Instance.GetWorldPosition(currentPos)
                            );
                        }

                        OnActionCompleted?.Invoke(this, EventArgs.Empty);

                        onComplete?.Invoke();

                        // Only drop back to FreeMovement if a menu isn't opening
                        if (currentPhase != GamePhase.ActionSelection)
                        {
                            CheckTurnEnd();
                        }
                    }
                );
            }
            else
            {
                // If they didn't actually move anywhere, just run the callback immediately
                onComplete?.Invoke();
            }
        }

        private void CheckTurnEnd()
        {
            if (selectedUnit.GetActionPointsRemaining() <= 0)
            {
                EndTurn();
            }
            else
            {
                // Refresh movement range
                validMovePositions = Pathfinding.GetReachableGridPositions(
                    selectedUnit.CurrentGridPosition,
                    selectedUnit.GetMaxMoveCost()
                );
                if (currentPhase != GamePhase.ActionSelection)
                    SetPhase(GamePhase.FreeMovement);
            }
        }

        private void SetSelectedUnit(Unit unit)
        {
            selectedUnit = unit;
            selectedUnit.StartTurn();
            validMovePositions = Pathfinding.GetReachableGridPositions(
                selectedUnit.CurrentGridPosition,
                selectedUnit.GetMaxMoveCost()
            );
            // OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
            CameraController.Instance.SetFollowTarget(unit.transform);
            // SetPhase(GamePhase.FreeMovement);
        }

        public void ClearSelectedUnit()
        {
            selectedUnit = null;
            CameraController.Instance.ClearFollowTarget();
            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
            SetPhase(GamePhase.UnitSelection);
        }

        public void EndTurn()
        {
            if (selectedUnit != null)
            {
                Vector3 endPos = GridSystem.Instance.GetWorldPosition(
                    selectedUnit.CurrentGridPosition
                );
                selectedUnit.SnapToGrid(endPos);
            }
            ClearSelectedUnit();
            TurnManager.Instance.NextTurn();
        }

        public void SetPhase(GamePhase newPhase)
        {
            // Debug.Log($"[STATE MACHINE] Phase changing from {currentPhase} to {newPhase}");
            currentPhase = newPhase;

            if (currentPhase == GamePhase.FreeMovement && selectedUnit != null)
            {
                validMovePositions = Pathfinding.GetReachableGridPositions(
                    selectedUnit.CurrentGridPosition,
                    selectedUnit.GetMaxMoveCost()
                );
            }

            // Hide tooltip if we leave targeting mode
            if (currentPhase != GamePhase.ActionTargeting && UnitTooltipUI.Instance != null)
            {
                UnitTooltipUI.Instance.Hide();
            }
            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
        }

        private Vector3 GetMouseWorldPosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (
                Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    float.MaxValue,
                    groundLayerMask | unitLayerMask
                )
            )
            {
                return hit.point;
            }
            return Vector3.zero;
        }

        private void OnJumpPerformed(InputAction.CallbackContext ctx)
        {
            if (currentPhase == GamePhase.FreeMovement)
                selectedUnit.HandleJump();
        }
    }
}
