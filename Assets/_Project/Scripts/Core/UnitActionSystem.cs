using System;
using System.Collections.Generic;
using TacticsGame.Actions;
using TacticsGame.Characters;
using TacticsGame.Combat;
using TacticsGame.Grid;
using TacticsGame.InputSystem;
using TacticsGame.Reactions;
using TacticsGame.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TacticsGame.Core
{
    public class UnitActionSystem : MonoBehaviour
    {
        public event EventHandler OnSelectedUnitChanged;
        public event EventHandler OnActionCompleted;
        public event EventHandler OnValidPositionsChanged;

        [SerializeField]
        private LayerMask unitLayerMask;

        [SerializeField]
        private LayerMask groundLayerMask;

        private Unit selectedUnit;
        private BaseAction selectedAction;

        private List<Vector3Int> validMovePositions;
        private HashSet<Vector2Int> validMoveColumns;

        public Unit SelectedUnit => selectedUnit;

        private SneakAction pendingSneakAction;
        private GridPosition pendingSneakStart;
        private Vector3Int pendingSneakStartLayered;

        private List<Vector3Int> movementWaypoints = new List<Vector3Int>();
        private bool isWaypointMode;
        private int spentWaypointCost;
        public bool IsWaypointMode => isWaypointMode;
        public List<Vector3Int> MovementWaypoints => movementWaypoints;
        public int SpentWaypointCost => spentWaypointCost;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void Start()
        {
            var inputService = ServiceLocator.Get<InputService>();
            inputService.OnSelectPerformed += OnSelectPerformed;
            inputService.OnConfirmPerformed += OnConfirmPerformed;
            inputService.OnCancelPerformed += OnCancelPerformed;
            inputService.OnJumpPerformed += OnJumpPerformed;
            inputService.OnOpenMenuPerformed += OnOpenMenuPerformed;
            inputService.OnEndTurnPerformed += OnEndTurnPerformed;
            inputService.OnEagleEyePerformed += OnEagleEyePerformed;
            inputService.OnToggleWaypointPerformed += OnToggleWaypointPerformed;

            var phaseManager = ServiceLocator.Get<PhaseManager>();
            phaseManager.OnPhaseChanged += PhaseManager_OnPhaseChanged;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<UnitActionSystem>();
            if (ServiceLocator.TryGet<InputService>(out var inputService))
            {
                inputService.OnSelectPerformed -= OnSelectPerformed;
                inputService.OnConfirmPerformed -= OnConfirmPerformed;
                inputService.OnCancelPerformed -= OnCancelPerformed;
                inputService.OnJumpPerformed -= OnJumpPerformed;
                inputService.OnOpenMenuPerformed -= OnOpenMenuPerformed;
                inputService.OnEndTurnPerformed -= OnEndTurnPerformed;
                inputService.OnEagleEyePerformed -= OnEagleEyePerformed;
                inputService.OnToggleWaypointPerformed -= OnToggleWaypointPerformed;
            }
            if (ServiceLocator.TryGet<PhaseManager>(out var phaseManager))
            {
                phaseManager.OnPhaseChanged -= PhaseManager_OnPhaseChanged;
            }
        }

        private void PhaseManager_OnPhaseChanged(object sender, GamePhase newPhase)
        {
            if (newPhase == GamePhase.FreeMovement && selectedUnit != null)
            {
                int maxMoveCost = selectedUnit.GetMaxMoveCost();
                if (pendingSneakAction != null)
                    maxMoveCost = Mathf.Max(0, maxMoveCost / 2);

                SetValidMovePositions(
                    Pathfinding.GetReachablePositions(
                        selectedUnit.CurrentLayeredPosition,
                        maxMoveCost
                    )
                );

                // Ensure waypoints are seeded with at least the current position
                if (isWaypointMode && (movementWaypoints == null || movementWaypoints.Count == 0))
                {
                    movementWaypoints.Clear();
                    movementWaypoints.Add(selectedUnit.CurrentLayeredPosition);
                    spentWaypointCost = 0;
                }
            }

            if (newPhase != GamePhase.ActionTargeting)
            {
                if (ServiceLocator.TryGet(out TacticsGame.UI.UnitTooltipUI tooltipUI))
                {
                    tooltipUI.Hide();
                }
            }
            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Update()
        {
            if (!ServiceLocator.Get<TurnManager>().IsPlayerTurn())
                return;

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;

            switch (currentPhase)
            {
                case GamePhase.FreeMovement:
                case GamePhase.EagleEye:
                    HandleFreeMovement();
                    break;
                case GamePhase.ActionTargeting:
                    if (selectedAction != null && selectedAction.IsUnitTargeted)
                    {
                        if (ServiceLocator.TryGet<TargetLockService>(out var tls))
                            tls.HandleInput();
                    }
                    else
                    {
                        ServiceLocator.Get<TargetingService>().HandleCursorMovement(selectedAction);
                    }
                    break;
            }
        }

        public void ForceSelectUnit(Unit unit)
        {
            SetSelectedUnit(unit);

            if (unit.GetFaction() == Faction.Enemy)
            {
                bool playerControlsEnemy =
                    ServiceLocator.TryGet<EnemyAIManager>(out var ai)
                    && ai != null
                    && ai.ControlMode == EnemyAIManager.EnemyControlMode.PlayerControlsEnemy;

                if (playerControlsEnemy)
                {
                    ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
                    Debug.Log("[ENEMY AI] Player control enabled for enemy unit turn.");
                }
                else
                {
                    ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.Busy);
                    Debug.Log("AI Turn Started. Player controls locked.");
                }
            }
            else
            {
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
            }
        }

        private bool AllowInteraction(string context)
        {
            if (ServiceLocator.TryGet<CameraController>(out var cc))
            {
                bool blending = cc.IsBlending();
                if (blending)
                {
                    Debug.Log(
                        $"[UnitActionSystem] Interaction '{context}' BLOCKED due to camera blend."
                    );
                }
                return !blending;
            }
            return true;
        }

        private void OnSelectPerformed(object sender, EventArgs e)
        {
            if (!AllowInteraction("Select"))
                return;

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;
            if (currentPhase == GamePhase.UnitSelection)
            {
                Vector2 mousePos = ServiceLocator.Get<InputService>().GetMousePosition();
                Ray ray = Camera.main.ScreenPointToRay(mousePos);
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
            Debug.Log($"[UnitActionSystem] SetSelectedAction: {action.GetActionName()}");

            if (!action.CanExecuteAction())
            {
                Debug.Log(
                    $"<color=red>Action '{action.GetActionName()}' is currently blocked by a condition!</color>"
                );
                return;
            }

            selectedAction = action;

            if (action is SneakAction sneak)
            {
                pendingSneakAction = sneak;
                pendingSneakStart = selectedUnit.CurrentGridPosition;
                pendingSneakStartLayered = selectedUnit.CurrentLayeredPosition;

                int maxMoveCost = selectedUnit.GetMaxMoveCost();
                int halfMoveCost = Mathf.Max(0, maxMoveCost / 2);
                SetValidMovePositions(
                    Pathfinding.GetReachablePositions(
                        selectedUnit.CurrentLayeredPosition,
                        halfMoveCost
                    )
                );

                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
                return;
            }

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;
            if (currentPhase == GamePhase.EagleEye)
                preEagleEyePhase = GamePhase.EagleEye;

            ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.ActionTargeting);

            ServiceLocator
                .Get<TargetingService>()
                .InitializeTargeting(selectedUnit.CurrentGridPosition);

            if (selectedAction != null && selectedAction.IsUnitTargeted)
            {
                if (ServiceLocator.TryGet<TargetLockService>(out var tls))
                {
                    tls.InitializeTargeting(selectedUnit, selectedAction);
                }
            }
        }

        private void OnConfirmPerformed(object sender, EventArgs e)
        {
            if (!AllowInteraction("Confirm"))
                return;

            if (!ServiceLocator.Get<TurnManager>().IsPlayerTurn())
                return;

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;

            if (currentPhase == GamePhase.FreeMovement || currentPhase == GamePhase.EagleEye)
            {
                if (pendingSneakAction != null)
                    CommitSneakMoveAction();
                else
                    CommitMoveAction();
            }
            else if (currentPhase == GamePhase.ActionTargeting)
            {
                Vector3Int targetPos;
                var targetingService = ServiceLocator.Get<TargetingService>();

                if (
                    selectedAction != null
                    && selectedAction.IsUnitTargeted
                    && ServiceLocator.TryGet<TargetLockService>(out var tls)
                    && tls.IsActive
                    && tls.CurrentTarget != null
                )
                {
                    targetPos = tls.CurrentTargetLayeredPosition;
                }
                else
                {
                    targetPos = targetingService.CurrentTargetLayeredPosition;
                }

                TryExecuteActionAtGridPos(targetPos);
            }
        }

        private void OnOpenMenuPerformed(object sender, EventArgs e)
        {
            if (!ServiceLocator.Get<TurnManager>().IsPlayerTurn())
            {
                Debug.LogWarning("[UAS DEBUG] Not Player turn, ignoring menu command.");
                return;
            }

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;

            if (currentPhase == GamePhase.FreeMovement || currentPhase == GamePhase.EagleEye)
            {
                if (selectedUnit != null)
                {
                    GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
                    Vector3Int snappedPos = gridSystem.GetLayeredGridPosition(
                        selectedUnit.transform.position
                    );
                    selectedUnit.SnapToGrid(gridSystem.GetWorldPosition(snappedPos));
                }

                CommitMoveAction(HandlePostMoveActionSelection);
            }
            else if (currentPhase == GamePhase.ActionSelection)
            {
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
            }
        }

        private GamePhase preEagleEyePhase;

        private void OnToggleWaypointPerformed(object sender, EventArgs e)
        {
            if (!ServiceLocator.Get<TurnManager>().IsPlayerTurn())
                return;

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;
            if (currentPhase != GamePhase.FreeMovement && currentPhase != GamePhase.EagleEye)
                return;

            DropWaypoint();
        }

        private void DropWaypoint()
        {
            if (selectedUnit == null)
                return;

            // Ensure waypoint mode is true once we start placing nodes
            if (!isWaypointMode)
            {
                isWaypointMode = true;
                movementWaypoints.Clear();
                movementWaypoints.Add(selectedUnit.CurrentLayeredPosition);
            }

            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            Vector3Int currentLayered = gridSystem.GetLayeredGridPosition(
                selectedUnit.transform.position
            );

            if (
                movementWaypoints.Count > 0
                && currentLayered == movementWaypoints[movementWaypoints.Count - 1]
            )
                return;

            // Check if we have budget for the path between last waypoint and current spot
            List<Vector3Int> segment = Pathfinding.FindPath(
                movementWaypoints[movementWaypoints.Count - 1],
                currentLayered
            );
            if (segment == null)
                return;

            int segmentCost = Pathfinding.CalculatePathCost(segment);
            int maxMoveCost = selectedUnit.GetMaxMoveCost();
            if (pendingSneakAction != null)
                maxMoveCost = Mathf.Max(0, maxMoveCost / 2);

            if (spentWaypointCost + segmentCost <= maxMoveCost)
            {
                movementWaypoints.Add(currentLayered);
                spentWaypointCost += segmentCost;
                UpdateValidMovePositions();
                // Debug.Log(
                //     $"[MOVEMENT RANGE WAYPOINTS DEBUG] [WAYPOINT] Added node at {currentLayered}. Segment Cost: {segmentCost}, Total Cost: {spentWaypointCost}, Max: {maxMoveCost}"
                // );
            }
            else
            {
                // Debug.LogWarning(
                //     $"[MOVEMENT RANGE WAYPOINTS DEBUG] [WAYPOINT] NOT ENOUGH BUDGET! Adding {segmentCost} would exceed {maxMoveCost}. (Current spent: {spentWaypointCost})"
                // );
            }
        }

        private void OnCancelPerformed(object sender, EventArgs e)
        {
            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;

            if (currentPhase == GamePhase.ActionTargeting)
            {
                if (
                    selectedAction != null
                    && selectedAction.IsUnitTargeted
                    && ServiceLocator.TryGet<TargetLockService>(out var tls)
                )
                {
                    tls.HideTargeting();
                }

                ServiceLocator.Get<TargetingService>().HideTargeting();

                if (selectedUnit != null)
                    ServiceLocator.Get<CameraController>().SetFollowTarget(selectedUnit.transform);

                if (preEagleEyePhase == GamePhase.EagleEye)
                {
                    ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.EagleEye);
                }
                else
                {
                    if (selectedUnit != null && selectedUnit.GetActionPointsRemaining() > 0)
                    {
                        ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.ActionSelection);
                    }
                    else
                    {
                        ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
                    }
                }
            }
            else if (currentPhase == GamePhase.FreeMovement || currentPhase == GamePhase.EagleEye)
            {
                if (isWaypointMode && movementWaypoints.Count > 1)
                {
                    Vector3Int lastWP = movementWaypoints[movementWaypoints.Count - 1];
                    GridSystem grid = ServiceLocator.Get<GridSystem>();
                    Vector3 wpWorldPos = grid.GetWorldPosition(lastWP);

                    // If we have moved into a DIFFERENT grid cell than the last waypoint, first snap back to it.
                    Vector3Int currentCell = grid.GetLayeredGridPosition(
                        selectedUnit.transform.position
                    );
                    if (currentCell != lastWP)
                    {
                        selectedUnit.SnapToGrid(wpWorldPos);
                        UpdateValidMovePositions();
                        // Debug.Log($"[WAYPOINT] Undo: Snapped back to current waypoint {lastWP}");
                        return;
                    }

                    // If we are already in the same cell as the last waypoint, remove it and go to the previous one.
                    Vector3Int removedPoint = lastWP;
                    movementWaypoints.RemoveAt(movementWaypoints.Count - 1);

                    // Recalculate full spent cost
                    spentWaypointCost = 0;
                    for (int i = 0; i < movementWaypoints.Count - 1; i++)
                    {
                        var path = Pathfinding.FindPath(
                            movementWaypoints[i],
                            movementWaypoints[i + 1],
                            selectedUnit.CurrentLayeredPosition
                        );
                        spentWaypointCost += Pathfinding.CalculatePathCost(path);
                    }

                    // Snap unit back to the previous waypoint center
                    Vector3 prevWorldPos = grid.GetWorldPosition(
                        movementWaypoints[movementWaypoints.Count - 1]
                    );
                    selectedUnit.SnapToGrid(prevWorldPos);

                    UpdateValidMovePositions();
                    // Debug.Log(
                    //     $"[WAYPOINT] Undo: Removed node at {removedPoint}. Returning to {movementWaypoints[movementWaypoints.Count - 1]}. Remaining Cost: {spentWaypointCost}"
                    // );
                }
                else if (isWaypointMode && movementWaypoints.Count == 1)
                {
                    // Snap back to start
                    Vector3 startPos = ServiceLocator
                        .Get<GridSystem>()
                        .GetWorldPosition(movementWaypoints[0]);
                    selectedUnit.SnapToGrid(startPos);
                }
            }
            else if (currentPhase == GamePhase.ActionSelection)
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
        }

        private void OnEagleEyePerformed(object sender, EventArgs e)
        {
            if (!ServiceLocator.Get<TurnManager>().IsPlayerTurn())
                return;

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;

            if (currentPhase == GamePhase.EagleEye)
            {
                // Toggle OFF
                ServiceLocator.Get<PhaseManager>().SetPhase(preEagleEyePhase);
                ServiceLocator.Get<CameraController>().ExitEagleEyeMode();
            }
            else if (currentPhase == GamePhase.FreeMovement)
            {
                // Toggle ON
                preEagleEyePhase = currentPhase;
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.EagleEye);
                ServiceLocator.Get<CameraController>().EnterEagleEyeMode(selectedUnit.transform);
            }
        }

        private void HandleFreeMovement()
        {
            if (selectedUnit == null || !ServiceLocator.Get<TurnManager>().IsPlayerTurn())
                return;

            var conditions = selectedUnit.GetComponent<UnitConditions>();
            if (conditions != null)
            {
                if (
                    conditions.IsDead()
                    || conditions.HasCondition(ConditionType.Unconscious)
                    || conditions.GetConditionValue(ConditionType.Stunned) > 0
                )
                {
                    selectedUnit.HandleMovement(Vector3.zero);
                    return;
                }
                if (!conditions.CanMove() || conditions.HasCondition(ConditionType.Prone))
                {
                    selectedUnit.HandleMovement(Vector3.zero);
                    return;
                }
            }

            Vector2 inputMoveDir = ServiceLocator.Get<InputService>().GetMovementVectorNormalized();

            if (inputMoveDir == Vector2.zero)
            {
                selectedUnit.HandleMovement(Vector3.zero);
                return;
            }

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
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            GridPosition currentGridPos = gridSystem.GetGridPosition(
                selectedUnit.transform.position
            );
            Vector3Int currentLayered = gridSystem.GetLayeredGridPosition(
                selectedUnit.transform.position
            );
            Vector3 cellCenterWorld = gridSystem.GetWorldPosition(currentLayered);
            float cellSize = gridSystem.CellSize;
            float unitRadius = selectedUnit.GetUnitRadius();

            if (
                !IsValidMoveColumn(currentGridPos.x, currentGridPos.z + 1)
                || IsOccupiedByOther(currentGridPos.x, currentGridPos.z + 1, currentLayered.y)
            )
            {
                float maxZ = cellCenterWorld.z + (cellSize * 0.5f) - unitRadius;
                if (proposedPosition.z > maxZ)
                    proposedPosition.z = maxZ;
            }

            if (
                !IsValidMoveColumn(currentGridPos.x, currentGridPos.z - 1)
                || IsOccupiedByOther(currentGridPos.x, currentGridPos.z - 1, currentLayered.y)
            )
            {
                float minZ = cellCenterWorld.z - (cellSize * 0.5f) + unitRadius;
                if (proposedPosition.z < minZ)
                    proposedPosition.z = minZ;
            }

            if (
                !IsValidMoveColumn(currentGridPos.x + 1, currentGridPos.z)
                || IsOccupiedByOther(currentGridPos.x + 1, currentGridPos.z, currentLayered.y)
            )
            {
                float maxX = cellCenterWorld.x + (cellSize * 0.5f) - unitRadius;
                if (proposedPosition.x > maxX)
                    proposedPosition.x = maxX;
            }

            if (
                !IsValidMoveColumn(currentGridPos.x - 1, currentGridPos.z)
                || IsOccupiedByOther(currentGridPos.x - 1, currentGridPos.z, currentLayered.y)
            )
            {
                float minX = cellCenterWorld.x - (cellSize * 0.5f) + unitRadius;
                if (proposedPosition.x < minX)
                    proposedPosition.x = minX;
            }

            GridPosition targetGridPos = gridSystem.GetGridPosition(proposedPosition);
            if (
                !IsValidMoveColumn(targetGridPos.x, targetGridPos.z)
                || IsOccupiedByOther(targetGridPos.x, targetGridPos.z, currentLayered.y)
            )
            {
                selectedUnit.HandleMovement(Vector3.zero);
                return;
            }

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

        private bool IsValidMoveColumn(int x, int z)
        {
            return validMoveColumns != null && validMoveColumns.Contains(new Vector2Int(x, z));
        }

        private bool IsOccupiedByOther(int x, int z, int referenceY)
        {
            // Block all occupied cells (allies and enemies).
            // This is for technical simplicity and deviates from PF2e rules.
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3Int layered = grid.ResolveClosestLayeredPosition(
                new GridPosition(x, z),
                referenceY
            );
            Unit unitAtPos = grid.GetUnitAt(layered);
            return unitAtPos != null && unitAtPos != selectedUnit;
        }

        private void TryExecuteActionAtGridPos(Vector3Int targetPos)
        {
            if (selectedAction == null)
                return;

            if (!selectedAction.GetValidActionGridPositions().Contains(targetPos))
            {
                return;
            }

            if (!selectedAction.CanExecuteAction())
            {
                Debug.LogWarning(
                    $"[SPELL DEBUG] [UnitActionSystem] Cannot execute {selectedAction.GetActionName()}: CanExecuteAction returned false."
                );
                return;
            }

            ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.Busy);
            ServiceLocator.Get<TargetingService>().HideTargeting();

            // TakeAction relies on the Active state of TLS to lock on correctly.
            TargetLockService confirmTls = null;
            if (ServiceLocator.TryGet<TargetLockService>(out var tls) && tls.IsActive)
            {
                confirmTls = tls;
            }

            if (selectedUnit != null)
                ServiceLocator.Get<CameraController>().SetFollowTarget(selectedUnit.transform);

            selectedUnit.SpendActionPoints(selectedAction.GetActionPointsCost());

            // Handle reactions for Manipulate and Ranged Attack triggers
            GameEvent reactionTrigger = null;
            if (selectedAction.IsRangedAttack)
            {
                Unit target = ServiceLocator.Get<GridSystem>().GetUnitAt(targetPos);
                reactionTrigger = new RangedAttackEvent(selectedUnit, target);
            }
            else if (selectedAction.IsManipulateAction)
            {
                reactionTrigger = new ManipulateEvent(selectedUnit, selectedAction.GetActionName());
            }
            else if (selectedAction.IsMoveAction)
            {
                reactionTrigger = new MoveActionEvent(selectedUnit, selectedAction.GetActionName());
            }

            if (reactionTrigger != null)
            {
                ServiceLocator
                    .Get<ReactionManager>()
                    .EvaluateEvent(
                        reactionTrigger,
                        (resolvedEvent) =>
                        {
                            if (resolvedEvent.IsCancelled)
                            {
                                // Action was disrupted
                                Debug.Log(
                                    $"<color=red>[ACTION]</color> {selectedUnit.name}'s {selectedAction.GetActionName()} was DISRUPTED!"
                                );
                                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                                if (preEagleEyePhase == GamePhase.EagleEye)
                                    ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.EagleEye);
                                CheckTurnEnd();
                            }
                            else
                            {
                                // Proceed with action
                                ExecuteActionWithVisuals(selectedAction, targetPos, confirmTls);
                            }
                        }
                    );
            }
            else
            {
                ExecuteActionWithVisuals(selectedAction, targetPos, confirmTls);
            }

            // Now that TakeAction has successfully pulled the unit, we can wipe the visuals.
            if (confirmTls != null)
            {
                confirmTls.HideTargeting();
            }
        }

        private void ExecuteActionWithVisuals(
            BaseAction action,
            Vector3Int targetPos,
            TargetLockService tls
        )
        {
            action.TakeAction(
                targetPos,
                () =>
                {
                    OnActionCompleted?.Invoke(this, EventArgs.Empty);
                    if (preEagleEyePhase == GamePhase.EagleEye)
                        ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.EagleEye);
                    CheckTurnEnd();
                }
            );
        }

        public void AiExecuteAction(BaseAction action, Vector3Int targetPos)
        {
            selectedAction = action;
            TryExecuteActionAtGridPos(targetPos);
        }

        public void AiCommitMoveAction(Action onComplete = null)
        {
            CommitMoveAction(onComplete);
        }

        private void CommitMoveAction(Action onComplete = null)
        {
            if (selectedUnit == null)
                return;

            var conditions = selectedUnit.GetComponent<UnitConditions>();
            if (conditions != null)
            {
                if (!conditions.CanMove())
                {
                    onComplete?.Invoke();
                    return;
                }

                if (conditions.HasCondition(ConditionType.Prone))
                {
                    onComplete?.Invoke();
                    return;
                }
            }

            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            Vector3Int currentLayeredTerminal = gridSystem.GetLayeredGridPosition(
                selectedUnit.transform.position
            );

            bool hasMoved = currentLayeredTerminal != selectedUnit.CurrentLayeredPosition;

            if (hasMoved)
            {
                if (selectedUnit.GetActionPointsRemaining() < 1)
                {
                    selectedUnit.SnapToGrid(
                        gridSystem.GetWorldPosition(selectedUnit.CurrentLayeredPosition)
                    );
                    // Debug.LogWarning("[MOVE] Not enough AP to commit movement.");
                    return;
                }

                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.Busy);

                // Build the full path through all waypoints
                List<Vector3Int> fullPath = new List<Vector3Int>();
                if (isWaypointMode)
                {
                    // Ensure the current unit position is part of the waypoints list if it's the destination
                    if (currentLayeredTerminal != movementWaypoints[movementWaypoints.Count - 1])
                    {
                        // Temporary list to build segments
                        List<Vector3Int> plannedNodes = new List<Vector3Int>(movementWaypoints);
                        plannedNodes.Add(currentLayeredTerminal);

                        for (int i = 0; i < plannedNodes.Count - 1; i++)
                        {
                            var segment = Pathfinding.FindPath(
                                plannedNodes[i],
                                plannedNodes[i + 1],
                                selectedUnit.CurrentLayeredPosition
                            );
                            if (segment != null)
                            {
                                foreach (var p in segment)
                                {
                                    if (fullPath.Count == 0 || p != fullPath[fullPath.Count - 1])
                                        fullPath.Add(p);
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < movementWaypoints.Count - 1; i++)
                        {
                            var segment = Pathfinding.FindPath(
                                movementWaypoints[i],
                                movementWaypoints[i + 1],
                                selectedUnit.CurrentLayeredPosition
                            );
                            if (segment != null)
                            {
                                foreach (var p in segment)
                                {
                                    if (fullPath.Count == 0 || p != fullPath[fullPath.Count - 1])
                                        fullPath.Add(p);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Standard move Start -> Current position
                    fullPath = Pathfinding.FindPath(
                        selectedUnit.CurrentLayeredPosition,
                        currentLayeredTerminal,
                        selectedUnit.CurrentLayeredPosition
                    );
                }

                if (fullPath == null || fullPath.Count < 2)
                {
                    ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
                    onComplete?.Invoke();
                    return;
                }

                // Execute reactive move sequence
                bool isStep = fullPath.Count == 2;
                if (isStep)
                {
                    Debug.Log(
                        $"<color=yellow>[MOVE]</color> 5-foot move detected for {selectedUnit.name}. Treating as a STEP (no reactions)."
                    );
                }
                ExecuteReactiveSnap(fullPath, 1, isStep, onComplete);
                // TODO: test this
                // Reset planning state after commitment
                // We don't clear fully here because ExecuteReactiveSnap will re-seed WP0 at the end.
                spentWaypointCost = 0;
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        private void ExecuteReactiveSnap(
            List<Vector3Int> path,
            int nextIndex,
            bool isStep,
            Action onComplete
        )
        {
            if (nextIndex >= path.Count)
            {
                // Sequence finished successfully - Final Snap
                Vector3Int finalPos = path[path.Count - 1];
                selectedUnit.SpendActionPoints(1);

                GridSystem grid = ServiceLocator.Get<GridSystem>();
                grid.MoveUnit(selectedUnit, selectedUnit.CurrentLayeredPosition, finalPos);
                selectedUnit.FinalizeMove(finalPos);
                selectedUnit.SnapToGrid(grid.GetWorldPosition(finalPos));

                // Post-Move effects
                // TODO: update auras to be 3d, and work with waypoints.
                UnitAuraEmitter[] allEmitters = FindObjectsByType<UnitAuraEmitter>(
                    FindObjectsSortMode.None
                );
                foreach (var emitter in allEmitters)
                    emitter.UpdateAuras(AuraTriggerType.OnEnter);

                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                ResetWaypointState(finalPos);
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);

                onComplete?.Invoke();
                CheckTurnEnd();
                return;
            }

            Vector3Int from = path[nextIndex - 1];
            Vector3Int to = path[nextIndex];

            // Evaluate reaction specifically for LEAVING 'from'
            GridPosition fromGP = new GridPosition(from.x, from.z);
            GridPosition toGP = new GridPosition(to.x, to.z);

            BeforeMoveEvent moveEvent = new BeforeMoveEvent(
                selectedUnit,
                fromGP,
                toGP,
                from,
                to,
                isStep
            );

            ServiceLocator
                .Get<ReactionManager>()
                .EvaluateEvent(
                    moveEvent,
                    (resolvedEvent) =>
                    {
                        if (
                            resolvedEvent.IsCancelled
                            || selectedUnit.GetComponent<UnitConditions>().IsDead()
                        )
                        {
                            // Interrupted while moving from 'from' to 'to'
                            // In PF2e, you are considered to be in the square you were entering.
                            selectedUnit.SpendActionPoints(1);

                            GridSystem grid = ServiceLocator.Get<GridSystem>();
                            grid.MoveUnit(selectedUnit, selectedUnit.CurrentLayeredPosition, to);
                            selectedUnit.FinalizeMove(to);
                            selectedUnit.SnapToGrid(grid.GetWorldPosition(to));

                            OnActionCompleted?.Invoke(this, EventArgs.Empty);
                            ResetWaypointState(to);
                            ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
                            CheckTurnEnd();
                        }
                        else
                        {
                            // Move unit to 'to' immediately (The Snap)
                            GridSystem grid = ServiceLocator.Get<GridSystem>();
                            selectedUnit.SnapToGrid(grid.GetWorldPosition(to));

                            // PF2e: The unit is now in the new square.
                            // Update grid position and check for detection changes (e.g. entering reach of someone else)
                            selectedUnit.FinalizeMove(to);

                            // Recursive call to check next tile in path
                            ExecuteReactiveSnap(path, nextIndex + 1, isStep, onComplete);
                        }
                    }
                );
        }

        private void CommitSneakMoveAction(Action onComplete = null)
        {
            if (selectedUnit == null || pendingSneakAction == null)
                return;

            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            GridPosition startPos = pendingSneakStart;
            GridPosition endPos = gridSystem.GetGridPosition(selectedUnit.transform.position);
            Vector3Int endLayered = gridSystem.GetLayeredGridPosition(
                selectedUnit.transform.position
            );

            if (endLayered == pendingSneakStartLayered)
            {
                pendingSneakAction = null;
                onComplete?.Invoke();
                return;
            }

            if (!IsValidMoveColumn(endPos.x, endPos.z))
            {
                selectedUnit.SnapToGrid(
                    gridSystem.GetWorldPosition(selectedUnit.CurrentLayeredPosition)
                );
                onComplete?.Invoke();
                return;
            }

            if (selectedUnit.GetActionPointsRemaining() < pendingSneakAction.GetActionPointsCost())
            {
                selectedUnit.SnapToGrid(
                    gridSystem.GetWorldPosition(selectedUnit.CurrentLayeredPosition)
                );
                pendingSneakAction = null;
                onComplete?.Invoke();
                return;
            }

            ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.Busy);

            BeforeMoveEvent moveEvent = new BeforeMoveEvent(
                selectedUnit,
                startPos,
                endPos,
                isStep: false
            );

            ServiceLocator
                .Get<ReactionManager>()
                .EvaluateEvent(
                    moveEvent,
                    (resolvedEvent) =>
                    {
                        if (resolvedEvent.IsCancelled)
                        {
                            selectedUnit.SnapToGrid(
                                gridSystem.GetWorldPosition(selectedUnit.CurrentLayeredPosition)
                            );
                        }
                        else
                        {
                            selectedUnit.SpendActionPoints(
                                pendingSneakAction.GetActionPointsCost()
                            );

                            gridSystem.MoveUnit(
                                selectedUnit,
                                selectedUnit.CurrentLayeredPosition,
                                endLayered
                            );

                            StealthResolver.SetPassiveEvaluationSuppressed(selectedUnit, true);
                            selectedUnit.FinalizeMove(endLayered);
                            selectedUnit.SnapToGrid(gridSystem.GetWorldPosition(endLayered));

                            List<Vector3Int> path = Pathfinding.FindPath(startPos, endPos);
                            StealthResolver.ResolveSneak(
                                selectedUnit,
                                startPos,
                                endPos,
                                path,
                                actorMakesNoise: pendingSneakAction.MakesNoise
                            );
                            StealthResolver.SetPassiveEvaluationSuppressed(selectedUnit, false);

                            UnitAuraEmitter[] allEmitters = FindObjectsByType<UnitAuraEmitter>(
                                FindObjectsSortMode.None
                            );
                            foreach (var emitter in allEmitters)
                            {
                                emitter.UpdateAuras(AuraTriggerType.OnEnter);
                            }
                        }

                        pendingSneakAction = null;
                        OnActionCompleted?.Invoke(this, EventArgs.Empty);
                        onComplete?.Invoke();
                        CheckTurnEnd();
                    }
                );
        }

        public List<Vector3Int> GetValidMovePositions()
        {
            return validMovePositions == null ? null : new List<Vector3Int>(validMovePositions);
        }

        private void SetValidMovePositions(List<Vector3Int> positions)
        {
            validMovePositions = positions;
            validMoveColumns = new HashSet<Vector2Int>();
            if (positions != null)
            {
                foreach (Vector3Int p in positions)
                    validMoveColumns.Add(new Vector2Int(p.x, p.z));
            }
            OnValidPositionsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateValidMovePositions()
        {
            if (selectedUnit == null)
                return;

            int maxMoveCost = selectedUnit.GetMaxMoveCost();
            if (pendingSneakAction != null)
                maxMoveCost = Mathf.Max(0, maxMoveCost / 2);

            Vector3Int searchStart = selectedUnit.CurrentLayeredPosition;
            int budget = maxMoveCost;

            if (isWaypointMode && movementWaypoints.Count > 0)
            {
                searchStart = movementWaypoints[movementWaypoints.Count - 1];
                budget = maxMoveCost - spentWaypointCost;
                // Debug.Log(
                //     $"[MOVEMENT RANGE WAYPOINTS DEBUG] Waypoint Mode Active: LastWP={searchStart}, CurrentSpent={spentWaypointCost}, RemainingBudget={budget}"
                // );
            }
            else
            {
                // Debug.Log(
                //     $"[MOVEMENT RANGE WAYPOINTS DEBUG] Waypoint Mode Inactive: Using unit pos {searchStart}, Budget={budget}"
                // );
            }

            var reachable = Pathfinding.GetReachablePositions(
                searchStart,
                budget,
                selectedUnit.CurrentLayeredPosition
            );
            // Debug.Log(
            //     $"[MOVEMENT RANGE WAYPOINTS DEBUG] Pathfinding returned {reachable.Count} reachable tiles for budget {budget} from start {searchStart}."
            // );
            SetValidMovePositions(reachable);
        }

        private void CheckTurnEnd()
        {
            if (selectedUnit == null)
                return;

            if (selectedUnit.GetActionPointsRemaining() <= 0)
            {
                EndTurn();
            }
            else
            {
                int maxMoveCost = selectedUnit.GetMaxMoveCost();
                if (pendingSneakAction != null)
                    maxMoveCost = Mathf.Max(0, maxMoveCost / 2);

                SetValidMovePositions(
                    Pathfinding.GetReachablePositions(
                        selectedUnit.CurrentLayeredPosition,
                        maxMoveCost
                    )
                );
                if (
                    ServiceLocator.Get<PhaseManager>().CurrentPhase != GamePhase.ActionSelection
                    && ServiceLocator.Get<PhaseManager>().CurrentPhase != GamePhase.EagleEye
                )
                {
                    ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
                }
            }
        }

        private void SetSelectedUnit(Unit unit)
        {
            if (selectedUnit != null)
            {
                var oldEquipment = selectedUnit.GetComponent<UnitEquipment>();
                if (oldEquipment != null)
                {
                    oldEquipment.OnEquipmentChanged -= HandleEquipmentChanged;
                }
            }

            selectedUnit = unit;

            if (selectedUnit != null)
            {
                // Initialize waypoint state first to prevent stale cost from previous unit
                isWaypointMode = true;
                movementWaypoints.Clear();
                movementWaypoints.Add(selectedUnit.CurrentLayeredPosition);
                spentWaypointCost = 0;

                // calculate the range safely
                RefreshMovePositions();

                var newEquipment = selectedUnit.GetComponent<UnitEquipment>();
                if (newEquipment != null)
                {
                    newEquipment.OnEquipmentChanged += HandleEquipmentChanged;
                }

                ServiceLocator.Get<CameraController>().SetFollowTarget(unit.transform);
            }

            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
        }

        private void HandleEquipmentChanged()
        {
            if (selectedUnit == null)
                return;

            Debug.Log($"[UAS] Equipment changed on {selectedUnit.name}. Refreshing move range.");
            RefreshMovePositions();

            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshMovePositions()
        {
            UpdateValidMovePositions();
        }

        public void ClearSelectedUnit()
        {
            if (selectedUnit != null)
            {
                var equipment = selectedUnit.GetComponent<UnitEquipment>();
                if (equipment != null)
                {
                    equipment.OnEquipmentChanged -= HandleEquipmentChanged;
                }
            }

            selectedUnit = null;
            pendingSneakAction = null;
            isWaypointMode = false;
            movementWaypoints.Clear();
            spentWaypointCost = 0;
            ServiceLocator.Get<CameraController>().ClearFollowTarget();
            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
            ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.UnitSelection);
        }

        public void EndTurn()
        {
            if (selectedUnit != null)
            {
                GridSystem grid = ServiceLocator.Get<GridSystem>();
                selectedUnit.SnapToGrid(grid.GetWorldPosition(selectedUnit.CurrentLayeredPosition));
            }
            ClearSelectedUnit();
            ServiceLocator.Get<TurnManager>().NextTurn();
        }

        private void OnJumpPerformed(object sender, EventArgs e)
        {
            if (ServiceLocator.Get<PhaseManager>().CurrentPhase == GamePhase.FreeMovement)
                selectedUnit.HandleJump();
        }

        private void OnEndTurnPerformed(object sender, EventArgs e)
        {
            if (!ServiceLocator.Get<TurnManager>().IsPlayerTurn())
                return;

            GamePhase phase = ServiceLocator.Get<PhaseManager>().CurrentPhase;
            if (phase == GamePhase.ActionTargeting || phase == GamePhase.Busy)
                return;

            pendingSneakAction = null;
            EndTurn();
        }

        private Vector3 GetMouseWorldPosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(
                ServiceLocator.Get<InputService>().GetMousePosition()
            );
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

        private void HandlePostMoveActionSelection()
        {
            bool isPlayerTurn = ServiceLocator.Get<TurnManager>().IsPlayerTurn();
            Debug.Log(
                $"<color=yellow>[UAS DEBUG]</color> HandlePostMoveActionSelection. Unit: {selectedUnit?.name}, Faction: {selectedUnit?.GetFaction()}, IsPlayerTurn: {isPlayerTurn}, AP: {selectedUnit?.GetActionPointsRemaining()}"
            );

            if (selectedUnit != null && selectedUnit.GetActionPointsRemaining() > 0)
            {
                Debug.Log("<color=yellow>[UAS DEBUG]</color> Setting phase to ActionSelection.");
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.ActionSelection);
            }
            else
            {
                Debug.Log(
                    "<color=yellow>[UAS DEBUG]</color> Phase transition skipped. Zero AP or no unit."
                );
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
            }
        }

        private void ResetWaypointState(Vector3Int position)
        {
            isWaypointMode = true;
            movementWaypoints.Clear();
            movementWaypoints.Add(position);
            spentWaypointCost = 0;

            RefreshMovePositions();
            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
