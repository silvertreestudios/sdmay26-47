using System;
using UnityEngine;

namespace TacticsGame.Data
{
    /// <summary>
    /// Represents a complete, persistent save state for a player character.
    /// Wraps the character's payload and tracks gameplay progress.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        [Tooltip("The unique identifier for this save file (usually based on character name).")]
        public string saveId;

        [Tooltip("The last scene or level the character was in.")]
        public string lastSceneName;

        [Tooltip("The binary representation of the DateTime this was last saved.")]
        public long lastSavedTimeBinary;

        [Tooltip("The core character definition (stats, appearance, identity).")]
        public CharacterDataPayload characterData;

        public GameSaveData()
        {
            characterData = new CharacterDataPayload();
        }

        public GameSaveData(CharacterDataPayload payload, string initialScene)
        {
            characterData = payload;
            saveId = GenerateSaveId(payload.Name);
            lastSceneName = initialScene;
            UpdateSaveTime();
        }

        public void UpdateSaveTime()
        {
            lastSavedTimeBinary = DateTime.UtcNow.ToBinary();
        }

        public DateTime GetLastSavedTime()
        {
            return DateTime.FromBinary(lastSavedTimeBinary);
        }

        /// <summary>
        /// Generates a file-safe ID based on the character's name.
        /// </summary>
        public static string GenerateSaveId(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
                return "Unknown_Adventurer";

            return string.Join(
                "_",
                characterName.Split(
                    System.IO.Path.GetInvalidFileNameChars(),
                    StringSplitOptions.RemoveEmptyEntries
                )
            );
        }
    }
}
