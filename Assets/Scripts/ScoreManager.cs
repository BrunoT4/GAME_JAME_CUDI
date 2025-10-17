using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance; // Singleton
    public TextMeshProUGUI scoreText;

    public int score = 0;
    public int passivePoints = 1;   // Points added every few seconds
    public float interval = 2f;     // Interval in seconds

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);  // keep across scenes
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        UpdateScoreUI();
        StartCoroutine(AddScoreOverTime());
    }

    // 🔹 Hide the in-game score text when not in the GameScene
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scoreText == null) return;

        // Show only in the main gameplay scene
        if (scene.name == "GameScene")
            scoreText.gameObject.SetActive(true);
        else
            scoreText.gameObject.SetActive(false);
    }

    // 🔹 Add points manually (for kills, pickups, etc.)
    public void AddScore(int points)
    {
        score += points;
        UpdateScoreUI();
    }

    // 🔹 Automatic passive score increase
    private System.Collections.IEnumerator AddScoreOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);
            AddScore(passivePoints);
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score.ToString();
    }

    public void ResetScore()
    {
        score = 0;
        UpdateScoreUI();
    }
}
