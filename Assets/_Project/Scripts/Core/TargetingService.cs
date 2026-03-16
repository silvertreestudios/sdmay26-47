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
                UpdateCursorVisual(ServiceLocator.Get<UnitActionSystem>().GetSelectedAction());
                ServiceLocator.Get<CameraController>().SetFollowTarget(gridCursorVisual);
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
            // Clear AoE preview when exiting targeting
            if (ServiceLocator.TryGet<SpellAoEVisualizer>(out var aoeVis))
            {
                aoeVis.HidePreview();
            }
        }

        public void HandleCursorMovement(BaseAction selectedAction)
        {
            cursorMoveTimer -= Time.deltaTime;

            Vector2 input = ServiceLocator.Get<InputService>().GetMovementVectorNormalized();

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

                if (ServiceLocator.Get<GridSystem>().IsValidGridPosition(newPos))
                {
                    if (
                        selectedAction != null
                        && selectedAction.GetActionRangeGridPositions().Contains(newPos)
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

                // Determine if this is a burst spell (needs 2x2 cursor)
                bool isBurstSpell = false;
                if (
                    selectedAction is CastSpellAction spellAction
                    && spellAction.GetCurrentSpell() != null
                )
                {
                    isBurstSpell =
                        spellAction.GetCurrentSpell().Area.Shape == Data.PF2e.AreaShape.Burst;
                }

                // Set cursor size: 2x2 for burst, 1x1 for everything else
                if (cursorScript != null)
                {
                    if (isBurstSpell)
                        cursorScript.SetCursorSize(2);
                    else
                        cursorScript.ResetCursorSize();

                    // Use GridCursor.SetPosition - handles intersection offset for 2x2 internally
                    cursorScript.SetPosition(currentCursorGridPosition);

                    if (selectedAction != null)
                    {
                        bool isValidTarget = selectedAction
                            .GetValidActionGridPositions()
                            .Contains(currentCursorGridPosition);
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
                    if (ServiceLocator.TryGet<SpellAoEVisualizer>(out var aoeVis))
                    {
                        aoeVis.UpdateAoEPreview(currentCursorGridPosition, spellAct);
                    }
                }
                else
                {
                    // Not a spell - clear any lingering AoE preview
                    if (ServiceLocator.TryGet<SpellAoEVisualizer>(out var aoeVis))
                    {
                        aoeVis.HidePreview();
                    }
                }
            }
        }
    }
}
