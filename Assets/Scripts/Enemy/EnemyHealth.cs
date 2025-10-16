using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] int maxHP = 3;
    [SerializeField] bool destroyOnDeath = true;

    [Header("Stun / AI freeze")]
    [SerializeField] MonoBehaviour[] disableDuringStun; // e.g. SimpleEnemyBrain, FlyingEnemyBrain, JumpingEnemyBrain
    [SerializeField] bool zeroVelocityOnStun = true;

    private int hp;
    private float stunTimer;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hp = maxHP;
    }

    void Update()
    {
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
                SetStunned(false);
        }
    }

    public bool IsStunned() => stunTimer > 0f;

    public void TakeDamage(int dmg, Vector2 hitFromWorldPos)
    {
        hp = Mathf.Max(0, hp - Mathf.Max(1, dmg));
        if (hp <= 0)
        {
            if (destroyOnDeath) Destroy(gameObject);
        }
        // (Optional: add flash/FX here)
    }

    /// <summary>
    /// Knock the enemy away from hit point and apply stun.
    /// Horizontal distance is approximate over the given duration.
    /// </summary>
    public void ApplyKnockbackAndStun(
        Vector2 hitFromWorldPos,
        float desiredKBHorizontalDistance = 1.5f,
        float knockbackDuration = 0.20f,
        float stunDuration = 0.15f,
        float minHorizontalRatio = 0.65f,
        float upwardBias = 0.10f,
        float maxVertical = 10f)
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();

        // Compute away direction
        Vector2 away = (Vector2)transform.position - hitFromWorldPos;
        if (Mathf.Abs(away.x) < 0.0001f) away.x = (away.y >= 0f) ? 1f : -1f;

        // Add slight upward bias so hits pop a bit
        away.y += Mathf.Abs(away.y) * upwardBias;

        away = away.normalized;
        float signX = Mathf.Sign(away.x);
        float ax = Mathf.Abs(away.x);
        float ay = Mathf.Abs(away.y);

        // Ensure enough sideways push
        if (ax < minHorizontalRatio)
        {
            ax = minHorizontalRatio;
            float denom = Mathf.Sqrt(ax * ax + ay * ay);
            away = new Vector2(signX * ax / Mathf.Max(denom, 0.0001f), away.y / Mathf.Max(denom, 0.0001f));
        }

        // Speed so horizontal travels about desiredKBHorizontalDistance over knockbackDuration
        float enemyWidth = GetComponent<Collider2D>() ? GetComponent<Collider2D>().bounds.size.x : 1f;
        float horizSpeedTarget = (desiredKBHorizontalDistance * enemyWidth) / Mathf.Max(0.06f, knockbackDuration);
        float speed = horizSpeedTarget / Mathf.Max(0.15f, Mathf.Abs(away.x));

        // Apply immediate velocity
        Vector2 v = rb.linearVelocity;
        Vector2 kb = away * speed;
        v.x = kb.x;
        v.y = (kb.y >= 0f) ? Mathf.Max(v.y, kb.y) : Mathf.Max(v.y, kb.y);
        if (Mathf.Abs(v.y) > maxVertical) v.y = Mathf.Sign(v.y) * maxVertical;
        rb.linearVelocity = v;

        // Stun
        Stun(stunDuration);
    }

    public void Stun(float duration)
    {
        stunTimer = Mathf.Max(stunTimer, duration);
        SetStunned(true);
        if (zeroVelocityOnStun && rb) rb.linearVelocity = Vector2.zero;
    }

    private void SetStunned(bool on)
    {
        // Freeze/unfreeze enemy brains during stun
        if (disableDuringStun != null)
        {
            foreach (var mb in disableDuringStun)
                if (mb) mb.enabled = !on;
        }
    }
}
