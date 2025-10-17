using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverMenu : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI finalScoreText; // text object in the Game Over UI

    private void Start()
    {
        if (ScoreManager.Instance != null && finalScoreText != null)
        {
            finalScoreText.text = "Final Score: " + ScoreManager.Instance.score;
        }
    }

    public void Retry()
    {
        // optional: reset score before restarting
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.score = 0;

        SceneManager.LoadScene("GameScene");
    }

    public void MainMenu()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.score = 0;

        SceneManager.LoadScene("StartScene");
    }

    public void Quit()
    {
        Application.Quit();
    }
}
