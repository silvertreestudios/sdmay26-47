using System;
using System.Collections.Generic;
using System.Linq;
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

    [Header("Controls Settings")]
    [SerializeField]
    private Sprite controlsSprite;

    [Header("Audio Settings")]
    [SerializeField]
    private string menuMusicName = "MenuMusic";

    [SerializeField]
    private string creditsMusicName = "CreditsMusic";

    private UIDocument uiDocument;

    // Core Panels
    private VisualElement menuLeftPanel;
    private VisualElement menuRightPanel;

    // Sub Views
    private VisualElement licenseView;
    private VisualElement creditsView;
    private VisualElement controlsView;
    private VisualElement characterSelectionView;
    private VisualElement deleteConfirmationView;

    // Character Selection UI
    private VisualElement characterListContainer;
    private ScrollView characterSelectionScroll;
    private Button btnNewCharacter;
    private Button btnCloseSelection;
    private Button btnConfirmDelete;
    private Button btnCancelDelete;
    private Label deleteMessageLabel;

    // Save state
    private List<GameSaveData> availableSaves = new List<GameSaveData>();
    private GameSaveData selectedSave;
    private GameSaveData saveToDelete;

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
        deleteConfirmationView = root.Q<VisualElement>("DeleteConfirmationView");

        // Character Selection UI Elements
        characterListContainer = characterSelectionView?.Q<VisualElement>("CharacterListContainer");
        characterSelectionScroll = characterSelectionView?.Q<ScrollView>(
            "CharacterSelectionScroll"
        );
        btnNewCharacter = root.Q<Button>("BtnNewCharacter");
        btnCloseSelection = root.Q<Button>("BtnCloseSelection");
        btnConfirmDelete = root.Q<Button>("BtnConfirmDelete");
        btnCancelDelete = root.Q<Button>("BtnCancelDelete");
        deleteMessageLabel = root.Q<Label>("DeleteConfirmationMessage");

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

        // Controls UI Elements
        controlsView = root.Q<VisualElement>("ControlsView");
        var controlsImage = controlsView?.Q<VisualElement>("ControlsImage");
        if (controlsImage != null && controlsSprite != null)
        {
            controlsImage.style.backgroundImage = new StyleBackground(controlsSprite);
        }
        var controlsCloseBtn = controlsView?.Q<Button>("ControlsCloseBtn");
        controlsCloseBtn?.RegisterCallback<ClickEvent>(ev => HideAllSubPanels());

        // Main Menu Buttons
        var btnNewGame = root.Q<Button>("BtnNewGame");
        var btnLoadGame = root.Q<Button>("BtnLoadGame");
        var btnCredits = root.Q<Button>("BtnCredits");
        var btnControls = root.Q<Button>("BtnControls");
        var btnLicense = root.Q<Button>("BtnLicense");
        var btnQuit = root.Q<Button>("BtnQuit");

        // Register Callbacks
        btnNewGame?.RegisterCallback<ClickEvent>(ev => OnNewGame());
        btnLoadGame?.RegisterCallback<ClickEvent>(ev => OnLoadGame());
        btnCredits?.RegisterCallback<ClickEvent>(ev => ShowCredits());
        btnControls?.RegisterCallback<ClickEvent>(ev => ShowControls());
        btnLicense?.RegisterCallback<ClickEvent>(ev => ShowLicense());
        btnQuit?.RegisterCallback<ClickEvent>(ev => OnQuitGame());

        btnConfirmDelete?.RegisterCallback<ClickEvent>(ev => ConfirmDelete());
        btnCancelDelete?.RegisterCallback<ClickEvent>(ev => HideDeleteConfirmation());

        // Initially hide sub-panels
        HideAllSubPanels();

        // Force unfocusable scrollbars via C# to guarantee they are never selectable
        HardenScrollViewFocus(licenseScroll);
        HardenScrollViewFocus(creditsScroll);
        HardenScrollViewFocus(characterSelectionScroll);

        // GLOBAL NAVIGATION DEBOUNCER
        root.RegisterCallback<NavigationMoveEvent>(
            evt =>
            {
                // BLOCK NAVIGATION IF ANY MODAL IS OPEN
                if (IsAnyModalOpen())
                {
                    var focused = root.focusController.focusedElement as VisualElement;
                    VisualElement activeModal = GetActiveModal();

                    // If focus escaped or we're moving, make sure we stay in the modal
                    if (activeModal != null && (focused == null || !activeModal.Contains(focused)))
                    {
                        // Snap focus back to modal's primary button
                        if (activeModal == deleteConfirmationView)
                            btnCancelDelete?.Focus();
                        else if (activeModal == licenseView)
                            licenseView.Q<Button>()?.Focus();
                        else if (activeModal == controlsView)
                            controlsView.Q<Button>()?.Focus();

                        evt.PreventDefault();
                        return;
                    }
                }

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

        // AUTO-PLAY CREDITS check (coming from Thank You scene)
        if (GlobalGameState.Instance.ShowCreditsOnMainMenu)
        {
            GlobalGameState.Instance.ShowCreditsOnMainMenu = false;
            root.schedule.Execute(ShowCredits).StartingIn(100);
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
    private float lastDeleteInputTime = 0f; // Kept for shortcut debounce
    private float lastModalCloseTime = 0f; // New for input bleed protection
    private const float deleteDebounceTime = 0.5f;

    private void Update()
    {
        if (inputService == null)
        {
            inputService = ServiceLocator.Get<InputService>();
            if (inputService != null)
            {
                inputService.OnCancelPerformed += HandleCancel;
                inputService.OnUICancelPerformed += HandleCancel;

                // Register for delete shortcut (using PagePrev/L1/LB as shortcut)
                inputService.OnUIPagePrevPerformed += HandleDeleteInput;

                inputService.OnUIConfirmPerformed += HandleConfirm;
                inputService.OnConfirmPerformed += HandleConfirm;

                // Force switch to UI map for menu interactions
                inputService.SwitchToActionMap("UI");
            }
        }

        if (isCreditsRolling)
        {
            HandleCreditsUpdate();
        }

        HandleDpadNavigation();
        HandleRightStickScrolling();
    }

    private float lastCancelTime = 0f;

    private void HandleCancel(object sender, EventArgs e)
    {
        if (Time.unscaledTime - lastCancelTime < 0.2f)
            return;
        lastCancelTime = Time.unscaledTime;

        if (
            deleteConfirmationView != null
            && !deleteConfirmationView.ClassListContains("screen-hidden")
        )
        {
            HideDeleteConfirmation();
        }
        else if (licenseView != null && !licenseView.ClassListContains("screen-hidden"))
        {
            HideAllSubPanels();
        }
        else if (controlsView != null && !controlsView.ClassListContains("screen-hidden"))
        {
            HideAllSubPanels();
        }
        else if (
            characterSelectionView != null
            && !characterSelectionView.ClassListContains("screen-hidden")
        )
        {
            HideAllSubPanels();
        }
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

                // Block if a modal was just closed (prevents input bleed)
                if (Time.unscaledTime - lastModalCloseTime < 0.6f)
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

            // Store save data for easy access during shortcut input
            card.userData = save;

            // Auto-scroll to card when it gains focus via controller/keyboard
            card.RegisterCallback<FocusInEvent>(ev =>
            {
                characterSelectionScroll?.ScrollTo(card);
            });

            // Explicitly handle Up/Down navigation to stay within the list
            card.RegisterCallback<NavigationMoveEvent>(ev =>
            {
                if (IsAnyModalOpen())
                    return;

                if (ev.direction == NavigationMoveEvent.Direction.Down)
                {
                    int index = characterListContainer.IndexOf(itemElement);
                    if (index < characterListContainer.childCount - 1)
                    {
                        var nextItem = characterListContainer[index + 1];
                        var nextCard = nextItem.Q<VisualElement>(className: "character-item-card");
                        if (nextCard != null)
                        {
                            nextCard.Focus();
                            ev.PreventDefault();
                        }
                    }
                }
                else if (ev.direction == NavigationMoveEvent.Direction.Up)
                {
                    int index = characterListContainer.IndexOf(itemElement);
                    if (index > 0)
                    {
                        var prevItem = characterListContainer[index - 1];
                        var prevCard = prevItem.Q<VisualElement>(className: "character-item-card");
                        if (prevCard != null)
                        {
                            prevCard.Focus();
                            ev.PreventDefault();
                        }
                    }
                }
            });

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

        // Switch music back to main menu music if coming from credits
        ServiceLocator.Get<TacticsGame.Core.MusicManager>()?.PlayMusic(menuMusicName);

        licenseView?.AddToClassList("screen-hidden");
        creditsView?.AddToClassList("screen-hidden");
        controlsView?.AddToClassList("screen-hidden");
        deleteConfirmationView?.AddToClassList("screen-hidden");
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

        // Switch to credits music
        ServiceLocator.Get<TacticsGame.Core.MusicManager>()?.PlayMusic(creditsMusicName);
    }

    private void ShowControls()
    {
        HideAllSubPanels();
        menuLeftPanel?.SetEnabled(false);
        menuRightPanel.style.backgroundColor = new StyleColor(
            new Color(0.05f, 0.07f, 0.08f, 0.85f)
        );
        controlsView?.RemoveFromClassList("screen-hidden");

        // Focus close button for controller support
        var closeBtn = controlsView?.Q<Button>("ControlsCloseBtn");
        closeBtn?.Focus();
    }

    private void HandleDeleteInput(object sender, EventArgs e)
    {
        // Only allow if character selection is open and delete modal is NOT open
        if (
            characterSelectionView == null
            || characterSelectionView.ClassListContains("screen-hidden")
        )
            return;

        // DEBOUNCE: Prevent rapid-fire deletion
        if (Time.unscaledTime - lastDeleteInputTime < deleteDebounceTime)
            return;

        if (
            deleteConfirmationView != null
            && !deleteConfirmationView.ClassListContains("screen-hidden")
        )
            return;

        lastDeleteInputTime = Time.unscaledTime;
        Debug.Log("[MainMenu] Delete shortcut detected");

        var focused = uiDocument.rootVisualElement.focusController.focusedElement as VisualElement;
        Debug.Log(
            $"[MainMenu] Focused element for delete: {focused?.name ?? "null"} (Class: {focused?.GetClasses().FirstOrDefault() ?? "none"})"
        );

        if (focused != null && focused.ClassListContains("character-item-card"))
        {
            saveToDelete = focused.userData as GameSaveData;
            if (saveToDelete != null)
            {
                ShowDeleteConfirmation();
            }
        }
    }

    private void ShowDeleteConfirmation()
    {
        if (deleteConfirmationView == null)
            return;

        if (deleteMessageLabel != null && saveToDelete != null)
        {
            deleteMessageLabel.text =
                $"Are you sure you want to delete {saveToDelete.characterData?.Name}? This cannot be undone.";
        }

        deleteConfirmationView.RemoveFromClassList("screen-hidden");
        btnCancelDelete?.Focus(); // Default to Cancel for safety
    }

    private void ConfirmDelete()
    {
        if (saveToDelete != null)
        {
            SaveSystem.Delete(saveToDelete.saveId);
            saveToDelete = null;

            // Reset modal close timer to prevent input bleed into the character list
            lastModalCloseTime = Time.unscaledTime;

            // Refresh list
            PopulateCharacterList();

            // Focus the close button or first item if list is empty
            if (availableSaves.Count > 0)
            {
                var root = uiDocument.rootVisualElement;
                root.schedule.Execute(() =>
                    {
                        var firstCard = characterListContainer.Q<VisualElement>(
                            className: "character-item-card"
                        );
                        firstCard?.Focus();
                    })
                    .StartingIn(50);
            }
            else
            {
                btnCloseSelection?.Focus();
            }
        }

        HideDeleteConfirmation();
    }

    private void HideDeleteConfirmation()
    {
        deleteConfirmationView?.AddToClassList("screen-hidden");

        // Reset modal close timer to prevent input bleed into the character list
        lastModalCloseTime = Time.unscaledTime;

        // Return focus to character selection if we are still in it
        if (
            characterSelectionView != null
            && !characterSelectionView.ClassListContains("screen-hidden")
        )
        {
            // Focus the previously focused element or first card
            var firstCard = characterListContainer.Q<VisualElement>(
                className: "character-item-card"
            );
            firstCard?.Focus();
        }
    }

    private bool IsAnyModalOpen()
    {
        return GetActiveModal() != null;
    }

    private VisualElement GetActiveModal()
    {
        if (
            deleteConfirmationView != null
            && !deleteConfirmationView.ClassListContains("screen-hidden")
        )
            return deleteConfirmationView;
        if (licenseView != null && !licenseView.ClassListContains("screen-hidden"))
            return licenseView;
        if (controlsView != null && !controlsView.ClassListContains("screen-hidden"))
            return controlsView;
        return null;
    }
}
