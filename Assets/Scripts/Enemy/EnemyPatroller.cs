using UnityEngine;

public class EnemyPatroller : MonoBehaviour
{
    [SerializeField] private Rigidbody2D theRB;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float waitAtPoints;
    [SerializeField] private float jumpForce;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private Animator anim;

    [Header("Wall jump")]
    [Tooltip("Feet position for grounded check before wall jump. Uses offset below root if empty.")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private float wallJumpCooldown = 0.35f;

    private int currentPatrolPoint;
    private float waitCounter;
    private bool isTouchingWall;
    private float _lastWallJumpTime = -100f;
    private EnemyKnockback _knockback;

    void Start()
    {
        waitCounter = waitAtPoints;
        _knockback = GetComponent<EnemyKnockback>();

        foreach (Transform pPoint in patrolPoints)
            pPoint.SetParent(null);
    }

    void Update()
    {
        if (Mathf.Abs(transform.position.x - patrolPoints[currentPatrolPoint].position.x) > 0.2f)
        {
            if (transform.position.x < patrolPoints[currentPatrolPoint].position.x)
            {
                theRB.linearVelocity = new Vector2(moveSpeed, theRB.linearVelocity.y);
                transform.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                theRB.linearVelocity = new Vector2(-moveSpeed, theRB.linearVelocity.y);
                transform.localScale = Vector3.one;
            }

            TryWallJump();
        }
        else
        {
            theRB.linearVelocity = new Vector2(0f, theRB.linearVelocity.y);
            waitCounter -= Time.deltaTime;

            if (waitCounter <= 0)
            {
                waitCounter = waitAtPoints;
                currentPatrolPoint++;

                if (currentPatrolPoint >= patrolPoints.Length)
                    currentPatrolPoint = 0;
            }
        }

        anim.SetFloat("speed", Mathf.Abs(theRB.linearVelocity.x));
    }

    void TryWallJump()
    {
        if (!isTouchingWall)
            return;

        if (_knockback != null && _knockback.IsActive)
            return;

        if (!IsGrounded())
            return;

        if (Mathf.Abs(theRB.linearVelocity.y) >= 0.01f)
            return;

        if (Time.time < _lastWallJumpTime + wallJumpCooldown)
            return;

        theRB.linearVelocity = new Vector2(theRB.linearVelocity.x, jumpForce);
        _lastWallJumpTime = Time.time;
    }

    bool IsGrounded()
    {
        Vector2 point = groundCheck != null
            ? groundCheck.position
            : (Vector2)transform.position + Vector2.down * 0.6f;

        return Physics2D.OverlapCircle(point, groundCheckRadius, groundLayers);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
            isTouchingWall = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
            isTouchingWall = false;
    }
}
