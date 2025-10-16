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

    [Header("Dash / Lunge (horizontal only)")]
    [SerializeField] private bool enableDash = true;
    [SerializeField] private float telegraphTime = 0.2f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashSpeed = 8f;
    [SerializeField] private int burstCount = 3;
    [SerializeField] private float interDashGap = 0.08f;
    [SerializeField] private Vector2 dashCooldownRange = new Vector2(2.5f, 4.5f);
    [SerializeField] private bool onlyDashWhenGrounded = true;
    [SerializeField] private bool requireGroundAheadToDash = true;

    // --- caches ---
    private Rigidbody2D rb;
    private Collider2D col;

    // --- runtime (chase) ---
    private float desiredXVel;

    // --- runtime (dash FSM) ---
    private enum State { Chase, Telegraph, Dashing, InterDash }
    private State state = State.Chase;
    private float stateEndTime = 0f;
    private float nextDashTime = 0f;
    private int dashDirX = 1;     // -1 or +1; locked at dash start
    private int dashIndex = 0;    // which dash in the burst (0..burstCount-1)

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    private void Start()
    {
        if (player == null)
        {
            var pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }
        ScheduleNextDash();
    }

    private void FixedUpdate()
    {
        if (player == null || groundCheck == null) return;

        float dt = Time.fixedDeltaTime;

        // --- Sensing ---
        bool grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        float dx = player.position.x - transform.position.x;
        float dirToPlayer = Mathf.Sign(dx);
        if (dirToPlayer == 0f) dirToPlayer = 1f;

        // Use per-direction ground-ahead helper
        bool groundAheadTowardPlayer = GroundAhead((int)dirToPlayer);

        // --- Dash FSM ---
        if (enableDash)
        {
            switch (state)
            {
                case State.Chase:
                    if (Time.time >= nextDashTime &&
                        (!onlyDashWhenGrounded || grounded) &&
                        (!requireGroundAheadToDash || groundAheadTowardPlayer) &&
                        Mathf.Abs(dx) > stopDistance)
                    {
                        dashDirX = (dx >= 0f) ? 1 : -1;
                        dashIndex = 0;
                        state = State.Telegraph;
                        stateEndTime = Time.time + telegraphTime;
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // pause tell
                    }
                    break;

                case State.Telegraph:
                    if (Time.time >= stateEndTime)
                    {
                        state = State.Dashing;
                        stateEndTime = Time.time + dashDuration;
                        rb.linearVelocity = new Vector2(dashDirX * dashSpeed, rb.linearVelocity.y);
                    }
                    break;

                case State.Dashing:
                    rb.linearVelocity = new Vector2(dashDirX * dashSpeed, rb.linearVelocity.y); // hold speed
                    if (Time.time >= stateEndTime)
                    {
                        dashIndex++;
                        if (dashIndex < burstCount)
                        {
                            state = State.InterDash;
                            stateEndTime = Time.time + interDashGap;
                            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                        }
                        else
                        {
                            ScheduleNextDash();
                            state = State.Chase;
                        }
                    }
                    break;

                case State.InterDash:
                    if (Time.time >= stateEndTime)
                    {
                        // Re-aim next mini-dash
                        dx = player.position.x - transform.position.x;
                        if (dx != 0f) dashDirX = (dx > 0f) ? 1 : -1;

                        // Safety checks should use the ACTUAL dash direction
                        if ((onlyDashWhenGrounded && !grounded) ||
                            (requireGroundAheadToDash && !GroundAhead(dashDirX)))
                        {
                            ScheduleNextDash();
                            state = State.Chase;
                            break;
                        }

                        state = State.Dashing;
                        stateEndTime = Time.time + dashDuration;
                        rb.linearVelocity = new Vector2(dashDirX * dashSpeed, rb.linearVelocity.y);
                    }
                    break;
            }
        }

        // During dash/telegraph, skip normal steering
        if (state == State.Telegraph || state == State.Dashing || state == State.InterDash)
        {
            FaceByDesired(rb.linearVelocity.x != 0 ? rb.linearVelocity.x : dashDirX);
            return;
        }

        // --- Normal chase steering ---
        desiredXVel = 0f;
        bool canMove = grounded || chaseWhileAirborne;

        if (canMove && Mathf.Abs(dx) > stopDistance)
        {
            if (grounded)
            {
                if (groundAheadTowardPlayer) desiredXVel = dirToPlayer * moveSpeed;
            }
            else
            {
                desiredXVel = dirToPlayer * moveSpeed;
            }
            FaceByDesired(desiredXVel);
        }

        float accel = grounded ? acceleration : airAcceleration;
        float newX = Mathf.MoveTowards(rb.linearVelocity.x, desiredXVel, accel * dt);
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
    }

    // ---- Helpers ----
    private bool GroundAhead(int dirX)
    {
        Vector2 center = col.bounds.center;
        Vector2 ext    = col.bounds.extents;
        Vector2 origin = new Vector2(
            center.x + dirX * (ext.x + probeSideMargin),
            center.y - ext.y * 0.2f + probeDropOffset
        );
        return Physics2D.Raycast(origin, Vector2.down, ledgeProbeDistance, groundLayer);
    }

    private void FaceByDesired(float x)
    {
        if (!flipWithScale) return;
        if (Mathf.Abs(x) > 0.01f)
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Sign(x) * Mathf.Abs(s.x);
            transform.localScale = s;
        }
    }

    private void ScheduleNextDash()
    {
        float min = Mathf.Max(0.05f, dashCooldownRange.x);
        float max = Mathf.Max(min, dashCooldownRange.y);
        nextDashTime = Time.time + Random.Range(min, max);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (Application.isPlaying && col != null && player != null)
        {
            float dirToPlayer = Mathf.Sign(player.position.x - transform.position.x);
            if (dirToPlayer == 0f) dirToPlayer = 1f;

            Vector2 center = col.bounds.center;
            Vector2 ext    = col.bounds.extents;

            Vector3 origin = new Vector3(
                center.x + dirToPlayer * (ext.x + probeSideMargin),
                center.y - ext.y * 0.2f + probeDropOffset,
                0f
            );

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.8f);
            UnityEditor.Handles.DrawLine(origin, origin + Vector3.down * ledgeProbeDistance);

            float timeLeft = Mathf.Max(0f, nextDashTime - Time.time);
            if (enableDash && timeLeft < 0.75f)
            {
                UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.6f);
                UnityEditor.Handles.DrawWireArc(transform.position + Vector3.up * 0.6f, Vector3.forward, Vector3.right, Mathf.Lerp(0, 360, 1f - timeLeft / 0.75f), 0.18f);
            }
#endif
        }
    }
#endif
}
