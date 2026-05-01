using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Data;
using TacticsGame.Data.TacticsCore;
using TacticsGame.Data.TacticsRuleset;
using TacticsGame.InputSystem;
using TacticsGame.Items;
using UnityEngine;
using UnityEngine.UIElements;
using TacticsSkillType = TacticsGame.Data.TacticsCore.SkillType;

namespace TacticsGame.UI.CharacterCreator
{
    [RequireComponent(typeof(UIDocument))]
    public class CharacterCreatorUIToolkit : MonoBehaviour
    {
        [Header("State")]
        [SerializeField]
        private CreatorState currentState = CreatorState.Visuals;

        [Header("Data")]
        [SerializeField]
        private TacticsRulesetDatabase database;

        private CharacterDataPayload payload;
        private CharacterCreationRulesSummary rulesSummary;

        [Header("Preview Rendering")]
        [SerializeField]
        private RenderTexture previewTexture;

        [SerializeField]
        private TacticsGame.Characters.Visuals.VisualPartManager visualManager;

        [SerializeField]
        private UnitEquipment previewEquipment;

        [Header("Preview Controls")]
        [SerializeField]
        private Camera previewCamera;

        [SerializeField]
        private float rotationSpeed = 200f;

        [SerializeField]
        private float zoomSpeed = 50f;

        [SerializeField]
        private float minFOV = 15f;

        [SerializeField]
        private float maxFOV = 60f;
        private float targetFOV = 40f;

        [Header("UXML Templates")]
        [SerializeField]
        private VisualTreeAsset stepConceptTemplate;

        [SerializeField]
        private VisualTreeAsset stepAncestryTemplate;

        [SerializeField]
        private VisualTreeAsset stepBackgroundTemplate;

        [SerializeField]
        private VisualTreeAsset stepClassTemplate;

        [SerializeField]
        private VisualTreeAsset stepFreeBoostsTemplate;

        [SerializeField]
        private VisualTreeAsset stepClassDetailsTemplate;

        [SerializeField]
        private VisualTreeAsset stepEquipmentTemplate;

        [SerializeField]
        private VisualTreeAsset stepModifiersTemplate;

        [SerializeField]
        private VisualTreeAsset stepFinishingDetailsTemplate;

        [SerializeField]
        private VisualTreeAsset stepVisualsTemplate;

        [Header("Component Library")]
        [SerializeField]
        private VisualTreeAsset choiceCardTemplate;

        [SerializeField]
        private VisualTreeAsset carouselSelectorTemplate;

        [SerializeField]
        private int rowCount = 8;

        [SerializeField]
        private int visualRowCount = 8;

        // UI Elements
        private UIDocument uiDocument;
        private VisualElement root;
        private VisualElement contentArea;
        private VisualElement previewPanel;
        private VisualElement previewTextureArea;
        private VisualElement statSummary;
        private VisualElement sidebar;
        private ScrollView activeStepScrollView;
        private ListView equipmentOffHandList;
        private Label lblPreviewName;

        // Stat Summary Elements
        private Label lblTotalHP;
        private Label lblSpeed;
        private Label lblClassDC;
        private Label lblFocusPoints;
        private Button btnFinalize;
        private Button btnPreviousStep;
        private Button btnNextStep;

        private Label lblLoadoutMain;
        private Label lblLoadoutOff;
        private Label lblLoadoutArmor;
        private Button btnEquipmentMainTab;
        private Button btnEquipmentOffTab;
        private Button btnEquipmentArmorTab;

        private EquipmentSlotTab activeEquipmentTab = EquipmentSlotTab.MainHand;

        private enum EquipmentSlotTab
        {
            MainHand,
            OffHand,
            Armor,
        }

        // Navigation Buttons
        private Dictionary<CreatorState, Button> navButtons =
            new Dictionary<CreatorState, Button>();

        // Controller Navigation Tracking
        private List<VisualElement> focusableRows = new List<VisualElement>();
        private readonly Dictionary<VisualElement, Action> carouselLeftActions =
            new Dictionary<VisualElement, Action>();
        private readonly Dictionary<VisualElement, Action> carouselRightActions =
            new Dictionary<VisualElement, Action>();
        private readonly List<VisualElement> controllerFocusElements = new List<VisualElement>();
        private int currentControllerFocusIndex = -1;
        private InputService inputService;
        private Action onPageNext;
        private Action onPagePrev;
        private bool isInitialized;
        private bool isStepDrawerOpen;

        private Dictionary<ScrollView, float> activeScrollTargets =
            new Dictionary<ScrollView, float>();
        private const float SCROLL_SMOOTH_SPEED = 14f;

        public event Action<CreatorState> OnStateChanged;
        public event Action<CharacterDataPayload> OnPayloadUpdated;

        private void OnEnable()
        {
            uiDocument = GetComponent<UIDocument>();
            payload = new CharacterDataPayload();
            isInitialized = false;
        }

        private void Start()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            StartCoroutine(InitializeWhenDocumentReady());
        }

        private System.Collections.IEnumerator InitializeWhenDocumentReady()
        {
            // Give UIDocument's own OnEnable a couple of frames to clone its
            // visualTreeAsset into rootVisualElement.
            yield return null;
            yield return null;

            if (uiDocument == null)
            {
                yield break;
            }

            VisualTreeAsset masterAsset = uiDocument.visualTreeAsset;
            if (masterAsset == null)
            {
                yield break;
            }

            // Verify the imported asset actually has content. If Unity's import is stale,
            // Instantiate() will return an empty TemplateContainer; bail loudly.
            VisualElement probe = masterAsset.Instantiate();
            int probeChildren = probe?.childCount ?? 0;
            if (probeChildren == 0)
            {
                yield break;
            }

            // Wait up to ~0.5s for the panel root to be ready, then host our own clone
            // so UIDocument cannot clear it under us.
            VisualElement hostedClone = null;
            for (int i = 0; i < 30; i++)
            {
                root = uiDocument.rootVisualElement;
                if (root != null)
                {
                    BindMasterLayout();
                    if (contentArea != null)
                        break;

                    if (hostedClone == null)
                    {
                        hostedClone = masterAsset.Instantiate();
                        hostedClone.name = "CharacterCreatorUI_Hosted";
                        hostedClone.style.flexGrow = 1;
                        root.Add(hostedClone);

                        BindMasterLayout();
                        if (contentArea != null)
                            break;
                    }
                }

                yield return null;
            }

            if (contentArea == null)
            {
                yield break;
            }

            if (isInitialized)
                yield break;

            isInitialized = true;

            // Clear the controller-focus highlight whenever the player switches to the mouse
            // so the cyan outline doesn't linger after a mouse click.
            root.RegisterCallback<PointerDownEvent>(
                evt =>
                {
                    // pointerId 0 = mouse (guaranteed across all Unity versions)
                    if (evt.pointerId != 0)
                        return;
                    foreach (VisualElement el in controllerFocusElements)
                    {
                        el.RemoveFromClassList("creator-controller-focused");
                        el.RemoveFromClassList("carousel-container--focused");
                    }
                    currentControllerFocusIndex = -1;
                },
                TrickleDown.TrickleDown
            );

            SetupInput();
            ChangeState(CreatorState.Visuals);
            UpdateStatSummary();
        }

        private void LogVisualTreeForDiagnostics()
        {
            string assetName =
                uiDocument != null && uiDocument.visualTreeAsset != null
                    ? uiDocument.visualTreeAsset.name
                    : "<null>";
            Debug.LogError(
                $"[CharacterCreatorUI] Could not find 'ContentArea' after the UIDocument finished loading.\n"
                    + $"  - UIDocument source asset: {assetName}\n"
                    + $"  - rootVisualElement child count: {(root != null ? root.childCount : 0)}\n"
                    + $"  - Hierarchy:\n{DescribeVisualTree(root, 0)}\n"
                    + $"Make sure the UIDocument's Source Asset is CharacterCreatorUI.uxml (which contains a VisualElement named 'ContentArea')."
            );
        }

        private static string DescribeVisualTree(VisualElement element, int depth)
        {
            if (element == null)
                return "(null)";

            string indent = new string(' ', depth * 2);
            string line =
                $"{indent}- {(string.IsNullOrEmpty(element.name) ? "<unnamed>" : element.name)} ({element.GetType().Name})";
            if (element.childCount == 0)
                return line;

            System.Text.StringBuilder builder = new System.Text.StringBuilder(line);
            for (int i = 0; i < element.childCount; i++)
            {
                builder.AppendLine();
                builder.Append(DescribeVisualTree(element[i], depth + 1));
            }
            return builder.ToString();
        }

        private void OnDisable()
        {
            isInitialized = false;
            if (inputService != null)
            {
                inputService.OnConfirmPerformed -= HandleConfirmPerformed;
                inputService.OnCancelPerformed -= HandleCancelPerformed;
                inputService.OnToggleWaypointPerformed -= HandleToggleStepDrawerPerformed;

                inputService.OnUIConfirmPerformed -= HandleConfirmPerformed;
                inputService.OnUICancelPerformed -= HandleCancelPerformed;
                inputService.OnUIToggleStepsPerformed -= HandleToggleStepDrawerPerformed;
                inputService.OnUIPageNextPerformed -= HandlePageNext;
                inputService.OnUIPagePrevPerformed -= HandlePagePrev;
                inputService.OnUIStepNextPerformed -= HandleNextStepPerformed;
                inputService.OnUIStepPrevPerformed -= HandlePrevStepPerformed;

                inputService.SwitchToActionMap("Player");
            }
        }

        private void SetupInput()
        {
            if (ServiceLocator.TryGet(out inputService))
            {
                inputService.OnConfirmPerformed -= HandleConfirmPerformed;
                inputService.OnCancelPerformed -= HandleCancelPerformed;
                inputService.OnToggleWaypointPerformed -= HandleToggleStepDrawerPerformed;

                inputService.OnUIConfirmPerformed -= HandleConfirmPerformed;
                inputService.OnUICancelPerformed -= HandleCancelPerformed;
                inputService.OnUIToggleStepsPerformed -= HandleToggleStepDrawerPerformed;
                inputService.OnUIPageNextPerformed -= HandlePageNext;
                inputService.OnUIPagePrevPerformed -= HandlePagePrev;
                inputService.OnUIStepNextPerformed -= HandleNextStepPerformed;
                inputService.OnUIStepPrevPerformed -= HandlePrevStepPerformed;

                inputService.OnUIConfirmPerformed += HandleConfirmPerformed;
                inputService.OnUICancelPerformed += HandleCancelPerformed;
                inputService.OnUIToggleStepsPerformed += HandleToggleStepDrawerPerformed;
                inputService.OnUIPageNextPerformed += HandlePageNext;
                inputService.OnUIPagePrevPerformed += HandlePagePrev;
                inputService.OnUIStepNextPerformed += HandleNextStepPerformed;
                inputService.OnUIStepPrevPerformed += HandlePrevStepPerformed;

                inputService.SwitchToActionMap("UI");
            }
        }

        private void HandleNextStepPerformed(object sender, EventArgs e) => NextState();

        private void HandlePrevStepPerformed(object sender, EventArgs e) => PreviousState();

        private void HandlePageNext(object sender, EventArgs e) => onPageNext?.Invoke();

        private void HandlePagePrev(object sender, EventArgs e) => onPagePrev?.Invoke();

        private void Update()
        {
            // If we are using a controller but the UI focus has drifted to an element
            // not in our focus list (like a TextField we didn't explicitly select),
            // we need to pull it back.
            MaintainControllerFocus();

            HandleControllerNavigation();
            HandlePreviewControls();
            UpdateSmoothScrolling();
        }

