using System;
using System.Collections.Generic;
using TacticsGame.Core;
using TacticsGame.InputSystem;
using TacticsGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    [SerializeField]
    private string gameSceneName = "Level 1";

    [SerializeField]
    private VisualTreeAsset loadingTemplate;

    [Header("Content Texts")]
    [TextArea(15, 50)]
    [SerializeField]
    private string creditsText = "Insert credits here";

    [TextArea(15, 50)]
    [SerializeField]
    private string licenseText = "Insert license here";

    [Header("Credits Settings")]
    [SerializeField]
    private float creditsScrollSpeed = 60f;

    [SerializeField]
    private float holdToSkipTime = 1.5f;

    private UIDocument uiDocument;

    // Core Panels
    private VisualElement menuLeftPanel;
    private VisualElement menuRightPanel;

    // Sub Views
    private VisualElement licenseView;
    private VisualElement creditsView;

    // License UI
    private ScrollView licenseScroll;
    private Label licenseTextLabel;

    // Credits UI
    private ScrollView creditsScroll;
    private Label creditsTextLabel;
    private RadialProgress holdSkipProgress;

    // State
    private bool isCreditsRolling = false;
    private float currentHoldTime = 0f;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
            return;

        var root = uiDocument.rootVisualElement;

        // Get Main Panels
        menuLeftPanel = root.Q<VisualElement>("MenuLeftPanel");
        menuRightPanel = root.Q<VisualElement>("MenuRightPanel");

        // Get Sub Views
        licenseView = root.Q<VisualElement>("LicenseView");
        creditsView = root.Q<VisualElement>("CreditsView");

        // License UI Elements
        licenseScroll = licenseView?.Q<ScrollView>("LicenseScroll");
        licenseTextLabel = licenseView?.Q<Label>("LicenseText");
        var licenseCloseBtn = licenseView?.Q<Button>("LicenseCloseBtn");
        licenseCloseBtn?.RegisterCallback<ClickEvent>(ev => HideAllSubPanels());
        if (licenseTextLabel != null)
            licenseTextLabel.text = licenseText;

        // Credits UI Elements
        creditsScroll = creditsView?.Q<ScrollView>("CreditsScroll");
        creditsTextLabel = creditsView?.Q<Label>("CreditsText");
        holdSkipProgress = creditsView?.Q<RadialProgress>("HoldSkipProgress");
        if (creditsTextLabel != null)
            creditsTextLabel.text = creditsText;

        // Main Menu Buttons
        var btnNewGame = root.Q<Button>("BtnNewGame");
        var btnLoadGame = root.Q<Button>("BtnLoadGame");
        var btnCredits = root.Q<Button>("BtnCredits");
        var btnLicense = root.Q<Button>("BtnLicense");
        var btnQuit = root.Q<Button>("BtnQuit");

        // Register Callbacks
        btnNewGame?.RegisterCallback<ClickEvent>(ev => OnNewGame());
        btnLoadGame?.RegisterCallback<ClickEvent>(ev => OnLoadGame());
        btnCredits?.RegisterCallback<ClickEvent>(ev => ShowCredits());
        btnLicense?.RegisterCallback<ClickEvent>(ev => ShowLicense());
        btnQuit?.RegisterCallback<ClickEvent>(ev => OnQuitGame());

        // Initially hide sub-panels
        HideAllSubPanels();

        // Force unfocusable scrollbars via C# to guarantee they are never selectable
        HardenScrollViewFocus(licenseScroll);
        HardenScrollViewFocus(creditsScroll);

        // GLOBAL NAVIGATION DEBOUNCER
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

        // Initialize Loading Manager
        if (loadingTemplate != null)
        {
            LoadingManager.Instance.SetupUI(loadingTemplate, uiDocument.panelSettings);
        }
    }

    private void HardenScrollViewFocus(ScrollView scrollView)
    {
        if (scrollView == null)
            return;
        scrollView.focusable = false;
        scrollView.verticalScroller.focusable = false;
        scrollView.verticalScroller.pickingMode = PickingMode.Ignore;
        scrollView.horizontalScroller.focusable = false;
        scrollView.horizontalScroller.pickingMode = PickingMode.Ignore;
        scrollView
            .verticalScroller.Query<VisualElement>()
            .ForEach(v =>
            {
                v.focusable = false;
                v.pickingMode = PickingMode.Ignore;
            });
        scrollView
            .horizontalScroller.Query<VisualElement>()
            .ForEach(v =>
            {
                v.focusable = false;
                v.pickingMode = PickingMode.Ignore;
            });
    }

    private InputService inputService;
    private float nextNavTime = 0f;
    private float navDelay = 0.18f;
    private float lastNavTime = 0f;
    private float debounceThreshold = 0.12f;

    private void Update()
    {
        if (inputService == null)
        {
            inputService = ServiceLocator.Get<InputService>();
            if (inputService != null)
            {
                inputService.OnCancelPerformed += HandleCancel;
                inputService.OnUICancelPerformed += HandleCancel;
                inputService.OnUIConfirmPerformed += HandleConfirm;
                inputService.OnConfirmPerformed += HandleConfirm;
            }
        }

        if (isCreditsRolling)
        {
            HandleCreditsUpdate();
        }

        HandleDpadNavigation();
        HandleRightStickScrolling();
    }

    private void HandleCancel(object sender, EventArgs e)
    {
        if (licenseView != null && !licenseView.ClassListContains("screen-hidden"))
            HideAllSubPanels();
        else if (creditsView != null && !creditsView.ClassListContains("screen-hidden"))
            HideAllSubPanels();
    }

    private void HandleConfirm(object sender, EventArgs e)
    {
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
        if (inputService == null)
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

    private void HandleRightStickScrolling()
    {
        if (inputService == null)
            return;
        Vector2 rightStick = inputService.GetRotationVector();
        if (Mathf.Abs(rightStick.y) > 0.05f)
        {
            float scrollSpeed = 1200f;

            if (
                licenseView != null
                && !licenseView.ClassListContains("screen-hidden")
                && licenseScroll != null
            )
            {
                licenseScroll.scrollOffset = new Vector2(
                    0,
                    licenseScroll.scrollOffset.y
                        - rightStick.y * scrollSpeed * Time.unscaledDeltaTime
                );
            }
        }
    }

    private void HandleCreditsUpdate()
    {
        if (creditsScroll != null)
        {
            creditsScroll.scrollOffset = new Vector2(
                creditsScroll.scrollOffset.x,
                creditsScroll.scrollOffset.y + creditsScrollSpeed * Time.deltaTime
            );
        }

        bool isHoldingButton = inputService != null && inputService.IsAnyButtonHeld();

        if (isHoldingButton)
        {
            currentHoldTime += Time.deltaTime;
            if (currentHoldTime >= holdToSkipTime)
            {
                currentHoldTime = 0f;
                HideAllSubPanels();
            }
        }
        else
        {
            currentHoldTime = Mathf.Max(0f, currentHoldTime - Time.deltaTime * 2f);
        }

        if (holdSkipProgress != null)
        {
            holdSkipProgress.progress = currentHoldTime / holdToSkipTime;
        }
    }

    private void OnNewGame()
    {
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            LoadingManager.Instance.LoadScene(gameSceneName);
        }
    }

    private void OnLoadGame()
    {
        Debug.Log("Load Game Not Implemented Yet");
    }

    private void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HideAllSubPanels()
    {
        isCreditsRolling = false;
        currentHoldTime = 0f;

        licenseView?.AddToClassList("screen-hidden");
        creditsView?.AddToClassList("screen-hidden");

        if (menuRightPanel != null)
        {
            menuRightPanel.style.backgroundColor = new StyleColor(Color.clear);
        }

        menuLeftPanel?.RemoveFromClassList("screen-hidden");
        menuLeftPanel?.SetEnabled(true);

        var firstBtn = menuLeftPanel?.Q<Button>();
        firstBtn?.Focus();
    }

    private void ShowLicense()
    {
        HideAllSubPanels();
        menuLeftPanel?.SetEnabled(false);
        if (licenseScroll != null)
            licenseScroll.scrollOffset = Vector2.zero;
        menuRightPanel.style.backgroundColor = new StyleColor(
            new Color(0.05f, 0.07f, 0.08f, 0.85f)
        );
        licenseView?.RemoveFromClassList("screen-hidden");
    }

    private void ShowCredits()
    {
        HideAllSubPanels();
        menuLeftPanel?.AddToClassList("screen-hidden");
        creditsView?.RemoveFromClassList("screen-hidden");
        if (creditsScroll != null)
            creditsScroll.scrollOffset = Vector2.zero;
        currentHoldTime = 0f;
        if (holdSkipProgress != null)
            holdSkipProgress.progress = 0f;
        isCreditsRolling = true;
    }
}
