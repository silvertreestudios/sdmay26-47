using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    // Name of your game scene (must match exactly in Build Settings)
    [SerializeField] private string gameSceneName = "Level 1";

    // Filename of your PDF inside StreamingAssets/
    [SerializeField] private string pdfFileName = "ORC_LicenseFINAL.pdf";

    // Called by Start Game button
    public void OnStartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    // Called by View PDF button
    public void OnViewPDF()
    {
        string pdfPath = "";

#if UNITY_EDITOR
        pdfPath = System.IO.Path.Combine(Application.streamingAssetsPath, pdfFileName);
#elif UNITY_STANDALONE_WIN
        pdfPath = System.IO.Path.Combine(Application.streamingAssetsPath, pdfFileName);
#elif UNITY_STANDALONE_OSX
        pdfPath = System.IO.Path.Combine(Application.streamingAssetsPath, pdfFileName);
#endif

        // Opens the PDF in the system's default PDF viewer
        Application.OpenURL("file:///" + pdfPath);
    }

    // Called by Quit button
    public void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}