using UnityEngine;
using System.Collections;

public class PlatformColorMorpher : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] platforms;
    [SerializeField] private Color dayColor = new(1f, 0.4f, 0.4f);
    [SerializeField] private Color nightColor = new(0.3f, 0.3f, 0.7f);
    [SerializeField] private float fadeDuration = 2f;

    private void OnEnable()
    {
        DayNightManager.OnCycleChanged += HandleCycleChange;
    }

    private void OnDisable()
    {
        DayNightManager.OnCycleChanged -= HandleCycleChange;
    }

    private IEnumerator Start()
    {
        // Wait until DayNightManager exists and finishes Start()
        DayNightManager manager = null;
        while (manager == null)
        {
            manager = FindFirstObjectByType<DayNightManager>();
            yield return null;
        }

        // Wait one more frame to ensure its Start() ran and OnCycleChanged fired once
        yield return null;

        // Set initial color directly from the manager’s background
        Color startColor = manager.IsNight ? nightColor : dayColor;

        foreach (var p in platforms)
            if (p != null) p.color = startColor;
    }

    private void HandleCycleChange(bool isNight)
    {
        StopAllCoroutines();
        StartCoroutine(FadePlatforms(isNight ? nightColor : dayColor));
    }

    private IEnumerator FadePlatforms(Color target)
    {
        if (platforms.Length == 0) yield break;

        Color start = platforms[0].color;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / fadeDuration;
            Color c = Color.Lerp(start, target, t);
            foreach (var p in platforms)
                if (p != null) p.color = c;
            yield return null;
        }
    }
}
