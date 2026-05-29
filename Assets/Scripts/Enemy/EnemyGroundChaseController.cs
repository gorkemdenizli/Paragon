using UnityEngine;
using UnityEngine.Serialization;

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

    [Tooltip("Sprite default halinde sağa bakıyorsa aç. Default halinde sola bakıyorsa kapalı kalsın.")]
    [SerializeField] private bool spriteFacesRightByDefault = false;

    [SerializeField] private Animator anim;

    [Header("Jump")]
    [Tooltip("Düşmanın ulaşabileceği maksimum dikey başlangıç hızı. Bu force değil velocity'dir.")]
    [FormerlySerializedAs("maxJumpForce")]
    [SerializeField] private float maxJumpVelocity = 14f;

    [SerializeField] private float minHeightToJump = 0.35f;
    [SerializeField] private float jumpCooldown = 0.35f;

    [Tooltip("Platformun üzerine rahatça çıkabilmesi için eklenen ekstra yükseklik payı.")]
    [SerializeField] private float jumpHeightBuffer = 0.6f;

    [Tooltip("Oyuncu platformu path'i açıksa yatay yakınlık eşiği.")]
    [SerializeField] private float jumpWhenCloseHorizontalDistance = 1.5f;

    [Header("Obstacle Jump")]
    [Tooltip("Açıksa düşman, önündeki tırmanılabilir duvar/basamak için player'dan bağımsız zıplar.")]
    [SerializeField] private bool jumpOverFrontObstacles = true;

    [Tooltip("Açıksa önünde duvar olmasa bile oyuncu yukarıdaki platformdaysa yaklaştığında zıplar.")]
    [SerializeField] private bool jumpTowardPlayerPlatform = false;

    [Tooltip("Bundan daha alçak engeller için zıplama denemez.")]
    [SerializeField] private float minObstacleHeightToJump = 0.08f;

    [Tooltip("Düşmanın önündeki engel için deneyeceği maksimum yükseklik.")]
    [SerializeField] private float maxObstacleJumpHeight = 2.5f;

    [Tooltip("Duvar hit noktasının biraz ilerisine, üst yüzeyi bulmak için atılan dikey ray offset'i.")]
    [SerializeField] private float obstacleTopProbeForwardOffset = 0.2f;

    [Tooltip("Üst yüzeyi arayan ray'in duvar yüzünden başlayacağı ekstra yükseklik payı.")]
    [SerializeField] private float obstacleTopProbeExtraHeight = 0.15f;

    [Tooltip("Wall ray origin'i facing yönünde bu kadar geri çekilir (collider içinden çıkmak için).")]
    [SerializeField] private float wallRayStartInset = 0.08f;

    [Header("Platforms")]
    [Tooltip("Sadece gerçekten üzerinde durulabilen zemin/platform layer'ları olmalı. Player/Enemy/Hitbox ekleme.")]
    [SerializeField] private LayerMask groundLayers;

    [Tooltip("Düşmanın kafasını çarpacağı katı tavan layer'ları. Boş bırakılırsa groundLayers kullanılır.")]
    [SerializeField] private LayerMask ceilingBlockLayers;

    [SerializeField] private float playerGroundProbeDistance = 20f;
    [SerializeField] private float playerFeetProbeOffset = 0.6f;

    [Header("Raycasts")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform ceilingCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Transform ledgeCheck;

    [SerializeField] private float groundCheckDistance = 0.5f;
    [SerializeField] private float ceilingCheckDistance = 0.5f;
    [SerializeField] private float wallProbeDistance = 0.4f;
    [SerializeField] private float ledgeProbeDistance = 0.6f;

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

    private Rigidbody2D _rb;
    private Transform _player;
    private Transform _playerGroundCheck;
    private EnemyKnockback _knockback;
    private DamagePlayer _damagePlayer;

    private Vector3 _baseScale;

    private bool _isChasing;
    private float _lastJumpTime = -100f;
    private bool _isGrounded;
    private bool _isCeilingAbove;

    private bool _isEscapingCeiling;
    private float _lockedFaceDir;

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
    }

    void Start()
    {
        TryCachePlayer();
    }

    void Update()
    {
        TryCachePlayer();

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

        UpdateSensorState();

        float chaseDir = GetFaceDir();
        float sepInput = GetSeparationInput();
        float finalDir = Mathf.Clamp(chaseDir + sepInput * separationWeight, -1f, 1f);

        ApplyHorizontalMove(finalDir);

        TryJump(chaseDir);
    }

    void TryCachePlayer()
    {
        if (_player != null)
            return;

        if (PlayerHealthController.instance != null)
        {
            _player = PlayerHealthController.instance.transform;
            _playerGroundCheck = _player.Find("Ground Check Point");
        }
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

        _isGrounded = Physics2D.Raycast(
            groundOrigin,
            Vector2.down,
            groundCheckDistance,
            groundLayers
        );

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

    void TryJump(float faceDir)
    {
        if (!CanStartJump())
            return;

        float jumpHeight;

        if (TryGetFrontObstacleJumpHeight(faceDir, out jumpHeight))
        {
            ApplyJumpForHeight(jumpHeight);
            return;
        }

        if (TryGetPlayerPlatformJumpHeight(faceDir, out jumpHeight))
            ApplyJumpForHeight(jumpHeight);
    }

    bool CanStartJump()
    {
        if (!_isGrounded)
            return false;

        if (Mathf.Abs(_rb.linearVelocity.y) > 0.05f)
            return false;

        if (Time.time < _lastJumpTime + jumpCooldown)
            return false;

        if (blockJumpWhileDamagingPlayer
            && _damagePlayer != null
            && _damagePlayer.IsPlayerInContact)
            return false;

        return true;
    }

    bool TryGetFrontObstacleJumpHeight(float faceDir, out float jumpHeight)
    {
        jumpHeight = 0f;

        if (!jumpOverFrontObstacles)
            return false;

        RaycastHit2D wallHit;

        if (!TryGetWallHitAhead(faceDir, out wallHit))
            return false;

        float feetY = GetFeetY();

        if (!TryFindObstacleTopY(wallHit, faceDir, feetY, out float topY))
            return false;

        float obstacleHeight = topY - feetY;

        if (obstacleHeight < minObstacleHeightToJump)
            return false;

        if (obstacleHeight > maxObstacleJumpHeight)
            return false;

        if (!CanReachJumpHeight(obstacleHeight))
            return false;

        jumpHeight = Mathf.Max(obstacleHeight, minHeightToJump);
        return true;
    }

    bool TryFindObstacleTopY(RaycastHit2D wallHit, float faceDir, float feetY, out float topY)
    {
        topY = 0f;

        Vector2 topProbeOrigin = new Vector2(
            wallHit.point.x + faceDir * obstacleTopProbeForwardOffset,
            wallHit.point.y + obstacleTopProbeExtraHeight
        );

        float probeDistance = maxObstacleJumpHeight + jumpHeightBuffer + 0.5f;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            topProbeOrigin,
            Vector2.down,
            probeDistance,
            groundLayers
        );

        if (hits.Length == 0)
            return false;

        float minTopY = feetY + minObstacleHeightToJump;
        float maxTopY = feetY + maxObstacleJumpHeight + jumpHeightBuffer;
        float bestY = float.MinValue;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.point.y < minTopY || hit.point.y > maxTopY)
                continue;

            if (hit.point.y > bestY)
                bestY = hit.point.y;
        }

        if (bestY <= float.MinValue)
            return false;

        topY = bestY;
        return true;
    }

    bool TryGetPlayerPlatformJumpHeight(float faceDir, out float jumpHeight)
    {
        jumpHeight = 0f;

        if (!jumpTowardPlayerPlatform)
            return false;

        if (_player == null)
            return false;

        if (_isCeilingAbove)
            return false;

        if (!IsPlayerOnChaseSide(faceDir))
            return false;

        float feetY = GetFeetY();
        float platformY = GetPlayerPlatformY();
        float heightDelta = platformY - feetY;

        if (heightDelta < minHeightToJump)
            return false;

        bool shouldJumpForPlayer =
            IsAtLedge(faceDir) ||
            Mathf.Abs(_player.position.x - transform.position.x) < jumpWhenCloseHorizontalDistance;

        if (!shouldJumpForPlayer)
            return false;

        if (!CanReachJumpHeight(heightDelta))
            return false;

        jumpHeight = heightDelta;
        return true;
    }

    bool ApplyJumpForHeight(float height)
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

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, finalJumpVelocity);
        _lastJumpTime = Time.time;

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
        RaycastHit2D hit;
        return TryGetWallHitAhead(faceDir, out hit);
    }

    bool CanJumpOverFrontObstacle(float faceDir)
    {
        float jumpHeight;
        return TryGetFrontObstacleJumpHeight(faceDir, out jumpHeight);
    }

    bool IsAtLedge(float faceDir)
    {
        if (Mathf.Abs(faceDir) < 0.01f)
            return false;

        Vector2 origin = GetLedgeRayOrigin(faceDir);

        return !Physics2D.Raycast(
            origin,
            Vector2.down,
            ledgeProbeDistance,
            groundLayers
        );
    }

    bool IsPlayerOnChaseSide(float faceDir)
    {
        float dx = _player.position.x - transform.position.x;

        return Mathf.Abs(dx) < 0.3f || Mathf.Sign(dx) == faceDir;
    }

    float GetPlayerPlatformY()
    {
        Vector2 probeOrigin = GetPlayerFeetPosition();

        RaycastHit2D hit = Physics2D.Raycast(
            probeOrigin,
            Vector2.down,
            playerGroundProbeDistance,
            groundLayers
        );

        if (hit.collider != null)
            return hit.point.y;

        return probeOrigin.y;
    }

    Vector2 GetPlayerFeetPosition()
    {
        if (_playerGroundCheck != null)
            return _playerGroundCheck.position;

        return (Vector2)_player.position + Vector2.down * playerFeetProbeOffset;
    }

    float GetFeetY()
    {
        if (groundCheck != null)
            return groundCheck.position.y;

        return transform.position.y - playerFeetProbeOffset;
    }

    Vector2 GetGroundRayOrigin()
    {
        return groundCheck != null ? groundCheck.position : (Vector2)transform.position;
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

    void DrawFrontObstacleTopProbeGizmo(float faceDir)
    {
        if (!jumpOverFrontObstacles)
            return;

        if (Mathf.Abs(faceDir) < 0.01f)
            return;

        RaycastHit2D wallHit;

        if (!TryGetWallHitAhead(faceDir, out wallHit))
            return;

        float feetY = GetFeetY();

        Vector2 topProbeOrigin = new Vector2(
            wallHit.point.x + faceDir * obstacleTopProbeForwardOffset,
            wallHit.point.y + obstacleTopProbeExtraHeight
        );

        float probeDistance = maxObstacleJumpHeight + jumpHeightBuffer + 0.5f;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            topProbeOrigin,
            Vector2.down,
            probeDistance,
            groundLayers
        );

        Vector2 end = topProbeOrigin + Vector2.down * probeDistance;
        bool foundTop = TryFindObstacleTopY(wallHit, faceDir, feetY, out float topY);

        if (foundTop)
            end = new Vector2(topProbeOrigin.x, topY);

        Gizmos.color = foundTop ? Color.blue : new Color(0f, 0f, 1f, 0.35f);
        Gizmos.DrawLine(topProbeOrigin, end);
        Gizmos.DrawWireSphere(end, 0.07f);
    }

    void OnDrawGizmos()
    {
        Vector2 groundOrigin = GetGroundRayOrigin();
        Vector2 ceilingOrigin = GetCeilingRayOrigin();

        Vector2 groundEnd = groundOrigin + Vector2.down * groundCheckDistance;
        Vector2 ceilingEnd = ceilingOrigin + Vector2.up * ceilingCheckDistance;

        RaycastHit2D groundHit = Physics2D.Raycast(
            groundOrigin,
            Vector2.down,
            groundCheckDistance,
            groundLayers
        );

        bool grounded = groundHit.collider != null;

        if (grounded)
            groundEnd = groundHit.point;

        RaycastHit2D ceilingHit = Physics2D.Raycast(
            ceilingOrigin,
            Vector2.up,
            ceilingCheckDistance,
            GetCeilingMask()
        );

        bool ceilingAbove = ceilingHit.collider != null;

        if (ceilingAbove)
            ceilingEnd = ceilingHit.point;

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

        RaycastHit2D wallHit = Physics2D.Raycast(
            wallOrigin,
            Vector2.right * faceDir,
            wallProbeDistance,
            groundLayers
        );

        if (wallHit.collider != null)
            wallEnd = wallHit.point;

        Gizmos.color = wallHit.collider != null ? Color.cyan : new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawLine(wallOrigin, wallEnd);
        Gizmos.DrawWireSphere(wallEnd, 0.06f);

        Vector2 ledgeOrigin = GetLedgeRayOrigin(faceDir);
        Vector2 ledgeEnd = ledgeOrigin + Vector2.down * ledgeProbeDistance;

        RaycastHit2D ledgeHit = Physics2D.Raycast(
            ledgeOrigin,
            Vector2.down,
            ledgeProbeDistance,
            groundLayers
        );

        if (ledgeHit.collider != null)
            ledgeEnd = ledgeHit.point;

        Gizmos.color = ledgeHit.collider == null ? Color.yellow : new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawLine(ledgeOrigin, ledgeEnd);
        Gizmos.DrawWireSphere(ledgeEnd, 0.06f);

        DrawFrontObstacleTopProbeGizmo(faceDir);

        if (_player != null && jumpTowardPlayerPlatform)
        {
            Vector2 playerProbeOrigin = GetPlayerFeetPosition();
            Vector2 playerProbeEnd = playerProbeOrigin + Vector2.down * playerGroundProbeDistance;

            RaycastHit2D playerGroundHit = Physics2D.Raycast(
                playerProbeOrigin,
                Vector2.down,
                playerGroundProbeDistance,
                groundLayers
            );

            if (playerGroundHit.collider != null)
                playerProbeEnd = playerGroundHit.point;

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(playerProbeOrigin, 0.06f);
            Gizmos.DrawLine(playerProbeOrigin, playerProbeEnd);
            Gizmos.DrawWireSphere(playerProbeEnd, 0.08f);
        }
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
        jumpWhenCloseHorizontalDistance = Mathf.Max(0f, jumpWhenCloseHorizontalDistance);

        minObstacleHeightToJump = Mathf.Max(0f, minObstacleHeightToJump);
        maxObstacleJumpHeight = Mathf.Max(minObstacleHeightToJump, maxObstacleJumpHeight);
        obstacleTopProbeForwardOffset = Mathf.Max(0.01f, obstacleTopProbeForwardOffset);
        obstacleTopProbeExtraHeight = Mathf.Max(0f, obstacleTopProbeExtraHeight);
        wallRayStartInset = Mathf.Max(0f, wallRayStartInset);

        playerGroundProbeDistance = Mathf.Max(0.01f, playerGroundProbeDistance);
        playerFeetProbeOffset = Mathf.Max(0f, playerFeetProbeOffset);

        groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
        ceilingCheckDistance = Mathf.Max(0.01f, ceilingCheckDistance);
        wallProbeDistance = Mathf.Max(0.01f, wallProbeDistance);
        ledgeProbeDistance = Mathf.Max(0.01f, ledgeProbeDistance);
    }
}
