using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Enemy Prefabs (Multiple)")]
    [SerializeField] private GameObject[] dayEnemyPrefabs;
    [SerializeField] private GameObject[] nightEnemyPrefabs;

    // ---------- DIFFICULTY SCALING ----------
    [Header("Difficulty (score-based)")]
    [Tooltip("Start scaling once score >= this value (e.g., 10000).")]
    [SerializeField] private int baseScoreForScaling = 10000;

    [Tooltip("Each level multiplies interval by this factor (e.g., 0.97 means 3% faster per level).")]
    [SerializeField] private float intervalMultiplierPerLevel = 0.95f;

    [Tooltip("Clamp so interval never goes below this.")]
    [SerializeField] private float minSpawnInterval = 0.35f;

    [Header("Extra Spawns per Tick (optional)")]
    [Tooltip("How many difficulty levels per +1 extra spawn (e.g., 8 => +1 every 8 levels). Set 999 to disable.")]
    [SerializeField] private int levelsPerExtraSpawn = 2;

    [Tooltip("Cap the number of extra enemies spawned per tick.")]
    [SerializeField] private int maxExtraSpawns = 10;

    private bool isNight = false;
    private bool spawning = true;

    private void OnEnable()
    {
        DayNightManager.OnCycleChanged += HandleCycleChange;
    }

    private void OnDisable()
    {
        DayNightManager.OnCycleChanged -= HandleCycleChange;
    }

    private void Start()
    {
        // --- Get the current day/night state ---
        var mgr = FindFirstObjectByType<DayNightManager>();
        if (mgr != null)
            isNight = mgr.IsNight;

        // --- Validate setup once at start ---
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("EnemySpawner: No spawn points assigned!");
            return;
        }

        if ((dayEnemyPrefabs == null || dayEnemyPrefabs.Length == 0) &&
            (nightEnemyPrefabs == null || nightEnemyPrefabs.Length == 0))
        {
            Debug.LogWarning("EnemySpawner: No enemy prefabs assigned!");
            return;
        }

        StartCoroutine(SpawnRoutine());
    }

    private void HandleCycleChange(bool newIsNight)
    {
        isNight = newIsNight;
    }

    private IEnumerator SpawnRoutine()
    {
        while (spawning)
        {
            // -------- DIFFICULTY --------
            int score = GetScore();
            int level = GetDifficultyLevel(score);
            float effectiveInterval = GetScaledInterval(level);

            // Wait a random time between 80–120% of the base interval for variety
            float wait = Random.Range(effectiveInterval * 0.8f, effectiveInterval * 1.2f);
            yield return new WaitForSeconds(wait);

            // --- Safety checks each cycle ---
            if (spawnPoints == null || spawnPoints.Length == 0)
                continue;

            // How many to spawn this tick (1 + extra from difficulty)
            int extra = (levelsPerExtraSpawn <= 0) ? 0
                        : Mathf.Clamp(level / levelsPerExtraSpawn, 0, maxExtraSpawns);
            int countThisTick = 1 + extra;

            for (int i = 0; i < countThisTick; i++)
            {
                GameObject prefab = PickPrefabForCurrentCycle();
                if (prefab == null) continue;

                Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                Instantiate(prefab, spawnPoint.position, Quaternion.identity);
            }
        }
    }

    // --------------- Helpers ---------------

    private int GetScore()
    {
        return (ScoreManager.Instance != null) ? ScoreManager.Instance.score : 0;
    }

    private int GetDifficultyLevel(int score)
    {
        const int pointsPerLevel = 10000;
        return Mathf.Max(0, score / pointsPerLevel);
    }

    private float GetScaledInterval(int level)
    {
        // exponential (multiplicative) shrink, clamped to min
        float scaled = spawnInterval;
        if (level > 0)
        {
            // scaled = spawnInterval * (intervalMultiplierPerLevel ^ level)
            scaled *= Mathf.Pow(Mathf.Clamp(intervalMultiplierPerLevel, 0.01f, 1.0f), level);
        }
        return Mathf.Max(minSpawnInterval, scaled);
    }

    private GameObject PickPrefabForCurrentCycle()
    {
        if (isNight && nightEnemyPrefabs != null && nightEnemyPrefabs.Length > 0)
            return nightEnemyPrefabs[Random.Range(0, nightEnemyPrefabs.Length)];

        if (!isNight && dayEnemyPrefabs != null && dayEnemyPrefabs.Length > 0)
            return dayEnemyPrefabs[Random.Range(0, dayEnemyPrefabs.Length)];

        // fallback if arrays for this cycle are empty
        if (dayEnemyPrefabs != null && dayEnemyPrefabs.Length > 0)
            return dayEnemyPrefabs[Random.Range(0, dayEnemyPrefabs.Length)];
        if (nightEnemyPrefabs != null && nightEnemyPrefabs.Length > 0)
            return nightEnemyPrefabs[Random.Range(0, nightEnemyPrefabs.Length)];

        return null;
    }
}
