using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TacticsGame.UI
{
    public class LoadingManager : MonoBehaviour
    {
        private static LoadingManager instance;
        public static LoadingManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("LoadingManager");
                    instance = go.AddComponent<LoadingManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private UIDocument uiDocument;
        private VisualElement loadingRoot;
        private VisualElement loadingBarFill;
        private Label statusText;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
            }

            // Set a very high sort order to ensure it's always on top
            uiDocument.sortingOrder = 10000;
        }

        /// <summary>
        /// Sets up the UI for the loading screen.
        /// </summary>
        public void SetupUI(VisualTreeAsset loadingTemplate, PanelSettings panelSettings)
        {
            if (uiDocument == null)
                return;

            uiDocument.panelSettings = panelSettings;
            uiDocument.visualTreeAsset = loadingTemplate;

            var root = uiDocument.rootVisualElement;
            loadingRoot = root.Q<VisualElement>("LoadingRoot");
            loadingBarFill = root.Q<VisualElement>("LoadingBarFill");
            statusText = root.Q<Label>("StatusText");

            if (loadingRoot == null)
            {
                Debug.LogError(
                    "[LoadingManager] Could not find 'LoadingRoot' in the provided template!"
                );
            }
            else
            {
                loadingRoot.AddToClassList("screen-hidden");
                // Ensure opacity is 0 initially for the fade-in
                loadingRoot.style.opacity = 0f;
            }
        }

        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[LoadingManager] Scene name is empty!");
                return;
            }
            StopAllCoroutines();
            StartCoroutine(LoadAsync(sceneName));
        }

        private IEnumerator LoadAsync(string sceneName)
        {
            Debug.Log($"[LoadingManager] Starting async load for: {sceneName}");

            // Show Loading Screen
            if (loadingRoot != null)
            {
                loadingRoot.RemoveFromClassList("screen-hidden");
                // Force visibility and fade in
                loadingRoot.style.display = DisplayStyle.Flex;
                loadingRoot.style.opacity = 1f;
            }
            else
            {
                Debug.LogWarning(
                    "[LoadingManager] loadingRoot is null, cannot show loading screen visuals."
                );
            }

            // Wait a moment for the UI to definitely be visible and for the fade to start
            yield return new WaitForSecondsRealtime(0.5f);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                Debug.LogError(
                    $"[LoadingManager] Failed to start LoadSceneAsync for {sceneName}. Is it in Build Settings?"
                );
                yield break;
            }

            operation.allowSceneActivation = false;

            while (!operation.isDone)
            {
                // progress goes from 0 to 0.9 before it's ready to activate
                float progress = Mathf.Clamp01(operation.progress / 0.9f);

                if (loadingBarFill != null)
                {
                    loadingBarFill.style.width = new Length(progress * 100, LengthUnit.Percent);
                }

                if (statusText != null)
                {
                    statusText.text = $"{(progress * 100):0}%";
                }

                // Check if loading is complete (0.9 means it's finished loading but waiting for allowSceneActivation)
                if (operation.progress >= 0.9f)
                {
                    if (statusText != null)
                        statusText.text = "READY!";
                    yield return new WaitForSecondsRealtime(0.5f);
                    operation.allowSceneActivation = true;
                }

                yield return null;
            }

            Debug.Log("[LoadingManager] Scene activation complete.");

            // Hide Loading Screen
            if (loadingRoot != null)
            {
                loadingRoot.style.opacity = 0f;
                yield return new WaitForSecondsRealtime(0.5f);
                loadingRoot.AddToClassList("screen-hidden");
                loadingRoot.style.display = DisplayStyle.None;
            }
        }
    }
}
