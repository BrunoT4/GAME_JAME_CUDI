using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FlyingEnemyBrain : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;       // drag your Player here

    [Header("Motion")]
    [SerializeField] private float maxSpeed = 5f;    // top speed
    [SerializeField] private float acceleration = 20f;     // how fast we speed up
    [SerializeField] private float braking = 30f;           // how fast we slow down near target
    [SerializeField] private float stopDistance = 0.9f;     // don't enter this radius around player
    [SerializeField] private float slowDownDistance = 3.0f; // start blending to "braking" inside this radius

    [Header("Facing")]
    [SerializeField] private bool faceMovement = true;      // flip to face velocity on X

    [Header("Obstacle Avoidance")]
    [Tooltip("Layers to treat as obstacles (usually your Ground/Wall layers).")]
    [SerializeField] private LayerMask obstacleLayers;
    [Tooltip("How far ahead to look for obstacles.")]
    [SerializeField] private float avoidRayDistance = 1.5f;
    [Tooltip("Side feelers offset from center (left/right) to anticipate corners.")]
    [SerializeField] private float avoidSideOffset = 0.35f;
    [Tooltip("How strongly we steer away when something is detected.")]
    [SerializeField] private float avoidStrength = 4f;

    [Header("Hover (optional)")]
    [SerializeField] private bool enableHover = true;
    [SerializeField] private float hoverAmplitude = 0.25f;
    [SerializeField] private float hoverFrequency = 2.0f;

    private Rigidbody2D rb;
    private float hoverPhase;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // Flying body: turn off gravity
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        Vector2 pos = rb.position;
        Vector2 toPlayer = (Vector2)player.position - pos;
        float dist = toPlayer.magnitude;

        // --- Desired velocity toward the player ---
        Vector2 desiredVel = Vector2.zero;

        if (dist > stopDistance)
        {
            // Within slowDownDistance we blend top speed down linearly (arrival behavior)
            float t = Mathf.InverseLerp(stopDistance, slowDownDistance, dist);
            float targetSpeed = Mathf.Lerp(0f, maxSpeed, t);
            desiredVel = toPlayer.normalized * targetSpeed;
        }
        // else desiredVel = zero (idle / circle)

        // ===== Optional Hover =====
        if (enableHover)
        {
            hoverPhase += Time.fixedDeltaTime * hoverFrequency * 2f * Mathf.PI;
            float bob = Mathf.Sin(hoverPhase) * hoverAmplitude;
            desiredVel += new Vector2(0f, bob); // adds a gentle up/down drift
        }

        // ===== Obstacle Avoidance (simple) =====
        // If moving, cast a forward ray and two side feelers; steer away from hits.
        Vector2 fwd = (desiredVel.sqrMagnitude > 0.0001f) ? desiredVel.normalized : toPlayer.normalized;
        Vector2 right = new Vector2(fwd.y, -fwd.x);  // 90� clockwise
        Vector2 left = -right;

        Vector2 avoid = Vector2.zero;

        // Center ray
        var hitC = Physics2D.Raycast(pos, fwd, avoidRayDistance, obstacleLayers);
        if (hitC.collider != null)
        {
            // steer perpendicular away from obstacle (use surface normal too)
            avoid += hitC.normal * avoidStrength;
        }

        // Side rays (to reduce snagging corners)
        var hitR = Physics2D.Raycast(pos + right * avoidSideOffset, fwd, avoidRayDistance, obstacleLayers);
        if (hitR.collider != null) avoid += left * avoidStrength;

        var hitL = Physics2D.Raycast(pos + left * avoidSideOffset, fwd, avoidRayDistance, obstacleLayers);
        if (hitL.collider != null) avoid += right * avoidStrength;

        if (avoid != Vector2.zero)
        {
            // Blend avoidance into desired velocity (weighted)
            desiredVel += avoid;
        }

        // ===== Apply acceleration/braking =====
        Vector2 v = rb.linearVelocity;

        // Choose accel based on whether we need to speed up or slow down
        float accel = (desiredVel.magnitude < v.magnitude && dist < slowDownDistance) ? braking : acceleration;

        // Smooth towards desired velocity
        Vector2 newV = Vector2.MoveTowards(v, desiredVel, accel * Time.fixedDeltaTime);

        // Clamp absolute top speed (safety)
        if (newV.magnitude > maxSpeed) newV = newV.normalized * maxSpeed;

        rb.linearVelocity = newV;

        // ===== Face movement (flip X) =====
        if (faceMovement && Mathf.Abs(rb.linearVelocity.x) > 0.01f)
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Sign(rb.linearVelocity.x) * Mathf.Abs(s.x);
            transform.localScale = s;
        }
    }

#if UNITY_EDITOR
private static Vector3 V3(Vector2 v) => new Vector3(v.x, v.y, 0f);

private void OnDrawGizmosSelected()
{
    // Stop/slow rings around the player (fine as-is; uses Vector3)
    if (player != null)
    {
        UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.6f);
        UnityEditor.Handles.DrawWireDisc(player.position, Vector3.forward, stopDistance);

        UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.4f);
        UnityEditor.Handles.DrawWireDisc(player.position, Vector3.forward, slowDownDistance);
    }

    // Draw avoidance rays (convert Vector2 -> Vector3)
    if (Application.isPlaying && rb != null)
    {
        Vector2 pos = rb.position;
        Vector2 fwd = (rb.linearVelocity.sqrMagnitude > 0.001f) ? rb.linearVelocity.normalized : Vector2.right;
        Vector2 right = new Vector2(fwd.y, -fwd.x);
        Vector2 left  = -right;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(V3(pos), V3(pos + fwd * avoidRayDistance));

        Gizmos.color = Color.blue;
        Vector2 pR = pos + right * avoidSideOffset;
        Vector2 pL = pos + left  * avoidSideOffset;
        Gizmos.DrawLine(V3(pR), V3(pR + fwd * avoidRayDistance));
        Gizmos.DrawLine(V3(pL), V3(pL + fwd * avoidRayDistance));
    }
}
#endif
}