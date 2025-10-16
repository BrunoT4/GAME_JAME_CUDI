using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance; // Singleton
    public TextMeshProUGUI scoreText;

    public int score = 0;
    public int passivePoints = 1;   // Points added every few seconds
    public float interval = 2f;     // Interval in seconds

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        UpdateScoreUI();
        StartCoroutine(AddScoreOverTime());
    }

    // Add points manually (e.g. for kills)
    public void AddScore(int points)
    {
        score += points;
        UpdateScoreUI();
    }

    // Coroutine for automatic score increase
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
        scoreText.text = "Score: " + score.ToString();
    }
}
