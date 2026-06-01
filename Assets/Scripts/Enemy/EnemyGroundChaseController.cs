using UnityEngine;
using UnityEngine.Serialization;

// Walker düşman: SADECE yatay chase + küçük basamak/tek-tile engel zıplaması.
// Üst/alt platforma geçiş (kat değiştirme) EnemyLadderNavigator'ın işidir; burada
// player platformuna körlemesine zıplama, stuck-recovery veya Y-velocity sıfırlama YOK.
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyGroundChaseController : MonoBehaviour
{
    [Header("Chase")]
    [Tooltip("Kapalıysa oyuncuyu takip etmez.")]
    [SerializeField] private bool chasePlayer = true;

    [SerializeField] private float rangeToStartChase = 10f;

    [Tooltip("Açıksa oyuncu bu mesafeden uzaklaşınca takip bırakılır.")]
    [SerializeField] private bool stopChaseWhenOutOfRange = true;

    [SerializeField] private float rangeToStopChase = 14f;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float horizontalAcceleration = 40f;

    public float BaseMoveSpeed => moveSpeed;
    public void SetMoveSpeed(float newSpeed) { moveSpeed = Mathf.Max(0f, newSpeed); }

    public float MoveSpeed              => moveSpeed;
    public float HorizontalAcceleration => horizontalAcceleration;
    public bool  IsGrounded             => _isGrounded;

    public bool ExternalControlActive      { get; set; }

    // Vestigial — ladder navigator hâlâ set ediyor; API uyumu için tutuldu.
    public bool SuppressPlayerPlatformJump { get; set; }

    public bool IsChasing                  => _isChasing;
    public LayerMask GroundLayers          => groundLayers;

    public void ApplyFacing(float faceDir) => ApplyFacingScale(faceDir);

    [Tooltip("Sprite default halinde sağa bakıyorsa aç. Default halinde sola bakıyorsa kapalı kalsın.")]
    [SerializeField] private bool spriteFacesRightByDefault = false;

    [SerializeField] private Animator anim;

    [Header("Jump")]
    [Tooltip("Düşmanın ulaşabileceği maksimum dikey başlangıç hızı. Bu force değil velocity'dir.")]
    [FormerlySerializedAs("maxJumpForce")]
    [SerializeField] private float maxJumpVelocity = 14f;

    [SerializeField] private float minHeightToJump = 0.35f;
    [SerializeField] private float jumpCooldown = 0.35f;

    [Tooltip("Engelin üzerine rahatça çıkabilmesi için eklenen ekstra yükseklik payı.")]
    [SerializeField] private float jumpHeightBuffer = 0.35f;

    [Tooltip("Zıplama bu süreden önce 'landed' sayılmaz; jump başında ground ray hâlâ zemini görürse erken iniş bug'ını önler.")]
    [SerializeField] private float minJumpTimeBeforeLanding = 0.12f;

    [Header("Obstacle Jump")]
    [Tooltip("Açıksa düşman, önündeki tırmanılabilir küçük duvar/basamak için zıplar.")]
    [SerializeField] private bool jumpOverFrontObstacles = true;

    [Tooltip("Bundan daha alçak engeller için zıplama denemez.")]
    [SerializeField] private float minObstacleHeightToJump = 0.08f;

    [Tooltip("Düşmanın önündeki engel için deneyeceği maksimum yükseklik. Kat değiştirme ladder işi olduğundan küçük tutulur.")]
    [SerializeField] private float maxObstacleJumpHeight = 1.6f;

    [Tooltip("Duvar hit noktasının biraz ilerisine, üst yüzeyi bulmak için atılan dikey ray offset'i.")]
    [SerializeField] private float obstacleTopProbeForwardOffset = 0.2f;

    [Tooltip("Wall ray origin'i facing yönünde bu kadar geri çekilir (collider içinden çıkmak için).")]
    [SerializeField] private float wallRayStartInset = 0.08f;

    [Header("Air Control")]
    [Tooltip("Açıksa düşman havadayken de (sınırlı) yön kontrolü yapar. Default kapalı: havada chase yok.")]
    [SerializeField] private bool  allowAirSteering     = false;
    [Tooltip("allowAirSteering açıksa havadaki yatay kontrol çarpanı (0 = yok).")]
    [SerializeField] private float airControlMultiplier = 0f;
    [Tooltip("allowAirSteering kapalıyken havada X hızını 0'a çeken damping (0 = momentum aynen korunur).")]
    [SerializeField] private float airHorizontalDamping = 0f;

    [Header("Platforms")]
    [Tooltip("Sadece gerçekten üzerinde durulabilen zemin/platform layer'ları olmalı. Player/Enemy/Hitbox ekleme.")]
    [SerializeField] private LayerMask groundLayers;

    [Tooltip("Düşmanın kafasını çarpacağı katı tavan layer'ları. Boş bırakılırsa groundLayers kullanılır.")]
    [SerializeField] private LayerMask ceilingBlockLayers;

    [Header("Raycasts")]
    [Tooltip("Enemy'nin ayak hizasında / hemen altında olmalı. Çok yukarıdaysa düşman havadayken yanlışlıkla grounded sayılır.")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform ceilingCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Transform ledgeCheck;

    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private float ceilingCheckDistance = 0.5f;
    [SerializeField] private float wallProbeDistance = 0.45f;
    [SerializeField] private float ledgeProbeDistance = 0.6f;

    [Tooltip("Açıksa grounded kontrolü tek ray yerine ayak altında küçük bir box overlap ile yapılır (daire collider için daha stabil). groundCheck foot hizasında olmalı; değilse yanlış grounded verebilir.")]
    [SerializeField] private bool    useBoxGroundCheck = false;
    [SerializeField] private Vector2 groundBoxSize     = new Vector2(0.45f, 0.08f);
    [Tooltip("Box merkezinin groundCheck'e göre dikey ofseti (negatif = aşağı).")]
    [SerializeField] private float   groundBoxOffsetY  = -0.05f;

    [Header("Combat")]
    [Tooltip("Açıksa hasar trigger zone içindeyken zıplamaz.")]
    [SerializeField] private bool blockJumpWhileDamagingPlayer = true;

    [Header("Chase Offset")]
    [Tooltip("Açıksa her enemy player'ın tam merkezine değil kişisel ofset noktasına yönelir.")]
    [SerializeField] private bool  usePersonalChaseOffset   = true;
    [SerializeField] private float minChaseOffsetFromPlayer = 0.35f;
    [SerializeField] private float maxChaseOffsetFromPlayer = 1.4f;
    private float _personalChaseOffsetX;

    [Header("Crowd Separation")]
    [Tooltip("Açıksa yakın enemy'lerden yatay olarak hafifçe uzaklaşır (physics collision gerektirmez).")]
    [SerializeField] private bool      useCrowdSeparation  = true;
    [SerializeField] private LayerMask enemyAvoidanceLayers;
    [SerializeField] private float     separationRadius    = 0.75f;
    [SerializeField] private float     separationWeight    = 0.75f;
    [SerializeField] private int       maxNearbyEnemies    = 8;
    private Collider2D[]  _separationBuffer;
    private ContactFilter2D _separationFilter;

    [Header("Debug")]
    [Tooltip("Açıksa obstacle jump kararları (başarı + ret nedeni) Console'a loglanır.")]
    [SerializeField] private bool enableJumpDebugLogs = false;

    private Rigidbody2D _rb;
    private Transform _player;
    private EnemyKnockback _knockback;
    private DamagePlayer _damagePlayer;

    private Vector3 _baseScale;

    private bool _isChasing;
    private bool _isJumping;
    private float _lastJumpTime = -100f;
    private float _jumpStartTime = -100f;
    private float _jumpMoveDir;
    private bool _isGrounded;
    private bool _isCeilingAbove;

    private bool _isEscapingCeiling;
    private float _lockedFaceDir;

    private string _jumpRejectReason;
    private bool _groundCheckWarned;
    private float _normalGravityScale = 1f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _knockback = GetComponent<EnemyKnockback>();
        _damagePlayer = GetComponentInChildren<DamagePlayer>();
        _baseScale = transform.localScale;

        if (usePersonalChaseOffset)
        {
            float r = Random.Range(minChaseOffsetFromPlayer, maxChaseOffsetFromPlayer);
            _personalChaseOffsetX = Random.value > 0.5f ? r : -r;
        }
        _separationBuffer = new Collider2D[Mathf.Max(1, maxNearbyEnemies)];
        _separationFilter = new ContactFilter2D();
        _separationFilter.SetLayerMask(enemyAvoidanceLayers);
        _separationFilter.useTriggers = true;

        // Gerçek gravity'yi cache'le — Update'teki sanity-net, gravity yanlışlıkla 0'da
        // takılırsa (örn. bir ladder abort sızıntısı) buna geri döner.
        _normalGravityScale = _rb != null ? _rb.gravityScale : 1f;

        // NOT: Enemy↔Enemy fiziksel çarpışması Physics2D Layer Collision Matrix'ten yönetilir.
        // Runtime'da collider.excludeLayers DEĞİŞTİRİLMEZ — yanlış bir maske Ground/Platform
        // collision'ını kapatıp "havada yürüme"ye yol açabilirdi. Crowd separation yalnız
        // OverlapCircle steering ile çalışır; fiziksel collision'a dokunmaz.
        if (useCrowdSeparation && (enemyAvoidanceLayers.value & groundLayers.value) != 0)
            Debug.LogWarning("[EnemyWalker] enemyAvoidanceLayers contains GroundLayers. This can break ground collision.", this);
    }

    void Start()
    {
        TryCachePlayer();
    }

    void Update()
    {
        TryCachePlayer();

        // Gravity sanity-net: ladder kontrolde DEĞİLKEN gravity 0'da takılı kalmışsa düzelt.
        // ("Havada yürüme"yi kesen kalkan; ladder climb'da gravity 0 normal olduğundan
        // yalnız !ExternalControlActive iken müdahale eder → ladder ile çakışmaz.)
        if (!ExternalControlActive && _rb != null && _normalGravityScale > 0f
            && Mathf.Approximately(_rb.gravityScale, 0f))
        {
            _rb.gravityScale = _normalGravityScale;
            Debug.LogWarning("[EnemyWalker] gravityScale is 0 while not climbing — restored (hover guard).", this);
        }

        if (_player == null)
        {
            UpdateAnimatorSpeed();
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);

        if (!chasePlayer)
        {
            if (_isChasing)
                StopChasing();

            UpdateAnimatorSpeed();
            return;
        }

        if (!_isChasing)
        {
            if (distanceToPlayer < rangeToStartChase)
                StartChasing();
        }
        else
        {
            if (stopChaseWhenOutOfRange && distanceToPlayer > rangeToStopChase)
                StopChasing();
        }

        UpdateAnimatorSpeed();
    }

    void FixedUpdate()
    {
        if (!_isChasing || _player == null)
            return;

        if (_knockback != null && _knockback.IsActive)
            return;

        // Ladder navigator kontrolü aldıysa ground chase tamamen pasif.
        if (ExternalControlActive)
            return;

        UpdateSensorState();
        UpdateJumpLandingState();

        // Havadayken chase/separation/jump YOK — düşman havada player'a yönelmesin,
        // mevcut X momentumuyla (veya hafif damping) balistik düşsün.
        if (!_isGrounded)
        {
            ApplyAirBehavior();
            return;
        }

        float chaseDir = GetFaceDir();
        float sepInput = GetSeparationInput();
        float finalDir = Mathf.Clamp(chaseDir + sepInput * separationWeight, -1f, 1f);

        ApplyHorizontalMove(finalDir);

        TryJump(chaseDir);
    }

    // Jump 'landed' kontrolü — min süre geçmeden tetiklenmez (jump başında ground ray
    // hâlâ zemini görüyor olabilir; bu da _isJumping'i erken false yapardı).
    void UpdateJumpLandingState()
    {
        if (_isJumping
            && Time.time >= _jumpStartTime + minJumpTimeBeforeLanding
            && _isGrounded
            && _rb.linearVelocity.y <= 0.05f)
        {
            _isJumping = false;
            if (enableJumpDebugLogs) Debug.Log("[GroundChase] Jump landed.", this);
        }
    }

    // Havadaki yatay davranış. Default: hiç müdahale yok (X momentumu korunur).
    void ApplyAirBehavior()
    {
        if (!allowAirSteering)
        {
            if (airHorizontalDamping > 0f)
            {
                float nx = Mathf.MoveTowards(_rb.linearVelocity.x, 0f, airHorizontalDamping * Time.fixedDeltaTime);
                _rb.linearVelocity = new Vector2(nx, _rb.linearVelocity.y);
            }
            return;
        }

        // Air steering açıksa bile player'a göre DEĞİL, mevcut bakış yönüne göre ve çok sınırlı.
        float dir = GetCurrentFacingDir();
        float tx  = dir * moveSpeed * airControlMultiplier;
        float nax = Mathf.MoveTowards(_rb.linearVelocity.x, tx, horizontalAcceleration * airControlMultiplier * Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector2(nax, _rb.linearVelocity.y);
    }

    void TryCachePlayer()
    {
        if (_player != null)
            return;

        if (PlayerHealthController.instance != null)
            _player = PlayerHealthController.instance.transform;
    }

    void StartChasing()
    {
        _isChasing = true;

        if (anim != null)
            anim.SetBool("isChasing", true);
    }

    void StopChasing()
    {
        _isChasing = false;
        _isEscapingCeiling = false;

        if (anim != null)
            anim.SetBool("isChasing", false);

        if (_knockback == null || !_knockback.IsActive)
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

    void UpdateAnimatorSpeed()
    {
        if (anim != null && _rb != null)
            anim.SetFloat("speed", Mathf.Abs(_rb.linearVelocity.x));
    }

    void UpdateSensorState()
    {
        Vector2 groundOrigin = GetGroundRayOrigin();
        Vector2 ceilingOrigin = GetCeilingRayOrigin();

        if (useBoxGroundCheck)
        {
            Vector2 boxCenter = groundOrigin + new Vector2(0f, groundBoxOffsetY);
            _isGrounded = Physics2D.OverlapBox(boxCenter, groundBoxSize, 0f, groundLayers);
        }
        else
        {
            _isGrounded = Physics2D.Raycast(groundOrigin, Vector2.down, groundCheckDistance, groundLayers);
        }

        _isCeilingAbove = Physics2D.Raycast(
            ceilingOrigin,
            Vector2.up,
            ceilingCheckDistance,
            GetCeilingMask()
        );
    }

    float GetFaceDir()
    {
        float targetPlayerX = usePersonalChaseOffset
            ? _player.position.x + _personalChaseOffsetX
            : _player.position.x;
        float dx = targetPlayerX - transform.position.x;
        float dy = _player.position.y - transform.position.y;

        // Tavan altındayken ve player yukarıdayken: önündeki aşılamaz duvara dayanıp donmamak
        // için yön kilitle/çevir. Böylece düşman platform altından çıkıp ladder'a yönelebilir.
        if (_isCeilingAbove && dy > 1f)
        {
            if (!_isEscapingCeiling)
            {
                _isEscapingCeiling = true;

                if (Mathf.Abs(dx) < 0.1f)
                    _lockedFaceDir = GetCurrentFacingDir();
                else
                    _lockedFaceDir = Mathf.Sign(dx);
            }

            if (IsWallAhead(_lockedFaceDir) && !CanJumpOverFrontObstacle(_lockedFaceDir))
                _lockedFaceDir *= -1f;

            return _lockedFaceDir;
        }

        _isEscapingCeiling = false;

        if (Mathf.Abs(dx) < 0.05f)
            return GetCurrentFacingDir();

        return Mathf.Sign(dx);
    }

    float GetSeparationInput()
    {
        if (!useCrowdSeparation) return 0f;
        int count = Physics2D.OverlapCircle(
            transform.position, separationRadius, _separationFilter, _separationBuffer);
        float sep = 0f;
        for (int i = 0; i < count; i++)
        {
            var col = _separationBuffer[i];
            if (col == null || col.gameObject == gameObject) continue;
            float dx = transform.position.x - col.transform.position.x;
            if (Mathf.Abs(dx) < 0.001f) dx = Random.value > 0.5f ? 0.1f : -0.1f;
            sep += Mathf.Sign(dx);
        }
        return Mathf.Clamp(sep, -1f, 1f);
    }

    void ApplyHorizontalMove(float faceDir)
    {
        float targetX = faceDir * moveSpeed;

        float newX = Mathf.MoveTowards(
            _rb.linearVelocity.x,
            targetX,
            horizontalAcceleration * Time.fixedDeltaTime
        );

        _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);

        ApplyFacingScale(faceDir);
    }

    void ApplyFacingScale(float faceDir)
    {
        if (Mathf.Abs(faceDir) < 0.01f)
            return;

        float absX = Mathf.Abs(_baseScale.x);
        float targetX;

        if (spriteFacesRightByDefault)
            targetX = faceDir > 0f ? absX : -absX;
        else
            targetX = faceDir > 0f ? -absX : absX;

        transform.localScale = new Vector3(targetX, _baseScale.y, _baseScale.z);
    }

    float GetCurrentFacingDir()
    {
        bool facingRight;

        if (spriteFacesRightByDefault)
            facingRight = transform.localScale.x > 0f;
        else
            facingRight = transform.localScale.x < 0f;

        return facingRight ? 1f : -1f;
    }

    // ── Obstacle jump (tek zıplama yolu) ─────────────────────────────────────────

    void TryJump(float faceDir)
    {
        if (!CanStartJump())
        {
            if (enableJumpDebugLogs && IsWallAhead(faceDir))
                Debug.Log($"[GroundChase] Jump blocked: {_jumpRejectReason}", this);
            return;
        }

        if (TryGetFrontObstacleJumpHeight(faceDir, out float jumpHeight))
            ApplyJumpForHeight(jumpHeight, faceDir);
        else if (enableJumpDebugLogs && IsWallAhead(faceDir))
            Debug.Log($"[GroundChase] Obstacle jump rejected: {_jumpRejectReason}", this);
    }

    bool CanStartJump()
    {
        if (!_isGrounded)            { _jumpRejectReason = "not grounded"; return false; }
        if (Mathf.Abs(_rb.linearVelocity.y) > 0.05f) { _jumpRejectReason = "vy not settled"; return false; }
        if (Time.time < _lastJumpTime + jumpCooldown) { _jumpRejectReason = "cooldown"; return false; }
        if (_isCeilingAbove)        { _jumpRejectReason = "ceiling blocked"; return false; }

        if (blockJumpWhileDamagingPlayer
            && _damagePlayer != null
            && _damagePlayer.IsPlayerInContact)
        { _jumpRejectReason = "damaging player"; return false; }

        return true;
    }

    bool TryGetFrontObstacleJumpHeight(float faceDir, out float jumpHeight)
    {
        jumpHeight = 0f;

        if (!jumpOverFrontObstacles)
        { _jumpRejectReason = "obstacle jump disabled"; return false; }

        if (!TryGetWallHitAhead(faceDir, out RaycastHit2D wallHit))
        { _jumpRejectReason = "no wall ahead"; return false; }

        float feetY = GetFeetY();

        if (!TryFindObstacleTopY(wallHit, faceDir, feetY, out float topY))
        { _jumpRejectReason = "no obstacle top found"; return false; }

        float obstacleHeight = topY - feetY;

        if (obstacleHeight < minObstacleHeightToJump)
        { _jumpRejectReason = "obstacle too low"; return false; }

        if (obstacleHeight > maxObstacleJumpHeight)
        { _jumpRejectReason = "obstacle too high (ladder işi)"; return false; }

        if (!CanReachJumpHeight(obstacleHeight))
        { _jumpRejectReason = "cannot reach"; return false; }

        jumpHeight = Mathf.Max(obstacleHeight, minHeightToJump);
        return true;
    }

    // Engelin üst yüzeyini bul. Probe'u DAİMA zıplanabilir en yüksek noktanın ÜSTÜNDEN başlatır,
    // böylece (m_QueriesStartInColliders=1 iken) duvarın içinden başlayıp yüksekliği olduğundan az
    // ölçme hatası olmaz. Birkaç ileri-offset'le ince/kenardaki duvarların üstü de yakalanır.
    bool TryFindObstacleTopY(RaycastHit2D wallHit, float faceDir, float feetY, out float topY)
    {
        topY = 0f;

        float reach = Mathf.Min(MaxReachableJumpHeight(), maxObstacleJumpHeight);
        if (reach < minObstacleHeightToJump)
            return false;

        float startY  = feetY + reach + jumpHeightBuffer + 0.35f;   // her zıplanabilir üstün üstünde
        float minTopY = feetY + minObstacleHeightToJump;
        float dist    = startY - (feetY - 0.5f);
        float baseOff = obstacleTopProbeForwardOffset;
        float bestY   = float.MinValue;

        ProbeObstacleTop(wallHit, faceDir, 0.05f,           startY, dist, minTopY, ref bestY);
        ProbeObstacleTop(wallHit, faceDir, baseOff,         startY, dist, minTopY, ref bestY);
        ProbeObstacleTop(wallHit, faceDir, baseOff + 0.25f, startY, dist, minTopY, ref bestY);

        if (bestY <= float.MinValue)
            return false;

        topY = bestY;
        return true;
    }

    void ProbeObstacleTop(RaycastHit2D wallHit, float faceDir, float forwardOffset,
                          float startY, float dist, float minTopY, ref float bestY)
    {
        Vector2 origin = new Vector2(wallHit.point.x + faceDir * forwardOffset, startY);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, dist, groundLayers);
        if (hit.collider == null || hit.point.y < minTopY)
            return;
        if (hit.point.y > bestY)
            bestY = hit.point.y;
    }

    bool ApplyJumpForHeight(float height, float jumpDir)
    {
        float gravity = Mathf.Abs(Physics2D.gravity.y * _rb.gravityScale);

        if (gravity < 0.01f)
            return false;

        float targetHeight = height + jumpHeightBuffer;
        float maxReachableHeight = (maxJumpVelocity * maxJumpVelocity) / (2f * gravity);

        if (targetHeight > maxReachableHeight)
            return false;

        float requiredJumpVelocity = Mathf.Sqrt(2f * gravity * targetHeight);
        float finalJumpVelocity = Mathf.Min(requiredJumpVelocity, maxJumpVelocity);

        // Kalkışta X momentumunu zıplama yönüne kilitle; havada ApplyAirBehavior bunu korur
        // (player yönüne göre değişmez).
        _jumpMoveDir = Mathf.Abs(jumpDir) < 0.01f ? GetCurrentFacingDir() : Mathf.Sign(jumpDir);
        _rb.linearVelocity = new Vector2(_jumpMoveDir * moveSpeed, finalJumpVelocity);
        _lastJumpTime  = Time.time;
        _jumpStartTime = Time.time;
        _isJumping     = true;

        if (enableJumpDebugLogs)
            Debug.Log($"[GroundChase] Obstacle jump: height {height:F2}, velocity {finalJumpVelocity:F2}, dir {_jumpMoveDir:F0}", this);

        return true;
    }

    bool CanReachJumpHeight(float height)
    {
        float gravity = Mathf.Abs(Physics2D.gravity.y * _rb.gravityScale);

        if (gravity < 0.01f)
            return false;

        float targetHeight = height + jumpHeightBuffer;
        float maxReachableHeight = (maxJumpVelocity * maxJumpVelocity) / (2f * gravity);

        return targetHeight <= maxReachableHeight;
    }

    // Bu düşmanın fizik olarak çıkabileceği net engel yüksekliği (jumpHeightBuffer düşülmüş).
    float MaxReachableJumpHeight()
    {
        float gravity = Mathf.Abs(Physics2D.gravity.y * _rb.gravityScale);
        if (gravity < 0.01f)
            return 0f;
        return (maxJumpVelocity * maxJumpVelocity) / (2f * gravity) - jumpHeightBuffer;
    }

    // ── Sensör yardımcıları ──────────────────────────────────────────────────────

    bool TryGetWallHitAhead(float faceDir, out RaycastHit2D hit)
    {
        hit = default;

        if (Mathf.Abs(faceDir) < 0.01f)
            return false;

        Vector2 origin = GetWallRayOrigin(faceDir);

        hit = Physics2D.Raycast(
            origin,
            Vector2.right * faceDir,
            wallProbeDistance,
            groundLayers
        );

        return hit.collider != null;
    }

    bool IsWallAhead(float faceDir)
    {
        return TryGetWallHitAhead(faceDir, out _);
    }

    bool CanJumpOverFrontObstacle(float faceDir)
    {
        return TryGetFrontObstacleJumpHeight(faceDir, out _);
    }

    float GetFeetY()
    {
        if (groundCheck != null)
            return groundCheck.position.y;

        WarnNoGroundCheck();
        return transform.position.y - 0.9f;
    }

    Vector2 GetGroundRayOrigin()
    {
        if (groundCheck != null)
            return groundCheck.position;

        WarnNoGroundCheck();
        return transform.position;
    }

    Vector2 GetCeilingRayOrigin()
    {
        return ceilingCheck != null ? ceilingCheck.position : (Vector2)transform.position;
    }

    Vector2 GetWallRayOrigin(float faceDir)
    {
        Vector2 origin;

        if (wallCheck != null)
            origin = wallCheck.position;
        else
            origin = (Vector2)transform.position + Vector2.right * faceDir * 0.8f + Vector2.down * 0.2f;

        return origin - Vector2.right * faceDir * wallRayStartInset;
    }

    Vector2 GetLedgeRayOrigin(float faceDir)
    {
        if (ledgeCheck != null)
            return ledgeCheck.position;

        return (Vector2)transform.position
               + Vector2.right * faceDir * 0.55f
               + Vector2.down * 0.85f;
    }

    LayerMask GetCeilingMask()
    {
        return ceilingBlockLayers.value == 0 ? groundLayers : ceilingBlockLayers;
    }

    void WarnNoGroundCheck()
    {
        if (_groundCheckWarned) return;
        _groundCheckWarned = true;
        Debug.LogWarning($"[GroundChase] {name}: groundCheck atanmamış — transform.position fallback kullanılıyor (daha az güvenilir).", this);
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────────

    void DrawObstacleTopProbeGizmo(float faceDir)
    {
        if (!jumpOverFrontObstacles || Mathf.Abs(faceDir) < 0.01f)
            return;

        if (!TryGetWallHitAhead(faceDir, out RaycastHit2D wallHit))
            return;

        float feetY = GetFeetY();
        float reach = Mathf.Min(MaxReachableJumpHeight(), maxObstacleJumpHeight);
        if (reach < minObstacleHeightToJump)
            return;

        float startY = feetY + reach + jumpHeightBuffer + 0.35f;
        float dist   = startY - (feetY - 0.5f);
        float baseOff = obstacleTopProbeForwardOffset;

        Gizmos.color = new Color(0f, 0.4f, 1f, 0.5f);
        DrawProbeLine(wallHit, faceDir, 0.05f,           startY, dist);
        DrawProbeLine(wallHit, faceDir, baseOff,         startY, dist);
        DrawProbeLine(wallHit, faceDir, baseOff + 0.25f, startY, dist);

        if (TryFindObstacleTopY(wallHit, faceDir, feetY, out float topY))
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(new Vector2(wallHit.point.x + faceDir * baseOff, topY), 0.08f);
        }
    }

    void DrawProbeLine(RaycastHit2D wallHit, float faceDir, float forwardOffset, float startY, float dist)
    {
        Vector2 o = new Vector2(wallHit.point.x + faceDir * forwardOffset, startY);
        RaycastHit2D h = Physics2D.Raycast(o, Vector2.down, dist, groundLayers);
        Vector2 e = h.collider != null ? h.point : o + Vector2.down * dist;
        Gizmos.DrawLine(o, e);
    }

    void OnDrawGizmos()
    {
        Vector2 groundOrigin = GetGroundRayOrigin();
        Vector2 ceilingOrigin = GetCeilingRayOrigin();

        Vector2 groundEnd = groundOrigin + Vector2.down * groundCheckDistance;
        Vector2 ceilingEnd = ceilingOrigin + Vector2.up * ceilingCheckDistance;

        RaycastHit2D groundHit = Physics2D.Raycast(groundOrigin, Vector2.down, groundCheckDistance, groundLayers);
        bool grounded = groundHit.collider != null;
        if (grounded) groundEnd = groundHit.point;

        RaycastHit2D ceilingHit = Physics2D.Raycast(ceilingOrigin, Vector2.up, ceilingCheckDistance, GetCeilingMask());
        bool ceilingAbove = ceilingHit.collider != null;
        if (ceilingAbove) ceilingEnd = ceilingHit.point;

        Gizmos.color = grounded ? Color.green : new Color(0f, 1f, 0f, 0.35f);
        Gizmos.DrawLine(groundOrigin, groundEnd);
        Gizmos.DrawWireSphere(groundEnd, 0.08f);

        Gizmos.color = ceilingAbove ? Color.red : new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawLine(ceilingOrigin, ceilingEnd);
        Gizmos.DrawWireSphere(ceilingEnd, 0.08f);

        float faceDir = GetCurrentFacingDir();

        if (_player != null)
        {
            float dx = _player.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.01f)
                faceDir = Mathf.Sign(dx);
        }

        Vector2 wallOrigin = GetWallRayOrigin(faceDir);
        Vector2 wallEnd = wallOrigin + Vector2.right * faceDir * wallProbeDistance;

        RaycastHit2D wallHit = Physics2D.Raycast(wallOrigin, Vector2.right * faceDir, wallProbeDistance, groundLayers);
        if (wallHit.collider != null) wallEnd = wallHit.point;

        Gizmos.color = wallHit.collider != null ? Color.cyan : new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawLine(wallOrigin, wallEnd);
        Gizmos.DrawWireSphere(wallEnd, 0.06f);

        Vector2 ledgeOrigin = GetLedgeRayOrigin(faceDir);
        Vector2 ledgeEnd = ledgeOrigin + Vector2.down * ledgeProbeDistance;

        RaycastHit2D ledgeHit = Physics2D.Raycast(ledgeOrigin, Vector2.down, ledgeProbeDistance, groundLayers);
        if (ledgeHit.collider != null) ledgeEnd = ledgeHit.point;

        Gizmos.color = ledgeHit.collider == null ? Color.yellow : new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawLine(ledgeOrigin, ledgeEnd);
        Gizmos.DrawWireSphere(ledgeEnd, 0.06f);

        DrawObstacleTopProbeGizmo(faceDir);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            UpdateSensorState();
    }
