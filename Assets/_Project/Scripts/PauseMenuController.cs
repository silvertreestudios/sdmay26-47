using System;
using System.Collections.Generic;
using TacticsGame.Core;
using TacticsGame.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField]
    private string mainMenuSceneName = "MainMenu";

    [Header("Controls Settings")]
    [SerializeField]
    private Sprite controlsSprite;

    private UIDocument uiDocument;
    private VisualElement pauseRoot;
    private VisualElement buttonList;
    private VisualElement controlsView;

    private bool isPaused = false;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
            return;

        var root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[PauseMenu] Root Visual Element is null!");
            return;
        }

        pauseRoot = root.Q<VisualElement>("PauseRoot");
        buttonList = root.Q<VisualElement>("ButtonList");

        var btnResume = root.Q<Button>("BtnResume");
        var btnSave = root.Q<Button>("BtnSave");
        var btnRestart = root.Q<Button>("BtnRestart");
        var btnControls = root.Q<Button>("BtnControls");
        var btnMainMenu = root.Q<Button>("BtnMainMenu");
        var btnQuit = root.Q<Button>("BtnQuit");

        btnResume?.RegisterCallback<ClickEvent>(ev => OnResume());
        btnSave?.RegisterCallback<ClickEvent>(ev => OnSaveGame());
        btnRestart?.RegisterCallback<ClickEvent>(ev => OnRestartLevel());
        btnControls?.RegisterCallback<ClickEvent>(ev => ShowControls());
        btnMainMenu?.RegisterCallback<ClickEvent>(ev => OnMainMenu());
        btnQuit?.RegisterCallback<ClickEvent>(ev => OnQuitDesktop());

        // Controls Setup
        controlsView = root.Q<VisualElement>("ControlsView");
        var controlsImage = controlsView?.Q<VisualElement>("ControlsImage");
        if (controlsImage != null && controlsSprite != null)
        {
            controlsImage.style.backgroundImage = new StyleBackground(controlsSprite);
        }
        var controlsCloseBtn = controlsView?.Q<Button>("ControlsCloseBtn");
        controlsCloseBtn?.RegisterCallback<ClickEvent>(ev => HideControls());

        // Initially hide the pause menu
        if (pauseRoot != null)
        {
            pauseRoot.AddToClassList("screen-hidden");
        }
        else
        {
            Debug.LogWarning("[PauseMenu] PauseRoot not found in UXML!");
        }

        root.RegisterCallback<NavigationMoveEvent>(
            evt =>
            {
                if (Time.unscaledTime - lastNavTime < debounceThreshold)
                {
                    evt.PreventDefault();
                    return;
                }
                lastNavTime = Time.unscaledTime;
            },
            TrickleDown.TrickleDown
        );
    }

    private TacticsGame.InputSystem.InputService inputService;
    private float nextNavTime = 0f;
    private float navDelay = 0.18f;
    private float lastNavTime = 0f;
    private float debounceThreshold = 0.12f;

    void Update()
    {
        if (inputService == null)
        {
            inputService =
                TacticsGame.Core.ServiceLocator.Get<TacticsGame.InputSystem.InputService>();
            if (inputService != null)
            {
                inputService.OnCancelPerformed += HandleCancel;
                inputService.OnUICancelPerformed += HandleCancel;
                inputService.OnUIConfirmPerformed += HandleConfirm;
                inputService.OnConfirmPerformed += HandleConfirm;
                inputService.OnPausePerformed += (s, e) => TogglePause();
            }
        }

        HandleDpadNavigation();
    }

    private void HandleCancel(object sender, EventArgs e)
    {
        if (controlsView != null && !controlsView.ClassListContains("screen-hidden"))
        {
            HideControls();
            return;
        }

        // Close menu if it's open
        if (isPaused)
            TogglePause();
    }

    private void HandleConfirm(object sender, EventArgs e)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
            return;

        var focused = uiDocument.rootVisualElement.focusController.focusedElement as VisualElement;
        if (focused == null)
            return;

        using (var submitEvent = NavigationSubmitEvent.GetPooled())
        {
            submitEvent.target = focused;
            focused.SendEvent(submitEvent);
        }
        using (var clickEvent = ClickEvent.GetPooled())
        {
            clickEvent.target = focused;
            focused.SendEvent(clickEvent);
        }
    }

    private void HandleDpadNavigation()
    {
        if (inputService == null || !isPaused)
            return;

        Vector2 dpad = inputService.GetMovementVectorNormalized();
        if (dpad.sqrMagnitude > 0.1f)
        {
            if (Time.unscaledTime > nextNavTime)
            {
                NavigationMoveEvent.Direction dir = NavigationMoveEvent.Direction.None;
                if (dpad.y > 0.5f)
                    dir = NavigationMoveEvent.Direction.Up;
                else if (dpad.y < -0.5f)
                    dir = NavigationMoveEvent.Direction.Down;
                else if (dpad.x < -0.5f)
                    dir = NavigationMoveEvent.Direction.Left;
                else if (dpad.x > 0.5f)
                    dir = NavigationMoveEvent.Direction.Right;

                if (dir != NavigationMoveEvent.Direction.None)
                {
                    if (
                        uiDocument == null
                        || uiDocument.rootVisualElement == null
                        || uiDocument.rootVisualElement.focusController == null
                    )
                        return;

                    var focused =
                        uiDocument.rootVisualElement.focusController.focusedElement
                        as VisualElement;
                    if (focused != null)
                    {
                        using (var e = NavigationMoveEvent.GetPooled(dir))
                        {
                            e.target = focused;
                            focused.SendEvent(e);
                        }
                    }
                    else
                    {
                        var firstBtn = uiDocument.rootVisualElement.Q<Button>();
                        firstBtn?.Focus();
                    }
                    nextNavTime = Time.unscaledTime + navDelay;
                }
            }
        }
        else
        {
            nextNavTime = 0f;
        }
    }

    public void TogglePause()
    {
        if (pauseRoot == null)
        {
            Debug.LogError("[PauseMenu] Cannot toggle pause: PauseRoot is null.");
            return;
        }

        isPaused = !isPaused;

        if (isPaused)
        {
            uiDocument.sortingOrder = 1000; // Ensure it's on top of everything
            pauseRoot.RemoveFromClassList("screen-hidden");
            controlsView?.AddToClassList("screen-hidden"); // Ensure controls hidden when opening pause
            buttonList?.RemoveFromClassList("screen-hidden");

            Time.timeScale = 0f;
            inputService?.SwitchToActionMap("UI");

            // Focus first button
            var firstBtn = buttonList?.Q<Button>() ?? pauseRoot.Q<Button>();
            firstBtn?.Focus();

            Debug.Log("[PauseMenu] Game Paused. UI visible (SortingOrder: 1000).");
        }
        else
        {
            uiDocument.sortingOrder = 0;
            pauseRoot.AddToClassList("screen-hidden");
            controlsView?.AddToClassList("screen-hidden");
            Time.timeScale = 1f;
            inputService?.SwitchToActionMap("Player");

            Debug.Log("[PauseMenu] Game Resumed.");
        }
    }

    public void OnResume()
    {
        if (isPaused)
            TogglePause();
    }

    private void ShowControls()
    {
        buttonList?.AddToClassList("screen-hidden");
        controlsView?.RemoveFromClassList("screen-hidden");

        var closeBtn = controlsView?.Q<Button>("ControlsCloseBtn");
        closeBtn?.Focus();
    }

    private void HideControls()
    {
        controlsView?.AddToClassList("screen-hidden");
        buttonList?.RemoveFromClassList("screen-hidden");

        var btnControls = buttonList?.Q<Button>("BtnControls");
        btnControls?.Focus();
    }

    public void OnSaveGame()
    {
        var activeData = TacticsGame.Core.GlobalGameState.Instance.ActiveSaveData;
        if (activeData != null)
        {
            activeData.lastSceneName = SceneManager.GetActiveScene().name;
            TacticsGame.Core.SaveSystem.Save(activeData);
            Debug.Log($"[PauseMenu] Game Saved. Scene: {activeData.lastSceneName}");

            TogglePause();
        }
        else
        {
            Debug.LogWarning("[PauseMenu] Cannot save: No active character data found.");
        }
    }

    public void OnRestartLevel()
    {
        Time.timeScale = 1f;
        TacticsGame.UI.LoadingManager.Instance.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnMainMenu()
    {
        Time.timeScale = 1f;
        TacticsGame.UI.LoadingManager.Instance.LoadScene(mainMenuSceneName);
    }

    public void OnQuitDesktop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
