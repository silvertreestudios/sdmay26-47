using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuCanvas;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isPaused = false;

    void Update()
    {
        // Check for Escape key on keyboard
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }

        // Check for Start/Select button on controller
        if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        // Show or hide the pause menu
        pauseMenuCanvas.SetActive(isPaused);

        // Freeze or unfreeze the game
        Time.timeScale = isPaused ? 0f : 1f;
    }

    public void OnResume()
    {
        TogglePause();
    }

    public void OnRestartLevel()
    {
        // Make sure time is running before reloading
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnMainMenu()
    {
        // Make sure time is running before changing scenes
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}