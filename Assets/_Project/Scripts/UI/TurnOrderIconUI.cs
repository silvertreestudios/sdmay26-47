using TacticsGame.Characters;
using TacticsGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsGame.UI
{
    /// <summary>
    /// Represents an individual unit's plate in the Turn Order UI.
    /// Handles portrait, health, and reaction status updates.
    /// </summary>
    public class TurnOrderIconUI : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField]
        private Sprite playerBorder;

        [SerializeField]
        private Sprite enemyBorder;

        [SerializeField]
        private Sprite reactionAvailableIcon;

        [SerializeField]
        private Sprite reactionSpentIcon;

        [Header("Internal References")]
        [SerializeField]
        private Image borderImage;

        [SerializeField]
        private Image portraitImage;

        [SerializeField]
        private RectTransform healthFillRect;

        [SerializeField]
        private float healthBarWidth = 100f;

        [SerializeField]
        private Image reactionIcon;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private RectTransform healthBarRoot;

        private Unit targetUnit;

        private void OnEnable()
        {
            GameEvents.OnUnitHealthChanged += HandleHealthChanged;
            GameEvents.OnUnitReactionChanged += HandleReactionChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnUnitHealthChanged -= HandleHealthChanged;
            GameEvents.OnUnitReactionChanged -= HandleReactionChanged;
        }

        public void SetUnit(Unit unit)
        {
            targetUnit = unit;

            // Set Portrait
            if (unit.GetStats() != null && unit.GetStats().GetPortraitIcon() != null)
            {
                portraitImage.sprite = unit.GetStats().GetPortraitIcon();
            }

            // Set Border based on Faction
            if (unit.GetFaction() == Faction.Player)
                borderImage.sprite = playerBorder;
            else
                borderImage.sprite = enemyBorder;

            // Initial Health
            var health = unit.GetComponent<UnitHealth>();
            if (health != null)
            {
                UpdateHealth(health.GetCurrentHealth(), health.GetMaxHealth());
            }

            // Initial Reaction
            var economy = unit.GetComponent<UnitActionEconomy>();
            if (economy != null)
            {
                UpdateReaction(economy.HasReactionAvailable);
            }
        }

        private void HandleHealthChanged(Unit unit, int current, int max)
        {
            if (unit == targetUnit)
            {
                UpdateHealth(current, max);
            }
        }

        public void SetHealthBarLayout(Vector2 localPos, float scale)
        {
            if (healthBarRoot != null)
            {
                healthBarRoot.anchoredPosition = localPos;
                healthBarRoot.localScale = Vector3.one * scale;
            }
        }

        public void SetReactionLayout(Vector2 localPos, float scale)
        {
            if (reactionIcon != null)
            {
                reactionIcon.rectTransform.anchoredPosition = localPos;
                reactionIcon.rectTransform.localScale = Vector3.one * scale;
            }
        }

        private void HandleReactionChanged(Unit unit, bool isAvailable)
        {
            if (unit == targetUnit)
            {
                UpdateReaction(isAvailable);
            }
        }

        private void UpdateHealth(int current, int max)
        {
            if (healthFillRect != null)
            {
                // Slide the rectangular fill horizontally behind a mask
                // At 100%, offset is 0. At 0%, offset is -healthBarWidth.
                float percent = (float)current / max;
                float xOffset = (percent - 1f) * healthBarWidth;
                healthFillRect.anchoredPosition = new Vector2(xOffset, 0f);
            }
        }

        private void UpdateReaction(bool isAvailable)
        {
            if (reactionIcon != null)
            {
                reactionIcon.sprite = isAvailable ? reactionAvailableIcon : reactionSpentIcon;
                // Dim the icon if spent
                reactionIcon.color = isAvailable ? Color.white : new Color(1, 1, 1, 0.4f);
            }
        }

        public void SetVisualState(float scale, float alpha)
        {
            transform.localScale = Vector3.one * scale;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
        }
    }
}
