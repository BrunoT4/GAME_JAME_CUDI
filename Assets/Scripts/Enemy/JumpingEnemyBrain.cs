using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class JumpingEnemyBrain : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;            // drag your Player here
    [SerializeField] private float stopDistance = 0.75f;  // don't hop if already this close

    [Header("Hop")]
    [SerializeField] private float hopSpeed = 6.0f;       // horizontal speed at takeoff
    [SerializeField] private float jumpVelocity = 14.0f;   // vertical velocity at takeoff
    [SerializeField] private float airMaxXSpeed = 10.0f;   // clamp in air
    [SerializeField] private float gravityScale = 3.0f;   // heavier feel

    [Header("Wind-Up & Cooldown")]
    [SerializeField] private float windupTime = 0.15f;    // crouch before hop
    [SerializeField] private float minHopCooldown = 1.5f;
    [SerializeField] private float maxHopCooldown = 2.0f;

    [Header("Grounding")]
    [SerializeField] private Transform groundCheck;       // child at feet
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;       // tick your Ground layer

    [Header("Ledge Safety")]
    [SerializeField] private float ledgeProbeDistance = 0.32f; // base ray length
    [SerializeField] private float probeSideMargin = 0.06f;    // how far beyond collider edge
    [SerializeField] private float probeDropOffset = 0.00f;    // vertical tweak near bottom
    [SerializeField] private float probeBottomOffset = 0.05f;  // start a bit above collider bottom

    [Header("Wall Check")]
    [SerializeField] private float wallCheckDistance = 0.2f;

    [Header("Facing")]
    [SerializeField] private bool flipWithScale = true;

    [Header("Ground Stop")]
    [SerializeField] private float groundBrake = 80f;     // how fast X goes to 0 while grounded

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool debugIgnoreLedge = false;

    // --- caches ---
    private Rigidbody2D rb;
    private Collider2D col;

    // --- state ---
    private float hopCooldownTimer = 0f;
    private float windupTimer = 0f;
    private int queuedHopDir = 0;           // -1 left, +1 right
    private Vector3 baseScale;
    private bool grounded = false;
    private bool wasGrounded = false;

    // --- Debug probe cache (drawn in Gizmos only) ---
    private Vector2 dbgProbeOrigin;
    private float dbgProbeLen;
    private bool dbgProbeHit;
    private int dbgProbeDir;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        baseScale = transform.localScale;
    }

