using System;
using System.IO;
using TacticsGame.Data;
using TacticsGame.Data.TacticsRuleset;
using UnityEngine;

namespace TacticsGame.UI.CharacterCreator
{
    public class CharacterCreatorUI : MonoBehaviour
    {
        [Header("State")]
        [SerializeField]
        private CreatorState currentState = CreatorState.Concept;

        [Header("Data")]
        [SerializeField]
        private TacticsRulesetDatabase database;
        private CharacterDataPayload payload;

        [Header("Preview")]
        [SerializeField]
        private GameObject previewModelParent;

        // Contains the VisualPartManager and UnitEquipment

        public event Action<CreatorState> OnStateChanged;
        public event Action<CharacterDataPayload> OnPayloadUpdated;

        private void Start()
        {
            payload = new CharacterDataPayload();
            ChangeState(CreatorState.Concept);
        }

        public void ChangeState(CreatorState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(currentState);
        }

        public CharacterDataPayload GetPayload() => payload;

        public TacticsRulesetDatabase GetDatabase() => database;

        public void UpdatePayload(Action<CharacterDataPayload> updateAction)
        {
            updateAction?.Invoke(payload);
            OnPayloadUpdated?.Invoke(payload);
        }

        // TODO: Wire up and fix and test Finalize Character.
        public void FinalizeCharacter()
        {
            if (string.IsNullOrEmpty(payload.Name))
                payload.Name = "Unknown Adventurer";

            // Save to Persistent Data Path
            string json = JsonUtility.ToJson(payload, true);
            string savesDir = Path.Combine(Application.persistentDataPath, "Saves", "Roster");

            if (!Directory.Exists(savesDir))
            {
                Directory.CreateDirectory(savesDir);
            }

            string filePath = Path.Combine(savesDir, $"{payload.Name.Replace(" ", "_")}.json");
            File.WriteAllText(filePath, json);

            Debug.Log($"Character saved to roster: {filePath}");
        }

        // Methods to be called by UI Buttons
        public void NextState()
        {
            if ((int)currentState < Enum.GetValues(typeof(CreatorState)).Length - 1)
            {
                ChangeState(currentState + 1);
            }
        }

        public void PreviousState()
        {
            if ((int)currentState > 0)
            {
                ChangeState(currentState - 1);
            }
        }
    }
}
