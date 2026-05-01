using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace TacticsGame.Utilities
{
    public class PortraitGenerator : EditorWindow
    {
        private GameObject targetUnit;
        private float camX = 0f;
        private float camY = 1.6f;
        private float camZ = 1.2f;
        private float lookAtX = 0f;
        private float lookAtY = 1.6f;
        private float fieldOfView = 30f;
        private int resolution = 512;
        private string folderPath = "Assets/_Project/Art/UI/Portraits/";
        private bool isolateUnit = true;

        // Animation Posing
        private AnimationClip poseClip;
        private float poseTime = 0f;

        // Preview System
        private RenderTexture previewTexture;
        private Camera previewCamera;
        private GameObject previewCamObj;

        [MenuItem("Tactics Core/Portrait Generator")]
        public static void ShowWindow()
        {
            GetWindow<PortraitGenerator>("Portrait Gen");
        }

        private void OnEnable()
        {
            CleanupPreview();
        }

        private void OnDisable()
        {
            CleanupPreview();
        }

        private void CleanupPreview()
        {
            if (previewTexture != null)
            {
                RenderTexture.active = null;
                DestroyImmediate(previewTexture);
            }
            if (previewCamObj != null)
            {
                DestroyImmediate(previewCamObj);
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Portrait Generator (Perspective)", EditorStyles.boldLabel);

            // Left Section: Settings
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            targetUnit = (GameObject)
                EditorGUILayout.ObjectField("Target Unit", targetUnit, typeof(GameObject), true);

            EditorGUILayout.Space();
            GUILayout.Label("Camera Position", EditorStyles.boldLabel);
            camX = EditorGUILayout.Slider("Cam Horizontal (L/R)", camX, -2f, 2f);
            camY = EditorGUILayout.Slider("Cam Vertical (U/D)", camY, 0f, 3f);
            camZ = EditorGUILayout.Slider("Cam Distance", camZ, 0.1f, 5f);

            EditorGUILayout.Space();
            GUILayout.Label("Look At (Target Offset)", EditorStyles.boldLabel);
            lookAtX = EditorGUILayout.Slider("Target L/R", lookAtX, -1f, 1f);
            lookAtY = EditorGUILayout.Slider("Target U/D (Height)", lookAtY, 0f, 3f);
            fieldOfView = EditorGUILayout.Slider("Field of View", fieldOfView, 5f, 90f);

            EditorGUILayout.Space();
            GUILayout.Label("Export Settings", EditorStyles.boldLabel);
            resolution = EditorGUILayout.IntField("Resolution", resolution);
            folderPath = EditorGUILayout.TextField("Save Folder", folderPath);
            isolateUnit = EditorGUILayout.Toggle("Only Render Unit", isolateUnit);

            EditorGUILayout.Space();
            GUILayout.Label("Animation / Posing", EditorStyles.boldLabel);
            poseClip = (AnimationClip)
                EditorGUILayout.ObjectField(
                    "Pose Animation",
                    poseClip,
                    typeof(AnimationClip),
                    false
                );

            if (poseClip != null)
            {
                EditorGUI.BeginChangeCheck();
                poseTime = EditorGUILayout.Slider("Pose Time", poseTime, 0f, poseClip.length);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyPose();
                }
            }

            if (GUILayout.Button("Apply Pose to Scene"))
            {
                ApplyPose();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Capture Portrait", GUILayout.Height(40)))
            {
                Capture();
            }
            EditorGUILayout.EndVertical();

            // Right Section: Live Preview
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Live Preview", EditorStyles.boldLabel);
            UpdatePreview();
            if (previewTexture != null)
            {
                float previewSize = 250f;
                Rect rect = GUILayoutUtility.GetRect(previewSize, previewSize);
                EditorGUI.DrawTextureTransparent(rect, previewTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUILayout.Box(
                    "Select a Unit to see preview",
                    GUILayout.Width(250),
                    GUILayout.Height(250)
                );
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Use 'Target L/R' and 'Height' to pan the camera away from the center.",
                MessageType.Info
            );
        }

        private void UpdatePreview()
        {
            if (targetUnit == null)
                return;

            // Ensure preview texture exists
            if (previewTexture == null || previewTexture.width != 512)
            {
                previewTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
                previewTexture.Create();
            }

            // Ensure preview camera exists
            if (previewCamObj == null)
            {
                previewCamObj = new GameObject("HiddenPortraitPreviewCamera");
                previewCamObj.hideFlags = HideFlags.HideAndDontSave;
                previewCamera = previewCamObj.AddComponent<Camera>();
                previewCamera.enabled = false;
            }

            // Update camera settings to match current UI
            Vector3 targetPos =
                targetUnit.transform.position
                + targetUnit.transform.right * lookAtX
                + Vector3.up * lookAtY;
            previewCamera.transform.position =
                targetUnit.transform.position
                + targetUnit.transform.forward * camZ
                + targetUnit.transform.right * camX
                + Vector3.up * camY;
            previewCamera.transform.LookAt(targetPos);

            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            previewCamera.orthographic = false;
            previewCamera.fieldOfView = fieldOfView;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 10f;
            previewCamera.targetTexture = previewTexture;

            previewCamera.cullingMask = -1;
            previewCamera.Render();
        }

        private void ApplyPose()
        {
            if (targetUnit == null || poseClip == null)
                return;
            poseClip.SampleAnimation(targetUnit, poseTime);
            SceneView.RepaintAll();
        }

        private void Capture()
        {
            if (targetUnit == null)
            {
                Debug.LogError("Please assign a Target Unit.");
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            const int TEMP_LAYER = 31;
            Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();

            if (isolateUnit)
            {
                foreach (Transform child in targetUnit.GetComponentsInChildren<Transform>(true))
                {
                    originalLayers[child.gameObject] = child.gameObject.layer;
                    child.gameObject.layer = TEMP_LAYER;
                }
            }

            // High-res capture camera
            GameObject camObj = new GameObject("TempPortraitCaptureCamera");
            Camera cam = camObj.AddComponent<Camera>();
            Vector3 targetPos =
                targetUnit.transform.position
                + targetUnit.transform.right * lookAtX
                + Vector3.up * lookAtY;
            cam.transform.position =
                targetUnit.transform.position
                + targetUnit.transform.forward * camZ
                + targetUnit.transform.right * camX
                + Vector3.up * camY;
            cam.transform.LookAt(targetPos);

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;

            if (isolateUnit)
            {
                cam.cullingMask = (1 << TEMP_LAYER);
            }

            RenderTexture rt = new RenderTexture(
                resolution,
                resolution,
                24,
                RenderTextureFormat.ARGB32
            );
            rt.Create();
            cam.targetTexture = rt;

            Texture2D portrait = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            cam.Render();

            RenderTexture.active = rt;
            portrait.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            portrait.Apply();

            byte[] bytes = portrait.EncodeToPNG();
            string fileName = folderPath + targetUnit.name + "_Portrait.png";
            File.WriteAllBytes(fileName, bytes);

            if (isolateUnit)
            {
                foreach (var pair in originalLayers)
                {
                    if (pair.Key != null)
                        pair.Key.layer = pair.Value;
                }
            }

            RenderTexture.active = null;
            cam.targetTexture = null;
            DestroyImmediate(rt);
            DestroyImmediate(camObj);

            Debug.Log($"Portrait saved: {fileName}");
            AssetDatabase.Refresh();
        }
    }
}
#endif
