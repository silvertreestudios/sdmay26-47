using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using TacticsGame.InputSystem;
using TacticsGame.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UIElements;

namespace TacticsGame.Story
{
    public class StoryManager : MonoBehaviour
    {
        [Tooltip("The JSON script file containing the sequence to play.")]
        public TextAsset jsonScript;

        [Tooltip("Reference to the UI controller for the dialogue box.")]
        public DialogueUIController dialogueUI;

        [Tooltip("Optional: The Cinemachine camera used for cinematic framing.")]
        public CinemachineCamera storyCamera;

        [Header("Scene Transitions")]
        [Tooltip(
            "The Loading Screen template to use for transitions (ensures it works if starting from this scene)."
        )]
        public VisualTreeAsset loadingTemplate;

        [Tooltip("Panel Settings for the loading screen.")]
        public PanelSettings panelSettings;

        private StoryData storyData;
        private Dictionary<string, StoryActor> actorMap = new Dictionary<string, StoryActor>();

        private int currentSequenceIndex = 0;
        private bool isDialogueWaiting = false;

        private TacticsGame.InputSystem.InputService inputService;

        private void Start()
        {
            if (jsonScript == null)
            {
                Debug.LogError("[StoryManager] No JSON script assigned!");
                return;
            }

            // Parse the JSON directly into our heavily typed models
            storyData = JsonConvert.DeserializeObject<StoryData>(jsonScript.text);
            BindActors();

            if (storyCamera != null)
            {
                storyCamera.Priority = 100; // Force take over
            }

            if (
                TacticsGame.Core.ServiceLocator.TryGet<TacticsGame.InputSystem.InputService>(
                    out inputService
                )
            )
            {
                inputService.OnAdvanceDialoguePerformed += HandleAdvanceDialogue;
                inputService.SwitchToActionMap("UI"); // Story cutscenes are UI-driven
            }

            // Ensure Loading Manager is set up
            if (loadingTemplate != null)
            {
                var settings = panelSettings;
                if (settings == null && dialogueUI != null)
                    settings = dialogueUI.GetComponent<UIDocument>().panelSettings;

                LoadingManager.Instance.SetupUI(loadingTemplate, settings);
            }

            if (dialogueUI != null)
            {
                dialogueUI.OnDialogueCompleted += HandleDialogueCompleted;
            }

            StartCoroutine(RunSequence());
        }

        private void OnDestroy()
        {
            if (inputService != null)
            {
                inputService.OnAdvanceDialoguePerformed -= HandleAdvanceDialogue;
            }
            if (dialogueUI != null)
            {
                dialogueUI.OnDialogueCompleted -= HandleDialogueCompleted;
            }
        }

        /// <summary>
        /// Finds the objects in the scene based on the JSON IDs and attaches StoryActor wrappers to them.
        /// </summary>
        private void BindActors()
        {
            if (storyData.actors == null)
                return;

            foreach (var actorData in storyData.actors)
            {
                GameObject obj = GameObject.Find(actorData.sceneObject);
                if (obj != null)
                {
                    StoryActor actor = obj.GetComponent<StoryActor>();
                    if (actor == null)
                        actor = obj.AddComponent<StoryActor>();

                    actor.actorId = actorData.id;
                    actorMap[actorData.id] = actor;
                }
                else
                {
                    Debug.LogWarning(
                        $"[StoryManager] Could not find sceneObject: {actorData.sceneObject}"
                    );
                }
            }
        }

        private void HandleAdvanceDialogue(object sender, System.EventArgs e)
        {
            if (dialogueUI != null)
            {
                dialogueUI.Advance();
            }
        }

        private void HandleDialogueCompleted()
        {
            isDialogueWaiting = false;
        }

        /// <summary>
        /// Main sequence loop that evaluates waitUntilFinished for parallel vs sequential execution.
        /// </summary>
        private IEnumerator RunSequence()
        {
            if (storyData.sequence == null)
                yield break;

            while (currentSequenceIndex < storyData.sequence.Count)
            {
                StoryAction currentAction = storyData.sequence[currentSequenceIndex];

                if (currentAction.waitUntilFinished)
                {
                    yield return StartCoroutine(ExecuteAction(currentAction));
                }
                else
                {
                    StartCoroutine(ExecuteAction(currentAction));
                }

                currentSequenceIndex++;
            }

            Debug.Log("[StoryManager] Story Sequence Complete.");
            inputService?.SwitchToActionMap("Player"); // Return to standard map
        }

