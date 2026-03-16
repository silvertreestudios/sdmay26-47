using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PathfinderTactics.UI
{
    public class TurnSystemUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Transform turnOrderContainer;

        [SerializeField]
        private GameObject turnOrderPortraitPrefab; // Needs a Text child

        [SerializeField]
        private TextMeshProUGUI currentTurnText;

        private void Start()
        {
            ServiceLocator.Get<TurnManager>().OnTurnChanged += TurnManager_OnTurnChanged;
            ServiceLocator.Get<TurnManager>().OnCombatStarted += TurnManager_OnCombatStarted;
            UpdateVisuals();
        }

        private void TurnManager_OnCombatStarted(
            object sender,
            TurnManager.OnTurnOrderedEventArgs e
        )
        {
            // Rebuild the portrait list
            foreach (Transform child in turnOrderContainer)
                Destroy(child.gameObject);

            foreach (Unit unit in e.turnOrder)
            {
                GameObject portrait = Instantiate(turnOrderPortraitPrefab, turnOrderContainer);
                // Assume prefab has a TextMeshPro for now
                var text = portrait.GetComponentInChildren<TextMeshProUGUI>();
                if (text)
                    text.text = unit.name.Substring(0, 3); // First 3 chars
            }
            UpdateVisuals();
        }

        private void TurnManager_OnTurnChanged(object sender, EventArgs e)
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            Unit current = ServiceLocator.Get<TurnManager>().CurrentUnit;
            if (current != null)
            {
                currentTurnText.text = $"Turn: {current.name}";

                // TODO: Highlight the active portrait in the list
            }
        }
    }
}
