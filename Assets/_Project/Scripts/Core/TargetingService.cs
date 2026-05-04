using System.Collections.Generic;
using TacticsGame.Actions;
using TacticsGame.Characters;
using TacticsGame.Grid;
using TacticsGame.InputSystem;
using TacticsGame.Spells;
using TacticsGame.UI;
using UnityEngine;

namespace TacticsGame.Core
{
    public class TargetingService : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        private Transform gridCursorVisual;

        private GridPosition currentCursorGridPosition;
        private float cursorMoveTimer;

        public GridPosition CurrentCursorGridPosition => currentCursorGridPosition;
        public Vector3Int CurrentTargetLayeredPosition { get; private set; }
        private bool wasInEagleEyeBeforeTargeting;
        private Vector3 baseCursorScale = Vector3.one;

        private void Awake()
        {
            ServiceLocator.Register(this);

            if (gridCursorVisual != null)
            {
                gridCursorVisual.gameObject.SetActive(false);
                baseCursorScale = gridCursorVisual.localScale;
            }
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<TargetingService>();
        }

        public void InitializeTargeting(
            GridPosition startPosition,
            BaseAction actionOverride = null
        )
        {
            currentCursorGridPosition = startPosition;

            // Assume the start position is on its natural layer
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            CurrentTargetLayeredPosition = grid.ResolveClosestLayeredPosition(startPosition, 0);
            if (gridCursorVisual != null)
            {
                BaseAction selectedAction =
                    actionOverride ?? ServiceLocator.Get<UnitActionSystem>().GetSelectedAction();

                bool showCursor = selectedAction == null || !selectedAction.IsUnitTargeted;
                gridCursorVisual.gameObject.SetActive(showCursor);

                UpdateCursorVisual(selectedAction);

                var cam = ServiceLocator.Get<CameraController>();
                wasInEagleEyeBeforeTargeting = cam.IsEagleEyeActive;

                // Spell targeting uses birdseye camera
                if (selectedAction is CastSpellAction)
                {
                    cam.EnterEagleEyeMode(gridCursorVisual);
                }
                else
                {
                    cam.SetFollowTarget(gridCursorVisual);
                }
            }
        }

        public void HideTargeting()
        {
            if (gridCursorVisual != null)
            {
                gridCursorVisual.gameObject.SetActive(false);
            }
            if (ServiceLocator.TryGet<UnitTooltipUI>(out var ui))
            {
                ui.Hide();
            }

            // Reset cameras - only exit EagleEye if we weren't already in it manually
            if (!wasInEagleEyeBeforeTargeting)
            {
                ServiceLocator.Get<CameraController>().ExitEagleEyeMode();
            }

            // Clear AoE preview when exiting targeting
            if (ServiceLocator.TryGet<AoEVisualizer>(out var aoeVis))
            {
                aoeVis.HidePreview();
            }
        }

        public void HandleCursorMovement(BaseAction selectedAction)
        {
            cursorMoveTimer -= Time.deltaTime;

            InputService inputService = ServiceLocator.Get<InputService>();
            Vector2 input = inputService.GetMovementVectorNormalized();

            // Layer cycling
            int layerInput = inputService.GetLayerCycleInput();
            if (layerInput != 0 && gridCursorVisual != null)
            {
                GridCursor cursorScript = gridCursorVisual.GetComponent<GridCursor>();
                if (cursorScript != null && cursorScript.CycleLayer(layerInput))
                {
                    UpdateCursorVisual(selectedAction);
                }
            }

            if (input != Vector2.zero && cursorMoveTimer <= 0f)
            {
                cursorMoveTimer = 0.15f; // Cooldown

                Transform cameraTransform = Camera.main.transform;
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();

                Vector3 moveDirWorld = (forward * input.y + right * input.x).normalized;

                int moveX = 0;
                int moveZ = 0;

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

                GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
                GridCursor cursorScript = gridCursorVisual.GetComponent<GridCursor>();

                if (gridSystem.IsValidGridPosition(newPos))
                {
                    // Search for the nearest valid tile in a 3-tile wide sweep in the input direction
                    GridPosition targetSnapPos = currentCursorGridPosition;
                    bool foundValidSnap = false;
                    int searchLimit = 10;

                    int perpX = moveZ != 0 ? 1 : 0;
                    int perpZ = moveX != 0 ? 1 : 0;

                    for (int i = 1; i <= searchLimit; i++)
                    {
                        // Check in order: Center (0), then Sides (-1, 1)
                        int[] offsets = { 0, -1, 1 };
                        foreach (int offset in offsets)
                        {
                            GridPosition testPos = new GridPosition(
                                currentCursorGridPosition.x + (moveX * i) + (perpX * offset),
                                currentCursorGridPosition.z + (moveZ * i) + (perpZ * offset)
                            );

                            if (!gridSystem.IsValidGridPosition(testPos))
                                continue;

                            int refY =
                                cursorScript != null
                                    ? cursorScript.CurrentLayeredPosition.y
                                    : CurrentTargetLayeredPosition.y;

                            Vector3Int testPos3D = gridSystem.ResolveClosestLayeredPosition(
                                testPos,
                                refY
                            );
                            var rangePositions = selectedAction?.GetActionRangeGridPositions();

                            bool inRange =
                                (selectedAction == null)
                                || (rangePositions != null && rangePositions.Contains(testPos3D));

                            if (inRange)
                            {
                                targetSnapPos = testPos;
                                foundValidSnap = true;
                                break;
                            }
                        }

                        if (foundValidSnap)
                            break;
                    }

                    if (foundValidSnap)
                    {
                        currentCursorGridPosition = targetSnapPos;
                        UpdateCursorVisual(selectedAction);
                    }
                }
            }
            else if (input != Vector2.zero && cursorMoveTimer > 0f)
            {
                // not logging every frame to avoid spam
            }
            else if (input == Vector2.zero)
            {
                cursorMoveTimer = 0f;
            }
        }

