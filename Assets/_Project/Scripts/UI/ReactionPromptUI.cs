using System;
using TacticsGame.Characters;
using TacticsGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsGame.UI
{
    public class ReactionPromptUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TextMeshProUGUI promptText;

        [SerializeField]
        private ActionButtonUI yesButton;

        [SerializeField]
        private ActionButtonUI noButton;

        [SerializeField]
        private CanvasGroup canvasGroup;

        private Action<bool> onChoiceMade;

        private void Awake()
        {
            ServiceLocator.Register(this);
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            Hide();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ReactionPromptUI>();
        }

        public void Show(
            string unitName,
            string reactionName,
            string triggerSource,
            Action<bool> callback
        )
        {
            onChoiceMade = callback;

            if (promptText != null)
            {
                promptText.text =
                    $"{unitName} wants to use {reactionName} against {triggerSource}. Use it?";
            }

            if (yesButton != null)
            {
                yesButton.Setup("Yes", null, null, () => HandleChoice(true));
            }

            if (noButton != null)
            {
                noButton.Setup("No", null, null, () => HandleChoice(false));
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                gameObject.SetActive(true);
            }

            // Auto-focus the yes button for controller support
            if (yesButton != null)
            {
                var btn = yesButton.GetComponent<Button>();
                if (btn != null)
                    btn.Select();
            }
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void HandleChoice(bool choice)
        {
            Hide();
            // Cache and clear before invoking to prevent recursion from wiping the next prompt's callback
            var callback = onChoiceMade;
            onChoiceMade = null;
            callback?.Invoke(choice);
        }
    }
}
