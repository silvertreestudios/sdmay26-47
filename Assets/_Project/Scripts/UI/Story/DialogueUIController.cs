using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace TacticsGame.Story
{
    public class DialogueUIController : MonoBehaviour
    {
        private UIDocument uiDocument;
        private VisualElement dialogueContainer;
        private VisualElement portraitContainer;
        private Image speakerPortrait;
        private Label speakerName;
        private Label dialogueText;
        private Label advanceIndicator;

        [SerializeField]
        private float charsPerSecond = 30f;

        private Coroutine typingCoroutine;
        private string fullText;
        private bool isTyping;

        public event System.Action OnDialogueCompleted;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (uiDocument == null)
                return;
            var root = uiDocument.rootVisualElement;

            dialogueContainer = root.Q<VisualElement>("DialogueContainer");
            portraitContainer = root.Q<VisualElement>("PortraitContainer");
            speakerPortrait = root.Q<Image>("SpeakerPortrait");
            speakerName = root.Q<Label>("SpeakerName");
            dialogueText = root.Q<Label>("DialogueText");
            advanceIndicator = root.Q<Label>("AdvanceIndicator");

            HideDialogue();
        }

        public void ShowDialogue(string name, string text, Texture2D portrait = null)
        {
            dialogueContainer?.RemoveFromClassList("screen-hidden");
            advanceIndicator?.AddToClassList("screen-hidden");

            if (speakerName != null)
                speakerName.text = name;

            if (portrait != null)
            {
                portraitContainer?.RemoveFromClassList("screen-hidden");
                if (speakerPortrait != null)
                    speakerPortrait.image = portrait;
            }
            else
            {
                portraitContainer?.AddToClassList("screen-hidden");
            }

            fullText = text;
            if (dialogueText != null)
                dialogueText.text = "";

            if (typingCoroutine != null)
                StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypewriterCoroutine());
        }

        public void HideDialogue()
        {
            dialogueContainer?.AddToClassList("screen-hidden");
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                isTyping = false;
            }
        }

        public void Advance()
        {
            // If the box is hidden, we shouldn't handle advance inputs.
            if (dialogueContainer != null && dialogueContainer.ClassListContains("screen-hidden"))
                return;

            if (isTyping)
            {
                // Complete instantly
                if (typingCoroutine != null)
                    StopCoroutine(typingCoroutine);
                if (dialogueText != null)
                    dialogueText.text = fullText;
                isTyping = false;
                advanceIndicator?.RemoveFromClassList("screen-hidden");
            }
            else
            {
                // Already done typing, proceed
                HideDialogue();
                OnDialogueCompleted?.Invoke();
            }
        }

        private IEnumerator TypewriterCoroutine()
        {
            isTyping = true;
            float timePerChar = 1f / charsPerSecond;
            int length = fullText.Length;

            for (int i = 0; i <= length; i++)
            {
                if (dialogueText != null)
                    dialogueText.text = fullText.Substring(0, i);
                yield return new WaitForSeconds(timePerChar);
            }

            isTyping = false;
            advanceIndicator?.RemoveFromClassList("screen-hidden");
        }
    }
}