        private void UpdateCursorVisual(BaseAction selectedAction)
        {
            if (gridCursorVisual != null)
            {
                GridCursor cursorScript = gridCursorVisual.GetComponent<GridCursor>();
                if (cursorScript == null)
                {
                    // Search children just in case the script is on a sub-object
                    cursorScript = gridCursorVisual.GetComponentInChildren<GridCursor>();
                }

                Debug.Log(
                    $"[TargetingService] UpdateCursorVisual called with action: {(selectedAction != null ? selectedAction.GetActionName() : "NULL")} (Type: {selectedAction?.GetType().FullName}) | CursorScript={cursorScript != null}"
                );

                // Determine if this is an intersection-targeted spell (Bursts and Cones)
                bool isIntersectionTargeted = false;
                if (
                    selectedAction is CastSpellAction spellAction
                    && spellAction.GetCurrentSpell() != null
                )
                {
                    var shape = spellAction.GetCurrentSpell().Area.Shape;
                    isIntersectionTargeted = (
                        shape == Data.TacticsRuleset.AreaShape.Burst
                        || shape == Data.TacticsRuleset.AreaShape.Cone
                    );
                }

                if (cursorScript != null)
                {
                    // Snap the cursor visual to 2x2 for intersection targeting
                    if (isIntersectionTargeted)
                        cursorScript.SetCursorSize(2);
                    else
                        cursorScript.ResetCursorSize();

                    cursorScript.SetPosition(currentCursorGridPosition);
                    CurrentTargetLayeredPosition = cursorScript.CurrentLayeredPosition;

                    if (selectedAction != null)
                    {
                        var validPositions = selectedAction.GetValidActionGridPositions();
                        bool isValidTarget = validPositions.Contains(CurrentTargetLayeredPosition);
                        cursorScript.SetValidState(isValidTarget);
                    }
                }
                else
                {
                    GridSystem grid = ServiceLocator.Get<GridSystem>();
                    CurrentTargetLayeredPosition = grid.ResolveClosestLayeredPosition(
                        currentCursorGridPosition,
                        CurrentTargetLayeredPosition.y
                    );

                    Vector3 worldPos = grid.GetWorldPosition(CurrentTargetLayeredPosition);
                    float scaleMultiplier = 1f;

                    if (isIntersectionTargeted)
                    {
                        worldPos += new Vector3(grid.CellSize * 0.5f, 0, grid.CellSize * 0.5f);
                        scaleMultiplier = 2f;
                    }
                    else
                    {
                        scaleMultiplier = 1f;
                    }

                    gridCursorVisual.position = worldPos;
                    gridCursorVisual.localScale = baseCursorScale * scaleMultiplier;
                }

                Unit unitAtCursor = ServiceLocator
                    .Get<GridSystem>()
                    .GetUnitAt(currentCursorGridPosition);
                if (unitAtCursor != null)
                {
                    ServiceLocator.Get<UnitTooltipUI>().Show(unitAtCursor);
                }
                else
                {
                    ServiceLocator.Get<UnitTooltipUI>().Hide();
                }

                // Update AoE preview if this is a spell action
                if (selectedAction is CastSpellAction spellAct)
                {
                    bool foundAoEVis = ServiceLocator.TryGet<AoEVisualizer>(out var aoeVis);
                    Debug.Log(
                        $"[TargetingService] Detected spell '{spellAct.GetActionName()}'. AoEVisualizer Found? {foundAoEVis}"
                    );

                    if (foundAoEVis)
                    {
                        if (cursorScript != null)
                        {
                            // Pass the exact origin (intersection or center) to the visualizer
                            aoeVis.UpdateAoEPreview(cursorScript.CurrentLayeredPosition, spellAct);
                        }
                        else
                        {
                            // FALLBACK: If the cursor script is missing, derive the 3D position manually
                            // This allows AoE visuals to work even without the specialized GridCursor component.
                            Vector3Int pos3D = ServiceLocator
                                .Get<GridSystem>()
                                .ResolveClosestLayeredPosition(currentCursorGridPosition, 0);
                            aoeVis.UpdateAoEPreview(pos3D, spellAct);

                            Debug.LogWarning(
                                "[TargetingService] UpdateAoEPreview called with manual fallback (GridCursor component missing). Intersection snapping will not be active."
                            );
                        }
                    }
                }
                else
                {
                    // Not a spell - clear any lingering AoE preview
                    if (ServiceLocator.TryGet<AoEVisualizer>(out var aoeVis))
                    {
                        aoeVis.HidePreview();
                    }
                }
            }
        }
    }
}
