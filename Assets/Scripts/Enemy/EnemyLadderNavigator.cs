using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyLadderNavigator : MonoBehaviour
{
    private enum LadderState { None, MovingToLadder, ClimbingLadder }

    [Header("Ladder Navigation")]
    [SerializeField] private bool  useLadders                           = true;
    [SerializeField] private float ladderSearchInterval                 = 0.3f;
    [SerializeField] private float minVerticalDifferenceToUseLadder     = 1.2f;
    [SerializeField] private float maxLadderSearchDistance              = 25f;
    [SerializeField] private float ladderBottomReachDistance            = 0.25f;
    [SerializeField] private float ladderTopReachDistance               = 0.15f;
    [SerializeField] private float enemyClimbSpeed                      = 2.5f;
    [SerializeField] private bool  snapToLadderXWhileClimbing           = true;
    [SerializeField] private float ladderXSnapSpeed                     = 10f;
    [SerializeField] private bool  disableGravityWhileClimbing          = true;
    [SerializeField] private bool  stopHorizontalMovementWhileClimbing  = true;
    [SerializeField] private bool  preventJumpSpamWhenLadderAvailable   = true;
    [SerializeField] private bool  preventJumpSpamWhenPlayerUnreachable = true;
    [Tooltip("Downward ray length used to check whether the player is standing on ground.")]
    [SerializeField] private float playerGroundCheckDistance            = 0.8f;
    [SerializeField] private bool  enableDebugLogs                      = false;

    private Rigidbody2D                _rb;
    private EnemyGroundChaseController _chase;
    private EnemyKnockback             _knockback;
    private Collider2D                 _bodyCollider;
    private Transform                  _player;

    private LadderState _state;
    private LadderZone  _targetLadder;
    private float       _ladderSearchTimer;
    private float       _originalGravity;
    private float       _reentryBlock;

    void Awake()
    {
        _rb        = GetComponent<Rigidbody2D>();
        _chase     = GetComponent<EnemyGroundChaseController>();
        _knockback = GetComponent<EnemyKnockback>();

        foreach (var col in GetComponentsInChildren<Collider2D>())
            if (!col.isTrigger) { _bodyCollider = col; break; }
    }

    void Start() => TryCachePlayer();

    // ── Update — state machine ────────────────────────────────────────────────────

    void Update()
    {
        if (!useLadders) return;
        if (_reentryBlock > 0f) _reentryBlock -= Time.deltaTime;

        TryCachePlayer();

        switch (_state)
        {
            case LadderState.None:           TickNone();   break;
            case LadderState.MovingToLadder: TickMoving(); break;
        }
    }

    // ── FixedUpdate — climbing physics ───────────────────────────────────────────

    void FixedUpdate()
    {
        if (_state != LadderState.ClimbingLadder) return;

        if (_knockback != null && _knockback.IsActive) { AbortNavigation(); return; }
        if (_targetLadder == null) { AbortNavigation(); return; }

        float vy = enemyClimbSpeed;
        float vx = stopHorizontalMovementWhileClimbing ? 0f : _rb.linearVelocity.x;
        _rb.linearVelocity = new Vector2(vx, vy);

        if (snapToLadderXWhileClimbing)
        {
            float newX = Mathf.MoveTowards(_rb.position.x, _targetLadder.LadderX, ladderXSnapSpeed * Time.fixedDeltaTime);
            _rb.position = new Vector2(newX, _rb.position.y);
        }

        if (_rb.position.y >= _targetLadder.EnemyTopPosition.y - ladderTopReachDistance)
            FinishClimbing();
    }

    // ── State ticks ──────────────────────────────────────────────────────────────

    void TickNone()
    {
        if (_player == null || _reentryBlock > 0f) return;

        // Only activate ladder navigation while actively chasing
        if (_chase != null && !_chase.IsChasing) return;

        _ladderSearchTimer -= Time.deltaTime;
        if (_ladderSearchTimer > 0f) return;
        _ladderSearchTimer = ladderSearchInterval;

        float yDiff = _player.position.y - transform.position.y;
        if (yDiff <= minVerticalDifferenceToUseLadder)
        {
            if (_chase != null) _chase.SuppressPlayerPlatformJump = false;
            return;
        }

        // Only use ladder when player is standing on ground, not mid-air
        if (!IsPlayerOnGround()) return;

        LadderZone best = FindBestLadder();
        if (best != null)
        {
            _targetLadder = best;
            _state        = LadderState.MovingToLadder;
            if (_chase != null) _chase.ExternalControlActive = preventJumpSpamWhenLadderAvailable;
            if (enableDebugLogs) Debug.Log($"[LadderNav] Moving to {best.name}.");
        }
        else if (preventJumpSpamWhenPlayerUnreachable && _chase != null)
        {
            _chase.SuppressPlayerPlatformJump = true;
            if (enableDebugLogs) Debug.Log("[LadderNav] No ladder found, suppressing platform jump.");
        }
    }

    void TickMoving()
    {
        if (_targetLadder == null) { AbortNavigation(); return; }

        // Abort if player came back down to our level
        if (_player != null)
        {
            float yDiff = _player.position.y - transform.position.y;
            if (yDiff <= minVerticalDifferenceToUseLadder * 0.5f) { AbortNavigation(); return; }
        }

        float targetX = _targetLadder.BottomPosition.x;
        float dx      = targetX - _rb.position.x;
        float dir     = Mathf.Abs(dx) < 0.01f ? 0f : Mathf.Sign(dx);

        float speed = _chase != null ? _chase.MoveSpeed : 4f;
        float accel = _chase != null ? _chase.HorizontalAcceleration : 40f;
        float newVx = Mathf.MoveTowards(_rb.linearVelocity.x, dir * speed, accel * Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector2(newVx, _rb.linearVelocity.y);

        if (Mathf.Abs(dir) > 0.01f) _chase?.ApplyFacing(dir);

        if (Mathf.Abs(dx) <= ladderBottomReachDistance)
            StartClimbing();
    }

    // ── Transitions ──────────────────────────────────────────────────────────────

    void StartClimbing()
    {
        _state = LadderState.ClimbingLadder;
        if (disableGravityWhileClimbing)
        {
            _originalGravity = _rb.gravityScale;
            _rb.gravityScale = 0f;
        }
        _targetLadder?.gate?.IgnoreForCollider(_bodyCollider);
        if (_chase != null) _chase.SuppressPlayerPlatformJump = false;
        if (enableDebugLogs) Debug.Log("[LadderNav] Started climbing.");
    }

    void FinishClimbing()
    {
        RestoreAfterClimb();
        _reentryBlock = 0.6f;
        if (enableDebugLogs) Debug.Log("[LadderNav] Reached top.");
    }

    void AbortNavigation()
    {
        RestoreAfterClimb();
        if (enableDebugLogs) Debug.Log("[LadderNav] Aborted.");
    }

    void RestoreAfterClimb()
    {
        _targetLadder?.gate?.RestoreForCollider(_bodyCollider);
        if (disableGravityWhileClimbing && _rb != null) _rb.gravityScale = _originalGravity;
        if (_chase != null)
        {
            _chase.ExternalControlActive      = false;
            _chase.SuppressPlayerPlatformJump = false;
        }
        _state        = LadderState.None;
        _targetLadder = null;
    }

    void OnDisable()  { if (_state != LadderState.None) RestoreAfterClimb(); }
    void OnDestroy()  { if (_state != LadderState.None) RestoreAfterClimb(); }

    // ── Ladder scoring ───────────────────────────────────────────────────────────

    LadderZone FindBestLadder()
    {
        LadderZone best  = null;
        float      bestS = float.MaxValue;

        foreach (var lz in LadderZone.All)
        {
            if (lz == null || !lz.CanEnemiesUse || !lz.IsValid) continue;

            float enemyToBot = Vector2.Distance(transform.position, lz.BottomPosition);
            if (enemyToBot > maxLadderSearchDistance) continue;

            float topY = lz.TopPosition.y;
            if (topY < transform.position.y + minVerticalDifferenceToUseLadder) continue;

            float topToPlayerY = Mathf.Abs(topY - _player.position.y);
            float topToPlayerX = Mathf.Abs(_player.position.x - lz.TopPosition.x);
            float score        = enemyToBot + topToPlayerY * 3f + topToPlayerX * 0.25f;

            if (score < bestS) { bestS = score; best = lz; }
        }

        return best;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    bool IsPlayerOnGround()
    {
        if (_player == null) return false;
        LayerMask layers = _chase != null ? _chase.GroundLayers : (LayerMask)(-1);
        return Physics2D.Raycast((Vector2)_player.position, Vector2.down, playerGroundCheckDistance, layers);
    }

    void TryCachePlayer()
    {
        if (_player != null) return;
        if (PlayerHealthController.instance != null)
            _player = PlayerHealthController.instance.transform;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_targetLadder == null) return;
        Gizmos.color = _state == LadderState.ClimbingLadder ? Color.green : Color.yellow;
        Gizmos.DrawLine(transform.position, _targetLadder.BottomPosition);
        Gizmos.DrawWireSphere(_targetLadder.BottomPosition, 0.15f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_targetLadder.TopPosition, 0.15f);
    }
#endif
}
