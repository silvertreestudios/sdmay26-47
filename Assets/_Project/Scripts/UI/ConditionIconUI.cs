using PathfinderTactics.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PathfinderTactics.UI
{
    public class ConditionIconUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private TextMeshProUGUI valueText;

        [Header("Settings")]
        [SerializeField]
        private bool hideValueIfOne = true;

        public void SetCondition(ConditionType type, int value, Sprite icon)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (valueText != null)
            {
                if (value > 1 || (value == 1 && !hideValueIfOne))
                {
                    valueText.text = value.ToString();
                    valueText.enabled = true;
                }
                else
                {
                    valueText.enabled = false;
                }
            }
        }
    }
}
