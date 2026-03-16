using Unity.Cinemachine;
using UnityEngine;

namespace PathfinderTactics.Core
{
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private CinemachineCamera virtualCamera;

        [Header("Camera Settings")]
        [SerializeField]
        private float moveSpeed = 10f;

        [SerializeField]
        private float rotationSpeed = 100f;

        private PlayerInputActions playerInputActions;
        private CinemachineOrbitalFollow orbitalFollow;

        private void Awake()
        {
            ServiceLocator.Register(this);

            playerInputActions = new PlayerInputActions();

            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow == null)
            {
                Debug.LogError("No CinemachineOrbitalFollow component found on this GameObject!");
            }
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<CameraController>();
        }

        private void OnEnable()
        {
            playerInputActions.Player.Enable();
        }

        private void OnDisable()
        {
            playerInputActions.Player.Disable();
        }

        private void Update()
        {
            // If a follow target is set, orbit. Otherwise, free fly.
            if (virtualCamera != null && virtualCamera.Follow != null)
            {
                HandleOrbitRotation();
            }
            else
            {
                HandleFreeCamMovement();
                HandleFreeCamRotation();
            }
        }

        /// <summary>
        /// Assigns the follow target to the Cinemachine Camera.
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            if (virtualCamera != null)
            {
                virtualCamera.Follow = target;
            }
        }

        /// <summary>
        /// Clears the follow target, returning to free movement mode.
        /// </summary>
        public void ClearFollowTarget()
        {
            if (virtualCamera != null)
            {
                virtualCamera.Follow = null;
            }
        }

        /// <summary>
        /// Free movement when no target is followed.
        /// </summary>
        private void HandleFreeCamMovement()
        {
            Vector2 inputMoveDir = playerInputActions.Player.Move.ReadValue<Vector2>();

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            Vector3 moveVector = (forward * inputMoveDir.y + right * inputMoveDir.x);
            transform.position += moveVector * moveSpeed * Time.deltaTime;
        }

        /// <summary>
        /// Rotate the camera freely (no follow target).
        /// </summary>
        private void HandleFreeCamRotation()
        {
            Vector2 inputRotateDir = playerInputActions.Player.Rotate.ReadValue<Vector2>();
            float yaw = inputRotateDir.x * rotationSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, yaw, Space.World);
        }

        /// <summary>
        /// Orbit around the assigned follow target using CinemachineOrbitalFollow.
        /// </summary>
        private void HandleOrbitRotation()
        {
            if (orbitalFollow == null)
                return;

            Vector2 inputRotateDir = playerInputActions.Player.Rotate.ReadValue<Vector2>();

            float yaw = inputRotateDir.x * rotationSpeed * Time.deltaTime;
            float pitch = inputRotateDir.y * rotationSpeed * Time.deltaTime;

            // Horizontal movement
            orbitalFollow.HorizontalAxis.Value += yaw;

            // Vertical movement
            orbitalFollow.VerticalAxis.Value -= pitch;
        }
    }
}
