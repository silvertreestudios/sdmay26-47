using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
using PathfinderTactics.InputSystem;
using PathfinderTactics.Spells;
using PathfinderTactics.UI;
using UnityEngine;

namespace PathfinderTactics.Core
{
    public class TargetingService : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        private Transform gridCursorVisual;

        private GridPosition currentCursorGridPosition;
        private float cursorMoveTimer;

        public GridPosition CurrentCursorGridPosition => currentCursorGridPosition;
        private bool wasInEagleEyeBeforeTargeting;

        private void Awake()
        {
            ServiceLocator.Register(this);

            if (gridCursorVisual != null)
                gridCursorVisual.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<TargetingService>();
        }

        public void InitializeTargeting(GridPosition startPosition)
        {
            currentCursorGridPosition = startPosition;
            if (gridCursorVisual != null)
            {
                gridCursorVisual.gameObject.SetActive(true);

                BaseAction selectedAction = ServiceLocator
                    .Get<UnitActionSystem>()
                    .GetSelectedAction();
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
                    // Resolve the new column position to the correct elevation for range checking
                    int refY = cursorScript != null ? cursorScript.CurrentLayeredPosition.y : 0;
                    Vector3Int newPos3D = gridSystem.ResolveClosestLayeredPosition(newPos, refY);

                    if (
                        selectedAction != null
                        && selectedAction.GetActionRangeGridPositions().Contains(newPos3D)
                    )
                    {
                        currentCursorGridPosition = newPos;
                        UpdateCursorVisual(selectedAction);
                    }
                }
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

                Debug.Log(
                    $"[TargetingService] UpdateCursorVisual called with action: {(selectedAction != null ? selectedAction.GetActionName() : "NULL")} (Type: {selectedAction?.GetType().FullName})"
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
                        shape == Data.PF2e.AreaShape.Burst || shape == Data.PF2e.AreaShape.Cone
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

                    if (selectedAction != null)
                    {
                        bool isValidTarget = selectedAction
                            .GetValidActionGridPositions()
                            .Contains(cursorScript.CurrentLayeredPosition);
                        cursorScript.SetValidState(isValidTarget);
                    }
                }
                else
                {
                    gridCursorVisual.position = ServiceLocator
                        .Get<GridSystem>()
                        .GetWorldPosition(currentCursorGridPosition);
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
