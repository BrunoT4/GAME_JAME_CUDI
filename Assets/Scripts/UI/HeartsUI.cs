using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HeartsUI : MonoBehaviour
{
    [Header("Wiring")]
    public PlayerHealth player;   // drag your PlayerHealth here

    [Header("Icon Hearts (optional)")]
    public Image[] heartIcons;    // size 5; assign in order (left to right)

    [Header("Text Fallback (optional)")]
    public TMP_Text heartsText;   // e.g., "♥♥♥" or "3/5"

    void OnEnable()
    {
        if (player != null)
            player.OnHealthChanged += Refresh;
        Refresh(player ? player.currentHearts : 0, player ? player.maxHearts : 5);
    }

    void OnDisable()
    {
        if (player != null)
            player.OnHealthChanged -= Refresh;
    }

    void Refresh(int current, int max)
    {
        // Icons
        if (heartIcons != null && heartIcons.Length > 0)
        {
            for (int i = 0; i < heartIcons.Length; i++)
            {
                if (!heartIcons[i]) continue;
                heartIcons[i].enabled = (i < max);
                heartIcons[i].color = (i < current) ? Color.white : new Color(1f, 1f, 1f, 0.25f); // dim empty
            }
        }

        // Text
        if (heartsText != null)
        {
            // heartsText.text = new string('♥', current);   // pretty hearts
            heartsText.text = $"{current}/{max}";
        }
    }
}
