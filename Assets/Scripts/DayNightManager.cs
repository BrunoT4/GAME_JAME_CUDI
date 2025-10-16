using UnityEngine;
using System.Collections;

public class DayNightManager : MonoBehaviour
{
    [Header("Cycle Settings")]
    [SerializeField] private float minCycleDuration = 10f; // shortest day/night
    [SerializeField] private float maxCycleDuration = 20f; // longest day/night

    [Header("References")]
    [SerializeField] private SpriteRenderer background; // assign your background sprite
    [SerializeField] private Color dayColor = new(1f, 0.95f, 0.6f);
    [SerializeField] private Color nightColor = new(0.1f, 0.1f, 0.25f);

    public bool IsNight { get; private set; } = false;

    public delegate void CycleChanged(bool isNight);
    public static event CycleChanged OnCycleChanged;

    private void Start()
    {
        // --- Initialize as day visually ---
        IsNight = false;
        if (background != null)
            background.color = dayColor;

        // --- Notify listeners (so platforms, etc., know the starting state) ---
        OnCycleChanged?.Invoke(IsNight);

        // --- Start automatic cycle ---
        StartCoroutine(CycleRoutine());
    }

    private IEnumerator CycleRoutine()
    {
        while (true)
        {
            // --- Wait for the random duration before switching ---
            float duration = Random.Range(minCycleDuration, maxCycleDuration);
            yield return new WaitForSeconds(duration);

            // --- Flip state first ---
            IsNight = !IsNight;
            OnCycleChanged?.Invoke(IsNight); // 🔥 Notify immediately so listeners can fade in sync

            // --- Fade background color to new target ---
            float elapsed = 0f;
            Color start = background.color;
            Color target = IsNight ? nightColor : dayColor;

            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime / 2f; // fade duration (2s)
                background.color = Color.Lerp(start, target, elapsed);
                yield return null;
            }
        }
    }
}
