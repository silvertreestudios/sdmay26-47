using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TacticsGame.UI
{
    public class ActionButtonUI : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [Header("UI References")]
        [SerializeField]
        private TMP_Text actionNameText;

        [SerializeField]
        private Image abilityIconImage;

        [SerializeField]
        private Image apCostImage;

        [SerializeField]
        private GameObject selectionHighlight;

        [SerializeField]
        private Button buttonComponent;

        public void Setup(
            string name,
            Sprite abilityIcon,
            Sprite apCostIcon,
            UnityAction onClickCallback
        )
        {
            // Set the Name
            if (actionNameText != null)
            {
                actionNameText.text = name;
            }

            // Set the Damage Type / Ability Icon
            if (abilityIconImage != null)
            {
                if (abilityIcon != null)
                {
                    abilityIconImage.sprite = abilityIcon;
                    abilityIconImage.enabled = true;
                }
                else
                {
                    abilityIconImage.enabled = false;
                }
            }

            // Set the AP Cost Sprite
            if (apCostImage != null)
            {
                if (apCostIcon != null)
                {
                    apCostImage.sprite = apCostIcon;
                    apCostImage.enabled = true;
                }
                else
                {
                    apCostImage.enabled = false;
                }
            }

            // Bind the Click Event
            if (buttonComponent != null)
            {
                buttonComponent.onClick.RemoveAllListeners();
                buttonComponent.onClick.AddListener(onClickCallback);
            }
        }

        public void SetSelected(bool isSelected)
        {
            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(isSelected);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetSelected(true);

            // Broadcast selection to parent menu to handle scrolling
            SendMessageUpwards(
                "OnButtonSelected",
                (RectTransform)transform,
                SendMessageOptions.DontRequireReceiver
            );
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetSelected(false);
        }

        public void SetInteractable(bool isInteractable)
        {
            if (buttonComponent != null)
            {
                buttonComponent.interactable = isInteractable;
            }

            float alpha = isInteractable ? 1.0f : 0.5f;

            if (actionNameText != null)
                actionNameText.alpha = alpha;
            if (abilityIconImage != null)
                abilityIconImage.color = new Color(1, 1, 1, alpha);
            if (apCostImage != null)
                apCostImage.color = new Color(1, 1, 1, alpha);
        }
    }
}
