using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Hearts")]
    [Range(1, 5)] public int maxHearts = 5;
    public int currentHearts = 5;

    [Header("Damage/I-Frames")]
    public float invulnDuration = 2f;   // seconds of invulnerability after a hit
    public float blinkInterval = 0.12f;  // sprite blink speed during i-frames

    [Header("Knockback (directional)")]
    public float desiredKBHorizontalDistance = 1.5f; // in *player widths*
    public float knockbackDuration = 0.30f;        // how long that push roughly lasts
    public float minHorizontalRatio = 0.70f;       // ensure strong sideways (0..1)
    public float upwardBias = 0.25f;               // add some up so it feels punchy
    public float maxVertical = 12f;                // clamp vertical velocity

    [Header("Control Lock on Hit")]
    public float controlLockDuration = 0.5f;      // disable horizontal input briefly

    [Header("Optional (auto-detected if left empty)")]
    public Rigidbody2D rb;
    public SpriteRenderer sprite;

    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action OnDeath;

    private bool invulnerable;
    private Collider2D pcol;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();
        pcol = GetComponent<Collider2D>();

        currentHearts = Mathf.Clamp(currentHearts, 1, maxHearts);
        OnHealthChanged?.Invoke(currentHearts, maxHearts);
    }

    public void Heal(int amount)
    {
        currentHearts = Mathf.Clamp(currentHearts + amount, 0, maxHearts);
        OnHealthChanged?.Invoke(currentHearts, maxHearts);
    }

    public void TakeDamage(int amount, Vector2 hitFromWorldPos)
    {
        if (invulnerable || currentHearts <= 0) return;

        currentHearts = Mathf.Max(0, currentHearts - amount);
        OnHealthChanged?.Invoke(currentHearts, maxHearts);

        // Directional knockback away from the enemy (diagonal), scaled to ~2 player widths.
        if (rb)
        {
            Vector2 away = (Vector2)transform.position - hitFromWorldPos;

            // Force some horizontal if nearly vertical
            if (Mathf.Abs(away.x) < 0.0001f)
                away.x = (away.y >= 0f) ? 1f : -1f;

            // Add a bit of upward bias
            away.y += Mathf.Abs(away.y) * upwardBias;

            // Normalize and enforce a minimum horizontal share
            away = away.normalized;
            float signX = Mathf.Sign(away.x);
            float ax = Mathf.Abs(away.x);
            float ay = Mathf.Abs(away.y);

            if (ax < minHorizontalRatio)
            {
                ax = minHorizontalRatio;
                float denom = Mathf.Sqrt(ax * ax + ay * ay);
                away = new Vector2(signX * ax / Mathf.Max(denom, 0.0001f), away.y / Mathf.Max(denom, 0.0001f));
            }

            // Compute speed to travel about N player widths horizontally over knockbackDuration
            float playerWidth = (pcol != null) ? pcol.bounds.size.x : 1f;
            float horizSpeedTarget = (desiredKBHorizontalDistance * playerWidth) / Mathf.Max(0.06f, knockbackDuration);
            float speed = horizSpeedTarget / Mathf.Max(0.15f, Mathf.Abs(away.x));

            Vector2 kb = away * speed;

            // Apply as an instantaneous velocity change (override X, keep stronger upward)
            Vector2 v = rb.linearVelocity;
            v.x = kb.x;
            v.y = (kb.y >= 0f) ? Mathf.Max(v.y, kb.y) : Mathf.Max(v.y, kb.y); // allow slight downward if needed
            if (Mathf.Abs(v.y) > maxVertical) v.y = Mathf.Sign(v.y) * maxVertical;

            rb.linearVelocity = v;
        }

        // Brief control lock so player can’t cancel the push instantly
        var pm = GetComponent<PlayerMovement>();
        if (pm != null) pm.ApplyControlLock(controlLockDuration);

        if (currentHearts <= 0)
        {
            OnDeath?.Invoke();
            // TODO: disable controls, play death anim, reload scene, etc.
            return;
        }

        // start i-frames
        StopAllCoroutines();
        StartCoroutine(InvulnBlink());
    }

    IEnumerator InvulnBlink()
    {
        invulnerable = true;
        float t = 0f;
        bool visible = true;

        while (t < invulnDuration)
        {
            if (sprite)
            {
                visible = !visible;
                var c = sprite.color;
                c.a = visible ? 1f : 0.35f;
                sprite.color = c;
            }
            yield return new WaitForSeconds(blinkInterval);
            t += blinkInterval;
        }

        if (sprite)
        {
            var c = sprite.color; c.a = 1f; sprite.color = c;
        }
        invulnerable = false;
    }
}
