using TacticsGame.Story;
using UnityEngine;

namespace TacticsGame.Core
{
    /// <summary>
    /// Connects the StoryManager to the BattleResolutionUI.
    /// It ensures the buttons only appear AFTER the dialogue has finished.
    /// </summary>
    public class StoryResolutionLinker : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The story manager to listen to. If null, will try to find one in the scene.")]
        [SerializeField]
        private StoryManager storyManager;

        [Tooltip("The UI GameObject containing the BattleResolutionUI script.")]
        [SerializeField]
        private GameObject resolutionUI;

        private void Start()
        {
            // Auto-find story manager if not assigned
            if (storyManager == null)
                storyManager = FindFirstObjectByType<StoryManager>();

            if (storyManager != null)
            {
                storyManager.OnSequenceComplete += HandleStoryComplete;
                Debug.Log("[StoryResolutionLinker] Subscribed to StoryManager completion.");
            }
            else
            {
                Debug.LogWarning("[StoryResolutionLinker] No StoryManager found in scene!");
            }

            // Ensure the buttons are hidden while the story is playing
            if (resolutionUI != null)
            {
                resolutionUI.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (storyManager != null)
            {
                storyManager.OnSequenceComplete -= HandleStoryComplete;
            }
        }

        private void HandleStoryComplete()
        {
            if (resolutionUI != null)
            {
                Debug.Log(
                    $"[StoryResolutionLinker] Story finished. Enabling GameObject: <color=yellow>{resolutionUI.name}</color>"
                );
                resolutionUI.SetActive(true);

                // Verify if the script is actually there
                var resScript = resolutionUI.GetComponentInChildren<BattleResolutionUI>(true);
                if (resScript != null)
                {
                    // Force enable script in case it was unchecked in the Inspector
                    resScript.enabled = true;
                }
                else
                {
                    Debug.LogError(
                        $"[StoryResolutionLinker] CRITICAL: No BattleResolutionUI script found on '{resolutionUI.name}' or its children!"
                    );
                }
            }
            else
            {
                Debug.Log(
                    "[StoryResolutionLinker] Story finished. No resolution UI assigned to enable."
                );
            }
        }
    }
}
