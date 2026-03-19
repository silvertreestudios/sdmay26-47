using System;
using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Characters;
using PathfinderTactics.Combat;
using PathfinderTactics.Grid;
using PathfinderTactics.InputSystem;
using PathfinderTactics.Reactions;
using PathfinderTactics.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PathfinderTactics.Core
{
    public class UnitActionSystem : MonoBehaviour
    {
        public event EventHandler OnSelectedUnitChanged;
        public event EventHandler OnActionCompleted;

        [SerializeField]
        private LayerMask unitLayerMask;

        [SerializeField]
        private LayerMask groundLayerMask;

        private Unit selectedUnit;
        private BaseAction selectedAction;

        private List<GridPosition> validMovePositions;

        public Unit SelectedUnit => selectedUnit;

        // Sneak movement-mode action support:
        // selecting Sneak enters FreeMovement with half-speed boundary, and confirm commits
        // via movement pipeline then resolves StealthResolver.ResolveSneak at end.
        private SneakAction pendingSneakAction;
        private GridPosition pendingSneakStart;

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
                // If a movement-mode action is pending (e.g., Sneak), the move boundary
                // must reflect that action's rules (Sneak = half speed).
                int maxMoveCost = selectedUnit.GetMaxMoveCost();
                if (pendingSneakAction != null)
                    maxMoveCost = Mathf.Max(0, maxMoveCost / 2);

                validMovePositions = Pathfinding.GetReachableGridPositions(
                    selectedUnit.CurrentGridPosition,
                    maxMoveCost
                );
            }

            if (
                newPhase != GamePhase.ActionTargeting
                && ServiceLocator.Get<UnitTooltipUI>() != null
            )
            {
                ServiceLocator.Get<UnitTooltipUI>().Hide();
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
                    HandleFreeMovement();
                    break;
                case GamePhase.ActionTargeting:
                    ServiceLocator.Get<TargetingService>().HandleCursorMovement(selectedAction);
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

        private void OnSelectPerformed(object sender, EventArgs e)
        {
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

                int maxMoveCost = selectedUnit.GetMaxMoveCost();
                int halfMoveCost = Mathf.Max(0, maxMoveCost / 2);
                validMovePositions = Pathfinding.GetReachableGridPositions(
                    selectedUnit.CurrentGridPosition,
                    halfMoveCost
                );

                // Enter movement mode so the player can preview/choose a destination naturally.
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
                return;
            }

            // Default: use targeting cursor
            ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.ActionTargeting);
            ServiceLocator
                .Get<TargetingService>()
                .InitializeTargeting(selectedUnit.CurrentGridPosition);
        }

        private void OnConfirmPerformed(object sender, EventArgs e)
        {
            if (!ServiceLocator.Get<TurnManager>().IsPlayerTurn())
                return;

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;

            if (currentPhase == GamePhase.FreeMovement)
            {
                if (pendingSneakAction != null)
                    CommitSneakMoveAction();
                else
                    CommitMoveAction();
            }
            else if (currentPhase == GamePhase.ActionTargeting)
            {
                TryExecuteActionAtGridPos(
                    ServiceLocator.Get<TargetingService>().CurrentCursorGridPosition
                );
            }
        }

        private void OnOpenMenuPerformed(object sender, EventArgs e)
        {
            if (!ServiceLocator.Get<TurnManager>().IsPlayerTurn())
                return;

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;

            if (currentPhase == GamePhase.FreeMovement)
            {
                if (selectedUnit != null)
                {
                    GridPosition cellTheyreOver = ServiceLocator
                        .Get<GridSystem>()
                        .GetGridPosition(selectedUnit.transform.position);
                    selectedUnit.SnapToGrid(
                        ServiceLocator.Get<GridSystem>().GetWorldPosition(cellTheyreOver)
                    );
                }

                CommitMoveAction(() =>
                {
                    if (selectedUnit.GetActionPointsRemaining() > 0)
                    {
                        Debug.Log("Opening Menu...");
                        ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.ActionSelection);
                    }
                    else
                    {
                        EndTurn();
                    }
                });
            }
            else if (currentPhase == GamePhase.ActionSelection)
            {
                Debug.Log("Closing Menu...");
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
            }
        }

        private void OnCancelPerformed(object sender, EventArgs e)
        {
            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;

            if (currentPhase == GamePhase.ActionTargeting)
            {
                ServiceLocator.Get<TargetingService>().HideTargeting();
                if (selectedUnit != null)
                    ServiceLocator.Get<CameraController>().SetFollowTarget(selectedUnit.transform);
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.ActionSelection);
            }
            else if (currentPhase == GamePhase.ActionSelection)
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
            else if (currentPhase == GamePhase.FreeMovement)
                ClearSelectedUnit();
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
            Vector3 cellCenterWorld = gridSystem.GetWorldPosition(currentGridPos);
            float cellSize = gridSystem.CellSize;
            float unitRadius = selectedUnit.GetUnitRadius();

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

        private bool IsValidMovePosition(GridPosition pos)
        {
            return validMovePositions != null && validMovePositions.Contains(pos);
        }

        private void TryExecuteActionAtGridPos(GridPosition targetPos)
        {
            if (selectedAction == null)
                return;

            if (!selectedAction.GetValidActionGridPositions().Contains(targetPos))
            {
                Debug.Log("Invalid Target! Cannot attack here.");
                return;
            }

            ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.Busy);
            ServiceLocator.Get<TargetingService>().HideTargeting();
            if (selectedUnit != null)
                ServiceLocator.Get<CameraController>().SetFollowTarget(selectedUnit.transform);

            selectedUnit.SpendActionPoints(selectedAction.GetActionPointsCost());

            selectedAction.TakeAction(
                targetPos,
                () =>
                {
                    OnActionCompleted?.Invoke(this, EventArgs.Empty);
                    CheckTurnEnd();
                }
            );
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

            GridPosition currentPos = ServiceLocator
                .Get<GridSystem>()
                .GetGridPosition(selectedUnit.transform.position);

            if (currentPos != selectedUnit.CurrentGridPosition)
            {
                if (selectedUnit.GetActionPointsRemaining() < 1)
                {
                    Debug.Log("Not enough AP to Stride!");
                    selectedUnit.SnapToGrid(
                        ServiceLocator
                            .Get<GridSystem>()
                            .GetWorldPosition(selectedUnit.CurrentGridPosition)
                    );
                    return;
                }

                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.Busy);

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

                ServiceLocator
                    .Get<ReactionManager>()
                    .EvaluateEvent(
                        moveEvent,
                        (resolvedEvent) =>
                        {
                            if (resolvedEvent.IsCancelled)
                            {
                                selectedUnit.SnapToGrid(
                                    ServiceLocator
                                        .Get<GridSystem>()
                                        .GetWorldPosition(selectedUnit.CurrentGridPosition)
                                );
                            }
                            else
                            {
                                selectedUnit.SpendActionPoints(1);
                                ServiceLocator
                                    .Get<GridSystem>()
                                    .MoveUnit(
                                        selectedUnit,
                                        selectedUnit.CurrentGridPosition,
                                        currentPos
                                    );
                                selectedUnit.FinalizeMove(currentPos);
                                selectedUnit.SnapToGrid(
                                    ServiceLocator.Get<GridSystem>().GetWorldPosition(currentPos)
                                );

                                // Trigger Aura Refresh (Enter/Exit/Stay)
                                UnitAuraEmitter[] allEmitters = FindObjectsByType<UnitAuraEmitter>(
                                    FindObjectsSortMode.None
                                );
                                foreach (var emitter in allEmitters)
                                {
                                    emitter.UpdateAuras(AuraTriggerType.OnEnter);
                                }
                            }

                            OnActionCompleted?.Invoke(this, EventArgs.Empty);
                            onComplete?.Invoke();

                            if (
                                ServiceLocator.Get<PhaseManager>().CurrentPhase
                                != GamePhase.ActionSelection
                            )
                            {
                                CheckTurnEnd();
                            }
                        }
                    );
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        private void CommitSneakMoveAction(Action onComplete = null)
        {
            if (selectedUnit == null || pendingSneakAction == null)
                return;

            GridPosition startPos = pendingSneakStart;
            GridPosition endPos = ServiceLocator
                .Get<GridSystem>()
                .GetGridPosition(selectedUnit.transform.position);

            // If they didn't actually move to a different tile, treat as cancelled.
            if (endPos == startPos)
            {
                pendingSneakAction = null;
                onComplete?.Invoke();
                return;
            }

            // Must be within the current move boundary.
            if (validMovePositions == null || !validMovePositions.Contains(endPos))
            {
                selectedUnit.SnapToGrid(
                    ServiceLocator.Get<GridSystem>().GetWorldPosition(startPos)
                );
                onComplete?.Invoke();
                return;
            }

            if (selectedUnit.GetActionPointsRemaining() < pendingSneakAction.GetActionPointsCost())
            {
                Debug.Log("Not enough AP to Sneak!");
                selectedUnit.SnapToGrid(
                    ServiceLocator.Get<GridSystem>().GetWorldPosition(startPos)
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
                                ServiceLocator.Get<GridSystem>().GetWorldPosition(startPos)
                            );
                        }
                        else
                        {
                            selectedUnit.SpendActionPoints(
                                pendingSneakAction.GetActionPointsCost()
                            );

                            // Move logic: update grid occupancy and finalize logical position.
                            ServiceLocator
                                .Get<GridSystem>()
                                .MoveUnit(selectedUnit, startPos, endPos);

                            // Suppress passive evaluation spam during the Sneak move resolution.
                            StealthResolver.SetPassiveEvaluationSuppressed(selectedUnit, true);
                            selectedUnit.FinalizeMove(endPos);
                            selectedUnit.SnapToGrid(
                                ServiceLocator.Get<GridSystem>().GetWorldPosition(endPos)
                            );

                            // Resolve Sneak at end using a path for "cover throughout" check.
                            List<GridPosition> path = Pathfinding.FindPath(startPos, endPos);
                            StealthResolver.ResolveSneak(
                                selectedUnit,
                                startPos,
                                endPos,
                                path,
                                actorMakesNoise: pendingSneakAction.MakesNoise
                            );
                            StealthResolver.SetPassiveEvaluationSuppressed(selectedUnit, false);

                            // Trigger Aura Refresh (Enter/Exit/Stay)
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

        public List<GridPosition> GetValidMovePositions()
        {
            return validMovePositions == null ? null : new List<GridPosition>(validMovePositions);
        }

        private void CheckTurnEnd()
        {
            if (selectedUnit.GetActionPointsRemaining() <= 0)
            {
                EndTurn();
            }
            else
            {
                // If Sneak is pending, keep half-speed boundary even after actions complete.
                int maxMoveCost = selectedUnit.GetMaxMoveCost();
                if (pendingSneakAction != null)
                    maxMoveCost = Mathf.Max(0, maxMoveCost / 2);

                validMovePositions = Pathfinding.GetReachableGridPositions(
                    selectedUnit.CurrentGridPosition,
                    maxMoveCost
                );
                if (ServiceLocator.Get<PhaseManager>().CurrentPhase != GamePhase.ActionSelection)
                    ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
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
                selectedUnit.StartTurn();
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

            // Trigger visual update (MoveRangeVisualizer listens to this)
            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshMovePositions()
        {
            if (selectedUnit == null)
                return;

            int maxMoveCost = selectedUnit.GetMaxMoveCost();
            if (pendingSneakAction != null)
                maxMoveCost = Mathf.Max(0, maxMoveCost / 2);

            validMovePositions = Pathfinding.GetReachableGridPositions(
                selectedUnit.CurrentGridPosition,
                maxMoveCost
            );
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
            ServiceLocator.Get<CameraController>().ClearFollowTarget();
            OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
            ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.UnitSelection);
        }

        public void EndTurn()
        {
            if (selectedUnit != null)
            {
                Vector3 endPos = ServiceLocator
                    .Get<GridSystem>()
                    .GetWorldPosition(selectedUnit.CurrentGridPosition);
                selectedUnit.SnapToGrid(endPos);
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
    }
}
