using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyDamage : MonoBehaviour
{
    public int damage = 1;  // one heart
    [Tooltip("If set, only objects with this tag will be damaged (e.g., 'Player'). Leave empty to damage any PlayerHealth.")]
    public string targetTag = "Player";

    void Reset()
    {
        // Make sure the collider is a trigger
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag)) return;

        // Find PlayerHealth on the object or its parents
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;

        // Use the enemy’s position as the "hit from" point for knockback
        health.TakeDamage(damage, transform.position);
    }
}