        private void HandlePreviewControls()
        {
            Vector2 stick = GetRightStickVector();

            // Rotation (Horizontal)
            if (Mathf.Abs(stick.x) > 0.1f && visualManager != null)
            {
                visualManager.transform.Rotate(
                    Vector3.up,
                    -stick.x * rotationSpeed * Time.deltaTime
                );
            }

            // Zoom (Vertical - mapped to FOV)
            if (Mathf.Abs(stick.y) > 0.1f && previewCamera != null)
            {
                // Push up to zoom in (smaller FOV), pull down to zoom out (larger FOV)
                targetFOV -= stick.y * zoomSpeed * Time.deltaTime;
                targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
                previewCamera.fieldOfView = targetFOV;
            }
        }

        private Vector2 GetRightStickVector()
        {
            return inputService != null ? inputService.GetRotationVector() : Vector2.zero;
        }

        private void MaintainControllerFocus()
        {
            if (currentControllerFocusIndex < 0 || controllerFocusElements.Count == 0)
                return;

            // If the actual UI focus is null or stuck on the root, but we have a controller index,
            // re-apply it. This fixes theft by TextFields or auto-navigation.
            var currentUIFocus = root.focusController?.focusedElement;
            var expectedFocus = controllerFocusElements[currentControllerFocusIndex];

            if (
                currentUIFocus is VisualElement currentVE
                && currentVE != expectedFocus
                && !IsNavigatingWithMouse()
            )
            {
                // Only pull focus back if the stolen focus is a TextField or generic container
                if (currentVE is TextField || currentVE.name == "unity-content-container")
                {
                    expectedFocus.Focus();
                }
            }
        }

        private bool IsNavigatingWithMouse()
        {
            // If the mouse has moved recently, we assume the user is using the mouse
            return Input.GetAxis("Mouse X") != 0
                || Input.GetAxis("Mouse Y") != 0
                || Input.GetMouseButton(0);
        }

        private void HandleControllerNavigation()
        {
            if (controllerFocusElements.Count == 0)
                return;

            Vector2 move = GetCreatorMoveVector();
            if (move == Vector2.zero || Time.time <= nextMoveTime)
                return;

            // If a TextField is focused, ignore keyboard movement (so they can type),
            // but allow Gamepad movement to pull focus away.
            if (IsTextFieldFocused() && inputService.IsMovementFromKeyboard())
                return;

            if (move.y > 0.5f && Time.time > nextMoveTime)
            {
                MoveCurrentFocusVertical(-1);
                nextMoveTime = Time.time + moveDelay;
            }
            else if (move.y < -0.5f && Time.time > nextMoveTime)
            {
                MoveCurrentFocusVertical(1);
                nextMoveTime = Time.time + moveDelay;
            }
            else if (move.x > 0.5f && Time.time > nextMoveTime)
            {
                MoveCurrentFocusHorizontal(1);
                nextMoveTime = Time.time + moveDelay;
            }
            else if (move.x < -0.5f && Time.time > nextMoveTime)
            {
                MoveCurrentFocusHorizontal(-1);
                nextMoveTime = Time.time + moveDelay;
            }
        }

        private float nextMoveTime = 0;
        private float moveDelay = 0.2f;

        private Vector2 GetCreatorMoveVector()
        {
            return inputService != null ? inputService.GetMovementVectorNormalized() : Vector2.zero;
        }

        private void MoveCurrentFocusVertical(int delta)
        {
            VisualElement current = GetCurrentControllerFocus();
            if (current is ListView listView && TryMoveListSelection(listView, delta))
                return;

            if (focusableRows.Contains(current))
            {
                MoveCarouselFocus(delta);
                return;
            }

            if (IsNavButton(current))
            {
                MoveSidebarFocus(delta);
                return;
            }

            // Geometric 2D navigation - in UI screen space Y increases downward,
            // so pressing up (delta=-1) -> direction (0,-1), pressing down (delta=1) -> (0,1).
            VisualElement next = GetClosestElementInDirection(new Vector2(0, delta));
            if (next != null)
                SetControllerFocus(controllerFocusElements.IndexOf(next));
            else
                ChangeControllerFocus(delta); // Fallback to linear
        }

        private void MoveCurrentFocusHorizontal(int delta)
        {
            VisualElement current = GetCurrentControllerFocus();
            if (focusableRows.Contains(current))
            {
                InvokeCarouselAction(current, delta);
                return;
            }

            // Geometric 2D navigation
            VisualElement next = GetClosestElementInDirection(new Vector2(delta, 0));
            if (next != null)
                SetControllerFocus(controllerFocusElements.IndexOf(next));
            else
                ChangeControllerFocus(delta); // Fallback to linear
        }

        private VisualElement GetClosestElementInDirection(Vector2 direction)
        {
            VisualElement current = GetCurrentControllerFocus();
            if (current == null || controllerFocusElements.Count <= 1)
                return null;

            Rect currentRect = current.worldBound;
            Vector2 currentCenter = currentRect.center;

            VisualElement bestMatch = null;
            float bestScore = float.MaxValue;

            foreach (var candidate in controllerFocusElements)
            {
                if (candidate == current || !candidate.enabledInHierarchy || !candidate.visible)
                    continue;

                Rect candidateRect = candidate.worldBound;
                Vector2 candidateCenter = candidateRect.center;
                Vector2 diff = candidateCenter - currentCenter;
                float dot = Vector2.Dot(diff.normalized, direction);

                // Check if candidate is in the desired cone (approx 90 degrees)
                if (dot > 0.707f)
                {
                    float dist = diff.magnitude;
                    // Project the difference onto the axis perpendicular to direction
                    Vector2 perpDir = new Vector2(-direction.y, direction.x);
                    float perpDist = Math.Abs(Vector2.Dot(diff, perpDir));

                    // Score is distance + high penalty for perpendicular deviation
                    float score = dist + (perpDist * 3.0f);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestMatch = candidate;
                    }
                }
            }

