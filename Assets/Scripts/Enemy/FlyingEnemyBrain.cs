using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FlyingEnemyBrain : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;       // drag your Player here

    [Header("Motion")]
    [SerializeField] private float maxSpeed = 5f;           // top chase speed
    [SerializeField] private float acceleration = 20f;      // chase accel
    [SerializeField] private float braking = 30f;           // near-target braking
    [SerializeField] private float stopDistance = 0.9f;     // don't enter this radius
    [SerializeField] private float slowDownDistance = 3.0f; // start arrival here

    [Header("Facing")]
    [SerializeField] private bool faceMovement = true;      // flip X to face velocity

    [Header("Obstacle Avoidance")]
    [Tooltip("Layers to treat as obstacles (usually Ground/Wall).")]
    [SerializeField] private LayerMask obstacleLayers;
    [Tooltip("How far ahead to look for obstacles.")]
    [SerializeField] private float avoidRayDistance = 1.5f;
    [Tooltip("Side feelers offset from center (left/right).")]
    [SerializeField] private float avoidSideOffset = 0.35f;
    [Tooltip("How strongly we steer away when something is detected.")]
    [SerializeField] private float avoidStrength = 4f;

    [Header("Hover (optional)")]
    [SerializeField] private bool enableHover = true;
    [SerializeField] private float hoverAmplitude = 0.25f;
    [SerializeField] private float hoverFrequency = 2.0f;

    [Header("Weave (waver while chasing)")]
    [SerializeField] private bool enableWeave = true;
    [Tooltip("Sideways m/s added (perpendicular to forward).")]
    [SerializeField] private float weaveAmplitude = 0.75f;
    [Tooltip("Oscillations per second.")]
    [SerializeField] private float weaveFrequency = 1.2f;

    [Header("Dash Attack")]
    [SerializeField] private bool enableDash = true;
    [Tooltip("Only dash if player distance is within this range.")]
    [SerializeField] private float dashRangeMin = 1.2f;
    [SerializeField] private float dashRangeMax = 6.0f;
    [Tooltip("Optional LOS check to player before dashing.")]
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private float telegraphTime = 0.35f;   // pause before dash
    [SerializeField] private float dashDuration = 0.45f;    // time spent charging
    [SerializeField] private float dashSpeed = 12f;         // dash speed
    [SerializeField] private float recoverTime = 0.30f;     // short cooldown after dash
    [Tooltip("Seconds between dashes (random in [min,max]).")]
    [SerializeField] private Vector2 dashCooldownRange = new Vector2(2.5f, 4.5f);

    private Rigidbody2D rb;
    private float hoverPhase;
    private float weavePhase;

    private enum State { Chase, Telegraph, Dashing, Recover }
    private State state = State.Chase;
    private float stateEndTime;
    private float nextDashTime;
    private Vector2 dashDir;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        if (player == null)
        {
            var pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }

        ScheduleNextDash();
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        float dt = Time.fixedDeltaTime;
        Vector2 pos = rb.position;
        Vector2 toPlayer = (Vector2)player.position - pos;
        float dist = toPlayer.magnitude;

        // === State transitions (telegraph → dash → recover) ===
        if (enableDash)
        {
            switch (state)
            {
                case State.Chase:
                    if (Time.time >= nextDashTime &&
                        dist >= dashRangeMin && dist <= dashRangeMax &&
                        (!requireLineOfSight || HasLineOfSight(pos, (Vector2)player.position)))
                    {
                        // Lock direction at telegraph start
                        dashDir = (dist > 0.001f ? toPlayer.normalized : Vector2.right);
                        state = State.Telegraph;
                        stateEndTime = Time.time + telegraphTime;
                        rb.linearVelocity = Vector2.zero; // pause/telegraph
                    }
                    break;

                case State.Telegraph:
                    if (Time.time >= stateEndTime)
                    {
                        state = State.Dashing;
                        stateEndTime = Time.time + dashDuration;
                        rb.linearVelocity = dashDir * dashSpeed;
                    }
                    break;

                case State.Dashing:
                    // Maintain fixed dash velocity (don’t steer/avoid in dash)
                    rb.linearVelocity = dashDir * dashSpeed;
                    if (Time.time >= stateEndTime)
                    {
                        state = State.Recover;
                        stateEndTime = Time.time + recoverTime;
                        rb.linearVelocity = Vector2.zero;
                        ScheduleNextDash();
                    }
                    break;

                case State.Recover:
                    if (Time.time >= stateEndTime)
                        state = State.Chase;
                    break;
            }
        }

        // Skip chase steering while actively dashing
        if (state == State.Dashing || state == State.Telegraph)
        {
            FaceByVelocity();
            return;
        }

        // === Desired velocity toward the player (arrival) ===
        Vector2 desiredVel = Vector2.zero;

        if (dist > stopDistance)
        {
            float t = Mathf.InverseLerp(stopDistance, slowDownDistance, dist);
            float targetSpeed = Mathf.Lerp(0f, maxSpeed, t);
            Vector2 fwd = (dist > 0.001f ? toPlayer / dist : Vector2.right);
            desiredVel = fwd * targetSpeed;

            // Optional Hover (gentle vertical bob)
            if (enableHover)
            {
                hoverPhase += dt * hoverFrequency * 2f * Mathf.PI;
                float bob = Mathf.Sin(hoverPhase) * hoverAmplitude;      // units: m/s added
                desiredVel += new Vector2(0f, bob);
            }

            // Optional Weave (side-to-side waver)
            if (enableWeave && targetSpeed > 0.01f)
            {
                weavePhase += dt * weaveFrequency * 2f * Mathf.PI;
                Vector2 right = new Vector2(fwd.y, -fwd.x);
                float sway = Mathf.Sin(weavePhase) * weaveAmplitude;     // m/s sideways
                desiredVel += right * sway;
            }
        }

        // === Obstacle Avoidance (only when steering normally) ===
        Vector2 steeringFwd = (desiredVel.sqrMagnitude > 0.0001f) ? desiredVel.normalized : (dist > 0.001f ? toPlayer.normalized : Vector2.right);
        Vector2 rightPerp = new Vector2(steeringFwd.y, -steeringFwd.x);
        Vector2 leftPerp = -rightPerp;

        Vector2 avoid = Vector2.zero;

        // Center ray
        var hitC = Physics2D.Raycast(pos, steeringFwd, avoidRayDistance, obstacleLayers);
        if (hitC.collider != null) avoid += hitC.normal * avoidStrength;

        // Side feelers
        var hitR = Physics2D.Raycast(pos + rightPerp * avoidSideOffset, steeringFwd, avoidRayDistance, obstacleLayers);
        if (hitR.collider != null) avoid += leftPerp * avoidStrength;

        var hitL = Physics2D.Raycast(pos + leftPerp * avoidSideOffset, steeringFwd, avoidRayDistance, obstacleLayers);
        if (hitL.collider != null) avoid += rightPerp * avoidStrength;

        if (avoid != Vector2.zero)
            desiredVel += avoid;

        // === Apply acceleration/braking to reach desiredVel ===
        Vector2 v = rb.linearVelocity;
        float accel = (desiredVel.magnitude < v.magnitude && dist < slowDownDistance) ? braking : acceleration;
        Vector2 newV = Vector2.MoveTowards(v, desiredVel, accel * dt);
        if (newV.magnitude > maxSpeed) newV = newV.normalized * maxSpeed;
        rb.linearVelocity = newV;

        FaceByVelocity();
    }

    private void FaceByVelocity()
    {
        if (!faceMovement) return;
        Vector2 v = rb.linearVelocity;
        if (Mathf.Abs(v.x) > 0.01f)
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Sign(v.x) * Mathf.Abs(s.x);
            transform.localScale = s;
        }
    }

    private void ScheduleNextDash()
    {
        float delay = Random.Range(dashCooldownRange.x, dashCooldownRange.y);
        nextDashTime = Time.time + Mathf.Max(0.05f, delay);
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        float len = dir.magnitude;
        if (len < 0.0001f) return true;
        var hit = Physics2D.Raycast(from, dir / len, len, obstacleLayers);
        return hit.collider == null;
    }

