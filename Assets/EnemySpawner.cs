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
            // Wait a random time between 80–120% of the base interval for variety
            yield return new WaitForSeconds(Random.Range(spawnInterval * 0.8f, spawnInterval * 1.2f));

            // --- Safety checks each cycle ---
            if (spawnPoints == null || spawnPoints.Length == 0)
                continue;

            // --- Pick random prefab ---
            GameObject prefab = null;

            if (isNight && nightEnemyPrefabs != null && nightEnemyPrefabs.Length > 0)
            {
                prefab = nightEnemyPrefabs[Random.Range(0, nightEnemyPrefabs.Length)];
            }
            else if (!isNight && dayEnemyPrefabs != null && dayEnemyPrefabs.Length > 0)
            {
                prefab = dayEnemyPrefabs[Random.Range(0, dayEnemyPrefabs.Length)];
            }

            // Skip if still null (no enemies assigned for current cycle)
            if (prefab == null)
                continue;

            // --- Pick random spawn point ---
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

            // --- Spawn enemy ---
            Instantiate(prefab, spawnPoint.position, Quaternion.identity);
        }
    }
}