            return bestMatch;
        }

        private void ChangeControllerFocus(int delta)
        {
            if (controllerFocusElements.Count == 0)
                return;

            SetControllerFocus(currentControllerFocusIndex + delta);
        }

        private void SetControllerFocus(int index)
        {
            if (controllerFocusElements.Count == 0)
                return;

            if (
                currentControllerFocusIndex >= 0
                && currentControllerFocusIndex < controllerFocusElements.Count
            )
            {
                VisualElement oldFocus = controllerFocusElements[currentControllerFocusIndex];
                oldFocus.RemoveFromClassList("creator-controller-focused");
                oldFocus.RemoveFromClassList("carousel-container--focused");
            }

            currentControllerFocusIndex = WrapIndex(index, controllerFocusElements.Count);

            VisualElement newFocus = controllerFocusElements[currentControllerFocusIndex];
            newFocus.AddToClassList("creator-controller-focused");
            if (focusableRows.Contains(newFocus))
                newFocus.AddToClassList("carousel-container--focused");

            newFocus.Focus();

            newFocus.Focus();
            ScrollFocusedElementIntoView(newFocus);
        }

        private void UpdateSmoothScrolling()
        {
            if (activeScrollTargets.Count == 0)
                return;

            // We use a list to store keys because we may remove them during iteration
            List<ScrollView> activeViews = activeScrollTargets.Keys.ToList();
            foreach (var view in activeViews)
            {
                if (view == null || view.panel == null)
                {
                    activeScrollTargets.Remove(view);
                    continue;
                }

                float target = activeScrollTargets[view];
                float current = view.scrollOffset.y;
                float next = Mathf.Lerp(current, target, Time.deltaTime * SCROLL_SMOOTH_SPEED);

                view.scrollOffset = new Vector2(view.scrollOffset.x, next);

                if (Mathf.Abs(next - target) < 1f)
                {
                    view.scrollOffset = new Vector2(view.scrollOffset.x, target);
                    activeScrollTargets.Remove(view);
                }
            }
        }

        private void ScrollFocusedElementIntoView(VisualElement element)
        {
            if (element == null)
                return;

            ScrollView scrollView = FindContainingScrollView(element);
            if (scrollView == null || !IsDescendantOf(element, scrollView.contentContainer))
                return;

            // ScrollView.ScrollTo + schedule.Execute can fire before layout has resolved,
            // which is why the highlight slips off-screen. If the target hasn't been measured
            // yet, retry once geometry is available.
            if (float.IsNaN(element.layout.y) || element.layout.height <= 0f)
            {
                EventCallback<GeometryChangedEvent> handler = null;
                handler = _ =>
                {
                    element.UnregisterCallback(handler);
                    EnsureChildVisibleInScrollView(scrollView, element);
                };
                element.RegisterCallback(handler);
                return;
            }

            EnsureChildVisibleInScrollView(scrollView, element);
        }

        private void EnsureChildVisibleInScrollView(ScrollView scrollView, VisualElement element)
        {
            if (scrollView == null || element == null)
                return;

            VisualElement viewport = scrollView.contentViewport ?? scrollView;
            float viewportHeight = viewport.layout.height;
            if (viewportHeight <= 0f || float.IsNaN(viewportHeight))
                return;

            // Calculate the local position of the element relative to the content container
            Vector2 localPos = element.ChangeCoordinatesTo(
                scrollView.contentContainer,
                Vector2.zero
            );
            float top = localPos.y;
            float bottom = top + element.layout.height;

            float currentOffset = scrollView.scrollOffset.y;
            const float padding = 24f;

            float newOffset = currentOffset;

            // Only scroll if the element is actually outside the current view bounds (with padding)
            if (top < currentOffset + padding)
            {
                newOffset = Mathf.Max(0f, top - padding);
            }
            else if (bottom > currentOffset + viewportHeight - padding)
            {
                newOffset = bottom - viewportHeight + padding;
            }

            // Don't do anything if we're already centered or within bounds
            if (Mathf.Approximately(newOffset, currentOffset))
            {
                // If the element is already visible, clear any pending smooth scroll for this view
                if (activeScrollTargets.ContainsKey(scrollView))
                    activeScrollTargets.Remove(scrollView);
                return;
            }

            activeScrollTargets[scrollView] = newOffset;
        }

        private ScrollView FindContainingScrollView(VisualElement element)
        {
            VisualElement current = element;
            while (current != null)
            {
                if (current is ScrollView scrollView)
                    return scrollView;

                current = GetVisualParent(current);
            }

            return
                activeStepScrollView != null
                && IsDescendantOf(element, activeStepScrollView.contentContainer)
                ? activeStepScrollView
                : null;
        }

        private bool IsDescendantOf(VisualElement element, VisualElement ancestor)
        {
            VisualElement current = element;
            while (current != null)
            {
                if (current == ancestor)
                    return true;

                current = GetVisualParent(current);
            }

            return false;
        }

        private VisualElement GetDirectChildUnderAncestor(
            VisualElement element,
            VisualElement ancestor
        )
        {
            VisualElement current = element;
            VisualElement child = null;

            while (current != null && current != ancestor)
            {
                child = current;
                current = GetVisualParent(current);
            }

            return current == ancestor ? child : null;
        }

        private VisualElement GetVisualParent(VisualElement element)
        {
            return element?.hierarchy.parent ?? element?.parent;
        }

        private bool IsTextFieldFocused()
        {
            FocusController focusController = root?.focusController;
            return focusController?.focusedElement is VisualElement focusedElement
                && FindAncestor<TextField>(focusedElement) != null;
        }

        private T FindAncestor<T>(VisualElement element)
            where T : VisualElement
        {
            VisualElement current = element;
            while (current != null)
            {
                if (current is T typedElement)
                    return typedElement;

                current = current.parent;
            }

            return null;
        }

        private VisualElement GetCurrentControllerFocus()
        {
            if (
                currentControllerFocusIndex < 0
                || currentControllerFocusIndex >= controllerFocusElements.Count
            )
                return null;

            return controllerFocusElements[currentControllerFocusIndex];
        }

        private bool TryMoveListSelection(ListView listView, int delta)
        {
            if (listView.itemsSource == null || listView.itemsSource.Count == 0)
                return false;

            int selectedIndex = listView.selectedIndex >= 0 ? listView.selectedIndex : 0;
            listView.selectedIndex = Mathf.Clamp(
                selectedIndex + delta,
                0,
                listView.itemsSource.Count - 1
            );
            listView.ScrollToItem(listView.selectedIndex);
            return true;
        }

        private int WrapIndex(int index, int count)
        {
            if (count <= 0)
                return -1;

            if (index < 0)
                return count - 1;
            if (index >= count)
                return 0;
            return index;
        }

        private void HandleConfirmPerformed(object sender, EventArgs e)
        {
            // Allow confirmation even if a TextField is focused, UNLESS the controller
            // highlight is actually on the TextField itself.
            VisualElement focused = GetCurrentControllerFocus();
            if (focused is TextField && IsTextFieldFocused())
            {
                // If we are actually typing in the focused field, let it handle the input.
                return;
            }

            ActivateCurrentControllerFocus();
        }

        private void ActivateCurrentControllerFocus()
        {
            VisualElement current = GetCurrentControllerFocus();
            if (current == null)
            {
                return;
            }

            if (current is Button button)
            {
                SendButtonClick(button);
            }
            else if (current.ClassListContains("choice-card"))
            {
                SendVisualElementClick(current);
            }
            else if (focusableRows.Contains(current))
            {
                SendButtonClick(current.Q<Button>("Btn_Right"));
            }
            else if (current is TextField textField)
            {
                textField.Focus();
            }
            else if (current is ListView listView)
            {
                if (listView.selectedIndex < 0 && listView.itemsSource?.Count > 0)
                    listView.selectedIndex = 0;
            }
        }

        private void SendButtonClick(Button button)
        {
            if (button == null || !button.enabledSelf)
                return;

            SendVisualElementClick(button);
        }

        private void SendVisualElementClick(VisualElement element)
        {
            if (element == null || !element.enabledSelf)
                return;

            using (ClickEvent clickEvent = ClickEvent.GetPooled())
            {
                clickEvent.target = element;
                element.SendEvent(clickEvent);
            }
        }

        private void HandleToggleStepDrawerPerformed(object sender, EventArgs e)
        {
            if (IsTextFieldFocused())
                return;

            ToggleStepDrawer();
        }

        private void HandleCancelPerformed(object sender, EventArgs e)
        {
            if (IsTextFieldFocused())
                return;

            CancelControllerFocus();
        }

        private void CancelControllerFocus()
        {
            if (isStepDrawerOpen)
            {
                SetStepDrawerOpen(false);
                return;
            }

            CreatorState previousState = GetPreviousState(currentState);
            if (previousState != currentState)
                ChangeState(previousState);
        }

        private bool IsNavButton(VisualElement element)
        {
            return element is Button button && navButtons.ContainsValue(button);
        }

        private void FocusSidebarForCurrentState()
        {
            SetStepDrawerOpen(true);
            if (navButtons.TryGetValue(currentState, out Button navButton))
                FocusElement(navButton);
        }

        private void MoveSidebarFocus(int delta)
        {
            CreatorState[] states = GetOrderedCreatorStates();
            CreatorState currentNavState = currentState;
            VisualElement current = GetCurrentControllerFocus();

            foreach (var kvp in navButtons)
            {
                if (kvp.Value == current)
                {
                    currentNavState = kvp.Key;
                    break;
                }
            }

            int currentIndex = Array.IndexOf(states, currentNavState);
            CreatorState nextState = states[WrapIndex(currentIndex + delta, states.Length)];
            if (navButtons.TryGetValue(nextState, out Button navButton))
                FocusElement(navButton);
        }

        private void MoveCarouselFocus(int delta)
        {
            VisualElement current = GetCurrentControllerFocus();
            int currentRowIndex = focusableRows.IndexOf(current);
            if (currentRowIndex < 0)
                return;

            VisualElement nextRow = focusableRows[
                WrapIndex(currentRowIndex + delta, focusableRows.Count)
            ];
            FocusElement(nextRow);
        }

        private void FocusElement(VisualElement element)
        {
            int index = controllerFocusElements.IndexOf(element);
            if (index >= 0)
                SetControllerFocus(index);
        }

        private void InvokeCarouselAction(VisualElement row, int delta)
        {
            if (delta > 0 && carouselRightActions.TryGetValue(row, out Action rightAction))
            {
                rightAction.Invoke();
                return;
            }

            if (delta < 0 && carouselLeftActions.TryGetValue(row, out Action leftAction))
                leftAction.Invoke();
        }

        private void ToggleStepDrawer()
        {
            SetStepDrawerOpen(!isStepDrawerOpen);
        }

        private void SetStepDrawerOpen(bool open)
        {
            isStepDrawerOpen = open;
            if (sidebar == null)
                return;

            if (open)
            {
                sidebar.AddToClassList("layout-sidebar-left--open");
                sidebar.RemoveFromClassList("layout-sidebar-left--collapsed");
            }
            else
            {
                sidebar.RemoveFromClassList("layout-sidebar-left--open");
                sidebar.AddToClassList("layout-sidebar-left--collapsed");
            }

            RebuildControllerFocus();
            if (open && navButtons.TryGetValue(currentState, out Button navButton))
                FocusElement(navButton);
        }

        private void SelectStateFromSidebar(CreatorState state)
        {
            ChangeState(state);
            SetStepDrawerOpen(false);
            FocusFirstActiveContent();
        }

        private void FocusFirstActiveContent()
        {
            if (controllerFocusElements.Count > 0)
                SetControllerFocus(0);
        }

        private CreatorState GetPreviousState(CreatorState state)
        {
            CreatorState[] states = GetOrderedCreatorStates();
            int index = Array.IndexOf(states, state);
            if (index <= 0)
                return state;

            return states[index - 1];
        }

        public void NextState()
        {
            CreatorState[] states = GetOrderedCreatorStates();
            int index = Array.IndexOf(states, currentState);
            if (index >= 0 && index < states.Length - 1)
                ChangeState(states[index + 1]);
        }

        public void PreviousState()
        {
            CreatorState previousState = GetPreviousState(currentState);
            if (previousState != currentState)
                ChangeState(previousState);
        }

        private CreatorState[] GetOrderedCreatorStates()
        {
            return new[]
            {
                CreatorState.Visuals,
                CreatorState.Concept,
                CreatorState.Ancestry,
                CreatorState.Background,
                CreatorState.Class,
                CreatorState.FreeBoosts,
                CreatorState.ClassDetails,
                CreatorState.Equipment,
                CreatorState.Modifiers,
                CreatorState.FinishingDetails,
            };
        }

        private void BindMasterLayout()
        {
            contentArea = root.Q<VisualElement>("ContentArea");
            previewPanel = root.Q<VisualElement>("PreviewPanel");
            previewTextureArea = root.Q<VisualElement>("PreviewTextureArea");
            statSummary = root.Q<VisualElement>("StatSummary");
            sidebar = root.Q<VisualElement>("Sidebar");

            if (previewPanel != null)
                previewPanel.RegisterCallback<GeometryChangedEvent>(_ => UpdatePreviewAspect());
            if (statSummary != null)
                statSummary.RegisterCallback<GeometryChangedEvent>(_ => UpdatePreviewAspect());
            if (previewTextureArea != null)
            {
                previewTextureArea.RegisterCallback<GeometryChangedEvent>(_ =>
                    UpdatePreviewAspect()
                );
            }

            if (sidebar != null)
                SetStepDrawerOpen(false);

            // Apply RenderTexture to the preview area
            if (previewTexture != null && previewTextureArea != null)
            {
                previewTextureArea.style.backgroundImage = Background.FromRenderTexture(
                    previewTexture
                );
            }

            lblTotalHP = root.Q<Label>("Lbl_TotalHP");
            lblSpeed = root.Q<Label>("Lbl_Speed");
            lblClassDC = root.Q<Label>("Lbl_ClassDC");
            lblFocusPoints = root.Q<Label>("Lbl_FocusPoints");
            lblPreviewName = root.Q<Label>("Lbl_PreviewName");

            btnFinalize = root.Q<Button>("Btn_Finalize");
            if (btnFinalize != null)
                btnFinalize.clicked += FinalizeCharacter;

            btnPreviousStep = root.Q<Button>("Btn_PreviousStep");
            if (btnPreviousStep != null)
                btnPreviousStep.clicked += PreviousState;

            btnNextStep = root.Q<Button>("Btn_NextStep");
            if (btnNextStep != null)
                btnNextStep.clicked += NextState;

            // Bind Navigation Buttons
            navButtons[CreatorState.Concept] = root.Q<Button>("Btn_Concept");
            navButtons[CreatorState.Ancestry] = root.Q<Button>("Btn_Ancestry");
            navButtons[CreatorState.Background] = root.Q<Button>("Btn_Background");
            navButtons[CreatorState.Class] = root.Q<Button>("Btn_Class");
            navButtons[CreatorState.FreeBoosts] = root.Q<Button>("Btn_FreeBoosts");
            navButtons[CreatorState.ClassDetails] = root.Q<Button>("Btn_ClassDetails");
            navButtons[CreatorState.Equipment] = root.Q<Button>("Btn_Equipment");
            navButtons[CreatorState.Modifiers] = root.Q<Button>("Btn_Modifiers");
            navButtons[CreatorState.FinishingDetails] = root.Q<Button>("Btn_FinishingDetails");
            navButtons[CreatorState.Visuals] = root.Q<Button>("Btn_Visuals");

            foreach (var kvp in navButtons)
            {
                if (kvp.Value != null)
                {
                    CreatorState capturedState = kvp.Key;
                    kvp.Value.clicked += () => SelectStateFromSidebar(capturedState);
                }
            }
        }

        private void UpdatePreviewAspect()
        {
            if (previewPanel == null || previewTextureArea == null)
                return;

            float panelWidth = previewPanel.resolvedStyle.width;
            float panelHeight = previewPanel.resolvedStyle.height;
            // Pick the largest square that comfortably fits the panel width
            // (minus a small inset) without crowding out the live-stats /
            // finalize chrome below it.
            float maxByWidth = panelWidth > 0 ? panelWidth - 24f : 280f;
            float maxByHeight = panelHeight > 0 ? panelHeight * 0.55f : 280f;
            float size = Mathf.Floor(Mathf.Max(160f, Mathf.Min(maxByWidth, maxByHeight)));

            previewTextureArea.style.width = size;
            previewTextureArea.style.height = size;
        }

        public void ChangeState(CreatorState newState)
        {
            if (contentArea == null)
            {
                return;
            }

            currentState = newState;

            // Update Navigation UI
            foreach (var kvp in navButtons)
            {
                if (kvp.Value != null)
                {
                    if (kvp.Key == newState)
                        kvp.Value.AddToClassList("nav-node--active");
                    else
                        kvp.Value.RemoveFromClassList("nav-node--active");
                }
            }

            // Clear Content Area
            contentArea.Clear();
            activeStepScrollView = null;
            onPageNext = null;
            onPagePrev = null;

            // Load Template from Cache
            VisualTreeAsset templateToLoad = GetTemplateForState(newState);
            if (templateToLoad != null)
            {
                templateToLoad.CloneTree(contentArea);
                InitializeActiveStep(newState);
            }
            else
            {
                // Fallback if template is not yet created
                contentArea.Add(new Label($"UI Template for {newState} is not yet assigned."));
            }

            RebuildControllerFocus();
            OnStateChanged?.Invoke(currentState);
        }

        private VisualTreeAsset GetTemplateForState(CreatorState state)
        {
            switch (state)
            {
                case CreatorState.Concept:
                    return stepConceptTemplate;
                case CreatorState.Ancestry:
                    return stepAncestryTemplate;
                case CreatorState.Background:
                    return stepBackgroundTemplate;
                case CreatorState.Class:
                    return stepClassTemplate;
                case CreatorState.FreeBoosts:
                    return stepFreeBoostsTemplate;
                case CreatorState.ClassDetails:
                    return stepClassDetailsTemplate;
                case CreatorState.Equipment:
                    return stepEquipmentTemplate;
                case CreatorState.Modifiers:
                    return stepModifiersTemplate;
                case CreatorState.FinishingDetails:
                    return stepFinishingDetailsTemplate;
                case CreatorState.Visuals:
                    return stepVisualsTemplate;
            }
            return null;
        }

        private void InitializeActiveStep(CreatorState state)
        {
            switch (state)
            {
                case CreatorState.Concept:
                    InitializeConceptStep();
                    break;
                case CreatorState.Ancestry:
                    InitializeAncestryStep();
                    break;
                case CreatorState.Background:
                    InitializeBackgroundStep();
                    break;
                case CreatorState.Class:
                    InitializeClassStep();
                    break;
                case CreatorState.FreeBoosts:
                    InitializeFreeBoostsStep();
                    break;
                case CreatorState.ClassDetails:
                    InitializeClassDetailsStep();
                    break;
                case CreatorState.Equipment:
                    InitializeEquipmentStep();
                    break;
                case CreatorState.Modifiers:
                    InitializeModifiersStep();
                    break;
                case CreatorState.FinishingDetails:
                    InitializeFinishingDetailsStep();
                    break;
                case CreatorState.Visuals:
                    InitializeVisualsStep();
                    break;
            }
        }

        private static readonly AttributeType[] CreatorAttributes =
        {
            AttributeType.Strength,
            AttributeType.Dexterity,
            AttributeType.Constitution,
            AttributeType.Intelligence,
            AttributeType.Wisdom,
            AttributeType.Charisma,
        };

        private void InitializeConceptStep()
        {
            BindTextField("Txt_Name", payload.Name, value => payload.Name = value);
            BindTextField("Txt_Pronouns", payload.Pronouns, value => payload.Pronouns = value);
            BindTextField("Txt_Identity", payload.Identity, value => payload.Identity = value);
            BindTextField("Txt_Deity", payload.Deity, value => payload.Deity = value);
            BindTextField(
                "Txt_Age",
                payload.Age > 0 ? payload.Age.ToString() : string.Empty,
                value =>
                {
                    if (int.TryParse(value, out int age))
                        payload.Age = age;
                }
            );
            BindTextField(
                "Txt_Edicts",
                string.Join(", ", payload.Edicts),
                value => payload.Edicts = SplitTextList(value)
            );
            BindTextField(
                "Txt_Anathema",
                string.Join(", ", payload.Anathema),
                value => payload.Anathema = SplitTextList(value)
            );
        }

        private void InitializeAncestryStep()
        {
            var gridHost = contentArea.Q<VisualElement>("Grid_Ancestries");
            var searchField = contentArea.Q<TextField>("Search_Ancestry");
            if (gridHost == null)
                return;

            List<AncestryDataSO> source = database?.AllAncestries ?? new List<AncestryDataSO>();
            List<AncestryDataSO> filtered = source.OrderBy(DisplayName).ToList();
            var grid = CreatePagedChoiceGrid(
                gridHost,
                rowCount,
                filtered,
                ancestry => DisplayName(ancestry),
                ancestry => ancestry?.Description,
                ancestry => ancestry != null && ancestry.SourceId == payload.AncestryID,
                SelectAncestry
            );

            onPageNext = () => grid.GoToNextPage();
            onPagePrev = () => grid.GoToPreviousPage();
            grid.OnPageChanged = () => RebuildControllerFocusKeepingPage();

            Action refresh = () =>
            {
                string query = searchField?.value?.ToLowerInvariant() ?? string.Empty;
                filtered = source
                    .Where(item =>
                        item != null
                        && (
                            string.IsNullOrEmpty(query)
                            || DisplayName(item).ToLowerInvariant().Contains(query)
                        )
                    )
                    .OrderBy(DisplayName)
                    .ToList();
                grid.SetItems(filtered);
                grid.TryFocusByPredicate(ancestry =>
                    ancestry != null && ancestry.SourceId == payload.AncestryID
                );
            };

            searchField?.RegisterValueChangedCallback(_ => refresh());

            refresh();
            RefreshAncestryDetails(database?.GetCoreAncestry(payload.AncestryID));
            SyncPreviewEquipmentFromPayload();
        }

        private void SelectAncestry(AncestryDataSO selected)
        {
            payload.AncestryID = selected.SourceId;
            payload.HeritageID = string.Empty;
            payload.AncestryBoosts = DefaultAttributeSelections(selected.AttributeBoosts);
            payload.AncestryFlaws = DefaultAttributeSelections(selected.AttributeFlaws);
            payload.Languages.Clear();

            RefreshAncestryDetails(selected);
            NotifyPayloadUpdated();
            // Preserve focus so confirming a selection doesn't jump back to the top.
            RebuildControllerFocusImpl(preferredFocus: null);
        }

        private void RefreshAncestryDetails(AncestryDataSO ancestry)
        {
            SetLabel(
                "Lbl_AncestryName",
                ancestry != null ? DisplayName(ancestry) : "Select an Ancestry"
            );
            SetLabel(
                "Lbl_AncestryDescription",
                ancestry != null ? ancestry.Description : "Ancestry description will appear here."
            );
            SetLabel("Lbl_AncestryHP", ancestry != null ? ancestry.HitPoints.ToString() : "-");
            SetLabel("Lbl_AncestrySize", ancestry != null ? ancestry.Size.ToString() : "-");
            SetLabel("Lbl_AncestrySpeed", ancestry != null ? $"{ancestry.Speed} ft" : "-");

            RenderAttributeChoices(
                contentArea.Q<VisualElement>("AncestryBoostContainer"),
                "Ancestry Boost",
                ancestry?.AttributeBoosts,
                payload.AncestryBoosts,
                () => NotifyPayloadUpdated()
            );
            RenderAttributeChoices(
                contentArea.Q<VisualElement>("AncestryFlawContainer"),
                "Ancestry Flaw",
                ancestry?.AttributeFlaws,
                payload.AncestryFlaws,
                () => NotifyPayloadUpdated()
            );
            RenderLanguageChoices(ancestry);
            SetLabel(
                "Lbl_AncestryFeatures",
                ToBulletList(
                    ancestry?.GrantedFeatures.Select(feature => feature.DisplayName),
                    "No automatic ancestry features."
                )
            );
            BindHeritageList(ancestry);
        }

        private void BindHeritageList(AncestryDataSO ancestry)
        {
            var gridHost = contentArea.Q<VisualElement>("Grid_Heritages");
            if (gridHost == null)
                return;

            gridHost.Clear();
            List<HeritageDataSO> heritages =
                ancestry != null
                    ? database.GetCoreHeritagesForAncestry(ancestry.SourceId)
                    : new List<HeritageDataSO>();

            foreach (var h in heritages)
            {
                var heritage = h;
                VisualElement card = new VisualElement();
                card.AddToClassList("choice-card");
                if (payload.HeritageID == heritage.SourceId)
                    card.AddToClassList("choice-card--selected");

                Label title = new Label(DisplayName(heritage));
                title.AddToClassList("choice-card__title");
                card.Add(title);

                Label sub = new Label(heritage?.Description ?? string.Empty);
                sub.AddToClassList("choice-card__sub");
                card.Add(sub);

                card.RegisterCallback<ClickEvent>(_ =>
                {
                    payload.HeritageID = heritage.SourceId;
                    NotifyPayloadUpdated();
                    BindHeritageList(ancestry);
                    RebuildControllerFocus();
                });

                gridHost.Add(card);
            }
        }

        private void InitializeBackgroundStep()
        {
            var gridHost = contentArea.Q<VisualElement>("Grid_Backgrounds");
            var searchField = contentArea.Q<TextField>("Search_Background");
            if (gridHost == null)
                return;

            List<BackgroundDataSO> source =
                database?.AllBackgrounds ?? new List<BackgroundDataSO>();
            List<BackgroundDataSO> filtered = source.OrderBy(DisplayName).ToList();
            var grid = CreatePagedChoiceGrid(
                gridHost,
                rowCount,
                filtered,
                background => DisplayName(background),
                background => background?.Description,
                background => background != null && background.SourceId == payload.BackgroundID,
                background =>
                {
                    payload.BackgroundID = background.SourceId;
                    payload.BackgroundBoosts = DefaultAttributeSelections(
                        background.AttributeBoosts
                    );
                    RefreshBackgroundDetails(background);
                    NotifyPayloadUpdated();
                    RebuildControllerFocus();
                }
            );

            onPageNext = () => grid.GoToNextPage();
            onPagePrev = () => grid.GoToPreviousPage();
            grid.OnPageChanged = () => RebuildControllerFocusKeepingPage();
            Action refresh = () =>
            {
                string query = searchField?.value?.ToLowerInvariant() ?? string.Empty;
                filtered = source
                    .Where(item =>
                        item != null
                        && (
                            string.IsNullOrEmpty(query)
                            || DisplayName(item).ToLowerInvariant().Contains(query)
                        )
                    )
                    .OrderBy(DisplayName)
                    .ToList();
                grid.SetItems(filtered);
                grid.TryFocusByPredicate(background =>
                    background != null && background.SourceId == payload.BackgroundID
                );
            };
            searchField?.RegisterValueChangedCallback(_ => refresh());

            refresh();
            RefreshBackgroundDetails(database?.GetCoreBackground(payload.BackgroundID));
        }

        private void RefreshBackgroundDetails(BackgroundDataSO background)
        {
            SetLabel(
                "Lbl_BackgroundName",
                background != null ? DisplayName(background) : "Select a Background"
            );
            SetLabel(
                "Lbl_BackgroundDescription",
                background != null
                    ? background.Description
                    : "Background description will appear here."
            );
            RenderAttributeChoices(
                contentArea.Q<VisualElement>("BackgroundBoostContainer"),
                "Background Boost",
                background?.AttributeBoosts,
                payload.BackgroundBoosts,
                () => NotifyPayloadUpdated()
            );
            SetLabel(
                "Lbl_BackgroundTraining",
                background != null
                    ? $"Training: {string.Join(", ", background.TrainedSkills.Select(FormatSkillTraining))}"
                    : "Training: -"
            );
            SetLabel(
                "Lbl_BackgroundFeats",
                ToBulletList(
                    background?.GrantedFeats.Select(feature => feature.DisplayName),
                    "No granted background feats."
                )
            );
        }

        private void InitializeClassStep()
        {
            var gridHost = contentArea.Q<VisualElement>("Grid_Classes");
            var searchField = contentArea.Q<TextField>("Search_Class");
            if (gridHost == null)
                return;

            List<TacticsClassSO> source = database?.AllClasses ?? new List<TacticsClassSO>();
            List<TacticsClassSO> filtered = source.OrderBy(DisplayName).ToList();
            var grid = CreatePagedChoiceGrid(
                gridHost,
                rowCount,
                filtered,
                characterClass => DisplayName(characterClass),
                characterClass =>
                    characterClass != null
                        ? $"HP/Level {characterClass.HitPointsPerLevel} | {characterClass.Perception}"
                        : string.Empty,
                characterClass =>
                    characterClass != null && characterClass.SourceId == payload.ClassID,
                SelectClass
            );

            onPageNext = () => grid.GoToNextPage();
            onPagePrev = () => grid.GoToPreviousPage();
            grid.OnPageChanged = () => RebuildControllerFocusKeepingPage();
            Action refresh = () =>
            {
                string query = searchField?.value?.ToLowerInvariant() ?? string.Empty;
                filtered = source
                    .Where(item =>
                        item != null
                        && (
                            string.IsNullOrEmpty(query)
                            || DisplayName(item).ToLowerInvariant().Contains(query)
                        )
                    )
                    .OrderBy(DisplayName)
                    .ToList();
                grid.SetItems(filtered);
                grid.TryFocusByPredicate(characterClass =>
                    characterClass != null && characterClass.SourceId == payload.ClassID
                );
            };
            searchField?.RegisterValueChangedCallback(_ => refresh());

            refresh();
            RefreshClassDetails(database?.GetCoreClass(payload.ClassID));
        }

        private void SelectClass(TacticsClassSO selected)
        {
            if (selected == null)
                return;

            payload.ClassID = selected.SourceId;
            payload.ClassKeyAttribute =
                selected.KeyAttributes.Count == 1
                    ? selected.KeyAttributes[0].ToString()
                    : string.Empty;
            payload.TrainedSkills = selected
                .TrainedSkills.Select(skill => skill.Skill.ToString())
                .Where(skill => skill != TacticsSkillType.Custom.ToString())
                .Distinct()
                .ToList();
            RefreshClassDetails(selected);
            NotifyPayloadUpdated();
            // Preserve focus so confirming a class doesn't jump back to the top.
            RebuildControllerFocusImpl(preferredFocus: null);
        }

        private void RefreshClassDetails(TacticsClassSO characterClass)
        {
            SetLabel(
                "Lbl_ClassName",
                characterClass != null ? DisplayName(characterClass) : "Select a Class"
            );
            SetLabel(
                "Lbl_ClassDescription",
                characterClass != null
                    ? characterClass.Description
                    : "Class description will appear here."
            );
            RenderClassKeyAttributes(characterClass);
            SetLabel(
                "Lbl_ClassStats",
                characterClass != null
                    ? $"HP/Level: {characterClass.HitPointsPerLevel}\nPerception: {characterClass.Perception}\nClass DC: {characterClass.ClassDifficulty}\nSpellcasting: {(characterClass.HasSpellcasting ? "Yes" : "No")}"
                    : "-"
            );
            SetLabel(
                "Lbl_ClassFeatures",
                ToBulletList(
                    characterClass?.LevelOneFeatures.Select(feature => feature.DisplayName),
                    "No level 1 class features listed."
                )
            );
        }

        private void InitializeFreeBoostsStep()
        {
            EnsureListSize(payload.FreeBoosts, 4);
            RenderAttributeChoices(
                contentArea.Q<VisualElement>("FreeBoostContainer"),
                "Free Boost",
                Enumerable.Repeat(new AttributeChoiceSet(), 4).ToList(),
                payload.FreeBoosts,
                () =>
                {
                    // Enforce uniqueness without replacing the list instance (which breaks UI state).
                    for (int i = 0; i < payload.FreeBoosts.Count; i++)
                    {
                        if (string.IsNullOrEmpty(payload.FreeBoosts[i]))
                            continue;
                        for (int j = i + 1; j < payload.FreeBoosts.Count; j++)
                        {
                            if (payload.FreeBoosts[i] == payload.FreeBoosts[j])
                                payload.FreeBoosts[j] = string.Empty;
                        }
                    }
                    RefreshFreeBoostSummary();
                    NotifyPayloadUpdated();
                }
            );
            RefreshFreeBoostSummary();
        }

        private void RefreshFreeBoostSummary()
        {
            SetLabel(
                "Lbl_FreeBoostSummary",
                $"Selected: {string.Join(", ", payload.FreeBoosts.Where(value => !string.IsNullOrEmpty(value)))}"
            );
        }

        private void InitializeClassDetailsStep()
        {
            TacticsClassSO characterClass = database?.GetCoreClass(payload.ClassID);
            SetLabel(
                "Lbl_ClassDetailsHeader",
                characterClass != null
                    ? $"{DisplayName(characterClass)} details"
                    : "Choose a class first, then return here for class details."
            );
            RenderSkillChoices(characterClass);
            RenderSpellChoices(characterClass);
            SetLabel(
                "Lbl_ClassProficiencies",
                characterClass != null
                    ? $"Saves: {string.Join(", ", characterClass.SavingThrows.Select(save => $"{save.Save} {save.Rank}"))}\nArmor: {string.Join(", ", characterClass.ArmorProficiencies.Select(armor => $"{armor.Group} {armor.Rank}"))}\nWeapons: {string.Join(", ", characterClass.WeaponProficiencies.Select(weapon => $"{weapon.Group} {weapon.Rank}"))}"
                    : "-"
            );
            SetLabel(
                "Lbl_ClassLevelOneFeatures",
                ToBulletList(
                    characterClass?.LevelOneFeatures.Select(feature => feature.DisplayName),
                    "No level 1 class features listed."
                )
            );
        }

        private void InitializeEquipmentStep()
        {
            btnEquipmentMainTab = contentArea.Q<Button>("Btn_TabMain");
            btnEquipmentOffTab = contentArea.Q<Button>("Btn_TabOff");
            btnEquipmentArmorTab = contentArea.Q<Button>("Btn_TabArmor");
            lblLoadoutMain = contentArea.Q<Label>("Lbl_LoadoutMain");
            lblLoadoutOff = contentArea.Q<Label>("Lbl_LoadoutOff");
            lblLoadoutArmor = contentArea.Q<Label>("Lbl_LoadoutArmor");

            btnEquipmentMainTab?.RegisterCallback<ClickEvent>(_ =>
                SelectEquipmentTab(EquipmentSlotTab.MainHand)
            );
            btnEquipmentOffTab?.RegisterCallback<ClickEvent>(_ =>
                SelectEquipmentTab(EquipmentSlotTab.OffHand)
            );
            btnEquipmentArmorTab?.RegisterCallback<ClickEvent>(_ =>
                SelectEquipmentTab(EquipmentSlotTab.Armor)
            );

            SelectEquipmentTab(activeEquipmentTab);
            SyncPreviewEquipmentFromPayload();
            RefreshEquipmentSummary();
        }

        private void InitializeModifiersStep()
        {
            rulesSummary = CharacterCreationRules.BuildSummary(payload, database);
            SetLabel(
                "Lbl_ModifierAttributes",
                string.Join(
                    "\n",
                    CreatorAttributes.Select(attribute =>
                        $"{attribute}: {rulesSummary.GetAttributeModifier(attribute):+0;-0;0}"
                    )
                )
            );
            SetLabel(
                "Lbl_ModifierDefenses",
                $"HP: {rulesSummary.HitPoints}\nSpeed: {rulesSummary.Speed} ft\nClass DC: {rulesSummary.ClassDC}\nSpell DC: {rulesSummary.SpellDC}\nFocus Points: {rulesSummary.FocusPoints}"
            );
            SetLabel(
                "Lbl_ModifierSkills",
                string.Join(
                    "    ",
                    Enum.GetValues(typeof(TacticsSkillType))
                        .Cast<TacticsSkillType>()
                        .Where(skill => skill != TacticsSkillType.Custom)
                        .Select(skill => $"{skill}: {rulesSummary.GetSkillProficiency(skill)}")
                )
            );
        }

        private void SelectEquipmentTab(EquipmentSlotTab tab)
        {
            activeEquipmentTab = tab;
            SetEquipmentTabClass(btnEquipmentMainTab, tab == EquipmentSlotTab.MainHand);
            SetEquipmentTabClass(btnEquipmentOffTab, tab == EquipmentSlotTab.OffHand);
            SetEquipmentTabClass(btnEquipmentArmorTab, tab == EquipmentSlotTab.Armor);
            RenderEquipmentGrid();
            RebuildControllerFocus();
        }

        private void SetEquipmentTabClass(Button button, bool active)
        {
            if (button == null)
                return;

            if (active)
                button.AddToClassList("chip--active");
            else
                button.RemoveFromClassList("chip--active");
        }

        private void RenderEquipmentGrid()
        {
            VisualElement gridHost = contentArea.Q<VisualElement>("Grid_Equipment");
            if (gridHost == null)
                return;

            List<EquipmentSO> options = GetActiveEquipmentOptions();
            var grid = CreatePagedChoiceGrid(
                gridHost,
                rowCount,
                options,
                ItemName,
                FormatEquipmentSubText,
                equipment => ItemId(equipment) == GetActiveEquipmentId(),
                SelectEquipment
            );

            onPageNext = () => grid.GoToNextPage();
            onPagePrev = () => grid.GoToPreviousPage();
            grid.OnPageChanged = () => RebuildControllerFocusKeepingPage();
            grid.TryFocusByPredicate(equipment => ItemId(equipment) == GetActiveEquipmentId());
        }

        private List<EquipmentSO> GetActiveEquipmentOptions()
        {
            switch (activeEquipmentTab)
            {
                case EquipmentSlotTab.MainHand:
                    List<EquipmentSO> mainOptions =
                        database
                            ?.AllWeapons?.Where(item => item != null && item.level <= 1)
                            .OrderBy(ItemName)
                            .Cast<EquipmentSO>()
                            .ToList() ?? new List<EquipmentSO>();
                    mainOptions.Insert(0, null);
                    return mainOptions;
                case EquipmentSlotTab.OffHand:
                    return BuildOffHandOptions(FindWeapon(payload.MainHandWeaponID));
                case EquipmentSlotTab.Armor:
                    List<EquipmentSO> armorOptions =
                        database
                            ?.AllArmor?.Where(item => item != null && item.level <= 1)
                            .OrderBy(ItemName)
                            .Cast<EquipmentSO>()
                            .ToList() ?? new List<EquipmentSO>();
                    armorOptions.Insert(0, null);
                    return armorOptions;
                default:
                    return new List<EquipmentSO>();
            }
        }

        private string GetActiveEquipmentId()
        {
            switch (activeEquipmentTab)
            {
                case EquipmentSlotTab.MainHand:
                    return payload.MainHandWeaponID;
                case EquipmentSlotTab.OffHand:
                    return payload.OffHandEquipmentID;
                case EquipmentSlotTab.Armor:
                    return payload.ArmorID;
                default:
                    return string.Empty;
            }
        }

        private void SelectEquipment(EquipmentSO equipment)
        {
            switch (activeEquipmentTab)
            {
                case EquipmentSlotTab.MainHand:
                    payload.MainHandWeaponID = ItemId(equipment);
                    EquipPreviewMainHand(equipment as WeaponSO);
                    RefreshOffHandOptions();
                    break;
                case EquipmentSlotTab.OffHand:
                    payload.OffHandEquipmentID = ItemId(equipment);
                    EquipPreviewOffHand(equipment);
                    break;
                case EquipmentSlotTab.Armor:
                    payload.ArmorID = ItemId(equipment);
                    ResolvePreviewEquipment()?.EquipArmor(equipment as ArmorSO);
                    break;
            }

            RefreshEquipmentSummary();
            NotifyPayloadUpdated();
            RenderEquipmentGrid();
            RebuildControllerFocus();
        }

        private static string FormatEquipmentSubText(EquipmentSO equipment)
        {
            if (equipment == null)
                return "No item equipped";

            string description = string.IsNullOrWhiteSpace(equipment.description)
                ? "Level " + equipment.level
                : equipment.description;
            return description.Length > 80 ? description.Substring(0, 77) + "..." : description;
        }

        private void RefreshOffHandOptions()
        {
            WeaponSO mainWeapon = FindWeapon(payload.MainHandWeaponID);
            List<EquipmentSO> options = BuildOffHandOptions(mainWeapon);

            if (
                !string.IsNullOrEmpty(payload.OffHandEquipmentID)
                && !options.Any(item => ItemId(item) == payload.OffHandEquipmentID)
            )
            {
                payload.OffHandEquipmentID = string.Empty;
                EquipPreviewOffHand(null);
            }

            if (activeEquipmentTab == EquipmentSlotTab.OffHand)
                RenderEquipmentGrid();
        }

        private List<EquipmentSO> BuildOffHandOptions(WeaponSO mainWeapon)
        {
            var options = new List<EquipmentSO> { null };

            if (mainWeapon != null && mainWeapon.hands == HandsRequired.Two)
                return options;

            IEnumerable<WeaponSO> oneHandedWeapons =
                database?.AllWeapons?.Where(weapon =>
                    weapon != null && weapon.level <= 1 && weapon.hands == HandsRequired.One
                ) ?? Enumerable.Empty<WeaponSO>();
            IEnumerable<ShieldSO> shields =
                database?.AllShields?.Where(shield => shield != null && shield.level <= 1)
                ?? Enumerable.Empty<ShieldSO>();

            options.AddRange(oneHandedWeapons.Cast<EquipmentSO>());
            options.AddRange(shields.Cast<EquipmentSO>());
            return options.OrderBy(ItemName).ToList();
        }

        private void SyncPreviewEquipmentFromPayload()
        {
            UnitEquipment equipment = ResolvePreviewEquipment();
            if (equipment == null)
            {
                return;
            }

            WeaponSO mainWeapon = FindWeapon(payload.MainHandWeaponID);
            EquipmentSO offHand = FindOffHandEquipment(payload.OffHandEquipmentID);
            ArmorSO armor = FindArmor(payload.ArmorID);

            equipment.EquipMainHand(mainWeapon);
            equipment.EquipOffHand(offHand);
            equipment.EquipArmor(armor);
        }

        private void EquipPreviewMainHand(WeaponSO weapon)
        {
            UnitEquipment equipment = ResolvePreviewEquipment();
            if (equipment == null)
                return;

            equipment.EquipMainHand(weapon);
            if (weapon != null && weapon.hands == HandsRequired.Two)
            {
                payload.OffHandEquipmentID = string.Empty;
                equipment.EquipOffHand(null);
            }
        }

        private void EquipPreviewOffHand(EquipmentSO equipment)
        {
            ResolvePreviewEquipment()?.EquipOffHand(equipment);
        }

        private UnitEquipment ResolvePreviewEquipment()
        {
            // Standard Unity null check safely handles "destroyed" objects.
            // Accessing .gameObject on a destroyed object throws MissingReferenceException.
            if (previewEquipment == null)
                previewEquipment = null;

            if (previewEquipment != null)
                return previewEquipment;

            if (visualManager != null)
            {
                // Try finding it directly on the manager or its children
                if (previewEquipment == null)
                    previewEquipment = visualManager.GetComponentInChildren<UnitEquipment>();
            }

            return previewEquipment;
        }

        private WeaponSO FindWeapon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            return database?.AllWeapons?.FirstOrDefault(weapon => ItemId(weapon) == itemId);
        }

        private ArmorSO FindArmor(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            return database?.AllArmor?.FirstOrDefault(armor => ItemId(armor) == itemId);
        }

        private EquipmentSO FindOffHandEquipment(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            IEnumerable<EquipmentSO> weapons =
                database?.AllWeapons?.Cast<EquipmentSO>() ?? Enumerable.Empty<EquipmentSO>();
            IEnumerable<EquipmentSO> shields =
                database?.AllShields?.Cast<EquipmentSO>() ?? Enumerable.Empty<EquipmentSO>();

            return weapons.Concat(shields).FirstOrDefault(item => ItemId(item) == itemId);
        }

        private void InitializeFinishingDetailsStep()
        {
            rulesSummary = CharacterCreationRules.BuildSummary(payload, database);
            SetLabel(
                "Lbl_FinalSummary",
                $"{payload.Name}\n{DisplayName(database?.GetCoreAncestry(payload.AncestryID))} {DisplayName(database?.GetCoreClass(payload.ClassID))}\nHP {rulesSummary.HitPoints}, Speed {rulesSummary.Speed} ft"
            );
            SetLabel("Lbl_FinalValidation", string.Join("\n", GetValidationMessages()));
            Button finalizeButton = contentArea.Q<Button>("Btn_FinalizeFromStep");
            if (finalizeButton != null)
                finalizeButton.clicked += FinalizeCharacter;
        }

        private void BindTextField(string name, string value, Action<string> setter)
        {
            TextField field = contentArea.Q<TextField>(name);
            if (field == null)
                return;

            field.value = value ?? string.Empty;
            field.RegisterValueChangedCallback(evt =>
            {
                setter?.Invoke(evt.newValue);
                NotifyPayloadUpdated();
            });
        }

        private PagedCardGrid<T> CreatePagedChoiceGrid<T>(
            VisualElement host,
            int pageSize,
            List<T> items,
            Func<T, string> titleSelector,
            Func<T, string> subtitleSelector,
            Func<T, bool> selectedPredicate,
            Action<T> onSelected
        )
        {
            var grid = new PagedCardGrid<T>(
                host,
                pageSize,
                () =>
                {
                    VisualElement card = new VisualElement();
                    card.AddToClassList("choice-card");

                    Label title = new Label { name = "Title" };
                    title.AddToClassList("choice-card__title");
                    card.Add(title);

                    Label subtitle = new Label { name = "Description" };
                    subtitle.AddToClassList("choice-card__sub");
                    card.Add(subtitle);

                    return card;
                },
                (card, context) =>
                {
                    // Use ONLY PagedCardGrid's internal selectedIndex for the selected visual.
                    // The selectedPredicate is used during SetItems/TryFocusByPredicate to
                    // determine which item is pre-selected; once the grid is set up it tracks
                    // selection internally. Checking both causes a double-highlight.
                    bool selected = context.IsSelected;
                    Label title = card.Q<Label>("Title");
                    Label subtitle = card.Q<Label>("Description");
                    if (title != null)
                        title.text = titleSelector?.Invoke(context.Item) ?? "-";
                    if (subtitle != null)
                        subtitle.text = subtitleSelector?.Invoke(context.Item) ?? string.Empty;

                    if (selected)
                        card.AddToClassList("choice-card--selected");
                    else
                        card.RemoveFromClassList("choice-card--selected");
                }
            );

            grid.OnSelectionChanged += (_, selectedItem) => onSelected?.Invoke(selectedItem);
            grid.SetSingleColumn(true);
            grid.SetItems(items ?? new List<T>());
            return grid;
        }

        private static List<string> SplitTextList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrEmpty(item))
                .Distinct()
                .ToList();
        }

        private void BindElementList<T>(
            ListView listView,
            List<T> items,
            Func<T, string> displayName
        )
        {
            if (listView == null)
                return;

            listView.itemsSource = items;
            listView.makeItem = () =>
            {
                var label = new Label();
                label.AddToClassList("list-item");
                return label;
            };
            listView.bindItem = (element, index) =>
            {
                if (element is Label label && index >= 0 && index < items.Count)
                    label.text = displayName(items[index]);
            };
            listView.RefreshItems();
        }

        private void BindEquipmentList<T>(
            ListView listView,
            List<T> items,
            string selectedId,
            Action<T> onSelected
        )
            where T : ItemSO
        {
            if (listView == null)
                return;

            if (items == null)
                items = new List<T>();
            items = items.OrderBy(ItemName).ToList();
            BindElementList(listView, items, ItemName);
            listView.selectionChanged += selectedItems =>
            {
                T selected = selectedItems?.OfType<T>().FirstOrDefault();
                if (selected != null)
                    onSelected?.Invoke(selected);
            };

            int selectedIndex = items.FindIndex(item => ItemId(item) == selectedId);
            if (selectedIndex >= 0)
                listView.selectedIndex = selectedIndex;
        }

        private void BindOffHandEquipmentList(List<EquipmentSO> items, string selectedId)
        {
            if (equipmentOffHandList == null)
                return;

            if (items == null)
                items = new List<EquipmentSO>();
            equipmentOffHandList.itemsSource = items;
            equipmentOffHandList.makeItem = () =>
            {
                var label = new Label();
                label.AddToClassList("list-item");
                return label;
            };
            equipmentOffHandList.bindItem = (element, index) =>
            {
                if (element is Label label && index >= 0 && index < items.Count)
                    label.text = ItemName(items[index]);
            };
            equipmentOffHandList.selectionChanged -= HandleOffHandSelectionChanged;
            equipmentOffHandList.selectionChanged += HandleOffHandSelectionChanged;
            equipmentOffHandList.RefreshItems();

            int selectedIndex = items.FindIndex(item => ItemId(item) == selectedId);
            if (selectedIndex < 0)
                selectedIndex = 0;
            equipmentOffHandList.selectedIndex = selectedIndex;
        }

        private void HandleOffHandSelectionChanged(IEnumerable<object> selectedItems)
        {
            if (equipmentOffHandList == null)
                return;

            EquipmentSO selected = equipmentOffHandList.selectedItem as EquipmentSO;
            payload.OffHandEquipmentID = ItemId(selected);
            EquipPreviewOffHand(selected);
            RefreshEquipmentSummary();
            NotifyPayloadUpdated();
        }

        private void SelectBySourceId<T>(ListView listView, List<T> items, string sourceId)
            where T : TacticsGameElementSO
        {
            if (listView == null || string.IsNullOrEmpty(sourceId))
                return;

            int selectedIndex = items.FindIndex(item => item != null && item.SourceId == sourceId);
            if (selectedIndex >= 0)
                listView.selectedIndex = selectedIndex;
        }

        private void RenderClassKeyAttributes(TacticsClassSO characterClass)
        {
            VisualElement container = contentArea.Q<VisualElement>("ClassKeyAttributeContainer");
            if (container == null)
                return;

            container.Clear();
            if (characterClass == null || characterClass.KeyAttributes.Count == 0)
                return;

            int keyAttrCol = 0;
            foreach (AttributeType attribute in characterClass.KeyAttributes)
            {
                AttributeType capturedAttr = attribute;
                int capturedCol = keyAttrCol++;
                Button button = new Button
                {
                    text = capturedAttr.ToString(),
                    userData = new Vector2Int(40, capturedCol),
                };
                button.RegisterCallback<ClickEvent>(_ =>
                {
                    payload.ClassKeyAttribute = capturedAttr.ToString();
                    NotifyPayloadUpdated();
                    RenderClassKeyAttributes(characterClass);
                    RebuildControllerFocus();
                });
                if (payload.ClassKeyAttribute == capturedAttr.ToString())
                    button.AddToClassList("chip--active");
                button.AddToClassList("chip");
                container.Add(button);
            }
        }

        private void RenderAttributeChoices(
            VisualElement container,
            string title,
            IReadOnlyList<AttributeChoiceSet> choices,
            List<string> selections,
            Action onChanged
        )
        {
            if (container == null)
                return;

            container.Clear();
            if (choices == null || choices.Count == 0)
                return;

            EnsureListSize(selections, choices.Count);

            // Collect all attributes that are fixed in this set (non-free choices with only 1 option)
            var fixedAttrs = new HashSet<string>();
            foreach (var c in choices)
            {
                if (!c.IsFreeChoice && c.Options.Count == 1)
                    fixedAttrs.Add(c.Options[0].ToString());
            }

            for (int i = 0; i < choices.Count; i++)
            {
                int choiceIndex = i;
                AttributeChoiceSet choice = choices[i];
                var row = new VisualElement { name = $"{title}_{i}" };
                row.AddToClassList("attribute-choice-row");

                Label rowLabel = new Label($"{title} {i + 1}");
                rowLabel.AddToClassList("attribute-choice-label");
                row.Add(rowLabel);

                var chipWrap = new VisualElement();
                chipWrap.AddToClassList("attribute-choice-row__chips");
                row.Add(chipWrap);

                IEnumerable<AttributeType> options = choice.IsFreeChoice
                    ? CreatorAttributes
                    : choice.Options.Where(option => option != AttributeType.Any);
                int colIndex = 0;
                foreach (AttributeType attribute in options)
                {
                    int capturedCol = colIndex++;
                    AttributeType capturedAttribute = attribute;
                    string attrName = capturedAttribute.ToString();

                    // If this attribute is already selected in another row of this same step,
                    // we cannot select it in a flexible row.
                    bool isRequiredElsewhere = fixedAttrs.Contains(attrName) && choice.IsFreeChoice;

                    Button button = new Button
                    {
                        text = capturedAttribute.ToString(),
                        userData = new Vector2Int(choiceIndex, capturedCol),
                    };
                    button.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (isRequiredElsewhere)
                            return;

                        string currentAttrName = capturedAttribute.ToString();

                        // PF2e Rule: multiple boosts within a single step must go to different ability scores.
                        // If this attribute is already selected in another row of THIS step/set, clear that row.
                        for (int j = 0; j < selections.Count; j++)
                        {
                            if (j != choiceIndex && selections[j] == currentAttrName)
                            {
                                selections[j] = string.Empty;
                            }
                        }

                        selections[choiceIndex] = currentAttrName;
                        onChanged?.Invoke();
                        RenderAttributeChoices(container, title, choices, selections, onChanged);
                        RebuildControllerFocus();
                    });
                    button.AddToClassList("chip");
                    button.AddToClassList("attr-chip");

                    if (isRequiredElsewhere)
                    {
                        button.SetEnabled(false);
                        button.AddToClassList("chip--disabled");
                    }

                    if (selections[choiceIndex] == attrName)
                        button.AddToClassList("chip--active");
                    chipWrap.Add(button);
                }
                container.Add(row);
            }
        }

        private void RenderLanguageChoices(AncestryDataSO ancestry)
        {
            VisualElement container = contentArea.Q<VisualElement>("AncestryLanguageContainer");
            if (container == null)
                return;

            container.Clear();
            if (ancestry == null)
                return;

            container.Add(
                new Label(
                    $"Starting: {string.Join(", ", ancestry.StartingLanguages.Select(language => language.DisplayName))}"
                )
            );
            if (ancestry.AdditionalLanguageCount <= 0)
                return;

            Label chooseLabel = new Label($"Choose {ancestry.AdditionalLanguageCount} additional:");
            chooseLabel.AddToClassList("chip-group__label");
            container.Add(chooseLabel);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            int langCol = 0;
            foreach (LanguageEntry language in ancestry.AdditionalLanguageOptions)
            {
                string capturedLangName = language.DisplayName;
                int capturedCol = langCol++;
                Button button = new Button
                {
                    text = capturedLangName,
                    userData = new Vector2Int(10, capturedCol),
                };
                button.RegisterCallback<ClickEvent>(_ =>
                {
                    if (payload.Languages.Contains(capturedLangName))
                        payload.Languages.Remove(capturedLangName);
                    else if (payload.Languages.Count < ancestry.AdditionalLanguageCount)
                        payload.Languages.Add(capturedLangName);
                    RenderLanguageChoices(ancestry);
                    NotifyPayloadUpdated();
                    RebuildControllerFocus();
                });
                button.AddToClassList("chip");
                if (payload.Languages.Contains(capturedLangName))
                    button.AddToClassList("chip--active");
                row.Add(button);
            }
            container.Add(row);
        }

        private void RenderSkillChoices(TacticsClassSO characterClass)
        {
            VisualElement container = contentArea.Q<VisualElement>("ClassSkillContainer");
            if (container == null)
                return;

            container.Clear();
            if (characterClass == null)
                return;

            int selectedAdditional = payload
                .TrainedSkills.Except(
                    characterClass.TrainedSkills.Select(skill => skill.Skill.ToString())
                )
                .Count();
            container.Add(
                new Label(
                    $"Choose {characterClass.AdditionalSkillCount} additional trained skills. Selected additional: {selectedAdditional}"
                )
            );

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            int colIndex = 0;
            foreach (
                TacticsSkillType skill in Enum.GetValues(typeof(TacticsSkillType))
                    .Cast<TacticsSkillType>()
                    .Where(skill =>
                        skill != TacticsSkillType.Custom && skill != TacticsSkillType.Lore
                    )
            )
            {
                string capturedSkillName = skill.ToString();
                bool capturedClassGranted = characterClass.TrainedSkills.Any(entry =>
                    entry.Skill == skill
                );
                int capturedCol = colIndex++;

                Button button = new Button
                {
                    text = capturedClassGranted
                        ? $"{capturedSkillName} (class)"
                        : capturedSkillName,
                    userData = new Vector2Int(20, capturedCol),
                };
                button.RegisterCallback<ClickEvent>(_ =>
                {
                    if (capturedClassGranted)
                        return;

                    if (payload.TrainedSkills.Contains(capturedSkillName))
                        payload.TrainedSkills.Remove(capturedSkillName);
                    else
                    {
                        int currentAdditional = payload
                            .TrainedSkills.Except(
                                characterClass.TrainedSkills.Select(entry => entry.Skill.ToString())
                            )
                            .Count();
                        if (currentAdditional < characterClass.AdditionalSkillCount)
                            payload.TrainedSkills.Add(capturedSkillName);
                    }
                    RenderSkillChoices(characterClass);
                    NotifyPayloadUpdated();
                    RebuildControllerFocus();
                });
                button.AddToClassList("chip");
                if (payload.TrainedSkills.Contains(capturedSkillName) || capturedClassGranted)
                    button.AddToClassList("chip--active");
                row.Add(button);
            }
            container.Add(row);
        }

        private void RenderSpellChoices(TacticsClassSO characterClass)
        {
            VisualElement container = contentArea.Q<VisualElement>("SpellChoiceContainer");
            if (container == null)
                return;

            container.Clear();
            if (characterClass == null || !characterClass.HasSpellcasting)
            {
                container.Add(new Label("This class does not start with spellcasting."));
                return;
            }

            List<SpellSO> spells =
                database
                    ?.AllSpells?.Where(spell => spell != null && spell.Level <= 1)
                    .OrderBy(spell => spell.ElementName)
                    .Take(24)
                    .ToList() ?? new List<SpellSO>();
            container.Add(new Label("Curated working spell picks:"));
            int colIndex = 0;
            foreach (SpellSO spell in spells)
            {
                string capturedSpellId = !string.IsNullOrEmpty(spell.Id) ? spell.Id : spell.name;
                string capturedSpellName = spell.ElementName;
                int capturedSpellLevel = spell.Level;
                int capturedCol = colIndex++;

                Button button = new Button
                {
                    text = capturedSpellName,
                    userData = new Vector2Int(30, capturedCol),
                };
                button.RegisterCallback<ClickEvent>(_ =>
                {
                    if (payload.SpellIDs.Contains(capturedSpellId))
                    {
                        payload.SpellIDs.Remove(capturedSpellId);
                        payload.SpellLedger.RemoveAll(selection =>
                            selection.SpellID == capturedSpellId
                        );
                    }
                    else
                    {
                        payload.SpellIDs.Add(capturedSpellId);
                        payload.SpellLedger.Add(
                            new SpellSelection
                            {
                                SpellID = capturedSpellId,
                                Rank = capturedSpellLevel,
                                Tradition = SpellTradition.Focus,
                                SlotType =
                                    capturedSpellLevel == 0
                                        ? SpellSlotType.Cantrip
                                        : SpellSlotType.Prepared,
                                SourceID = payload.ClassID,
                                Level = 1,
                            }
                        );
                    }
                    RenderSpellChoices(characterClass);
                    NotifyPayloadUpdated();
                    RebuildControllerFocus();
                });
                button.AddToClassList("chip");
                if (payload.SpellIDs.Contains(capturedSpellId))
                    button.AddToClassList("chip--active");
                container.Add(button);
            }
        }

        private void RefreshEquipmentSummary()
        {
            string main = string.IsNullOrEmpty(payload.MainHandWeaponID)
                ? "-"
                : payload.MainHandWeaponID;
            string off = string.IsNullOrEmpty(payload.OffHandEquipmentID)
                ? "-"
                : payload.OffHandEquipmentID;
            string armor = string.IsNullOrEmpty(payload.ArmorID) ? "-" : payload.ArmorID;

            SetLabel("Lbl_LoadoutMain", main);
            SetLabel("Lbl_LoadoutOff", off);
            SetLabel("Lbl_LoadoutArmor", armor);
            SetLabel("Lbl_EquipmentSummary", $"Main Hand: {main}\nOff Hand: {off}\nArmor: {armor}");
        }

        private List<string> GetValidationMessages()
        {
            var messages = new List<string>();
            if (string.IsNullOrWhiteSpace(payload.Name))
                messages.Add("Missing character name.");
            if (string.IsNullOrEmpty(payload.AncestryID))
                messages.Add("Missing ancestry.");
            if (string.IsNullOrEmpty(payload.HeritageID))
                messages.Add("Missing heritage.");
            if (string.IsNullOrEmpty(payload.BackgroundID))
                messages.Add("Missing background.");
            if (string.IsNullOrEmpty(payload.ClassID))
                messages.Add("Missing class.");
            if (payload.FreeBoosts.Count(value => !string.IsNullOrEmpty(value)) < 4)
                messages.Add("Choose four free attribute boosts.");
            if (messages.Count == 0)
                messages.Add("Ready to finalize.");
            return messages;
        }

        private static List<string> DefaultAttributeSelections(
            IReadOnlyList<AttributeChoiceSet> choices
        )
        {
            var selections = new List<string>();
            if (choices == null)
                return selections;

            foreach (AttributeChoiceSet choice in choices)
            {
                AttributeType selected =
                    !choice.IsFreeChoice && choice.Options.Count == 1
                        ? choice.Options[0]
                        : AttributeType.Any;
                selections.Add(selected == AttributeType.Any ? string.Empty : selected.ToString());
            }
            return selections;
        }

        private static void EnsureListSize(List<string> list, int size)
        {
            while (list.Count < size)
                list.Add(string.Empty);
            while (list.Count > size)
                list.RemoveAt(list.Count - 1);
        }

        private void SetLabel(string name, string text)
        {
            Label label = contentArea.Q<Label>(name);
            if (label != null)
                label.text = text ?? string.Empty;
        }

        private static string DisplayName(TacticsGameElementSO element)
        {
            if (element == null)
                return "-";
            if (!string.IsNullOrEmpty(element.DisplayName))
                return element.DisplayName;
            if (!string.IsNullOrEmpty(element.Slug))
                return element.Slug;
            return element.name;
        }

        private static string ItemName(ItemSO item) =>
            item == null ? "-"
            : !string.IsNullOrEmpty(item.itemName) ? item.itemName
            : item.name;

        private static string ItemId(ItemSO item) =>
            item == null ? string.Empty
            : !string.IsNullOrEmpty(item.itemName) ? item.itemName
            : item.name;

        private static string FormatSkillTraining(SkillTrainingEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.LoreName))
                return $"{entry.LoreName} Lore";
            return $"{entry.Skill} {entry.Rank}";
        }

        private static string ToBulletList(IEnumerable<string> values, string emptyText)
        {
            List<string> lines =
                values?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList()
                ?? new List<string>();
            if (lines.Count == 0)
                return emptyText;
            return string.Join("\n", lines.Select(value => $"- {value}"));
        }

        private void InitializeVisualsStep()
        {
            var carouselContainer = contentArea.Q<VisualElement>("VisualsGridHost");

            if (carouselContainer == null || database == null || carouselSelectorTemplate == null)
            {
                return;
            }

            // Clear previous
            carouselContainer.Clear();
            focusableRows.Clear();
            carouselLeftActions.Clear();
            carouselRightActions.Clear();

            // Render categories
            var slots = Enum.GetValues(typeof(TacticsGame.Characters.Visuals.VisualSlot));

            foreach (TacticsGame.Characters.Visuals.VisualSlot slot in slots)
            {
                var parts = database.GetVisualsForSlot(slot);

                var rowTree = carouselSelectorTemplate.CloneTree();
                var carouselRoot = rowTree.Q<VisualElement>("CarouselRoot");
                if (carouselRoot == null)
                {
                    continue;
                }
                carouselRoot.RemoveFromHierarchy();

                var lblCategory = carouselRoot.Q<Label>("Lbl_Category");
                var lblValue = carouselRoot.Q<Label>("Lbl_Value");
                var btnLeft = carouselRoot.Q<Button>("Btn_Left");
                var btnRight = carouselRoot.Q<Button>("Btn_Right");

                lblCategory.text = slot.ToString();

                if (parts.Count == 0)
                {
                    lblValue.text = "None";
                    btnLeft.SetEnabled(false);
                    btnRight.SetEnabled(false);
                }
                else
                {
                    // Find current index based on payload
                    int currentIndex = 0;
                    if (payload.VisualPartIDs.TryGetValue(slot.ToString(), out string equippedId))
                    {
                        int foundIndex = parts.FindIndex(p => p.PartID == equippedId);
                        if (foundIndex >= 0)
                            currentIndex = foundIndex;
                    }
                    else
                    {
                        // Default to the first part and equip it if nothing is selected
                        payload.VisualPartIDs[slot.ToString()] = parts[0].PartID;
                        if (visualManager != null)
                            visualManager.EquipPart(parts[0]);
                    }

                    Action updateRow = () =>
                    {
                        string dispName = string.IsNullOrEmpty(parts[currentIndex].DisplayName)
                            ? parts[currentIndex].name
                            : parts[currentIndex].DisplayName;
                        lblValue.text = dispName;
                    };

                    Action selectPreviousPart = () =>
                    {
                        currentIndex--;
                        if (currentIndex < 0)
                            currentIndex = parts.Count - 1; // Wrap around
                        updateRow();
                        payload.VisualPartIDs[slot.ToString()] = parts[currentIndex].PartID;
                        if (visualManager != null)
                            visualManager.EquipPart(parts[currentIndex]);
                        NotifyPayloadUpdated();
                    };

                    Action selectNextPart = () =>
                    {
                        currentIndex++;
                        if (currentIndex >= parts.Count)
                            currentIndex = 0; // Wrap around
                        updateRow();
                        payload.VisualPartIDs[slot.ToString()] = parts[currentIndex].PartID;
                        if (visualManager != null)
                            visualManager.EquipPart(parts[currentIndex]);
                        NotifyPayloadUpdated();
                    };

                    btnLeft.clicked += selectPreviousPart;
                    btnRight.clicked += selectNextPart;

                    updateRow();

                    carouselLeftActions[carouselRoot] = selectPreviousPart;
                    carouselRightActions[carouselRoot] = selectNextPart;
                }

                carouselContainer.Add(carouselRoot);
                focusableRows.Add(carouselRoot);
            }

            WrapVisualRowsInPages(carouselContainer);
            SyncPreviewEquipmentFromPayload();
        }

        private void WrapVisualRowsInPages(VisualElement carouselContainer)
        {
            if (carouselContainer == null || focusableRows.Count == 0)
                return;

            List<VisualElement> rows = focusableRows.ToList();
            carouselContainer.Clear();
            focusableRows.Clear();

            int pageSize = Mathf.Max(1, visualRowCount);
            int pageIndex = 0;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(rows.Count / (float)pageSize));

            VisualElement page = new VisualElement { name = "VisualsPage" };
            page.AddToClassList("paged-grid__page");
            carouselContainer.Add(page);

            VisualElement pager = new VisualElement { name = "VisualsPager" };
            pager.AddToClassList("paged-grid__pager");

            Button previous = new Button { text = "<" };
            previous.AddToClassList("pager-btn");
            pager.Add(previous);

            Label pageLabel = new Label();
            pageLabel.AddToClassList("pager-label");
            pager.Add(pageLabel);

            Button next = new Button { text = ">" };
            next.AddToClassList("pager-btn");
            pager.Add(next);
            carouselContainer.Add(pager);

            void ShowVisualsPage(int nextPage)
            {
                pageIndex = Mathf.Clamp(nextPage, 0, totalPages - 1);
                page.Clear();
                focusableRows.Clear();

                int start = pageIndex * pageSize;
                int end = Mathf.Min(start + pageSize, rows.Count);
                for (int i = start; i < end; i++)
                {
                    page.Add(rows[i]);
                    focusableRows.Add(rows[i]);
                }

                pageLabel.text = $"PAGE {pageIndex + 1} / {totalPages}";
                previous.SetEnabled(pageIndex > 0);
                next.SetEnabled(pageIndex < totalPages - 1);
                RebuildControllerFocus();
            }

            previous.clicked += () => ShowVisualsPage(pageIndex - 1);
            next.clicked += () => ShowVisualsPage(pageIndex + 1);

            onPageNext = () => ShowVisualsPage(pageIndex + 1);
            onPagePrev = () => ShowVisualsPage(pageIndex - 1);

            ShowVisualsPage(0);
        }

        private void RebuildControllerFocus()
        {
            RebuildControllerFocusImpl(preferredFocus: null);
        }

        /// <summary>
        /// Rebuild focus list but try to keep focus on the same visual element (useful after
        /// a card selection that re-renders the grid without changing the page).
        /// </summary>
        private void RebuildControllerFocusKeepingPage()
        {
            // After a page turn we want to focus the first card on the new page.
            RebuildControllerFocusImpl(preferredFocus: null, focusFirst: true);
        }

        private void RebuildControllerFocusImpl(
            VisualElement preferredFocus,
            bool focusFirst = false
        )
        {
            // Snapshot before clearing
            VisualElement previousFocus =
                preferredFocus
                ?? (
                    currentControllerFocusIndex >= 0
                    && currentControllerFocusIndex < controllerFocusElements.Count
                        ? controllerFocusElements[currentControllerFocusIndex]
                        : null
                );
            int previousIndex = currentControllerFocusIndex;
            // Capture userData before the element reference becomes stale (e.g. chip buttons
            // get destroyed and recreated by RenderAttributeChoices).
            Vector2Int? previousUserData =
                (previousFocus?.userData is Vector2Int v2) ? v2 : (Vector2Int?)null;

            foreach (VisualElement element in controllerFocusElements)
            {
                element.RemoveFromClassList("creator-controller-focused");
                element.RemoveFromClassList("carousel-container--focused");
            }

            controllerFocusElements.Clear();
            currentControllerFocusIndex = -1;

            AddActiveStepFocusElements();

            if (isStepDrawerOpen)
            {
                foreach (CreatorState state in GetOrderedCreatorStates())
                {
                    if (navButtons.TryGetValue(state, out Button navButton) && navButton != null)
                        controllerFocusElements.Add(navButton);
                }
            }

            if (btnFinalize != null)
                controllerFocusElements.Add(btnFinalize);

            if (controllerFocusElements.Count == 0)
                return;

            if (focusFirst)
            {
                SetControllerFocus(0);
                return;
            }

            // Restore by exact element reference (fast path - element still in DOM).
            int restored =
                previousFocus != null ? controllerFocusElements.IndexOf(previousFocus) : -1;

            if (restored >= 0)
            {
                SetControllerFocus(restored);
                return;
            }

            // Restore by userData position. Used for chip buttons (Free Boosts, Ancestry
            // boosts, etc.) that are destroyed and recreated on each selection change. Each chip
            // stores Vector2Int(rowIndex, colIndex) in userData so we can find the same slot.
            if (previousUserData.HasValue)
            {
                Vector2Int targetPos = previousUserData.Value;
                int byData = controllerFocusElements.FindIndex(e =>
                    e.userData is Vector2Int pos && pos == targetPos
                );
                if (byData >= 0)
                {
                    SetControllerFocus(byData);
                    return;
                }
            }

            // If previousIndex was -1 and there was no preferred element, the controller
            // focus was intentionally cleared (e.g. the player clicked with the mouse). Rebuild
            // the list so it stays up-to-date, but don't snap focus back to any element.
            if (previousIndex < 0 && previousFocus == null)
                return;

            // Tier 3: Fall back to the previous index clamped to the new list size. This keeps
            // focus near where the player was instead of snapping back to the very top.
            int fallback = Mathf.Clamp(previousIndex, 0, controllerFocusElements.Count - 1);
            SetControllerFocus(fallback);
        }

        private void AddActiveStepFocusElements()
        {
            if (currentState == CreatorState.Visuals)
            {
                controllerFocusElements.AddRange(focusableRows.Where(row => row != null));
                // For visuals, we only want top-level buttons (like Reset/Save)
                var buttons = contentArea
                    .Query<Button>()
                    .ToList()
                    .Where(b => b.parent == contentArea || b.parent?.parent == contentArea);
                controllerFocusElements.AddRange(buttons);
                return;
            }

            if (contentArea == null)
                return;

            // TextFields are always top-priority for name entry
            controllerFocusElements.AddRange(contentArea.Query<TextField>().ToList());

            // Choice Cards (Ancestry, Class, etc.)
            // We only want the cards themselves, not buttons inside them
            controllerFocusElements.AddRange(
                contentArea.Query<VisualElement>(className: "choice-card").ToList()
            );

            // ListViews (Equipment, etc.)
            controllerFocusElements.AddRange(contentArea.Query<ListView>().ToList());

            // Buttons (Submit, etc.)
            // Filter out buttons that are children of choice-cards or other
            // complex components we already added, to prevent double-focusing.
            var allButtons = contentArea.Query<Button>().ToList();
            var filteredButtons = allButtons.Where(b =>
                !b.ClassListContains("choice-card")
                && b.GetFirstAncestorOfType<ListView>() == null
                && b.parent?.ClassListContains("choice-card") != true
            );
            controllerFocusElements.AddRange(filteredButtons);

            // Chips (Skills, Attributes)
            controllerFocusElements.AddRange(contentArea.Query(className: "chip").ToList());
        }

        public void UpdatePayload(Action<CharacterDataPayload> updateAction)
        {
            updateAction?.Invoke(payload);
            NotifyPayloadUpdated();
        }

        private void NotifyPayloadUpdated()
        {
            UpdateStatSummary();
            OnPayloadUpdated?.Invoke(payload);
        }

        private void UpdateStatSummary()
        {
            rulesSummary = CharacterCreationRules.BuildSummary(payload, database);

            if (lblTotalHP != null)
                lblTotalHP.text = rulesSummary.HitPoints.ToString();
            if (lblSpeed != null)
                lblSpeed.text = $"{rulesSummary.Speed} ft";
            if (lblClassDC != null)
                lblClassDC.text = rulesSummary.ClassDC.ToString();
            if (lblFocusPoints != null)
                lblFocusPoints.text = rulesSummary.FocusPoints.ToString();
            if (lblPreviewName != null)
                lblPreviewName.text = string.IsNullOrWhiteSpace(payload.Name)
                    ? "UNNAMED ADVENTURER"
                    : payload.Name.ToUpperInvariant();

            // Note: Visual Validation (e.g. choice-invalid class toggle) would be handled
            // inside InitializeActiveStep or a dedicated validation refresh method.
        }

        private void FinalizeCharacter()
        {
            List<string> validationMessages = GetValidationMessages();
            if (validationMessages.Any(message => message != "Ready to finalize."))
            {
                if (currentState == CreatorState.FinishingDetails)
                    SetLabel("Lbl_FinalValidation", string.Join("\n", validationMessages));
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.Name))
                payload.Name = "Unknown Adventurer";

            string savesDir = Path.Combine(Application.persistentDataPath, "Saves", "Roster");
            Directory.CreateDirectory(savesDir);
            string safeName = string.Join(
                "_",
                payload.Name.Split(
                    Path.GetInvalidFileNameChars(),
                    StringSplitOptions.RemoveEmptyEntries
                )
            );
            string filePath = Path.Combine(savesDir, $"{safeName}.json");
            File.WriteAllText(filePath, JsonUtility.ToJson(payload, true));
        }

        public TacticsRulesetDatabase GetDatabase() => database;

        public CharacterDataPayload GetPayload() => payload;
    }
}
