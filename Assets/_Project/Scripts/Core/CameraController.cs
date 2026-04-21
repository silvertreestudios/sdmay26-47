using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace PathfinderTactics.Core
{
    [DefaultExecutionOrder(1000)]
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

        [Header("Standard Orbit Settings")]
        [Tooltip(
            "Vertical offset from the Follow target root. Raise this so the camera orbits the chest/head, not the feet."
        )]
        [SerializeField]
        private Vector3 orbitTargetOffset = new Vector3(0, 1.5f, 0);

        [Tooltip("Min and Max pitch angles for the orbit camera.")]
        [SerializeField]
        private float minPitch = -20f;

        [SerializeField]
        private float maxPitch = 70f;

        [Header("Camera Collision")]
        [Tooltip("Layers the camera collides with (walls, floors, etc.). Defaults to Everything.")]
        [SerializeField]
        private LayerMask obstacleLayers = ~0;

        [Tooltip("Radius of camera collision sphere. Keep small (0.2-0.3).")]
        [SerializeField]
        [Range(0.05f, 0.5f)]
        private float cameraCollisionRadius = 0.2f;

        [Tooltip("How fast the camera returns to full distance after obstacle clears.")]
        [SerializeField]
        private float collisionRecoverySpeed = 5f;

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

        [Tooltip("Duration of the blend back to the standard camera when exiting targeting.")]
        [SerializeField]
        [Min(0.05f)]
        private float otsExitBlendDuration = 0.15f;

        [Header("OTS Tight-Space Handling")]
        [Tooltip("Distance at which the camera starts rising to see over the unit.")]
        [SerializeField]
        private float otsCollisionPushUpThreshold = 2.0f;

        [Tooltip("Max vertical lift applied when the camera is at its closest distance.")]
        [SerializeField]
        private float otsMaxCollisionPushUpHeight = 2.0f;

        [SerializeField]
        private string roofLayerName = "Roof";

        private PlayerInputActions playerInputActions;
        private CinemachineOrbitalFollow orbitalFollow;

        private float currentCollisionDistance;
        private float collisionVelocity;
        private float defaultOrbitRadius;
        private Vector3 lastCameraDirection;

        private CinemachineCamera otsVirtualCamera;
        private Transform otsAttacker;
        private Transform otsTarget;
        private Vector3 otsPositionVelocity;
        private bool isOTSActive;
        private float otsModeEnterTime;
        private float otsLastTargetSwitchTime = -1f;
        private float otsCurrentCollisionDistance;
        private float otsCollisionVelocity;

        private CinemachineBrain cinemachineBrain;
        private CinemachineBlendDefinition savedDefaultBlend;
        private bool hasSavedDefaultBlend;
        private Coroutine restoreBlendCoroutine;

        private CinemachineCamera eagleEyeVirtualCamera;
        private Transform eagleEyeFollowTarget;
        private bool isEagleEyeActive;
        private bool isEagleEyeFollowDetached;

        public bool IsOTSActive => isOTSActive;
        public bool IsEagleEyeActive => isEagleEyeActive;

        public bool IsBlending()
        {
            if (cinemachineBrain == null)
                CacheCinemachineBrain();
            return cinemachineBrain != null && cinemachineBrain.IsBlending;
        }

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
            CreateEagleEyeCamera();
            CacheCinemachineBrain();
        }

        private void Start()
        {
            SetupOrbitalFollow();

            if (orbitalFollow != null)
            {
                defaultOrbitRadius = orbitalFollow.Radius;
                currentCollisionDistance = defaultOrbitRadius;
            }
        }

        private void SetupOrbitalFollow()
        {
            if (orbitalFollow == null)
                return;

            orbitalFollow.TargetOffset = orbitTargetOffset;
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
            if (eagleEyeVirtualCamera != null)
                Destroy(eagleEyeVirtualCamera.gameObject);
        }

        private void OnEnable()
        {
            playerInputActions.Player.Enable();
        }

        private void OnDisable()
        {
            playerInputActions.Player.Disable();
        }

        public void SetFollowTarget(Transform target)
        {
            if (virtualCamera != null)
            {
                virtualCamera.Follow = target;
            }

            if (isEagleEyeActive)
            {
                eagleEyeFollowTarget = target;
                isEagleEyeFollowDetached = false;
            }
        }

        public void ClearFollowTarget()
        {
            if (virtualCamera != null)
            {
                virtualCamera.Follow = null;
            }

            if (isEagleEyeActive)
            {
                eagleEyeFollowTarget = null;
            }
        }

        #region Eagle Eye Camera

        private void CreateEagleEyeCamera()
        {
            var go = new GameObject("EagleEye_VirtualCamera");
            go.transform.SetParent(transform.parent);
            eagleEyeVirtualCamera = go.AddComponent<CinemachineCamera>();
            eagleEyeVirtualCamera.Lens = new LensSettings
            {
                FieldOfView = 60f,
                NearClipPlane = 0.1f,
                FarClipPlane = 1000f,
            };
            eagleEyeVirtualCamera.transform.rotation = Quaternion.Euler(85f, 0f, 0f);
            eagleEyeVirtualCamera.Priority = 200; // Above everything else
            go.SetActive(false);
        }

        public void EnterEagleEyeMode(Transform target = null)
        {
            if (eagleEyeVirtualCamera == null)
                return;

            isEagleEyeActive = true;
            eagleEyeFollowTarget = target;
            isEagleEyeFollowDetached = false;

            SetLayerVisibility(roofLayerName, false);

            Vector3 startPos = transform.position;
            if (target != null)
                startPos = target.position;
            else if (virtualCamera != null && virtualCamera.Follow != null)
                startPos = virtualCamera.Follow.position;

            eagleEyeVirtualCamera.transform.position = new Vector3(startPos.x, 25f, startPos.z);

            RestoreSavedBrainBlendIfNeeded(); // Clear any previous active stack
            ApplyFastBlend(0.25f);
            eagleEyeVirtualCamera.gameObject.SetActive(true);
            StopCoroutineSafe_RestoreBrain();
            restoreBlendCoroutine = StartCoroutine(RestoreBrainBlendAfterTime(0.25f));
        }

        public void ExitEagleEyeMode()
        {
            isEagleEyeActive = false;
            eagleEyeFollowTarget = null;

            SetLayerVisibility(roofLayerName, true);

            ApplyFastBlend(0.25f);
            if (eagleEyeVirtualCamera != null)
                eagleEyeVirtualCamera.gameObject.SetActive(false);
            StopCoroutineSafe_RestoreBrain();
            restoreBlendCoroutine = StartCoroutine(RestoreBrainBlendAfterTime(0.25f));
        }

        private void UpdateEagleEyeCamera()
        {
            Vector2 rotateInput = playerInputActions.Player.Rotate.ReadValue<Vector2>();
            Vector2 moveInput = playerInputActions.Player.Move.ReadValue<Vector2>();

            // Detach if player tries to pan away with Right Stick
            if (rotateInput.sqrMagnitude > 0.01f)
            {
                isEagleEyeFollowDetached = true;
            }
            // Re-attach if player moves the unit with Left Stick
            else if (moveInput.sqrMagnitude > 0.01f)
            {
                isEagleEyeFollowDetached = false;
            }

            if (!isEagleEyeFollowDetached && eagleEyeFollowTarget != null)
            {
                // Smoothly follow the target's position
                Vector3 targetPos = eagleEyeFollowTarget.position;
                targetPos.y = eagleEyeVirtualCamera.transform.position.y; // Maintain height

                eagleEyeVirtualCamera.transform.position = Vector3.SmoothDamp(
                    eagleEyeVirtualCamera.transform.position,
                    targetPos,
                    ref otsPositionVelocity,
                    otsPositionSmoothTime
                );
            }
            else
            {
                // Manual panning using Right Stick (Rotate input)
                Vector3 moveVector = new Vector3(rotateInput.x, 0, rotateInput.y);
                eagleEyeVirtualCamera.transform.position +=
                    moveVector * (moveSpeed * 1.5f) * Time.deltaTime;
            }
        }

        #endregion

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

            Debug.Log(
                $"[CameraController] EnterOTSMode: attacker={attacker.name}, target={target.name}"
            );

            CalculateOTSTransform(out Vector3 pos, out Quaternion rot);

            Vector3 attackerPivot = otsAttacker.position + orbitTargetOffset;
            otsCurrentCollisionDistance = Vector3.Distance(attackerPivot, pos);

            otsVirtualCamera.transform.SetPositionAndRotation(pos, rot);

            RestoreSavedBrainBlendIfNeeded(); // Handle consecutive transitions
            ApplyFastBlend(otsEntryBlendDuration, otsEntryBlendStyle);
            otsVirtualCamera.gameObject.SetActive(true);
            StopCoroutineSafe_RestoreBrain();
            restoreBlendCoroutine = StartCoroutine(
                RestoreBrainBlendAfterTime(otsEntryBlendDuration)
            );
        }

        public void ExitOTSMode()
        {
            Debug.Log("[CameraController] ExitOTSMode");
            StopCoroutineSafe_RestoreBrain();

            isOTSActive = false;
            otsAttacker = null;
            otsTarget = null;

            ApplyFastBlend(otsExitBlendDuration);
            if (otsVirtualCamera != null)
                otsVirtualCamera.gameObject.SetActive(false);

            restoreBlendCoroutine = StartCoroutine(
                RestoreBrainBlendAfterTime(otsExitBlendDuration)
            );
        }

        public void SetOTSTarget(Transform target)
        {
            otsTarget = target;
            otsPositionVelocity = Vector3.zero;
            otsLastTargetSwitchTime = Time.time;
        }

        private void ApplyFastBlend(
            float duration,
            CinemachineBlendDefinition.Styles style = CinemachineBlendDefinition.Styles.EaseInOut
        )
        {
            if (cinemachineBrain == null)
                CacheCinemachineBrain();
            if (cinemachineBrain == null)
                return;

            if (!hasSavedDefaultBlend)
            {
                savedDefaultBlend = cinemachineBrain.DefaultBlend;
                hasSavedDefaultBlend = true;
            }
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(style, duration);
        }

        private IEnumerator RestoreBrainBlendAfterTime(float duration)
        {
            yield return new WaitForSeconds(duration + 0.03f);
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

            // OTS Collision Handling
            Vector3 attackerPivot = otsAttacker.position + orbitTargetOffset;
            Vector3 toDesired = desiredPos - attackerPivot;
            float idealDistance = toDesired.magnitude;
            Vector3 dirToDesired = toDesired.normalized;

            int mask = obstacleLayers.value;
            mask &= ~(1 << otsAttacker.gameObject.layer);
            mask &= ~(1 << 2); // Ignore Raycast

            float targetDistance = idealDistance;
            if (
                Physics.SphereCast(
                    attackerPivot,
                    cameraCollisionRadius,
                    dirToDesired,
                    out RaycastHit hit,
                    idealDistance,
                    mask,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                targetDistance = Mathf.Max(hit.distance - 0.05f, 0.5f);
            }

            // Snapping behavior: inward is instant, outward is smooth
            if (targetDistance < otsCurrentCollisionDistance)
            {
                otsCurrentCollisionDistance = targetDistance;
                otsCollisionVelocity = 0f;
            }
            else
            {
                otsCurrentCollisionDistance = Mathf.SmoothDamp(
                    otsCurrentCollisionDistance,
                    targetDistance,
                    ref otsCollisionVelocity,
                    1f / collisionRecoverySpeed
                );
            }

            Vector3 finalDesiredPos = attackerPivot + dirToDesired * otsCurrentCollisionDistance;

            // If the camera is forced close to the unit, move it upward to maintain a view of the target.
            float pushUpFactor = Mathf.Clamp01(
                1f - (otsCurrentCollisionDistance / otsCollisionPushUpThreshold)
            );
            float pushUpAmount = pushUpFactor * otsMaxCollisionPushUpHeight;
            finalDesiredPos.y += pushUpAmount;

            // Recalculate rotation based on the final adjusted position to keep the target centered.
            Vector3 lookPoint = otsTarget.position + Vector3.up * otsTargetHeight;
            Quaternion finalDesiredRot = Quaternion.LookRotation(lookPoint - finalDesiredPos);

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
                finalDesiredPos,
                ref otsPositionVelocity,
                smoothTime
            );
            cam.rotation = Quaternion.Slerp(
                cam.rotation,
                finalDesiredRot,
                1f - Mathf.Exp(-rotSpeed * Time.deltaTime)
            );
        }

        #endregion

        #region Standard Camera

        private void Update()
        {
            if (isEagleEyeActive)
            {
                UpdateEagleEyeCamera();
                return;
            }

            if (isOTSActive)
            {
                UpdateOTSCamera();
                return;
            }

            if (virtualCamera != null && virtualCamera.Follow != null)
            {
                HandleOrbitRotation();
                HandleCameraCollision();
            }
            else
            {
                HandleFreeCamMovement();
                HandleFreeCamRotation();
            }
        }

        private void LateUpdate()
        {
            if (isOTSActive || isEagleEyeActive)
                return;
            if (virtualCamera == null || virtualCamera.Follow == null)
                return;
            if (Camera.main == null)
                return;

            Vector3 orbitCenter = virtualCamera.Follow.position + orbitTargetOffset;
            Vector3 toCamera = Camera.main.transform.position - orbitCenter;
            if (toCamera.sqrMagnitude > 0.001f)
                lastCameraDirection = toCamera.normalized;
        }

        private void HandleCameraCollision()
        {
            if (orbitalFollow == null || virtualCamera.Follow == null)
                return;

            // On the very first frame, we might not have a cached direction yet.
            // Use a sensible fallback.
            if (lastCameraDirection.sqrMagnitude < 0.001f)
            {
                orbitalFollow.Radius = defaultOrbitRadius;
                return;
            }

            Vector3 orbitCenter = virtualCamera.Follow.position + orbitTargetOffset;

            // Build collision mask: exclude the player's layer
            int mask = obstacleLayers.value;
            mask &= ~(1 << virtualCamera.Follow.gameObject.layer);
            mask &= ~(1 << 2); // Ignore IgnoreRaycast layer

            float targetRadius = defaultOrbitRadius;

            if (
                Physics.SphereCast(
                    orbitCenter,
                    cameraCollisionRadius,
                    lastCameraDirection,
                    out RaycastHit hit,
                    defaultOrbitRadius,
                    mask,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                targetRadius = Mathf.Max(hit.distance - 0.05f, 0.5f);
            }

            // Asymmetric smoothing: snap inward instantly, recover outward smoothly
            if (targetRadius < currentCollisionDistance)
            {
                currentCollisionDistance = targetRadius;
                collisionVelocity = 0f;
            }
            else
            {
                currentCollisionDistance = Mathf.SmoothDamp(
                    currentCollisionDistance,
                    targetRadius,
                    ref collisionVelocity,
                    1f / collisionRecoverySpeed
                );
            }

            orbitalFollow.Radius = currentCollisionDistance;
        }

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

            // Apply pitch and clamp it to prevent flipping over or hitting Gimbal lock
            float newPitch = orbitalFollow.VerticalAxis.Value - pitch;
            orbitalFollow.VerticalAxis.Value = Mathf.Clamp(newPitch, minPitch, maxPitch);
        }

        private void SetLayerVisibility(string layerName, bool visible)
        {
            if (Camera.main == null)
                return;

            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1)
            {
                return;
            }

            if (visible)
            {
                Camera.main.cullingMask |= (1 << layer);
            }
            else
            {
                Camera.main.cullingMask &= ~(1 << layer);
            }
        }

        #endregion
    }
}