        private IEnumerator ExecuteAction(StoryAction action)
        {
            if (action is DialogueAction dialogue)
            {
                isDialogueWaiting = true;
                dialogueUI?.ShowDialogue(dialogue.actor, dialogue.text);

                while (isDialogueWaiting)
                {
                    yield return null;
                }
            }
            else if (action is MoveAction move)
            {
                if (actorMap.TryGetValue(move.actor, out StoryActor actor))
                {
                    yield return StartCoroutine(
                        actor.MoveToCoroutine(move.destination, move.speed)
                    );
                }
            }
            else if (action is AnimateAction animate)
            {
                if (actorMap.TryGetValue(animate.actor, out StoryActor actor))
                {
                    actor.PlayAnimation(animate.triggerName);
                    // Approximate wait for standard triggers
                    if (action.waitUntilFinished)
                        yield return new WaitForSeconds(1.0f);
                }
            }
            else if (action is CameraMoveAction camMove)
            {
                if (storyCamera == null)
                {
                    Debug.LogWarning(
                        "[StoryManager] CameraMove failed: storyCamera is not assigned."
                    );
                }
                else if (actorMap.TryGetValue(camMove.target, out StoryActor targetActor))
                {
                    Debug.Log(
                        $"[StoryManager] Camera moving to target: {camMove.target} (GameObject: {targetActor.name})"
                    );
                    storyCamera.Follow = targetActor.transform;
                    storyCamera.LookAt = targetActor.transform;

                    if (camMove.offset != Vector3.zero)
                    {
                        var follow = storyCamera.GetComponent<CinemachineFollow>();
                        if (follow != null)
                            follow.FollowOffset = camMove.offset;

                        var posComposer = storyCamera.GetComponent<CinemachinePositionComposer>();
                        if (posComposer != null)
                            posComposer.TargetOffset = camMove.offset;
                    }

                    if (action.waitUntilFinished)
                    {
                        Debug.Log(
                            $"[StoryManager] Camera waiting for {camMove.duration}s blend/move."
                        );
                        yield return new WaitForSeconds(camMove.duration);
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"[StoryManager] CameraMove failed: Actor '{camMove.target}' not found in scene mapping."
                    );
                }
            }
            else if (action is CameraShakeAction camShake)
            {
                if (storyCamera != null)
                {
                    var perlin =
                        storyCamera.GetComponentInChildren<CinemachineBasicMultiChannelPerlin>();
                    if (perlin != null)
                    {
                        Debug.Log(
                            $"[StoryManager] Starting Camera Shake: intensity {camShake.intensity} for {camShake.duration}s"
                        );
                        perlin.AmplitudeGain = camShake.intensity;
                        yield return new WaitForSeconds(camShake.duration);
                        perlin.AmplitudeGain = 0f;
                        Debug.Log("[StoryManager] Camera Shake finished.");
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[StoryManager] CameraShake failed: No CinemachineBasicMultiChannelPerlin (Noise) found on camera or children."
                        );
                    }
                }
            }
            else if (action is CameraSetAction camSet)
            {
                if (storyCamera != null)
                {
                    Debug.Log(
                        $"[StoryManager] Manually setting camera to Pos:{camSet.position}, Rot:{camSet.rotation}"
                    );
                    // Clear tracking so manual transform control works
                    storyCamera.Follow = null;
                    storyCamera.LookAt = null;

                    if (camSet.duration <= 0)
                    {
                        storyCamera.transform.position = camSet.position;
                        storyCamera.transform.eulerAngles = camSet.rotation;
                    }
                    else
                    {
                        yield return StartCoroutine(
                            LerpCamera(camSet.position, camSet.rotation, camSet.duration)
                        );
                    }
                }
            }
            else if (action is SceneLoadAction sceneLoad)
            {
                Debug.Log(
                    $"[StoryManager] Sequence complete. Loading scene: {sceneLoad.sceneName}"
                );

                // Hide dialogue UI before loading to avoid it overlapping the loading screen
                if (dialogueUI != null)
                {
                    dialogueUI.gameObject.SetActive(false);
                }

                TacticsGame.UI.LoadingManager.Instance.LoadScene(sceneLoad.sceneName);
            }
        }

        private IEnumerator LerpCamera(Vector3 targetPos, Vector3 targetRot, float duration)
        {
            Vector3 startPos = storyCamera.transform.position;
            Quaternion startRot = storyCamera.transform.rotation;
            Quaternion endRot = Quaternion.Euler(targetRot);
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = Mathf.SmoothStep(0, 1, t);

                storyCamera.transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
                storyCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, smoothT);
                yield return null;
            }

            storyCamera.transform.position = targetPos;
            storyCamera.transform.rotation = endRot;
        }
    }
}
