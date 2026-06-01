using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyLadderNavigator : MonoBehaviour
{
    private enum LadderState
    {
        None,
        MovingToLadderBottom,
        ClimbingUp,
        MovingToLadderTop,
        ClimbingDown
    }

    [Header("Ladder Navigation")]
    [SerializeField] private bool  useLadders                           = true;
    [SerializeField] private float ladderSearchInterval                 = 0.3f;
    [SerializeField] private float minVerticalDifferenceToUseLadder     = 1.2f;
    [SerializeField] private float maxLadderSearchDistance              = 25f;
    [SerializeField] private float ladderBottomReachDistance            = 0.25f;
    [SerializeField] private float ladderTopReachDistance               = 0.15f;
    [SerializeField] private float ladderTopEntryReachDistance          = 0.3f;
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
    [Tooltip("Enemy must chase directly this many seconds before considering a ladder. Prevents freshly-spawned enemies from immediately running to a ladder instead of chasing on foot.")]
    [SerializeField] private float minChaseDurationBeforeLadder         = 2.5f;
    [Tooltip("Ladder'a yürürken hedefe yatay ilerleme olmadan bu süre geçerse vazgeçip normal chase'e döner (engele takılıp donmayı önler).")]
    [SerializeField] private float ladderApproachTimeout                = 3f;

    private Rigidbody2D                _rb;
    private EnemyGroundChaseController _chase;
    private EnemyKnockback             _knockback;
    private Collider2D                 _bodyCollider;
    private Transform                  _player;
    private PlayerClimbController      _playerClimb;

    private LadderState _state;
    private LadderZone  _targetLadder;
    private float       _ladderSearchTimer;
    private float       _originalGravity;
    private float       _reentryBlock;
    private float       _chaseDuration;
    private float       _approachTimer;
    private float       _approachBestDx;

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

        // Track time spent actively chasing on foot (not mid-ladder).
        // Only count when in None state and currently chasing the player.
        if (_state == LadderState.None && _chase != null && _chase.IsChasing)
            _chaseDuration += Time.deltaTime;

        switch (_state)
        {
            case LadderState.None:                 TickNone();           break;
            case LadderState.MovingToLadderBottom: TickMovingToBottom(); break;
            case LadderState.MovingToLadderTop:    TickMovingToTop();    break;
        }
    }

    // ── FixedUpdate — climbing physics ───────────────────────────────────────────

    void FixedUpdate()
    {
        if (_state == LadderState.ClimbingUp)
        {
            if (_knockback != null && _knockback.IsActive) { AbortNavigation(); return; }
            if (_targetLadder == null) { AbortNavigation(); return; }
            if (_player != null)
            {
                float yDiff = _player.position.y - transform.position.y;
                if (yDiff <= minVerticalDifferenceToUseLadder * 0.5f) { AbortNavigation(); return; }
            }

            float vx = stopHorizontalMovementWhileClimbing ? 0f : _rb.linearVelocity.x;
            _rb.linearVelocity = new Vector2(vx, enemyClimbSpeed);

            if (snapToLadderXWhileClimbing)
            {
                float newX = Mathf.MoveTowards(_rb.position.x, _targetLadder.LadderX, ladderXSnapSpeed * Time.fixedDeltaTime);
                _rb.position = new Vector2(newX, _rb.position.y);
            }

            if (_rb.position.y >= _targetLadder.EnemyTopPosition.y - ladderTopReachDistance)
                FinishClimbingUp();
        }
        else if (_state == LadderState.ClimbingDown)
        {
            if (_knockback != null && _knockback.IsActive) { AbortNavigation(); return; }
            if (_targetLadder == null) { AbortNavigation(); return; }

            float vx = stopHorizontalMovementWhileClimbing ? 0f : _rb.linearVelocity.x;
            _rb.linearVelocity = new Vector2(vx, -enemyClimbSpeed);

            if (snapToLadderXWhileClimbing)
            {
                float newX = Mathf.MoveTowards(_rb.position.x, _targetLadder.LadderX, ladderXSnapSpeed * Time.fixedDeltaTime);
                _rb.position = new Vector2(newX, _rb.position.y);
            }

            float feetY = _bodyCollider != null
                ? _rb.position.y - _bodyCollider.bounds.extents.y
                : _rb.position.y - 0.5f;
            if (feetY <= _targetLadder.BottomPosition.y + ladderBottomReachDistance)
                FinishClimbingDown();
        }
    }

    // ── State ticks ──────────────────────────────────────────────────────────────

    void TickNone()
    {
        if (_player == null || _reentryBlock > 0f) return;
        if (_chase != null && !_chase.IsChasing) return;
        if (_chaseDuration < minChaseDurationBeforeLadder) return;

        _ladderSearchTimer -= Time.deltaTime;
        if (_ladderSearchTimer > 0f) return;
        _ladderSearchTimer = ladderSearchInterval;

        float yDiff = _player.position.y - transform.position.y;

        if (yDiff > minVerticalDifferenceToUseLadder)
        {
            if (_chase != null) _chase.SuppressPlayerPlatformJump = false;
            if (!IsPlayerGroundedOrClimbing()) return;
            LadderZone best = FindBestLadderForGoingUp();
            if (best != null)
            {
                _targetLadder = best;
                _state        = LadderState.MovingToLadderBottom;
                BeginApproach();
                if (_chase != null) _chase.ExternalControlActive = preventJumpSpamWhenLadderAvailable;
                if (enableDebugLogs) Debug.Log($"[LadderNav] Going UP via {best.name}.");
            }
            else if (preventJumpSpamWhenPlayerUnreachable && _chase != null)
            {
                _chase.SuppressPlayerPlatformJump = true;
                if (enableDebugLogs) Debug.Log("[LadderNav] No up-ladder found.");
            }
        }
        else if (yDiff < -minVerticalDifferenceToUseLadder)
        {
            if (_chase != null) _chase.SuppressPlayerPlatformJump = false;
            if (!IsPlayerGroundedOrClimbing()) return;
            LadderZone best = FindBestLadderForGoingDown();
            if (best != null)
            {
                _targetLadder = best;
                _state        = LadderState.MovingToLadderTop;
                BeginApproach();
                if (_chase != null) _chase.ExternalControlActive = preventJumpSpamWhenLadderAvailable;
                if (enableDebugLogs) Debug.Log($"[LadderNav] Going DOWN via {best.name}.");
            }
        }
        else
        {
            if (_chase != null) _chase.SuppressPlayerPlatformJump = false;
        }
    }

    void TickMovingToBottom()
    {
        if (_targetLadder == null) { AbortNavigation(); return; }

        if (_player != null)
        {
            float yDiff = _player.position.y - transform.position.y;
            if (yDiff <= minVerticalDifferenceToUseLadder * 0.5f) { AbortNavigation(); return; }
        }

        float targetX = _targetLadder.BottomPosition.x;
        float dx      = targetX - _rb.position.x;
        if (CheckApproachTimeout(dx)) return;
        float dir     = Mathf.Abs(dx) < 0.01f ? 0f : Mathf.Sign(dx);
        float speed   = _chase != null ? _chase.MoveSpeed : 4f;
        float accel   = _chase != null ? _chase.HorizontalAcceleration : 40f;
        float newVx   = Mathf.MoveTowards(_rb.linearVelocity.x, dir * speed, accel * Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector2(newVx, _rb.linearVelocity.y);

        if (Mathf.Abs(dir) > 0.01f) _chase?.ApplyFacing(dir);

        if (Mathf.Abs(dx) <= ladderBottomReachDistance)
            StartClimbingUp();
    }

    void TickMovingToTop()
    {
        if (_targetLadder == null) { AbortNavigation(); return; }

        if (_player != null)
        {
            float yDiff = _player.position.y - transform.position.y;
            if (yDiff >= -minVerticalDifferenceToUseLadder * 0.5f) { AbortNavigation(); return; }
        }

        float targetX = _targetLadder.TopEntryPosition.x;
        float dx      = targetX - _rb.position.x;
        if (CheckApproachTimeout(dx)) return;
        float dir     = Mathf.Abs(dx) < 0.01f ? 0f : Mathf.Sign(dx);
        float speed   = _chase != null ? _chase.MoveSpeed : 4f;
        float accel   = _chase != null ? _chase.HorizontalAcceleration : 40f;
        float newVx   = Mathf.MoveTowards(_rb.linearVelocity.x, dir * speed, accel * Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector2(newVx, _rb.linearVelocity.y);

        if (Mathf.Abs(dir) > 0.01f) _chase?.ApplyFacing(dir);

        if (Mathf.Abs(dx) <= ladderTopEntryReachDistance)
            StartClimbingDown();
    }

    // ── Transitions ──────────────────────────────────────────────────────────────

    void StartClimbingUp()
    {
        _state = LadderState.ClimbingUp;
        if (disableGravityWhileClimbing)
        {
            _originalGravity = _rb.gravityScale;
            _rb.gravityScale = 0f;
        }
        _targetLadder?.gate?.IgnoreForCollider(_bodyCollider);
        if (_chase != null) _chase.SuppressPlayerPlatformJump = false;
        if (enableDebugLogs) Debug.Log("[LadderNav] Started climbing UP.");
    }

    void StartClimbingDown()
    {
        _state = LadderState.ClimbingDown;
        if (disableGravityWhileClimbing)
        {
            _originalGravity = _rb.gravityScale;
            _rb.gravityScale = 0f;
        }
        _targetLadder?.gate?.IgnoreForCollider(_bodyCollider);
        if (_chase != null) { _chase.ExternalControlActive = true; _chase.SuppressPlayerPlatformJump = false; }
        if (enableDebugLogs) Debug.Log("[LadderNav] Started climbing DOWN.");
    }

    void FinishClimbingUp()
    {
        RestoreAfterClimb();
        _reentryBlock = 0.6f;
        if (enableDebugLogs) Debug.Log("[LadderNav] Finished climbing UP.");
    }

    void FinishClimbingDown()
    {
        _rb.linearVelocity = new Vector2(0f, 0f);
        RestoreAfterClimb();
        _reentryBlock = 0.6f;
        if (enableDebugLogs) Debug.Log("[LadderNav] Finished climbing DOWN.");
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
        _chaseDuration = 0f;
    }

    public void SetUseLadders(bool value) => useLadders = value;

    void OnDisable()  { if (_state != LadderState.None) RestoreAfterClimb(); }
    void OnDestroy()  { if (_state != LadderState.None) RestoreAfterClimb(); }

    // ── Ladder scoring ───────────────────────────────────────────────────────────

    LadderZone FindBestLadderForGoingUp()
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

    LadderZone FindBestLadderForGoingDown()
    {
        LadderZone best  = null;
        float      bestS = float.MaxValue;

        foreach (var lz in LadderZone.All)
        {
            if (lz == null || !lz.CanEnemiesUse || !lz.IsValid) continue;

            float enemyToTop = Vector2.Distance(transform.position, lz.TopEntryPosition);
            if (enemyToTop > maxLadderSearchDistance) continue;

            if (lz.BottomPosition.y >= transform.position.y - minVerticalDifferenceToUseLadder) continue;

            float botToPlayerY = Mathf.Abs(lz.BottomPosition.y - _player.position.y);
            float botToPlayerX = Mathf.Abs(_player.position.x - lz.BottomPosition.x);
            float score        = enemyToTop + botToPlayerY * 3f + botToPlayerX * 0.25f;

            if (score < bestS) { bestS = score; best = lz; }
        }

        return best;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    void BeginApproach()
    {
        _approachTimer  = 0f;
        _approachBestDx = float.MaxValue;
    }

    // Ladder'a yürürken yatay ilerleme takibi. Hedefe yaklaşıyorsa timer sıfırlanır;
    // ilerleme yoksa (engele takılı) timeout'ta navigasyon iptal edilir. true → abort edildi.
    bool CheckApproachTimeout(float dx)
    {
        float adx = Mathf.Abs(dx);
        if (adx < _approachBestDx - 0.05f)
        {
            _approachBestDx = adx;
            _approachTimer  = 0f;
            return false;
        }

        _approachTimer += Time.deltaTime;
        if (_approachTimer >= ladderApproachTimeout)
        {
            if (enableDebugLogs) Debug.Log("[LadderNav] Approach timed out — aborting.");
            AbortNavigation();
            return true;
        }
        return false;
    }

    bool IsPlayerGroundedOrClimbing()
    {
        if (_player == null) return false;
        if (_playerClimb != null && (_playerClimb.IsClimbing || _playerClimb.IsOnLadder))
            return true;
        LayerMask layers = _chase != null ? _chase.GroundLayers : (LayerMask)(-1);
        return Physics2D.Raycast((Vector2)_player.position, Vector2.down, playerGroundCheckDistance, layers);
    }

    void TryCachePlayer()
    {
        if (_player != null) return;
        if (PlayerHealthController.instance != null)
        {
            _player = PlayerHealthController.instance.transform;
            _playerClimb = _player.GetComponent<PlayerClimbController>();
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_targetLadder == null) return;
        bool goingDown = _state == LadderState.ClimbingDown || _state == LadderState.MovingToLadderTop;
        Gizmos.color = (_state == LadderState.ClimbingUp || _state == LadderState.ClimbingDown)
            ? Color.green
            : (goingDown ? Color.blue : Color.yellow);

        Vector3 target = goingDown
            ? (Vector3)_targetLadder.TopEntryPosition
            : (Vector3)_targetLadder.BottomPosition;
        Gizmos.DrawLine(transform.position, target);
        Gizmos.DrawWireSphere(target, 0.15f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_targetLadder.TopPosition, 0.15f);
    }
#endif
}
