using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class HeartsUI : MonoBehaviour
{
    [Header("Wiring")]
    public PlayerHealth player;   // drag in Inspector, or auto-find

    [Header("Icon Hearts (optional)")]
    public Image[] heartIcons;    // size 5; assign in order (left → right)

    [Header("Text Fallback (optional)")]
    public TMP_Text heartsText;   // e.g., "♥♥♥" or "3/5"

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(ConnectNextFrame());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (player != null)
            player.OnHealthChanged -= Refresh;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Hide hearts visually outside the GameScene
        bool inGame = scene.name == "GameScene";

        if (heartIcons != null)
        {
            foreach (var img in heartIcons)
            {
                if (img != null)
                    img.gameObject.SetActive(inGame);
            }
        }
        if (heartsText != null)
            heartsText.gameObject.SetActive(inGame);

        if (inGame)
            StartCoroutine(ConnectNextFrame());
        else
            player = null;
    }


    private IEnumerator ConnectNextFrame()
    {
        // Wait one frame so PlayerHealth is fully spawned
        yield return null;

#if UNITY_2023_1_OR_NEWER
        player = Object.FindFirstObjectByType<PlayerHealth>();
#else
        player = FindObjectOfType<PlayerHealth>();
#endif

        if (player != null)
        {
            player.OnHealthChanged += Refresh;
            Refresh(player.currentHearts, player.maxHearts);
            Debug.Log($"[HeartsUI] Connected to {player.name}");
        }
        else
        {
            Debug.LogWarning("[HeartsUI] Could not find PlayerHealth after scene load.");
        }
    }

    private void Refresh(int current, int max)
    {
        if (heartIcons != null && heartIcons.Length > 0)
        {
            for (int i = 0; i < heartIcons.Length; i++)
            {
                if (!heartIcons[i]) continue;
                heartIcons[i].enabled = (i < max);
                heartIcons[i].color = (i < current)
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        if (heartsText != null)
            heartsText.text = $"{current}/{max}";
    }
}
