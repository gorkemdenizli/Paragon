using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayerClimbController : MonoBehaviour
{
    [Header("Ladder Climb")]
    [SerializeField] private bool  canClimbLadders                      = true;
    [SerializeField] private float climbSpeed                           = 3f;
    [SerializeField] private bool  disableGravityWhileClimbing          = true;
    [SerializeField] private bool  lockHorizontalMovementWhileClimbing  = false;
    [SerializeField] private bool  snapToLadderX                        = false;
    [SerializeField] private bool  allowJumpOffLadder                   = true;
    [SerializeField] private float ladderJumpOffForce                   = 6f;
    [SerializeField] private bool  topExitSnap                          = true;
    [SerializeField] private bool  bottomExitSnap                       = false;
    [Tooltip("Exit at top when player center is this many units below TopPosition (0 = exact top).")]
    [SerializeField] private float topExitDistance                      = 0f;
    [Tooltip("Exit at bottom when player center is this many units above BottomPosition.")]
    [SerializeField] private float bottomExitDistance                   = 0.1f;

    [Header("Gate")]
    [Tooltip("Non-trigger body collider used for gate IgnoreCollision. Auto-found if empty.")]
    [SerializeField] private Collider2D playerBodyCollider;

    [Header("Input — same actions as PlayerController")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    public bool IsClimbing { get; private set; }
    public bool IsOnLadder => _currentLadder != null;

    private LadderZone  _currentLadder;
    private LadderZone  _topEntryLadder;
    private bool        _onLadderThisPhysicsStep;
    private float       _originalGravity;
    private float       _climbReentryBlock;
    private Rigidbody2D _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (playerBodyCollider == null)
            foreach (var col in GetComponentsInChildren<Collider2D>())
                if (!col.isTrigger) { playerBodyCollider = col; break; }
    }

    void OnDisable() { if (IsClimbing) { RestoreGravity(); _currentLadder?.gate?.RestoreForPlayer(playerBodyCollider); } }
    void OnDestroy() { if (IsClimbing) { RestoreGravity(); _currentLadder?.gate?.RestoreForPlayer(playerBodyCollider); } }

    // ── Trigger detection (tutorial approach) ───────────────────────────────────
    // OnTriggerStay2D fires every physics step while inside trigger.
    // No Enter/Exit — avoids spurious cancel events.

    void OnTriggerStay2D(Collider2D other)
    {
        if (!canClimbLadders) return;
        var lz = other.GetComponent<LadderZone>();
        if (lz == null || !lz.IsValid) return;
        _onLadderThisPhysicsStep = true;
        _currentLadder = lz;
    }

    // ── Update — input-driven state transitions ──────────────────────────────────

    void Update()
    {
        if (_climbReentryBlock > 0f) _climbReentryBlock -= Time.deltaTime;

        if (!canClimbLadders) return;

        if (!IsClimbing)
        {
            if (_currentLadder != null && _climbReentryBlock <= 0f && Mathf.Abs(GetVerticalInput()) > 0.1f)
                EnterClimbing();
            else if (_topEntryLadder != null && _climbReentryBlock <= 0f && GetVerticalInput() < -0.1f)
                StartClimbingFromTop(_topEntryLadder);
            return;
        }

        if (allowJumpOffLadder && GetJumpPressedThisFrame())
            JumpOffLadder();
    }

    // ── FixedUpdate — physics + trigger presence check ───────────────────────────

    void FixedUpdate()
    {
        // Unity order: FixedUpdate fires before physics callbacks (Stay2D).
        // So _onLadderThisPhysicsStep here reflects the PREVIOUS physics step's Stay2D.
        bool onLadder = _onLadderThisPhysicsStep;
        _onLadderThisPhysicsStep = false; // Stay2D will set it again if still inside

        if (IsClimbing && !onLadder)
        {
            // Player left the trigger sideways
            ExitClimbing("left trigger");
            _currentLadder = null;
            return;
        }

        if (!IsClimbing)
        {
            if (!onLadder) _currentLadder = null;
            return;
        }

        // Apply climbing velocity
        float vertInput = GetVerticalInput();
        float vy = vertInput * climbSpeed;
        float vx = lockHorizontalMovementWhileClimbing ? 0f : _rb.linearVelocity.x;
        _rb.linearVelocity = new Vector2(vx, vy);

        if (snapToLadderX && _currentLadder != null)
            _rb.position = new Vector2(_currentLadder.LadderX, _rb.position.y);

        if (_currentLadder == null) return;

        // Top exit
        if (vertInput > 0.1f && _rb.position.y >= _currentLadder.TopPosition.y - topExitDistance)
        {
            ExitAtTop();
            return;
        }

        // Bottom exit
        if (vertInput < -0.1f && _rb.position.y <= _currentLadder.BottomPosition.y + bottomExitDistance)
            ExitAtBottom();
    }

    // ── State transitions ────────────────────────────────────────────────────────

    void EnterClimbing()
    {
        IsClimbing = true;
        if (disableGravityWhileClimbing)
        {
            _originalGravity = _rb.gravityScale;
            _rb.gravityScale = 0f;
        }
        _currentLadder?.gate?.IgnoreForPlayer(playerBodyCollider);
        if (enableDebugLogs) Debug.Log("[Ladder] Started climbing.");
    }

    void ExitClimbing(string reason)
    {
        IsClimbing = false;
        RestoreGravity();
        _currentLadder?.gate?.RestoreForPlayer(playerBodyCollider);
        if (enableDebugLogs) Debug.Log($"[Ladder] Exited: {reason}");
    }

    void ExitAtTop()
    {
        if (topExitSnap && _currentLadder != null)
            _rb.position = new Vector2(_rb.position.x, _currentLadder.TopPosition.y);
        ExitClimbing("top");
        _climbReentryBlock = 0.5f; // prevent re-entry immediately after reaching top
        if (enableDebugLogs) Debug.Log("[Ladder] Exited from top.");
    }

    void ExitAtBottom()
    {
        if (bottomExitSnap && _currentLadder != null)
            _rb.position = new Vector2(_rb.position.x, _currentLadder.BottomPosition.y);
        ExitClimbing("bottom");
        if (enableDebugLogs) Debug.Log("[Ladder] Exited from bottom.");
    }

    void JumpOffLadder()
    {
        ExitClimbing("jump off");
        _currentLadder = null;
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, ladderJumpOffForce);
        if (enableDebugLogs) Debug.Log("[Ladder] Jumped off.");
    }

    void RestoreGravity()
    {
        if (disableGravityWhileClimbing && _rb != null)
            _rb.gravityScale = _originalGravity;
    }

    // ── Top Entry Zone (called by LadderTopEntryZone) ────────────────────────────

    public void SetTopEntryZone(LadderZone ladder)   => _topEntryLadder = ladder;
    public void ClearTopEntryZone(LadderZone ladder) { if (_topEntryLadder == ladder) _topEntryLadder = null; }

    public void StartClimbingFromTop(LadderZone ladder)
    {
        if (!canClimbLadders || ladder == null) return;
        _currentLadder = ladder;
        EnterClimbing();
        if (enableDebugLogs) Debug.Log("[Ladder] Started climbing from top.");
    }

    // ── Input ────────────────────────────────────────────────────────────────────

    float GetVerticalInput()
    {
        if (moveAction?.action != null) return moveAction.action.ReadValue<Vector2>().y;
        return 0f;
    }

    bool GetJumpPressedThisFrame()
    {
        if (jumpAction?.action == null) return false;
        foreach (var ctrl in jumpAction.action.controls)
            if (ctrl is ButtonControl btn && btn.wasPressedThisFrame) return true;
        return false;
    }
}
