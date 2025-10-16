using UnityEngine;
using System.Collections;

public class DayNightManager : MonoBehaviour
{
    [Header("Cycle Settings")]
    [SerializeField] private float minCycleDuration = 10f; // shortest day/night
    [SerializeField] private float maxCycleDuration = 20f; // longest day/night

    [Header("References")]
    [SerializeField] private SpriteRenderer background; // assign your background sprite
    [SerializeField] private Color dayColor = new Color(1f, 0.95f, 0.6f);
    [SerializeField] private Color nightColor = new Color(0.1f, 0.1f, 0.25f);

    public bool IsNight { get; private set; } = false;


    public delegate void CycleChanged(bool isNight);
    public static event CycleChanged OnCycleChanged;

    private void Start()
    {
        // Ensure we start in day mode visually
        IsNight = false;
        if (background != null)
            background.color = dayColor;

        // Notify listeners (like EnemySpawner) that it's daytime at start
        OnCycleChanged?.Invoke(IsNight);

        // Start the automatic cycle
        StartCoroutine(CycleRoutine());
    }

    private IEnumerator CycleRoutine()
    {
        while (true)
        {
            // Random duration for current cycle
            float duration = Random.Range(minCycleDuration, maxCycleDuration);

            // Fade background color gradually
            float elapsed = 0f;
            Color start = background.color;
            Color target = IsNight ? dayColor : nightColor;

            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime / 2f; // fade duration (2s)
                background.color = Color.Lerp(start, target, elapsed);
                yield return null;
            }

            // Switch cycle
            IsNight = !IsNight;

            // Notify listeners
            OnCycleChanged?.Invoke(IsNight);

            // Wait for the random duration
            yield return new WaitForSeconds(duration);
        }
    }
}
