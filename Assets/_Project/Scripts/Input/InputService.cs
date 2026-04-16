using System;
using PathfinderTactics.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PathfinderTactics.InputSystem
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

        private PlayerInputActions playerInputActions;

        private void Awake()
        {
            ServiceLocator.Register(this);
            playerInputActions = new PlayerInputActions();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<InputService>();
        }

        private void OnEnable()
        {
            playerInputActions.Player.Enable();
            playerInputActions.Player.Select.performed += ctx =>
                InvokeEventIfValid(OnSelectPerformed);
            playerInputActions.Player.Confirm.performed += ctx =>
                InvokeEventIfValid(OnConfirmPerformed);
            playerInputActions.Player.Cancel.performed += ctx =>
                InvokeEventIfValid(OnCancelPerformed);
            playerInputActions.Player.Jump.performed += ctx => InvokeEventIfValid(OnJumpPerformed);
            playerInputActions.Player.OpenMenu.performed += ctx =>
                InvokeEventIfValid(OnOpenMenuPerformed);
            playerInputActions.Player.EndTurn.performed += ctx =>
                InvokeEventIfValid(OnEndTurnPerformed);
            playerInputActions.Player.LayerUp.performed += ctx =>
                InvokeEventIfValid(OnLayerUpPerformed);
            playerInputActions.Player.LayerDown.performed += ctx =>
                InvokeEventIfValid(OnLayerDownPerformed);
            playerInputActions.Player.EagleEye.performed += ctx =>
                InvokeEventIfValid(OnEagleEyePerformed);
        }

        private void OnDisable()
        {
            playerInputActions.Player.Disable();
            // In a deeper implementation, unsubscribe these, but disabling the map is usually enough in Unity.
        }

        private void InvokeEventIfValid(EventHandler ev)
        {
            // PREVENT UI CLICK-THROUGH CRASH / Blocking inputs when clicking on UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            ev?.Invoke(this, EventArgs.Empty);
        }

        public Vector2 GetMovementVectorNormalized()
        {
            return playerInputActions.Player.Move.ReadValue<Vector2>();
        }

        public Vector2 GetRotationVector()
        {
            return playerInputActions.Player.Rotate.ReadValue<Vector2>();
        }

        public Vector2 GetMousePosition()
        {
            return Mouse.current.position.ReadValue();
        }

        /// <summary>
        /// Returns +1 if layer-up was pressed this frame, -1 if layer-down,
        /// 0 otherwise.
        /// </summary>
        public int GetLayerCycleInput()
        {
            if (playerInputActions.Player.LayerUp.WasPressedThisFrame())
                return 1;
            if (playerInputActions.Player.LayerDown.WasPressedThisFrame())
                return -1;
            return 0;
        }
    }
}
