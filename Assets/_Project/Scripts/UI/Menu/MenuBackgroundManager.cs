using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the Main Menu background image, providing panning, zooming,
/// and easy swapping of backgrounds from a list.
/// </summary>
public class MenuBackgroundManager : MonoBehaviour
{
    [Header("Background List")]
    [Tooltip("List of background sprites to cycle through or pick from.")]
    public List<Sprite> backgrounds = new List<Sprite>();

    [Tooltip("Which background index to show on start. -1 for random.")]
    public int startIndex = -1;

    [Header("Animation Settings")]
    [Range(0f, 0.5f)]
    public float panSpeed = 0.05f;

    [Range(0f, 0.5f)]
    public float zoomSpeed = 0.03f;

    [Range(1.0f, 2.0f)]
    public float maxZoom = 1.15f;

    [Header("Swap Settings")]
    [Tooltip(
        "How many seconds to wait before swapping to a new background. Set to 0 to disable auto-swap."
    )]
    public float autoSwapInterval = 10f;

    [Header("Technical References")]
    public string backgroundElementName = "BackgroundContainer";

    private UIDocument uiDocument;
    private VisualElement backgroundElement;
    private float timer;
    private float swapTimer;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
            return;

        backgroundElement = uiDocument.rootVisualElement.Q<VisualElement>(backgroundElementName);

        if (backgroundElement == null)
        {
            Debug.LogWarning(
                $"MenuBackgroundManager: Could not find VisualElement named '{backgroundElementName}'. Make sure it exists in your UXML."
            );
            return;
        }

        SelectBackground();
    }

    private void SelectBackground()
    {
        if (backgrounds == null || backgrounds.Count == 0)
            return;

        int index = startIndex;
        if (index < 0 || index >= backgrounds.Count)
        {
            index = Random.Range(0, backgrounds.Count);
        }

        SetBackground(index);
    }

    public void SetBackground(int index)
    {
        if (index < 0 || index >= backgrounds.Count)
            return;

        if (backgroundElement != null)
        {
            backgroundElement.style.backgroundImage = new StyleBackground(backgrounds[index]);
        }
    }

    private void Update()
    {
        if (backgroundElement == null)
            return;

        timer += Time.deltaTime;

        if (autoSwapInterval > 0)
        {
            swapTimer += Time.deltaTime;
            if (swapTimer >= autoSwapInterval)
            {
                swapTimer = 0;
                NextBackground();
            }
        }

        // Subtle Zoom (Sine wave between 1.0 and maxZoom)
        float zoomRange = maxZoom - 1.0f;
        float currentScale = 1.0f + (Mathf.Sin(timer * zoomSpeed) * 0.5f + 0.5f) * zoomRange;

        backgroundElement.style.scale = new StyleScale(
            new Scale(new Vector2(currentScale, currentScale))
        );

        // Subtle Panning (Slow movement in a figure-eight or circular pattern)
        float panX = Mathf.Sin(timer * panSpeed) * 20f;
        float panY = Mathf.Cos(timer * panSpeed * 0.7f) * 20f;

        backgroundElement.style.translate = new StyleTranslate(new Translate(panX, panY, 0));
    }

    /// <summary>
    /// Call this to swap to the next background in the list.
    /// </summary>
    public void NextBackground()
    {
        if (backgrounds.Count <= 1)
            return;

        // Find current or just random
        int nextIndex = Random.Range(0, backgrounds.Count);
        SetBackground(nextIndex);
    }
}
