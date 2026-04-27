using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Level 1";
    [SerializeField] private GameObject ORCScrollView; // drag PDFScrollView here

    void Start()
    {
        // Make sure PDF viewer is hidden on startup
        ORCScrollView.SetActive(false);
    }

    public void OnStartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnViewPDF()
    {
        // Toggle the PDF viewer on/off
        bool isActive = ORCScrollView.activeSelf;
        ORCScrollView.SetActive(!isActive);
    }

    public void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}