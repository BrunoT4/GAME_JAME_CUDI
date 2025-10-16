using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class SwordHitbox : MonoBehaviour
{
    [HideInInspector] public SwordAttack owner;
    [HideInInspector] public int damage = 1;
    [HideInInspector] public bool enablePogo = false;

    // Knockback & stun params (set by SwordAttack each swing)
    [HideInInspector] public float kbHorizontalDist = 1.5f; // ~enemy widths to shove
    [HideInInspector] public float kbDuration = 0.20f;      // how fast the shove is
    [HideInInspector] public float stunDuration = 0.15f;    // AI freeze time
    [HideInInspector] public float kbMinHoriz = 0.65f;      // min sideways component
    [HideInInspector] public float kbUpBias = 0.10f;        // small upward tilt
    [HideInInspector] public float kbMaxVertical = 10f;     // clamp vertical

    private BoxCollider2D box;

    void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        gameObject.SetActive(false);
    }

    public void SetLocalBox(Vector2 localOffset, Vector2 size)
    {
        box.size = size;
        box.offset = localOffset;
    }

    public void SetActive(bool on)
    {
        box.enabled = on;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var eh = other.GetComponentInParent<EnemyHealth>();
        if (!eh) return;

        // Get the player's form (from the SwordAttack's movement reference)
        var playerForm = owner?.Movement?.GetFormType() ?? FormType.Light;

        // Skip damage if same form
        if (eh.GetFormType() == playerForm)
        {
            // Optional: feedback so player knows it's immune
            Debug.Log($"No damage — same form ({playerForm})");
            return;
        }

        // Deal damage
        eh.TakeDamage(damage, transform.position);

        // Apply knockback + stun (no i-frames for enemies)
        eh.ApplyKnockbackAndStun(
            transform.position,
            kbHorizontalDist,
            kbDuration,
            stunDuration,
            kbMinHoriz,
            kbUpBias,
            kbMaxVertical
        );

        // Pogo (down attack only)
        if (enablePogo && owner != null)
        {
            owner.DoPogoBounce(other.ClosestPoint(transform.position));
        }
    }

    // Optional gizmo to see the active box while selected
    void OnDrawGizmosSelected()
    {
        if (!box) box = GetComponent<BoxCollider2D>();
        Gizmos.color = Color.yellow;
        Vector3 worldCenter = transform.TransformPoint(box.offset);
        Vector3 worldSize = Vector3.Scale((Vector3)box.size, transform.lossyScale);
        Gizmos.DrawWireCube(worldCenter, worldSize);
    }
}