private void Start()
{
    rb.gravityScale = gravityScale;
    rb.freezeRotation = true;

    if (player == null)
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }
}

    private void OnValidate()
    {
        if (TryGetComponent(out Rigidbody2D r))
        {
            if (r.bodyType != RigidbodyType2D.Dynamic && debugLogs)
                Debug.LogWarning("[JumpingEnemyBrain] Rigidbody2D should be Dynamic.");
        }
    }

    private void Update()
    {
        if (!player) { if (debugLogs) Debug.LogWarning("[JumpingEnemyBrain] No player assigned."); return; }
        if (!groundCheck) { if (debugLogs) Debug.LogWarning("[JumpingEnemyBrain] No GroundCheck assigned."); return; }

        if (hopCooldownTimer > 0f) hopCooldownTimer -= Time.deltaTime;

        grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // immediate zero-X on landing (prevents any slide)
        if (grounded && !wasGrounded && windupTimer <= 0f)
        {
            var v0 = rb.linearVelocity; v0.x = 0f; rb.linearVelocity = v0;
        }
        wasGrounded = grounded;

        // face (velocity or intended dir during windup)
        float face = rb.linearVelocity.x;
        if (windupTimer > 0f && queuedHopDir != 0) face = queuedHopDir;
        if (flipWithScale && Mathf.Abs(face) > 0.01f)
        {
            var s = transform.localScale;
            s.x = Mathf.Sign(face) * Mathf.Abs(baseScale.x);
            transform.localScale = s;
        }

        // windup in progress?
        if (windupTimer > 0f)
        {
            windupTimer -= Time.deltaTime;

            // simple squash/stretch telegraph
            float t = Mathf.Clamp01(1f - (windupTimer / Mathf.Max(0.0001f, windupTime)));
            float squash = Mathf.Lerp(1f, 0.85f, t);
            float stretch = Mathf.Lerp(1f, 1.12f, t);
            transform.localScale = new Vector3(
                Mathf.Sign(transform.localScale.x) * Mathf.Abs(baseScale.x) * squash,
                baseScale.y * stretch,
                baseScale.z
            );

            if (windupTimer <= 0f)
            {
                DoHop(queuedHopDir);
                transform.localScale = new Vector3(
                    Mathf.Sign(transform.localScale.x) * Mathf.Abs(baseScale.x) * 0.9f,
                    baseScale.y * 1.1f,
                    baseScale.z
                );
            }
            return;
        }

        // ALWAYS AGGRO: if grounded, off cooldown, and not too close -> prepare hop toward player
        if (grounded && hopCooldownTimer <= 0f)
        {
            float dx = player.position.x - transform.position.x;
            if (Mathf.Abs(dx) > stopDistance)
            {
                int dir = (dx > 0f) ? +1 : -1;

                bool groundAhead = HasGroundAhead(dir);
                // bool safe = debugIgnoreLedge || groundAhead;
                bool safe = true;

                if (debugLogs)
                    Debug.Log($"[JumpingEnemyBrain] grounded={grounded} cooldown<=0, dx={dx:F2}, dir={dir}, groundAhead={groundAhead}, ignoreLedge={debugIgnoreLedge}");

                if (safe)
                {
                    queuedHopDir = dir;
                    StartWindup();
                }
            }
        }

        // clamp air x speed
        if (!grounded)
        {
            var v = rb.linearVelocity;
            v.x = Mathf.Clamp(v.x, -airMaxXSpeed, airMaxXSpeed);
            rb.linearVelocity = v;
        }
    }

    private void FixedUpdate()
    {
        // Only move during hops; once grounded & not winding, actively brake X to zero.
        if (grounded && windupTimer <= 0f)
        {
            var v = rb.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, 0f, groundBrake * Time.fixedDeltaTime);
            rb.linearVelocity = v;
        }
        // No continuous forces otherwise; jump sets velocity directly.
    }

    private void StartWindup()
    {
        windupTimer = windupTime;
        transform.localScale = new Vector3(
            Mathf.Sign(transform.localScale.x) * Mathf.Abs(baseScale.x),
            baseScale.y,
            baseScale.z
        );
        if (debugLogs) Debug.Log("[JumpingEnemyBrain] StartWindup()");
    }

    private void DoHop(int dir)
    {
        // short wall feeler at chest height
        Vector2 center = col.bounds.center;
        Vector2 ext = col.bounds.extents;
        Vector2 chestOrigin = new Vector2(center.x, center.y + ext.y * 0.2f);
        bool wallAhead = Physics2D.Raycast(chestOrigin, new Vector2(dir, 0f), wallCheckDistance, groundLayer);

        float xSpeed = hopSpeed * dir;
        if (wallAhead) xSpeed *= 0.75f;

        Vector2 v = rb.linearVelocity;
        v.x = xSpeed;
        v.y = Mathf.Max(v.y, jumpVelocity);
        rb.linearVelocity = v;

        hopCooldownTimer = Random.Range(minHopCooldown, maxHopCooldown);

        if (debugLogs)
            Debug.Log($"[JumpingEnemyBrain] DoHop dir={dir}, x={v.x:F2}, y={v.y:F2}, wallAhead={wallAhead}, nextCD={hopCooldownTimer:F2}s");
    }

    private bool HasGroundAhead(int dir)
    {
        // Directional ledge probe from the collider’s LEADING EDGE, near the BOTTOM.
        Vector2 center = col.bounds.center;
        Vector2 ext = col.bounds.extents;

        // Horizontal: slightly beyond the forward edge
        float x = center.x + dir * (ext.x + probeSideMargin);

        // Vertical: near the collider bottom, with tweak offsets
        float bottomY = center.y - ext.y;
        float y = bottomY + probeBottomOffset + probeDropOffset;

        Vector2 origin = new Vector2(x, y);

        // Pick a ray length that's definitely long enough for different sizes
        float minRay = ext.y * 0.75f + 0.1f; // robust for circle/capsule/box
        float rayLen = Mathf.Max(ledgeProbeDistance, minRay);

        bool groundAhead = Physics2D.Raycast(origin, Vector2.down, rayLen, groundLayer);

        // Cache for safe gizmo drawing
        dbgProbeOrigin = origin;
        dbgProbeLen = rayLen;
        dbgProbeHit = groundAhead;
        dbgProbeDir = dir;

        if (debugLogs)
            Debug.Log($"[JumpingEnemyBrain] Probe dir={dir}, origin=({origin.x:F2},{origin.y:F2}), rayLen={rayLen:F2}, groundAhead={groundAhead}");

        return groundAhead;
    }

    private void OnDrawGizmosSelected()
    {
        // GroundCheck gizmo
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // Draw the last ledge probe (captured during Update)
        if (dbgProbeLen > 0f)
        {
            Gizmos.color = dbgProbeHit ? Color.green : Color.cyan;
            Vector3 a = new Vector3(dbgProbeOrigin.x, dbgProbeOrigin.y, 0f);
            Vector3 b = new Vector3(dbgProbeOrigin.x, dbgProbeOrigin.y - dbgProbeLen, 0f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a + new Vector3(0.06f * Mathf.Sign(dbgProbeDir), 0f, 0f), 0.02f);
        }

        // Optional: draw chest wall feeler
        if (col != null && player != null)
        {
            Vector2 center = col.bounds.center;
            Vector2 ext = col.bounds.extents;
            float dir = Mathf.Sign(player.position.x - transform.position.x);
            if (dir == 0f) dir = 1f;

            Vector3 chest = new Vector3(center.x, center.y + ext.y * 0.2f, 0f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(chest, chest + new Vector3(dir * wallCheckDistance, 0f, 0f));
        }
    }
}
