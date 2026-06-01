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

    [Header("Staged Routing")]
    [Tooltip("Bir merdivenin giriş noktası (yukarı: bottom, aşağı: topEntry) enemy'nin ayak Y'sine bu kadar yakınsa 'mevcut seviyede erişilebilir' sayılır.")]
    [SerializeField] private float ladderEntryYTolerance             = 0.75f;
    [Tooltip("Tek merdivenin taşıyabileceği maksimum dikey adım. 999 = sınırsız (kademeli rota entry Y kontrolüyle sağlanır).")]
    [SerializeField] private float maxSingleLadderStepHeight         = 999f;
    [Tooltip("Açıksa enemy yalnız mevcut platform seviyesinden giriş yapılabilen merdivenleri aday görür (kademeli rota).")]
    [SerializeField] private bool  requireLadderEntryOnCurrentLevel  = true;
    [Tooltip("Açıksa skorlamada enemy'ye en yakın erişilebilir merdiven baskın olur (player'ın final yüksekliği değil).")]
    [SerializeField] private bool  preferNearestReachableLadderFirst = true;

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
    private bool        _gravityCached;
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

        // Gerçek gravity'yi bir kez, daha 0'lanmadan cache'le. Böylece ladder'a yürürken
        // (climb başlamadan) abort olsa bile restore gerçek değere döner, 0'a değil.
        _originalGravity = _rb != null ? _rb.gravityScale : 1f;
        _gravityCached   = true;
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
        SetClimbingPhysics();
        _targetLadder?.gate?.IgnoreForCollider(_bodyCollider);
        if (_chase != null) _chase.SuppressPlayerPlatformJump = false;
        if (enableDebugLogs) Debug.Log("[LadderNav] Started climbing UP.");
    }

    void StartClimbingDown()
    {
        _state = LadderState.ClimbingDown;
        SetClimbingPhysics();
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
        RestoreNormalPhysics();
        if (_chase != null)
        {
            _chase.ExternalControlActive      = false;
            _chase.SuppressPlayerPlatformJump = false;
        }
        _state        = LadderState.None;
        _targetLadder = null;
        _chaseDuration = 0f;
    }

    // ── Climb fizik yardımcıları (gravity'yi asla 0'da bırakma) ───────────────────

    void CacheOriginalGravityIfNeeded()
    {
        if (_gravityCached || _rb == null) return;
        _originalGravity = _rb.gravityScale;
        _gravityCached   = true;
    }

    void SetClimbingPhysics()
    {
        if (!disableGravityWhileClimbing || _rb == null) return;
        CacheOriginalGravityIfNeeded();
        _rb.gravityScale = 0f;
    }

    void RestoreNormalPhysics()
    {
        if (_rb != null && _gravityCached && disableGravityWhileClimbing)
            _rb.gravityScale = _originalGravity;   // her zaman GERÇEK değere döner, asla 0'a değil
        if (enableDebugLogs) Debug.Log($"[LadderNav] Gravity restored → {_originalGravity}.", this);
    }

    public void SetUseLadders(bool value) => useLadders = value;

    void OnDisable()  { if (_state != LadderState.None) RestoreAfterClimb(); }
    void OnDestroy()  { if (_state != LadderState.None) RestoreAfterClimb(); }

    // ── Ladder scoring ───────────────────────────────────────────────────────────

    LadderZone FindBestLadderForGoingUp()
    {
        LadderZone best       = null;
        float      bestS      = float.MaxValue;
        float      enemyFeetY = GetEnemyFeetY();

        foreach (var lz in LadderZone.All)
        {
            if (lz == null || !lz.CanEnemiesUse || !lz.IsValid) continue;

            float enemyToBot = Vector2.Distance(transform.position, lz.BottomPosition);
            if (enemyToBot > maxLadderSearchDistance) continue;

            // Giriş (bottom) enemy'nin mevcut platform seviyesinde mi?
            float bottomYDelta = Mathf.Abs(lz.BottomPosition.y - enemyFeetY);
            if (requireLadderEntryOnCurrentLevel && bottomYDelta > ladderEntryYTolerance)
            {
                if (enableDebugLogs) Debug.Log($"[LadderNav] UP reject {lz.name}: bottom not on level (Δ{bottomYDelta:F2} > {ladderEntryYTolerance}).", this);
                continue;
            }

            // Sadece anlamlı yukarı taşıyan, makul yükseklikteki merdivenler
            float stepH = lz.TopPosition.y - lz.BottomPosition.y;
            if (stepH < minVerticalDifferenceToUseLadder * 0.5f) continue;                   // yukarı taşımıyor / çok kısa
            if (stepH > maxSingleLadderStepHeight) continue;                                 // çok yüksek
            if (lz.TopPosition.y <= enemyFeetY + minVerticalDifferenceToUseLadder) continue; // anlamlı yukarı değil

            float nearW = preferNearestReachableLadderFirst ? 2.0f : 1.0f;
            float score = enemyToBot * nearW
                        + bottomYDelta * 5.0f
                        + Mathf.Abs(lz.TopPosition.y - _player.position.y) * 0.75f
                        + Mathf.Abs(lz.TopPosition.x - _player.position.x) * 0.15f;

            if (score < bestS) { bestS = score; best = lz; }
        }

        if (best != null && enableDebugLogs)
            Debug.Log($"[LadderNav] Selected UP {best.name} | feetY {enemyFeetY:F2} | bottomY {best.BottomPosition.y:F2} | topY {best.TopPosition.y:F2} | score {bestS:F2}", this);

        return best;
    }

    LadderZone FindBestLadderForGoingDown()
    {
        LadderZone best       = null;
        float      bestS      = float.MaxValue;
        float      enemyFeetY = GetEnemyFeetY();

        foreach (var lz in LadderZone.All)
        {
            if (lz == null || !lz.CanEnemiesUse || !lz.IsValid) continue;

            float enemyToTop = Vector2.Distance(transform.position, lz.TopEntryPosition);
            if (enemyToTop > maxLadderSearchDistance) continue;

            // Giriş (top entry) enemy'nin mevcut platform seviyesinde mi?
            float topEntryYDelta = Mathf.Abs(lz.TopEntryPosition.y - enemyFeetY);
            if (requireLadderEntryOnCurrentLevel && topEntryYDelta > ladderEntryYTolerance)
            {
                if (enableDebugLogs) Debug.Log($"[LadderNav] DOWN reject {lz.name}: topEntry not on level (Δ{topEntryYDelta:F2} > {ladderEntryYTolerance}).", this);
                continue;
            }

            // Sadece anlamlı aşağı taşıyan, makul yükseklikteki merdivenler
            float stepH = lz.TopEntryPosition.y - lz.BottomPosition.y;
            if (stepH < minVerticalDifferenceToUseLadder * 0.5f) continue;
            if (stepH > maxSingleLadderStepHeight) continue;
            if (lz.BottomPosition.y >= enemyFeetY - minVerticalDifferenceToUseLadder) continue; // anlamlı aşağı değil

            float nearW = preferNearestReachableLadderFirst ? 2.0f : 1.0f;
            float score = enemyToTop * nearW
                        + topEntryYDelta * 5.0f
                        + Mathf.Abs(lz.BottomPosition.y - _player.position.y) * 0.75f
                        + Mathf.Abs(lz.BottomPosition.x - _player.position.x) * 0.15f;

            if (score < bestS) { bestS = score; best = lz; }
        }

        if (best != null && enableDebugLogs)
            Debug.Log($"[LadderNav] Selected DOWN {best.name} | feetY {enemyFeetY:F2} | topEntryY {best.TopEntryPosition.y:F2} | bottomY {best.BottomPosition.y:F2} | score {bestS:F2}", this);

        return best;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    // Enemy'nin mevcut platform/ayak Y seviyesi (gövde collider'ının altı).
    float GetEnemyFeetY()
    {
        if (_bodyCollider != null) return _bodyCollider.bounds.min.y;
        return transform.position.y - 0.5f;
    }

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
