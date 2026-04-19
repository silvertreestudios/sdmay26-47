using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.UI
{
    /// <summary>
    /// Controller for the Turn Order UI.
    /// Manages spawning and positioning of TurnOrderIconUI elements.
    /// </summary>
    public class TurnOrderUI : MonoBehaviour
    {
        [Header("Focus Unit Layout")]
        [Tooltip("Exact anchored position of the large active unit.")]
        [SerializeField]
        private Vector2 focusPosition = new Vector2(150f, -550f);

        [SerializeField]
        private float focusScale = 1f;

        [Header("Queue Layout")]
        [Tooltip("The center point where the bottom-most queue unit begins.")]
        [SerializeField]
        private Vector2 queueStartPosition = new Vector2(50f, -220f);

        [SerializeField]
        private float verticalSpacing = 170f;

        [Tooltip("How far left and right the queue units zig-zag from the start position.")]
        [SerializeField]
        private float zigzagWidth = 80f;

        [Tooltip("If true, the first unit in the queue goes left. If false, it goes right.")]
        [SerializeField]
        private bool startZigLeft = true;

        [SerializeField]
        private float queueScale = 0.5f;

        [Header("Settings")]
        [SerializeField]
        private int maxVisibleUnits = 9;

        [Header("References")]
        [SerializeField]
        private TurnOrderIconUI iconPrefab;

        [SerializeField]
        private Transform container;

        [Header("Health Bar Layout")]
        [Tooltip(
            "Local position of the health bar for the large focus unit (offset to the right)."
        )]
        [SerializeField]
        private Vector2 focusHealthBarPosition = new Vector2(750f, 110f);

        [SerializeField]
        private float focusHealthBarScale = 1.0f;

        [Tooltip(
            "Local position of the health bar for the smaller queue units (centered at bottom)."
        )]
        [SerializeField]
        private Vector2 queueHealthBarPosition = new Vector2(0f, -90f);

        [SerializeField]
        private float queueHealthBarScale = 0.4f;

        [Header("Reaction Icon Layout")]
        [Tooltip("Local position of the reaction icon for the large focus unit.")]
        [SerializeField]
        private Vector2 focusReactionPosition = new Vector2(300f, -200f);

        [SerializeField]
        private float focusReactionScale = 1.0f;

        [Tooltip("Local position of the reaction icon for the smaller queue units.")]
        [SerializeField]
        private Vector2 queueReactionPosition = new Vector2(200f, 0f);

        [SerializeField]
        private float queueReactionScale = 1f;

        private List<TurnOrderIconUI> activeIcons = new List<TurnOrderIconUI>();

        private void Start()
        {
            GameEvents.OnTurnOrderChanged += HandleTurnOrderChanged;
            GameEvents.OnCombatStarted += HandleCombatStarted;
        }

        private void OnDestroy()
        {
            GameEvents.OnTurnOrderChanged -= HandleTurnOrderChanged;
            GameEvents.OnCombatStarted -= HandleCombatStarted;
        }

        private void HandleCombatStarted()
        {
            ClearUI();
        }

        private void HandleTurnOrderChanged(Unit current, List<Unit> turnOrder)
        {
            RefreshUI(current, turnOrder);
        }

        private void RefreshUI(Unit current, List<Unit> turnOrder)
        {
            ClearUI();

            if (turnOrder == null || turnOrder.Count == 0)
                return;

            int startIndex = turnOrder.IndexOf(current);
            if (startIndex == -1)
                startIndex = 0; // Fallback

            int count = Mathf.Min(turnOrder.Count, maxVisibleUnits);

            for (int i = 0; i < count; i++)
            {
                int listIndex = (startIndex + i) % turnOrder.Count;

                TurnOrderIconUI icon = Instantiate(iconPrefab, container);
                icon.SetUnit(turnOrder[listIndex]);
                activeIcons.Add(icon);
            }

            UpdateLayout();
        }

        private Vector2 CalculatePosition(int index)
        {
            // The Active Unit is separate from the queue math
            if (index == 0)
            {
                return focusPosition;
            }

            // Treat the queue as its own separate list starting at 0
            int queueIndex = index - 1;

            // Calculate vertical position moving upwards
            float yPos = queueStartPosition.y + (queueIndex * verticalSpacing);

            // Calculate horizontal zig-zag
            float direction = (queueIndex % 2 == 0) ? -1f : 1f;

            if (!startZigLeft)
                direction *= -1f;

            float xPos = queueStartPosition.x + (direction * zigzagWidth);

            return new Vector2(xPos, yPos);
        }

        private void OnValidate()
        {
            if (Application.isPlaying && activeIcons.Count > 0)
            {
                UpdateLayout();
            }
        }

        private void ClearUI()
        {
            if (activeIcons != null)
            {
                foreach (var icon in activeIcons)
                {
                    if (icon != null)
                        Destroy(icon.gameObject);
                }
                activeIcons.Clear();
            }
        }

        private void UpdateLayout()
        {
            for (int i = 0; i < activeIcons.Count; i++)
            {
                if (activeIcons[i] == null)
                    continue;

                // Root Position & Scale
                Vector2 pos = CalculatePosition(i);
                RectTransform rt = activeIcons[i].GetComponent<RectTransform>();
                rt.anchoredPosition = pos;

                float rootScale = (i == 0) ? focusScale : queueScale;
                activeIcons[i].SetVisualState(rootScale, 1.0f);

                // Health Bar Position & Scale
                Vector2 hpPos = (i == 0) ? focusHealthBarPosition : queueHealthBarPosition;
                float hpScale = (i == 0) ? focusHealthBarScale : queueHealthBarScale;
                activeIcons[i].SetHealthBarLayout(hpPos, hpScale);

                // Reaction Icon Position & Scale
                Vector2 rxnPos = (i == 0) ? focusReactionPosition : queueReactionPosition;
                float rxnScale = (i == 0) ? focusReactionScale : queueReactionScale;
                activeIcons[i].SetReactionLayout(rxnPos, rxnScale);

                // Push focus unit to the top of the render stack
                if (i == 0)
                    activeIcons[i].transform.SetAsLastSibling();
            }
        }
    }
}
