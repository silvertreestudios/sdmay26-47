using System;
using System.Collections.Generic;
using TacticsGame.Core;
using TacticsGame.Data;
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
    private string characterCreatorSceneName = "CharacterCreator";

    [SerializeField]
    private VisualTreeAsset loadingTemplate;

    [Header("Character Selection")]
    [SerializeField]
    private VisualTreeAsset characterItemTemplate;

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
    private VisualElement characterSelectionView;

    // Character Selection UI
    private VisualElement characterListContainer;
    private ScrollView characterSelectionScroll;
    private Button btnNewCharacter;
    private Button btnCloseSelection;

    // Save state
    private List<GameSaveData> availableSaves = new List<GameSaveData>();
    private GameSaveData selectedSave;

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
    private float lastMenuOpenTime = 0f;

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
        characterSelectionView = root.Q<VisualElement>("CharacterSelectionView");

        // Character Selection UI Elements
        characterListContainer = characterSelectionView?.Q<VisualElement>("CharacterListContainer");
        characterSelectionScroll = characterSelectionView?.Q<ScrollView>(
            "CharacterSelectionScroll"
        );
        btnNewCharacter = characterSelectionView?.Q<Button>("BtnNewCharacter");
        btnCloseSelection = characterSelectionView?.Q<Button>("BtnCloseSelection");

        btnNewCharacter?.RegisterCallback<ClickEvent>(ev => OnNewCharacter());
        btnCloseSelection?.RegisterCallback<ClickEvent>(ev => HideAllSubPanels());

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
        else if (
            characterSelectionView != null
            && !characterSelectionView.ClassListContains("screen-hidden")
        )
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
        ShowCharacterSelection();
    }

    private void OnLoadGame()
    {
        ShowCharacterSelection();
    }

    private void OnNewCharacter()
    {
        if (!string.IsNullOrEmpty(characterCreatorSceneName))
        {
            LoadingManager.Instance.LoadScene(characterCreatorSceneName);
        }
    }

    private void ShowCharacterSelection()
    {
        HideAllSubPanels();
        menuLeftPanel?.SetEnabled(false);
        menuRightPanel.style.backgroundColor = new StyleColor(
            new Color(0.05f, 0.07f, 0.08f, 0.85f)
        );
        characterSelectionView?.RemoveFromClassList("screen-hidden");
        lastMenuOpenTime = Time.unscaledTime;

        PopulateCharacterList();

        // Wait 50ms before focusing
        characterSelectionView
            .schedule.Execute(() =>
            {
                if (availableSaves.Count > 0)
                {
                    var firstCard = characterListContainer?.Q<VisualElement>(
                        className: "character-item-card"
                    );
                    firstCard?.Focus();
                }
                else
                {
                    btnNewCharacter?.Focus();
                }
            })
            .StartingIn(50);
    }

    private void PopulateCharacterList()
    {
        if (characterListContainer == null || characterItemTemplate == null)
            return;

        characterListContainer.Clear();
        availableSaves = SaveSystem.GetAllSaves();

        if (availableSaves.Count == 0)
        {
            characterListContainer.Add(
                new Label("No saved characters found.")
                {
                    style =
                    {
                        color = Color.gray,
                        marginTop = 20,
                        unityTextAlign = TextAnchor.MiddleCenter,
                    },
                }
            );
            return;
        }

        foreach (var save in availableSaves)
        {
            var itemElement = characterItemTemplate.Instantiate();
            itemElement.Q<Label>("Lbl_CharacterName").text = save.characterData.Name;

            // Format class level string
            string className = "Adventurer";
            if (!string.IsNullOrEmpty(save.characterData.ClassID))
            {
                className = save.characterData.ClassID;
            }
            itemElement.Q<Label>("Lbl_CharacterClass").text =
                $"Level {save.characterData.Level} {className}";
            itemElement.Q<Label>("Lbl_LastSaved").text =
                $"Saved: {save.GetLastSavedTime().ToShortDateString()}";

            var card = itemElement.Q<VisualElement>(className: "character-item-card");

            // Play Button
            var playAction = new Action(() =>
            {
                if (Time.unscaledTime - lastMenuOpenTime < 0.3f)
                    return;

                GlobalGameState.Instance.SetCurrentCharacter(save);
                LoadingManager.Instance.LoadScene(
                    string.IsNullOrEmpty(save.lastSceneName) ? gameSceneName : save.lastSceneName
                );
            });

            card.RegisterCallback<ClickEvent>(ev => playAction());

            // Allow controller to trigger it
            var submitEvent = new NavigationSubmitEvent();
            card.RegisterCallback<NavigationSubmitEvent>(ev => playAction());

            characterListContainer.Add(itemElement);
        }
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
        characterSelectionView?.AddToClassList("screen-hidden");

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