#endif

    void OnValidate()
    {
        rangeToStartChase = Mathf.Max(0f, rangeToStartChase);
        rangeToStopChase = Mathf.Max(rangeToStartChase, rangeToStopChase);

        moveSpeed = Mathf.Max(0f, moveSpeed);
        horizontalAcceleration = Mathf.Max(0f, horizontalAcceleration);

        maxJumpVelocity = Mathf.Max(0f, maxJumpVelocity);
        minHeightToJump = Mathf.Max(0f, minHeightToJump);
        jumpCooldown = Mathf.Max(0f, jumpCooldown);
        jumpHeightBuffer = Mathf.Max(0f, jumpHeightBuffer);

        minObstacleHeightToJump = Mathf.Max(0f, minObstacleHeightToJump);
        maxObstacleJumpHeight = Mathf.Max(minObstacleHeightToJump, maxObstacleJumpHeight);
        obstacleTopProbeForwardOffset = Mathf.Max(0.01f, obstacleTopProbeForwardOffset);
        wallRayStartInset = Mathf.Max(0f, wallRayStartInset);

        groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
        ceilingCheckDistance = Mathf.Max(0.01f, ceilingCheckDistance);
        wallProbeDistance = Mathf.Max(0.01f, wallProbeDistance);
        ledgeProbeDistance = Mathf.Max(0.01f, ledgeProbeDistance);

        minJumpTimeBeforeLanding = Mathf.Max(0f, minJumpTimeBeforeLanding);
        airControlMultiplier = Mathf.Max(0f, airControlMultiplier);
        airHorizontalDamping = Mathf.Max(0f, airHorizontalDamping);
    }
}
