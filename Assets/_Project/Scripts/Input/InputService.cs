using System;
using TacticsGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TacticsGame.InputSystem
{
    public class InputService : MonoBehaviour
    {
        public event EventHandler OnSelectPerformed;
        public event EventHandler OnConfirmPerformed;
        public event EventHandler OnCancelPerformed;
        public event EventHandler OnJumpPerformed;
        public event EventHandler OnOpenMenuPerformed;
        public event EventHandler OnEndTurnPerformed;
        public event EventHandler OnLayerUpPerformed;
        public event EventHandler OnLayerDownPerformed;
        public event EventHandler OnEagleEyePerformed;
        public event EventHandler OnToggleWaypointPerformed;
        public event EventHandler OnPausePerformed;

        // UI Map Events
        public event EventHandler OnUIPageNextPerformed;
        public event EventHandler OnUIPagePrevPerformed;
        public event EventHandler OnUIStepNextPerformed;
        public event EventHandler OnUIStepPrevPerformed;
        public event EventHandler OnUIToggleStepsPerformed;
        public event EventHandler OnUIConfirmPerformed;
        public event EventHandler OnUICancelPerformed;
        public event EventHandler OnAdvanceDialoguePerformed;

        private PlayerInputActions playerInputActions;

        private void Awake()
        {
            ServiceLocator.Register(this);
            playerInputActions = new PlayerInputActions();

            if (gameObject.GetComponent<HapticService>() == null)
                gameObject.AddComponent<HapticService>();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<InputService>();
        }

        private void OnEnable()
        {
            playerInputActions.Player.Enable();
            playerInputActions.Player.Select.performed += HandleSelect;
            playerInputActions.Player.Confirm.performed += HandleConfirm;
            playerInputActions.Player.Cancel.performed += HandleCancel;
            playerInputActions.Player.Jump.performed += HandleJump;
            playerInputActions.Player.OpenMenu.performed += HandleOpenMenu;
            playerInputActions.Player.EndTurn.performed += HandleEndTurn;
            playerInputActions.Player.LayerUp.performed += HandleLayerUp;
            playerInputActions.Player.LayerDown.performed += HandleLayerDown;
            playerInputActions.Player.EagleEye.performed += HandleEagleEye;
            playerInputActions.Player.ToggleWaypoint.performed += HandleToggleWaypoint;
            playerInputActions.Player.Pause.performed += HandlePause;

            playerInputActions.UI.PageNext.performed += HandleUIPageNext;
            playerInputActions.UI.PagePrev.performed += HandleUIPagePrev;
            playerInputActions.UI.StepNext.performed += HandleUIStepNext;
            playerInputActions.UI.StepPrev.performed += HandleUIStepPrev;
            playerInputActions.UI.ToggleSteps.performed += HandleUIToggleSteps;
            playerInputActions.UI.Submit.performed += HandleUIConfirm;
            playerInputActions.UI.Cancel.performed += HandleUICancel;
            playerInputActions.UI.Pause.performed += HandlePause;
            playerInputActions.UI.AdvanceDialogue.performed += HandleAdvanceDialogue;
        }

        private void OnDisable()
        {
            playerInputActions.Player.Select.performed -= HandleSelect;
            playerInputActions.Player.Confirm.performed -= HandleConfirm;
            playerInputActions.Player.Cancel.performed -= HandleCancel;
            playerInputActions.Player.Jump.performed -= HandleJump;
            playerInputActions.Player.OpenMenu.performed -= HandleOpenMenu;
            playerInputActions.Player.EndTurn.performed -= HandleEndTurn;
            playerInputActions.Player.LayerUp.performed -= HandleLayerUp;
            playerInputActions.Player.LayerDown.performed -= HandleLayerDown;
            playerInputActions.Player.EagleEye.performed -= HandleEagleEye;
            playerInputActions.Player.ToggleWaypoint.performed -= HandleToggleWaypoint;
            playerInputActions.Player.Pause.performed -= HandlePause;

            playerInputActions.UI.PageNext.performed -= HandleUIPageNext;
            playerInputActions.UI.PagePrev.performed -= HandleUIPagePrev;
            playerInputActions.UI.StepNext.performed -= HandleUIStepNext;
            playerInputActions.UI.StepPrev.performed -= HandleUIStepPrev;
            playerInputActions.UI.ToggleSteps.performed -= HandleUIToggleSteps;
            playerInputActions.UI.Submit.performed -= HandleUIConfirm;
            playerInputActions.UI.Cancel.performed -= HandleUICancel;
            playerInputActions.UI.Pause.performed -= HandlePause;
            playerInputActions.UI.AdvanceDialogue.performed -= HandleAdvanceDialogue;

            playerInputActions.Player.Disable();
            playerInputActions.UI.Disable();
        }

        private void HandleSelect(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnSelectPerformed, IsPointerInput(ctx));

        private void HandleConfirm(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnConfirmPerformed, IsPointerInput(ctx));

        private void HandleCancel(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnCancelPerformed, IsPointerInput(ctx));

        private void HandleJump(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnJumpPerformed, IsPointerInput(ctx));

        private void HandleOpenMenu(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnOpenMenuPerformed, IsPointerInput(ctx));

        private void HandleEndTurn(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnEndTurnPerformed, IsPointerInput(ctx));

        private void HandleLayerUp(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnLayerUpPerformed, IsPointerInput(ctx));

        private void HandleLayerDown(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnLayerDownPerformed, IsPointerInput(ctx));

        private void HandleEagleEye(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnEagleEyePerformed, IsPointerInput(ctx));

        private void HandleToggleWaypoint(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnToggleWaypointPerformed, IsPointerInput(ctx));

        private void HandlePause(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnPausePerformed, IsPointerInput(ctx));

        private void HandleUIPageNext(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnUIPageNextPerformed, IsPointerInput(ctx));

        private void HandleUIPagePrev(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnUIPagePrevPerformed, IsPointerInput(ctx));

        private void HandleUIStepNext(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnUIStepNextPerformed, IsPointerInput(ctx));

        private void HandleUIStepPrev(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnUIStepPrevPerformed, IsPointerInput(ctx));

        private void HandleUIToggleSteps(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnUIToggleStepsPerformed, IsPointerInput(ctx));

        private void HandleAdvanceDialogue(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnAdvanceDialoguePerformed, IsPointerInput(ctx));

        private void HandleUIConfirm(InputAction.CallbackContext ctx)
        {
            Debug.Log("[INPUT] UI Confirm Performed");
            InvokeEventIfValid(OnUIConfirmPerformed, IsPointerInput(ctx));
        }

        private void HandleUICancel(InputAction.CallbackContext ctx) =>
            InvokeEventIfValid(OnUICancelPerformed, IsPointerInput(ctx));

        private bool IsPointerInput(InputAction.CallbackContext ctx)
        {
            return ctx.control?.device is Pointer;
        }

        private void InvokeEventIfValid(EventHandler ev, bool isPointerInput)
        {
            // PREVENT UI CLICK-THROUGH CRASH / Blocking inputs when clicking on UI
            if (
                isPointerInput
                && EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject()
            )
                return;

            ev?.Invoke(this, EventArgs.Empty);
        }

        public Vector2 GetMovementVectorNormalized()
        {
            if (playerInputActions.UI.enabled)
                return playerInputActions.UI.Navigate.ReadValue<Vector2>();
            return playerInputActions.Player.Move.ReadValue<Vector2>();
        }

        public bool IsMovementFromKeyboard()
        {
            var action = playerInputActions.UI.enabled
                ? playerInputActions.UI.Navigate
                : playerInputActions.Player.Move;

            return action.activeControl?.device is UnityEngine.InputSystem.Keyboard;
        }

        public Vector2 GetRotationVector()
        {
            if (playerInputActions.UI.enabled)
                return playerInputActions.UI.Rotate.ReadValue<Vector2>();
            return playerInputActions.Player.Rotate.ReadValue<Vector2>();
        }

        public Vector2 GetMousePosition()
        {
            return Mouse.current.position.ReadValue();
        }

        public bool IsAnyButtonHeld()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
                return true;
            if (
                Mouse.current != null
                && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed)
            )
                return true;
            if (Gamepad.current != null)
            {
                foreach (var control in Gamepad.current.allControls)
                {
                    if (control.IsPressed())
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns +1 if layer-up was pressed this frame, -1 if layer-down,
        /// 0 otherwise.
        /// </summary>
        public int GetLayerCycleInput()
        {
            if (playerInputActions.UI.enabled)
            {
                if (playerInputActions.UI.PageNext.WasPressedThisFrame())
                    return 1;
                if (playerInputActions.UI.PagePrev.WasPressedThisFrame())
                    return -1;
                return 0;
            }

            if (playerInputActions.Player.LayerUp.WasPressedThisFrame())
                return 1;
            if (playerInputActions.Player.LayerDown.WasPressedThisFrame())
                return -1;
            return 0;
        }

        public void SwitchToActionMap(string mapName)
        {
            playerInputActions.Player.Disable();
            playerInputActions.UI.Disable();

            if (mapName == "UI")
                playerInputActions.UI.Enable();
            else
                playerInputActions.Player.Enable();
        }
    }
}
