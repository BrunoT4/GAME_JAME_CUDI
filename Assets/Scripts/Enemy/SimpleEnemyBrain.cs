using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))] // works great with CapsuleCollider2D
public class SimpleEnemyBrain : MonoBehaviour
{

    [Header("Target")]
    [SerializeField] private Transform player;        // drag your Player here

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stopDistance = 0.6f;     // don't overlap the player
    [SerializeField] private float acceleration = 30f;      // ground steering
    [SerializeField] private bool chaseWhileAirborne = true;
    [SerializeField] private float airAcceleration = 12f;   // weaker air steering feels nicer

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;         // child under feet
    [SerializeField] private float groundCheckRadius = 0.18f;
    [SerializeField] private LayerMask groundLayer;         // tick your Ground layer

    [Header("Directional Ledge Probe (no child objects needed)")]
    [Tooltip("How far down to look for floor in front of the enemy.")]
    [SerializeField] private float ledgeProbeDistance = 0.25f;
    [Tooltip("How far beyond the collider edge to place the probe horizontally.")]
    [SerializeField] private float probeSideMargin = 0.06f;
    [Tooltip("Vertical offset for the probe origin (down is positive if you want it lower).")]
    [SerializeField] private float probeDropOffset = 0.00f;

    [Header("Facing")]
    [SerializeField] private bool flipWithScale = true;

    // --- caches ---
    private Rigidbody2D rb;
    private Collider2D col;

    // --- runtime ---
    private float desiredXVel;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    private void Start()
{
    // auto-find the player in the scene
    if (player == null)
    {
        var pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null)
            player = pObj.transform;
    }
}


    private void FixedUpdate()
    {
        if (player == null || groundCheck == null) return;

        // Grounded check (simple circle at feet)
        bool grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // Always aggro: compute direction toward the player
        float dx = player.position.x - transform.position.x;
        float dir = Mathf.Sign(dx);               // -1 left, +1 right; 0 if exactly aligned
        if (dir == 0f) dir = 1f;                  // arbitrary to the right when centered

        // Compute a LEADING-EDGE probe from the collider bounds (works for Capsule/Box)
        Vector2 center = col.bounds.center;
        Vector2 ext = col.bounds.extents;

        // Probe origin is at the leading horizontal edge, slightly outside the collider,
        // and a bit lowered so it "looks" for floor in front of the feet.
        Vector2 forwardOrigin = new Vector2(
            center.x + dir * (ext.x + probeSideMargin),
            center.y - ext.y * 0.2f + probeDropOffset
        );

        // Look straight down to see if there's ground ahead (prevents walking off cliffs)
        bool groundAhead = Physics2D.Raycast(forwardOrigin, Vector2.down, ledgeProbeDistance, groundLayer);

        // Decide desired horizontal velocity
        desiredXVel = 0f;

        bool canMove = grounded || chaseWhileAirborne;
        if (canMove && Mathf.Abs(dx) > stopDistance)
        {
            // only step forward if there's floor in front, or we're allowed to move in air
            if (grounded)
            {
                if (groundAhead) desiredXVel = dir * moveSpeed;
            }
            else // airborne
            {
                desiredXVel = dir * moveSpeed;
            }

            // Face movement
            if (flipWithScale && Mathf.Abs(desiredXVel) > 0.01f)
            {
                Vector3 s = transform.localScale;
                s.x = Mathf.Sign(desiredXVel) * Mathf.Abs(s.x);
                transform.localScale = s;
            }
        }

        // Smoothly approach the desired velocity (separate ground/air accel)
        float accel = grounded ? acceleration : airAcceleration;
        float newX = Mathf.MoveTowards(rb.linearVelocity.x, desiredXVel, accel * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualize ground check
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // Visualize directional ledge probe when playing (so bounds are valid)
        if (Application.isPlaying && col != null && player != null)
        {
            float dir = Mathf.Sign(player.position.x - transform.position.x);
            if (dir == 0f) dir = 1f;

            Vector2 center = col.bounds.center;
            Vector2 ext    = col.bounds.extents;

            Vector3 origin = new Vector3(
                center.x + dir * (ext.x + probeSideMargin),
                center.y - ext.y * 0.2f + probeDropOffset,
                0f
            );

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.DrawLine(origin, origin + Vector3.down * ledgeProbeDistance);
#endif
        }
    }
#endif
}
