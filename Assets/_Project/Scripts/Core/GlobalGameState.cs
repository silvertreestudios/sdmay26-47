using TacticsGame.Data;
using UnityEngine;

namespace TacticsGame.Core
{
    /// <summary>
    /// Persistent singleton that tracks the current state of the game across scenes.
    /// Primarily used to hold the ActiveSaveData of the currently playing character.
    /// </summary>
    public class GlobalGameState : MonoBehaviour
    {
        private static GlobalGameState instance;
        public static GlobalGameState Instance
        {
            get
            {
                if (instance == null)
                {
                    // Create a new GameObject to hold the singleton if it doesn't exist
                    GameObject go = new GameObject("GlobalGameState");
                    instance = go.AddComponent<GlobalGameState>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Tooltip("The currently loaded character save profile.")]
        public GameSaveData ActiveSaveData { get; private set; }

        public string LastBattleScene { get; private set; }
        public string NextStoryScene { get; private set; }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto select a character for scene testing
            // If we start a scene directly in the editor without going through the main menu,
            // try to grab the most recently used character save.
            if (ActiveSaveData == null)
            {
                var allSaves = SaveSystem.GetAllSaves();
                if (allSaves.Count > 0)
                {
                    ActiveSaveData = allSaves[0];
                    Debug.Log(
                        $"<color=yellow>[GlobalGameState]</color> Auto-loaded fallback character for testing: {ActiveSaveData.characterData?.Name}"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[GlobalGameState] No character saves found. You may need to create a character in the Main Menu first."
                    );
                }
            }
        }

        /// <summary>
        /// Sets the active character and makes this the primary profile for the current session.
        /// </summary>
        public void SetCurrentCharacter(GameSaveData data)
        {
            ActiveSaveData = data;
            if (data != null)
            {
                Debug.Log(
                    $"[GlobalGameState] Active character set to: {data.characterData?.Name} (ID: {data.saveId})"
                );
            }
            else
            {
                Debug.Log("[GlobalGameState] Active character cleared.");
            }
        }

        /// <summary>
        /// Attempts to reload the current character's data from disk.
        /// </summary>
        public void ReloadCurrentCharacter()
        {
            if (ActiveSaveData != null && !string.IsNullOrEmpty(ActiveSaveData.saveId))
            {
                ActiveSaveData = SaveSystem.Load(ActiveSaveData.saveId);
            }
        }

        public void SetLastBattleScene(string sceneName) => LastBattleScene = sceneName;

        public void SetNextStoryScene(string sceneName) => NextStoryScene = sceneName;
    }
}
