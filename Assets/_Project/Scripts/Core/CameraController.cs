using System.Collections;
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

        [Header("Over-the-Shoulder Targeting")]
        [SerializeField]
        private float otsDistance = 3.5f;

        [SerializeField]
        private float otsHeight = 2.5f;

        [SerializeField]
        private float otsShoulderOffset = 1.2f;

        [SerializeField]
        private float otsPositionSmoothTime = 0.12f;

        [SerializeField]
        private float otsRotationSpeed = 14f;

        [SerializeField]
        private float otsTargetHeight = 1.2f;

        [Tooltip("How long the fast snap lasts when entering OTS from the action menu.")]
        [SerializeField]
        private float otsEntrySnapDuration = 0.15f;

        [Tooltip("Position smoothing during entry snap.")]
        [SerializeField]
        private float otsEntryPositionSmoothTime = 0.008f;

        [Tooltip("Slower smoothing when panning between targets (after initial entry).")]
        [SerializeField]
        private float otsBetweenTargetSmoothTime = 0.38f;

        [Tooltip("How long the slower between-target pan lasts after each target change.")]
        [SerializeField]
        private float otsBetweenTargetPanDuration = 0.65f;

        [Tooltip("Cinemachine blend from gameplay camera into OTS.")]
        [SerializeField]
        private CinemachineBlendDefinition.Styles otsEntryBlendStyle = CinemachineBlendDefinition
            .Styles
            .EaseInOut;

        [Tooltip("Duration of the blend into OTS when opening Strike targeting.")]
        [SerializeField]
        [Min(0.05f)]
        private float otsEntryBlendDuration = 0.22f;

        private PlayerInputActions playerInputActions;
        private CinemachineOrbitalFollow orbitalFollow;

        private CinemachineCamera otsVirtualCamera;
        private Transform otsAttacker;
        private Transform otsTarget;
        private Vector3 otsPositionVelocity;
        private bool isOTSActive;
        private float otsModeEnterTime;
        private float otsLastTargetSwitchTime = -1f;

        private CinemachineBrain cinemachineBrain;
        private CinemachineBlendDefinition savedDefaultBlend;
        private bool hasSavedDefaultBlend;
        private Coroutine restoreBlendCoroutine;

        public bool IsOTSActive => isOTSActive;

        private void Awake()
        {
            ServiceLocator.Register(this);

            playerInputActions = new PlayerInputActions();

            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow == null)
            {
                Debug.LogError("No CinemachineOrbitalFollow component found on this GameObject!");
            }

            CreateOTSCamera();
            CacheCinemachineBrain();
        }

        private void CacheCinemachineBrain()
        {
            if (Camera.main != null)
                cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<CameraController>();
            if (otsVirtualCamera != null)
                Destroy(otsVirtualCamera.gameObject);
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
            if (isOTSActive)
            {
                UpdateOTSCamera();
                return;
            }

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

        public void SetFollowTarget(Transform target)
        {
            if (virtualCamera != null)
            {
                virtualCamera.Follow = target;
            }
        }

        public void ClearFollowTarget()
        {
            if (virtualCamera != null)
            {
                virtualCamera.Follow = null;
            }
        }

        #region Over-the-Shoulder Targeting

        private void CreateOTSCamera()
        {
            var go = new GameObject("OTS_VirtualCamera");
            go.transform.SetParent(transform.parent);
            otsVirtualCamera = go.AddComponent<CinemachineCamera>();
            otsVirtualCamera.Lens = new LensSettings
            {
                FieldOfView = 50f,
                NearClipPlane = 0.1f,
                FarClipPlane = 1000f,
            };
            otsVirtualCamera.Priority = 100;
            go.SetActive(false);
        }

        public void EnterOTSMode(Transform attacker, Transform target)
        {
            if (otsVirtualCamera == null)
                return;

            otsAttacker = attacker;
            otsTarget = target;
            otsPositionVelocity = Vector3.zero;
            otsModeEnterTime = Time.time;
            otsLastTargetSwitchTime = -1f;
            isOTSActive = true;

            CalculateOTSTransform(out Vector3 pos, out Quaternion rot);
            otsVirtualCamera.transform.SetPositionAndRotation(pos, rot);

            ApplyFastBlendForOTSActivation();
            otsVirtualCamera.gameObject.SetActive(true);
            StopCoroutineSafe_RestoreBrain();
            restoreBlendCoroutine = StartCoroutine(RestoreBrainBlendAfterOTSActivation());
        }

        public void ExitOTSMode()
        {
            StopCoroutineSafe_RestoreBrain();
            RestoreSavedBrainBlendIfNeeded();

            isOTSActive = false;
            otsAttacker = null;
            otsTarget = null;

            if (otsVirtualCamera != null)
                otsVirtualCamera.gameObject.SetActive(false);
        }

        public void SetOTSTarget(Transform target)
        {
            otsTarget = target;
            otsPositionVelocity = Vector3.zero;
            otsLastTargetSwitchTime = Time.time;
        }

        private void ApplyFastBlendForOTSActivation()
        {
            if (cinemachineBrain == null)
                CacheCinemachineBrain();
            if (cinemachineBrain == null)
                return;

            savedDefaultBlend = cinemachineBrain.DefaultBlend;
            hasSavedDefaultBlend = true;
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(
                otsEntryBlendStyle,
                otsEntryBlendDuration
            );
        }

        private IEnumerator RestoreBrainBlendAfterOTSActivation()
        {
            // Small buffer so the brain finishes this blend before DefaultBlend is restored.
            yield return new WaitForSeconds(otsEntryBlendDuration + 0.03f);
            RestoreSavedBrainBlendIfNeeded();
            restoreBlendCoroutine = null;
        }

        private void RestoreSavedBrainBlendIfNeeded()
        {
            if (cinemachineBrain == null)
                CacheCinemachineBrain();
            if (cinemachineBrain != null && hasSavedDefaultBlend)
            {
                cinemachineBrain.DefaultBlend = savedDefaultBlend;
                hasSavedDefaultBlend = false;
            }
        }

        private void StopCoroutineSafe_RestoreBrain()
        {
            if (restoreBlendCoroutine != null)
            {
                StopCoroutine(restoreBlendCoroutine);
                restoreBlendCoroutine = null;
            }
        }

        private void CalculateOTSTransform(out Vector3 position, out Quaternion rotation)
        {
            Vector3 attackerPos = otsAttacker.position;
            Vector3 targetPos = otsTarget.position;

            Vector3 toTarget = targetPos - attackerPos;
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.001f)
                toTarget = otsAttacker.forward;
            Vector3 dir = toTarget.normalized;

            Vector3 behind = attackerPos - dir * otsDistance;
            behind.y = attackerPos.y + otsHeight;

            Vector3 shoulder = Vector3.Cross(Vector3.up, dir) * otsShoulderOffset;
            position = behind + shoulder;

            Vector3 lookPoint = targetPos + Vector3.up * otsTargetHeight;
            rotation = Quaternion.LookRotation(lookPoint - position);
        }

        private void UpdateOTSCamera()
        {
            if (otsAttacker == null || otsTarget == null)
            {
                ExitOTSMode();
                return;
            }

            CalculateOTSTransform(out Vector3 desiredPos, out Quaternion desiredRot);

            float timeSinceEnter = Time.time - otsModeEnterTime;
            float timeSinceTargetSwitch =
                otsLastTargetSwitchTime >= 0f
                    ? Time.time - otsLastTargetSwitchTime
                    : float.MaxValue;

            float smoothTime;
            float rotSpeed;

            if (
                otsLastTargetSwitchTime >= 0f
                && timeSinceTargetSwitch < otsBetweenTargetPanDuration
            )
            {
                smoothTime = otsBetweenTargetSmoothTime;
                rotSpeed = otsRotationSpeed * 0.55f;
            }
            else if (timeSinceEnter < otsEntrySnapDuration)
            {
                smoothTime = otsEntryPositionSmoothTime;
                rotSpeed = otsRotationSpeed * 5f;
            }
            else
            {
                smoothTime = otsPositionSmoothTime;
                rotSpeed = otsRotationSpeed;
            }

            Transform cam = otsVirtualCamera.transform;
            cam.position = Vector3.SmoothDamp(
                cam.position,
                desiredPos,
                ref otsPositionVelocity,
                smoothTime
            );
            cam.rotation = Quaternion.Slerp(
                cam.rotation,
                desiredRot,
                1f - Mathf.Exp(-rotSpeed * Time.deltaTime)
            );
        }

        #endregion

        #region Standard Camera

        private void HandleFreeCamMovement()
        {
            Vector2 inputMoveDir = playerInputActions.Player.Move.ReadValue<Vector2>();

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            Vector3 moveVector = (forward * inputMoveDir.y + right * inputMoveDir.x);
            transform.position += moveVector * moveSpeed * Time.deltaTime;
        }

        private void HandleFreeCamRotation()
        {
            Vector2 inputRotateDir = playerInputActions.Player.Rotate.ReadValue<Vector2>();
            float yaw = inputRotateDir.x * rotationSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, yaw, Space.World);
        }

        private void HandleOrbitRotation()
        {
            if (orbitalFollow == null)
                return;

            Vector2 inputRotateDir = playerInputActions.Player.Rotate.ReadValue<Vector2>();

            float yaw = inputRotateDir.x * rotationSpeed * Time.deltaTime;
            float pitch = inputRotateDir.y * rotationSpeed * Time.deltaTime;

            orbitalFollow.HorizontalAxis.Value += yaw;
            orbitalFollow.VerticalAxis.Value -= pitch;
        }

        #endregion
    }
}
