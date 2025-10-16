using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] float moveSpeed = 6f;

    [Header("Jump (variable height)")]
    [SerializeField] float jumpVelocity = 12f;   // from ground
    [SerializeField] float maxHoldTime = 0.4f;
    [SerializeField] float normalGravity = 3.0f;
    [SerializeField] float holdGravity = 1.0f;
    [SerializeField] float jumpCutGravity = 6.0f;

    [Header("Air Jump (double jump)")]
    [SerializeField] int maxAirJumps = 1;      // 1 = double jump
    [SerializeField] float airJumpVelocity = 12f;

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck;      // assign child
    [SerializeField] float groundCheckRadius = 0.2f;
    [SerializeField] LayerMask groundLayer;

    [Header("Stability")]
    [SerializeField] float groundLockTime = 0.08f; // ignore ground briefly after jump

    // ---------- WALL SETTINGS ----------
    [Header("Wall (slide & jump)")]
    [SerializeField] LayerMask wallLayer;
    [SerializeField] float wallCheckOffsetX = 0.4f;
    [SerializeField] float wallCheckRadius = 0.25f;
    [SerializeField] float wallCoyoteTime = 0.12f;
    [SerializeField] float maxWallSlideSpeed = -4.5f;
    [SerializeField] float wallSlideGravity = 1.5f;

    [Header("Wall Jump Impulse")]
    [SerializeField] float wallJumpUpVelocity = 12f;
    [SerializeField] float wallJumpHorizontalVelocity = 9f;
    [SerializeField] float wallJumpControlLock = 0.12f;

    [Header("Hitstun / Control Lock")]
    [SerializeField] float hitstunControlLock = 0.15f; // default if ApplyControlLock called with <= 0

    private Rigidbody2D rb;
    private PlayerControls controls;
    private Vector2 moveInput;

    private bool isJumping;
    private float heldTime;
    private float groundLockTimer;

    // ---------- AIR JUMP STATE ----------
    private int airJumpsLeft;

    // ---------- WALL STATE ----------
    private bool onWall;
    private int wallDir;                 // -1 = left wall, +1 = right wall
    private float lastOnWallTime;
    private float wallJumpLockTimer;

    // ---------- HITSTUN ----------
    private float hitstunTimer = 0f;

    private void Awake()
    {
        controls = new PlayerControls();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        controls.Player.Enable();

        controls.Player.Move.performed += c => moveInput = c.ReadValue<Vector2>();
        controls.Player.Move.canceled += _ => moveInput = Vector2.zero;

        // Jump action should be bound to Space in your Input Actions
        controls.Player.Jump.started += _ => TryStartJumpOrWallJumpOrAirJump(); // press
        controls.Player.Jump.canceled += _ => OnJumpReleased();                  // release
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }

    private void Start()
    {
        rb.freezeRotation = true;
        rb.gravityScale = normalGravity;
        airJumpsLeft = maxAirJumps;
    }

    private void Update()
    {
        if (!groundCheck) return;

        // --- timers ---
        if (groundLockTimer > 0f) groundLockTimer -= Time.deltaTime;
        if (wallJumpLockTimer > 0f) wallJumpLockTimer -= Time.deltaTime;
        if (lastOnWallTime > 0f) lastOnWallTime -= Time.deltaTime;
        if (hitstunTimer > 0f) hitstunTimer -= Time.deltaTime;

        // --- ground ---
        bool rawGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        bool grounded = rawGrounded && rb.linearVelocity.y <= 0.01f && groundLockTimer <= 0f;

        if (grounded)
        {
            airJumpsLeft = maxAirJumps;

            if (!isJumping)
            {
                heldTime = 0f;
                rb.gravityScale = normalGravity;
            }
        }

        // --- WALL DETECTION ---
        Vector2 pos = transform.position;
        Vector2 leftPos = pos + Vector2.left * wallCheckOffsetX;
        Vector2 rightPos = pos + Vector2.right * wallCheckOffsetX;

        bool hitLeft = Physics2D.OverlapCircle(leftPos, wallCheckRadius, wallLayer);
        bool hitRight = Physics2D.OverlapCircle(rightPos, wallCheckRadius, wallLayer);

        if (hitLeft ^ hitRight)
        {
            onWall = true;
            wallDir = hitLeft ? -1 : 1;
            lastOnWallTime = wallCoyoteTime;
        }
        else if (hitLeft && hitRight)
        {
            onWall = true;
            wallDir = moveInput.x >= 0f ? 1 : -1;
            lastOnWallTime = wallCoyoteTime;
        }
        else
        {
            onWall = false;
        }

        // --- WALL SLIDE ---
        bool pushingIntoWall = (wallDir == -1 && moveInput.x < -0.01f) || (wallDir == 1 && moveInput.x > 0.01f);
        bool eligibleForSlide = !grounded && (onWall || lastOnWallTime > 0f) && pushingIntoWall && wallJumpLockTimer <= 0f;

        if (eligibleForSlide)
        {
            isJumping = false; // cancel variable-jump shaping
            rb.gravityScale = wallSlideGravity;

            if (rb.linearVelocity.y < maxWallSlideSpeed)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxWallSlideSpeed);
        }
        else if (!isJumping)
        {
            rb.gravityScale = normalGravity;
        }

        // --- VARIABLE HEIGHT ---
        bool jumpHeld = controls.Player.Jump.IsPressed();
        if (isJumping && rb.linearVelocity.y > 0f)
        {
            if (jumpHeld && heldTime < maxHoldTime)
            {
                rb.gravityScale = holdGravity;
                heldTime += Time.deltaTime;
            }
            else
            {
                rb.gravityScale = normalGravity;
            }
        }
        else if (isJumping && rb.linearVelocity.y <= 0f)
        {
            isJumping = false;
            rb.gravityScale = normalGravity;
        }

        // --- fallback ground jump if event missed (SPACE ONLY; W removed) ---
        if (!isJumping && grounded && hitstunTimer <= 0f &&
            (Keyboard.current?.spaceKey.wasPressedThisFrame == true))
        {
            StartGroundJump();
        }
    }

    private void FixedUpdate()
    {
        // Do NOT overwrite X velocity while control-locked (hitstun or wall-jump lock).
        bool controlLocked = (wallJumpLockTimer > 0f || hitstunTimer > 0f);

        if (!controlLocked)
        {
            float xInput = moveInput.x;
            rb.linearVelocity = new Vector2(xInput * moveSpeed, rb.linearVelocity.y);
        }
        // else: preserve current rb.linearVelocity.x (knockback/wall-jump impulse)
    }

    // ------------ JUMP HANDLERS ------------
    private void TryStartJumpOrWallJumpOrAirJump()
    {
        // Block any jump starts during hitstun
        if (hitstunTimer > 0f) return;

        bool grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) && rb.linearVelocity.y <= 0.01f;
        bool canWallJump = (onWall || lastOnWallTime > 0f) && wallJumpLockTimer <= 0f;

        if (canWallJump && !grounded)
        {
            StartWallJump();
            return;
        }

        if (grounded && !isJumping)
        {
            StartGroundJump();
            return;
        }

        // AIR JUMP
        if (airJumpsLeft > 0)
        {
            StartAirJump();
            airJumpsLeft--;
            return;
        }
    }

    private void StartGroundJump()
    {
        isJumping = true;
        heldTime = 0f;

        var v = rb.linearVelocity;
        if (v.y < 0f) v.y = 0f;
        v.y = jumpVelocity;
        rb.linearVelocity = v;

        rb.gravityScale = holdGravity;
        groundLockTimer = groundLockTime;
    }

    private void StartAirJump()
    {
        isJumping = true;
        heldTime = 0f;

        var v = rb.linearVelocity;
        if (v.y < 0f) v.y = 0f;
        v.y = airJumpVelocity;
        rb.linearVelocity = v;

        rb.gravityScale = holdGravity;
    }

    private void StartWallJump()
    {
        int dir = (wallDir != 0) ? -wallDir : (moveInput.x >= 0f ? 1 : -1);
        float vx = dir * wallJumpHorizontalVelocity;
        float vy = wallJumpUpVelocity;

        var v = rb.linearVelocity;
        v.x = vx;
        v.y = vy;
        rb.linearVelocity = v;

        wallJumpLockTimer = wallJumpControlLock;

        isJumping = true;
        heldTime = 0f;
        rb.gravityScale = holdGravity;

        groundLockTimer = groundLockTime;
        lastOnWallTime = 0f;
    }

    private void OnJumpReleased()
    {
        if (rb.linearVelocity.y > 0f)
            rb.gravityScale = jumpCutGravity; // short hop on release
    }

    // ----- Called by PlayerHealth on hit -----
    public void ApplyControlLock(float duration)
    {
        if (duration <= 0f) duration = hitstunControlLock;
        hitstunTimer = Mathf.Max(hitstunTimer, duration);
    }

    // ===== POGO SUPPORT =====
    public void PogoBounce(float upVelocity, float lockTime = 0.05f)
    {
        var v = rb.linearVelocity;
        v.y = Mathf.Max(v.y, upVelocity);
        rb.linearVelocity = v;

        ApplyControlLock(lockTime);
        // Optional: give back an air jump after pogo
        // airJumpsLeft = Mathf.Max(airJumpsLeft, 1);
    }

    public bool IsControlLocked() => (hitstunTimer > 0f || wallJumpLockTimer > 0f);
    public float GetGroundCheckRadius() => groundCheckRadius;

    private void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        Gizmos.color = Color.cyan;
        Vector2 pos = transform.position;
        Gizmos.DrawWireSphere(pos + Vector2.left * wallCheckOffsetX, wallCheckRadius);
        Gizmos.DrawWireSphere(pos + Vector2.right * wallCheckOffsetX, wallCheckRadius);
    }
}
