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

        private List<Vector3Int> validMovePositions;
        private HashSet<Vector2Int> validMoveColumns;

        public Unit SelectedUnit => selectedUnit;

        private SneakAction pendingSneakAction;
        private GridPosition pendingSneakStart;
        private Vector3Int pendingSneakStartLayered;

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
                int maxMoveCost = selectedUnit.GetMaxMoveCost();
                if (pendingSneakAction != null)
                    maxMoveCost = Mathf.Max(0, maxMoveCost / 2);

                SetValidMovePositions(
                    Pathfinding.GetReachablePositions(
                        selectedUnit.CurrentLayeredPosition,
                        maxMoveCost
                    )
                );
            }

            if (newPhase != GamePhase.ActionTargeting)
            {
                if (ServiceLocator.TryGet(out PathfinderTactics.UI.UnitTooltipUI tooltipUI))
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

            ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.ActionTargeting);

            if (selectedAction != null && selectedAction.IsUnitTargeted)
            {
                if (ServiceLocator.TryGet<TargetLockService>(out var tls))
                {
                    tls.InitializeTargeting(selectedUnit, selectedAction);

                    if (!tls.IsActive)
                    {
                        Debug.LogWarning(
                            "[UnitActionSystem] TargetLockService failed to activate. Reverting to ActionSelection."
                        );
                        ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.ActionSelection);
                    }
                }
            }
            else
            {
                ServiceLocator
                    .Get<TargetingService>()
                    .InitializeTargeting(selectedUnit.CurrentGridPosition);
            }
        }

        private void OnConfirmPerformed(object sender, EventArgs e)
        {
            if (!AllowInteraction("Confirm"))
                return;

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
                Vector3Int targetPos;
                if (
                    selectedAction != null
                    && selectedAction.IsUnitTargeted
                    && ServiceLocator.TryGet<TargetLockService>(out var tls)
                    && tls.IsActive
                )
                {
                    targetPos = tls.CurrentTargetLayeredPosition;
                }
                else
                {
                    targetPos =
                        GridCursor.Instance != null
                            ? GridCursor.Instance.CurrentLayeredPosition
                            : new Vector3Int(
                                ServiceLocator.Get<TargetingService>().CurrentCursorGridPosition.x,
                                0,
                                ServiceLocator.Get<TargetingService>().CurrentCursorGridPosition.z
                            );
                }
                TryExecuteActionAtGridPos(targetPos);
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
                    GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
                    Vector3Int snappedPos = gridSystem.GetLayeredGridPosition(
                        selectedUnit.transform.position
                    );
                    selectedUnit.SnapToGrid(gridSystem.GetWorldPosition(snappedPos));
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

        private GamePhase preEagleEyePhase;

        private void OnCancelPerformed(object sender, EventArgs e)
        {
            Debug.Log("[UnitActionSystem] OnCancelPerformed");

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
                else
                {
                    ServiceLocator.Get<TargetingService>().HideTargeting();
                }
                if (selectedUnit != null)
                    ServiceLocator.Get<CameraController>().SetFollowTarget(selectedUnit.transform);
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.ActionSelection);
            }
            else if (currentPhase == GamePhase.ActionSelection)
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.FreeMovement);
            else if (currentPhase == GamePhase.FreeMovement)
            {
                preEagleEyePhase = currentPhase;
                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.EagleEye);
                ServiceLocator.Get<CameraController>().EnterEagleEyeMode();
            }
            else if (currentPhase == GamePhase.EagleEye)
            {
                ServiceLocator.Get<PhaseManager>().SetPhase(preEagleEyePhase);
                ServiceLocator.Get<CameraController>().ExitEagleEyeMode();
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
                Debug.Log("Invalid Target! Cannot attack here.");
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

            selectedAction.TakeAction(
                targetPos,
                () =>
                {
                    OnActionCompleted?.Invoke(this, EventArgs.Empty);
                    CheckTurnEnd();
                }
            );

            // Now that TakeAction has successfully pulled the unit, we can wipe the visuals.
            if (confirmTls != null)
            {
                confirmTls.HideTargeting();
            }
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

            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            GridPosition currentPos = gridSystem.GetGridPosition(selectedUnit.transform.position);
            Vector3Int currentLayered = gridSystem.GetLayeredGridPosition(
                selectedUnit.transform.position
            );

            bool hasMoved = currentLayered != selectedUnit.CurrentLayeredPosition;

            if (hasMoved)
            {
                if (selectedUnit.GetActionPointsRemaining() < 1)
                {
                    Debug.Log("Not enough AP to Stride!");
                    selectedUnit.SnapToGrid(
                        gridSystem.GetWorldPosition(selectedUnit.CurrentLayeredPosition)
                    );
                    return;
                }

                ServiceLocator.Get<PhaseManager>().SetPhase(GamePhase.Busy);

                int distanceX = Mathf.Abs(currentPos.x - selectedUnit.CurrentGridPosition.x);
                int distanceZ = Mathf.Abs(currentPos.z - selectedUnit.CurrentGridPosition.z);
                int distanceY = Mathf.Abs(currentLayered.y - selectedUnit.CurrentLayeredPosition.y);
                int totalDistance = Mathf.Max(distanceX, Mathf.Max(distanceZ, distanceY));

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
                                    gridSystem.GetWorldPosition(selectedUnit.CurrentLayeredPosition)
                                );
                            }
                            else
                            {
                                // Failsafe check for occupancy
                                if (IsOccupiedByOther(currentPos.x, currentPos.z, currentLayered.y))
                                {
                                    Debug.LogWarning(
                                        $"[UnitActionSystem] Move cancelled. Target destination {currentLayered} is occupied."
                                    );
                                    selectedUnit.SnapToGrid(
                                        gridSystem.GetWorldPosition(
                                            selectedUnit.CurrentLayeredPosition
                                        )
                                    );
                                    onComplete?.Invoke();
                                    return;
                                }

                                selectedUnit.SpendActionPoints(1);
                                gridSystem.MoveUnit(
                                    selectedUnit,
                                    selectedUnit.CurrentLayeredPosition,
                                    currentLayered
                                );
                                selectedUnit.FinalizeMove(currentLayered);
                                selectedUnit.SnapToGrid(
                                    gridSystem.GetWorldPosition(currentLayered)
                                );

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
                Debug.Log("Not enough AP to Sneak!");
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
        }

        private void CheckTurnEnd()
        {
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
            if (selectedUnit == null)
                return;

            int maxMoveCost = selectedUnit.GetMaxMoveCost();
            if (pendingSneakAction != null)
                maxMoveCost = Mathf.Max(0, maxMoveCost / 2);

            SetValidMovePositions(
                Pathfinding.GetReachablePositions(selectedUnit.CurrentLayeredPosition, maxMoveCost)
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
            pendingSneakAction = null;
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
    }
}
