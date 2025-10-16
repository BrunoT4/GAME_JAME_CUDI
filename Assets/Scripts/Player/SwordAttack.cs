using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class SwordAttack : MonoBehaviour
{
    public enum AttackDir { Up, Down, Right, Left }

    [Header("Wiring")]
    [SerializeField] private PlayerMovement movement;
    public PlayerMovement Movement => movement;
    [SerializeField] Transform groundCheck;        // reuse player groundCheck
    [SerializeField] LayerMask groundLayer;
    [SerializeField] SpriteRenderer sprite;        // your Visual's SpriteRenderer
    [SerializeField] SwordHitbox hitbox;           // child with BoxCollider2D (isTrigger)

    [Header("Attack")]
    [SerializeField] int damage = 1;
    [SerializeField] float attackActiveTime = 0.12f; // active hit frames
    [SerializeField] float attackCooldown = 0.18f;   // time before next attack

    [Header("Hitbox Shapes (local offsets/sizes)")]
    [SerializeField] Vector2 rightOffset = new(0.55f, 0.05f);
    [SerializeField] Vector2 rightSize = new(0.90f, 0.60f);

    // Left will mirror Right automatically; override if you want different shape
    [SerializeField] bool mirrorLeftFromRight = true;
    [SerializeField] Vector2 leftOffsetOverride;
    [SerializeField] Vector2 leftSizeOverride;

    [SerializeField] Vector2 upOffset = new(0.00f, 0.90f);
    [SerializeField] Vector2 upSize = new(0.70f, 0.90f);

    [SerializeField] Vector2 downOffset = new(0.00f, -0.90f);
    [SerializeField] Vector2 downSize = new(0.70f, 0.90f);

    [Header("Pogo")]
    [SerializeField] float pogoUpVelocity = 12f;     // how high to bounce
    [SerializeField] float pogoControlLock = 0.06f;  // tiny lock so bounce feels crisp
    [SerializeField] bool requireAirForDown = true; // if true, down attacks only in air

    [Header("On-Hit Effects (enemy)")]
    [SerializeField] float enemyKBHorizontalDist = 1.5f;
    [SerializeField] float enemyKBDuration = 0.20f;
    [SerializeField] float enemyStunDuration = 0.15f;
    [SerializeField] float enemyKBMinHoriz = 0.65f;
    [SerializeField] float enemyKBUpBias = 0.10f;
    [SerializeField] float enemyKBMaxVertical = 10f;

    [Header("Aiming")]
    [SerializeField] float aimDeadzone = 0.35f;   // input threshold to pick a direction
    [SerializeField] bool useMoveXToTrackFacing = true;

    private PlayerControls controls;
    private Rigidbody2D rb;
    private bool attacking;
    private bool cooling;
    private int lastFacing = 1; // +1 = right, -1 = left

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!movement) movement = GetComponent<PlayerMovement>();
        controls = new PlayerControls();
    }

    void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.Attack.started += _ => TryAttack();
    }

    void OnDisable()
    {
        controls.Player.Attack.started -= _ => TryAttack();
        controls.Player.Disable();
    }

void Update()
{
    Vector2 mv = controls.Player.Move.ReadValue<Vector2>();

    // Prefer sprite.flipX if available
    if (sprite != null)
    {
        lastFacing = sprite.flipX ? -1 : 1;
    }
    else if (useMoveXToTrackFacing && Mathf.Abs(mv.x) > 0.05f)
    {
        lastFacing = mv.x > 0f ? 1 : -1;
    }
}


    bool IsGrounded()
    {
        if (!groundCheck) return false;
        float r = movement ? movement.GetGroundCheckRadius() : 0.2f;
        return Physics2D.OverlapCircle(groundCheck.position, r, groundLayer);
    }

    void TryAttack()
    {
        if (attacking || cooling) return;
        if (movement && movement.IsControlLocked()) return; // block during hitstun/locks

        AttackDir dir = GetDesiredDirection();
        StartCoroutine(DoAttack(dir));
    }

    AttackDir GetDesiredDirection()
    {
        Vector2 mv = controls.Player.Move.ReadValue<Vector2>();
        bool grounded = IsGrounded();

        // Vertical has priority (Up > Down > Horizontal)
        if (mv.y > aimDeadzone) return AttackDir.Up;
        if (mv.y < -aimDeadzone && (!requireAirForDown || !grounded)) return AttackDir.Down;

        // Horizontal
        if (mv.x > aimDeadzone) return AttackDir.Right;
        if (mv.x < -aimDeadzone) return AttackDir.Left;

        // No aim input → use last facing
        return (lastFacing >= 0) ? AttackDir.Right : AttackDir.Left;
    }

    IEnumerator DoAttack(AttackDir dir)
    {
        attacking = true;

        // enable & configure hitbox
        hitbox.gameObject.SetActive(true);
        hitbox.owner = this;
        hitbox.damage = damage;
        hitbox.enablePogo = (dir == AttackDir.Down);

        // pass on-hit effects (enemy knockback + stun) to this swing
        hitbox.kbHorizontalDist = enemyKBHorizontalDist;
        hitbox.kbDuration = enemyKBDuration;
        hitbox.stunDuration = enemyStunDuration;
        hitbox.kbMinHoriz = enemyKBMinHoriz;
        hitbox.kbUpBias = enemyKBUpBias;
        hitbox.kbMaxVertical = enemyKBMaxVertical;

        // choose box (local)
        Vector2 size, offset;
        switch (dir)
        {
            case AttackDir.Up:
                size = upSize; offset = upOffset; break;
            case AttackDir.Down:
                size = downSize; offset = downOffset; break;
            case AttackDir.Left:
                if (mirrorLeftFromRight)
                {
                    size = rightSize;
                    offset = new Vector2(-rightOffset.x, rightOffset.y); // mirror X
                }
                else
                {
                    size = leftSizeOverride;
                    offset = leftOffsetOverride;
                }
                break;
            default: // Right
                size = rightSize; offset = rightOffset; break;
        }

        // --- ensure the hitbox matches player facing ---
        if (lastFacing < 0 && (dir == AttackDir.Right || dir == AttackDir.Left))
        {
            offset.x = -Mathf.Abs(offset.x);
        }
        else if (lastFacing > 0 && (dir == AttackDir.Right || dir == AttackDir.Left))
        {
            offset.x = Mathf.Abs(offset.x);
        }

        if (dir == AttackDir.Right && lastFacing < 0)
        {
            offset.x = -offset.x;
        }

        hitbox.SetLocalBox(offset, size);

        // Optional: lock movement during active frames so swing feels snappy
        movement?.ApplyControlLock(attackActiveTime);

        // active frames
        hitbox.SetActive(true);
        yield return new WaitForSeconds(attackActiveTime);
        hitbox.SetActive(false);

        // cleanup & cooldown
        hitbox.gameObject.SetActive(false);
        attacking = false;

        cooling = true;
        yield return new WaitForSeconds(attackCooldown);
        cooling = false;
    }

    // called by hitbox when we land a DOWN hit
    public void DoPogoBounce(Vector2 hitPoint)
    {
        if (movement)
        {
            movement.PogoBounce(pogoUpVelocity, pogoControlLock);
        }
        else
        {
            // fallback if movement not wired
            var v = rb.linearVelocity;
            v.y = Mathf.Max(v.y, pogoUpVelocity);
            rb.linearVelocity = v;
        }
    }

    // helpers (optional)
    public bool IsAttacking() => attacking;
    public float GetAttackCooldown() => attackCooldown;
}
