using TacticsGame.Core;
using TacticsGame.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TacticsGame.Story
{
    /// <summary>
    /// Controller for the Battle Resolution UI (Victory/Defeat).
    /// Attach this to a GameObject with a UIDocument using BattleResolutionOverlay.uxml.
    /// </summary>
    public class BattleResolutionUI : MonoBehaviour
    {
        [Header("State Configuration")]
        [Tooltip("Is this instance configured for a Victory or Defeat screen?")]
        [SerializeField]
        private bool isVictoryScreen = true;

        [Header("Text Configuration")]
        [SerializeField]
        private string victoryText = "VICTORY";

        [SerializeField]
        private string defeatText = "DEFEAT";

        private VisualElement root;
        private Label lblTitle;
        private Button btnRetry;
        private Button btnContinue;

        private void OnEnable()
        {
            Debug.Log("<color=cyan>[BattleResolutionUI] OnEnable called!</color>");
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc == null)
            {
                Debug.LogWarning(
                    "[BattleResolutionUI] No UIDocument found on this GameObject! Looking in children..."
                );
                uiDoc = GetComponentInChildren<UIDocument>();
            }

            if (uiDoc == null)
            {
                Debug.LogError(
                    "[BattleResolutionUI] Still no UIDocument found. Buttons cannot be hooked up."
                );
                return;
            }

            root = uiDoc.rootVisualElement;
            if (root == null)
            {
                Debug.LogError(
                    "[BattleResolutionUI] rootVisualElement is null! The UI might not be ready."
                );
                return;
            }

            // Query elements from the UXML
            lblTitle = root.Q<Label>("ResolutionTitle");
            btnRetry = root.Q<Button>("BtnRetry");
            btnContinue = root.Q<Button>("BtnContinue");

            // Setup Title
            if (lblTitle != null)
            {
                lblTitle.text = isVictoryScreen ? victoryText : defeatText;

                // color coding
                lblTitle.style.color = isVictoryScreen
                    ? new StyleColor(new Color(0.34f, 0.88f, 0.66f))
                    : // Green-ish
                    new StyleColor(new Color(0.92f, 0.35f, 0.35f)); // Red-ish
            }

            // Setup Buttons
            if (btnRetry != null)
            {
                Debug.Log(
                    $"[BattleResolutionUI] Found BtnRetry. Victory screen: {isVictoryScreen}"
                );
                btnRetry.style.display = isVictoryScreen ? DisplayStyle.None : DisplayStyle.Flex;

                // Clear old callbacks to prevent duplicates
                btnRetry.UnregisterCallback<ClickEvent>(OnRetryClicked);
                btnRetry.RegisterCallback<ClickEvent>(OnRetryClicked);

                // Controller submit buttons
                btnRetry.focusable = true;
                btnRetry.UnregisterCallback<NavigationSubmitEvent>(OnRetrySubmit);
                btnRetry.RegisterCallback<NavigationSubmitEvent>(OnRetrySubmit);
            }
            else
                Debug.LogWarning("[BattleResolutionUI] Could not find BtnRetry in UXML.");

            if (btnContinue != null)
            {
                Debug.Log(
                    $"[BattleResolutionUI] Found BtnContinue. Victory screen: {isVictoryScreen}"
                );
                btnContinue.style.display = isVictoryScreen ? DisplayStyle.Flex : DisplayStyle.None;

                // Clear old callbacks to prevent duplicates
                btnContinue.UnregisterCallback<ClickEvent>(OnContinueClicked);
                btnContinue.RegisterCallback<ClickEvent>(OnContinueClicked);

                // Controller submit buttons
                btnContinue.focusable = true;
                btnContinue.UnregisterCallback<NavigationSubmitEvent>(OnContinueSubmit);
                btnContinue.RegisterCallback<NavigationSubmitEvent>(OnContinueSubmit);
            }
            else
                Debug.LogWarning("[BattleResolutionUI] Could not find BtnContinue in UXML.");

            // Controller/Keyboard Focus Support
            root.schedule.Execute(() =>
                {
                    Button target = isVictoryScreen ? btnContinue : btnRetry;
                    if (target != null)
                    {
                        target.Focus();
                        Debug.Log(
                            $"[BattleResolutionUI] Attempted to focus <color=cyan>{target.name}</color>. Is Focused: {target.focusController?.focusedElement == target}"
                        );
                    }
                })
                .StartingIn(250); // Slightly longer delay to allow StoryManager to finish its switch
        }

        private void OnDisable()
        {
            if (btnRetry != null)
            {
                btnRetry.UnregisterCallback<ClickEvent>(OnRetryClicked);
                btnRetry.UnregisterCallback<NavigationSubmitEvent>(OnRetrySubmit);
            }
            if (btnContinue != null)
            {
                btnContinue.UnregisterCallback<ClickEvent>(OnContinueClicked);
                btnContinue.UnregisterCallback<NavigationSubmitEvent>(OnContinueSubmit);
            }
        }

        private void OnRetryClicked(ClickEvent ev)
        {
            Debug.Log("[BattleResolutionUI] OnRetryClicked trigger!");
            RetryBattle();
        }

        private void OnContinueClicked(ClickEvent ev)
        {
            Debug.Log("[BattleResolutionUI] OnContinueClicked trigger!");
            ContinueToNext();
        }

        private void OnRetrySubmit(NavigationSubmitEvent ev)
        {
            Debug.Log("[BattleResolutionUI] OnRetrySubmit trigger!");
            RetryBattle();
        }

        private void OnContinueSubmit(NavigationSubmitEvent ev)
        {
            Debug.Log("[BattleResolutionUI] OnContinueSubmit trigger!");
            ContinueToNext();
        }

        /// <summary>
        /// Reloads the battle scene where the defeat occurred.
        /// </summary>
        public void RetryBattle()
        {
            if (
                GlobalGameState.Instance != null
                && !string.IsNullOrEmpty(GlobalGameState.Instance.LastBattleScene)
            )
            {
                Debug.Log(
                    $"[BattleResolutionUI] Retrying battle: {GlobalGameState.Instance.LastBattleScene}"
                );
                LoadingManager.Instance.LoadScene(GlobalGameState.Instance.LastBattleScene);
            }
            else
            {
                Debug.LogWarning(
                    "[BattleResolutionUI] No LastBattleScene found! Defaulting to Level 1."
                );
                LoadingManager.Instance.LoadScene("Level 1");
            }
        }

        /// <summary>
        /// Continues to the next story or gameplay scene.
        /// </summary>
        public void ContinueToNext()
        {
            if (
                GlobalGameState.Instance != null
                && !string.IsNullOrEmpty(GlobalGameState.Instance.NextStoryScene)
            )
            {
                Debug.Log(
                    $"[BattleResolutionUI] Continuing to: {GlobalGameState.Instance.NextStoryScene}"
                );
                LoadingManager.Instance.LoadScene(GlobalGameState.Instance.NextStoryScene);
            }
            else
            {
                Debug.LogWarning("[BattleResolutionUI] No NextStoryScene found!");
            }
        }
    }
}
