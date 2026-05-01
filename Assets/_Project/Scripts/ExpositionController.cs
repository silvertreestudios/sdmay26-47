using UnityEngine;

public class ExpositionController : MonoBehaviour
{
    [SerializeField] private GameObject expositionCanvas;

    void Start()
    {
        // Freeze the game and show the exposition screen on level start
        Time.timeScale = 0f;
        expositionCanvas.SetActive(true);
    }

    public void OnContinue()
    {
        // Hide the exposition screen and resume the game
        expositionCanvas.SetActive(false);
        Time.timeScale = 1f;
    }
}