#if UNITY_EDITOR
    private static Vector3 V3(Vector2 v) => new Vector3(v.x, v.y, 0f);

    private void OnDrawGizmosSelected()
    {
        // Stop/slow rings around the player
        if (player != null)
        {
            UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.6f);
            UnityEditor.Handles.DrawWireDisc(player.position, Vector3.forward, stopDistance);

            UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.4f);
            UnityEditor.Handles.DrawWireDisc(player.position, Vector3.forward, slowDownDistance);

            // Dash range
            UnityEditor.Handles.color = new Color(0.2f, 1f, 0.2f, 0.35f);
            UnityEditor.Handles.DrawWireDisc(player.position, Vector3.forward, dashRangeMin);
            UnityEditor.Handles.DrawWireDisc(player.position, Vector3.forward, dashRangeMax);
        }

        // Draw avoidance rays
        if (Application.isPlaying && rb != null)
        {
            Vector2 pos = rb.position;
            Vector2 fwd = (rb.linearVelocity.sqrMagnitude > 0.001f) ? rb.linearVelocity.normalized : Vector2.right;
            Vector2 right = new Vector2(fwd.y, -fwd.x);
            Vector2 left = -right;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(V3(pos), V3(pos + fwd * avoidRayDistance));

            Gizmos.color = Color.blue;
            Vector2 pR = pos + right * avoidSideOffset;
            Vector2 pL = pos + left * avoidSideOffset;
            Gizmos.DrawLine(V3(pR), V3(pR + fwd * avoidRayDistance));
            Gizmos.DrawLine(V3(pL), V3(pL + fwd * avoidRayDistance));
        }
    }
#endif
}
