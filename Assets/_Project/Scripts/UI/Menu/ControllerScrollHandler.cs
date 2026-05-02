using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class ControllerScrollHandler : MonoBehaviour
{
    public float scrollSpeed = 500f;
    private UIDocument uiDocument;

    private ScrollView activeScrollView;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void Update()
    {
        if (uiDocument == null)
            return;

        // Try to find the scroll view if it's currently visible
        var root = uiDocument.rootVisualElement;
        if (root == null)
            return;

        var scrollViewContainer = root.Q<VisualElement>("ScrollView");
        if (scrollViewContainer != null && !scrollViewContainer.ClassListContains("screen-hidden"))
        {
            activeScrollView = scrollViewContainer.Q<ScrollView>("ScrollContent");
        }
        else
        {
            activeScrollView = null;
        }

        if (activeScrollView == null)
            return;

        // Check Input System for right stick
        if (Gamepad.current != null)
        {
            Vector2 rightStick = Gamepad.current.rightStick.ReadValue();

            // Apply deadzone
            if (Mathf.Abs(rightStick.y) > 0.1f)
            {
                // Invert Y so pushing UP scrolls UP
                float scrollDelta = -rightStick.y * scrollSpeed * Time.deltaTime;
                Vector2 currentOffset = activeScrollView.scrollOffset;

                // Clamp within bounds
                float maxScrollY =
                    activeScrollView.contentContainer.layout.height
                    - activeScrollView.layout.height;
                if (maxScrollY < 0)
                    maxScrollY = 0;

                currentOffset.y = Mathf.Clamp(currentOffset.y + scrollDelta, 0, maxScrollY);
                activeScrollView.scrollOffset = currentOffset;
            }
        }
    }
}
