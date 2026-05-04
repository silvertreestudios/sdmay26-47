using System;
using System.Collections.Generic;
using System.IO;
using TacticsGame.Data;
using UnityEngine;

namespace TacticsGame.Core
{
    /// <summary>
    /// Handles persistent storage of GameSaveData.
    /// Reads and writes JSON files to the Application.persistentDataPath.
    /// </summary>
    public static class SaveSystem
    {
        private static string SaveDirectory =>
            Path.Combine(Application.persistentDataPath, "Saves", "Characters");

        public static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        public static void Save(GameSaveData data)
        {
            EnsureDirectoryExists();

            // Ensure ID exists
            if (string.IsNullOrEmpty(data.saveId))
            {
                data.saveId = GameSaveData.GenerateSaveId(data.characterData.Name);
            }

            data.UpdateSaveTime();

            string filePath = Path.Combine(SaveDirectory, $"{data.saveId}.json");
            string json = JsonUtility.ToJson(data, true);

            try
            {
                File.WriteAllText(filePath, json);
                Debug.Log(
                    $"[SaveSystem] Successfully saved character: {data.saveId} to {filePath}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to save character {data.saveId}: {e.Message}");
            }
        }

        public static GameSaveData Load(string saveId)
        {
            string filePath = Path.Combine(SaveDirectory, $"{saveId}.json");

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[SaveSystem] Save file not found for ID: {saveId}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to load character {saveId}: {e.Message}");
                return null;
            }
        }

        public static void Delete(string saveId)
        {
            string filePath = Path.Combine(SaveDirectory, $"{saveId}.json");
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    Debug.Log($"[SaveSystem] Deleted save file for ID: {saveId}");
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[SaveSystem] Failed to delete save file for ID {saveId}: {e.Message}"
                    );
                }
            }
        }

        public static List<GameSaveData> GetAllSaves()
        {
            EnsureDirectoryExists();
            var saves = new List<GameSaveData>();

            string[] files = Directory.GetFiles(SaveDirectory, "*.json");
            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

                    if (data != null && data.characterData != null)
                    {
                        saves.Add(data);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveSystem] Failed to parse save file {file}: {e.Message}");
                }
            }

            // Sort by most recently saved
            saves.Sort((a, b) => b.GetLastSavedTime().CompareTo(a.GetLastSavedTime()));
            return saves;
        }
    }
}
