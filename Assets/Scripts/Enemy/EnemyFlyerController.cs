using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyFlyerController : MonoBehaviour
{
    [Header("Chase")]
    [SerializeField] private bool  chasePlayer             = true;
    [SerializeField] private float rangeToStartChase       = 10f;
    [SerializeField] private bool  stopChaseWhenOutOfRange = false;
    [SerializeField] private float rangeToStopChase        = 14f;

    [SerializeField] private float moveSpeed;
    [SerializeField] private float turnSpeed;
    [SerializeField] private Animator anim;

    public float BaseMoveSpeed => moveSpeed;
    public void SetMoveSpeed(float newSpeed) { moveSpeed = Mathf.Max(0f, newSpeed); }

    [Header("Chase Offset")]
    [SerializeField] private bool  usePersonalChaseOffset   = true;
    [SerializeField] private float minChaseOffsetFromPlayer = 0.2f;
    [SerializeField] private float maxChaseOffsetFromPlayer = 0.8f;
    private float _personalChaseOffsetX;

    private bool isChasing;
    private Transform player;
    private Collider2D _playerHurtbox;
    private Rigidbody2D _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        CachePlayer();

        if (usePersonalChaseOffset)
        {
            float r = Random.Range(minChaseOffsetFromPlayer, maxChaseOffsetFromPlayer);
            _personalChaseOffsetX = Random.value > 0.5f ? r : -r;
        }
    }

    void CachePlayer()
    {
        if (PlayerHealthController.instance == null) return;

        player = PlayerHealthController.instance.transform;
        _playerHurtbox = null;

        foreach (var col in player.GetComponentsInChildren<Collider2D>())
        {
            if (col.CompareTag("Player"))
            {
                _playerHurtbox = col;
                break;
            }
        }
    }

    Vector3 GetChaseTargetCenter()
    {
        if (_playerHurtbox != null)
            return _playerHurtbox.bounds.center;

        return player != null ? player.position : transform.position;
    }

    void Update()
    {
        if (!chasePlayer) return;
        if (player == null) CachePlayer();
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, GetChaseTargetCenter());
        if (!isChasing)
        {
            if (dist < rangeToStartChase)
            {
                isChasing = true;
                anim?.SetBool("isChasing", true);
            }
        }
        else if (stopChaseWhenOutOfRange && dist > rangeToStopChase)
        {
            isChasing = false;
            anim?.SetBool("isChasing", false);
        }
    }

    void FixedUpdate()
    {
        if (!chasePlayer || !isChasing || player == null || !player.gameObject.activeSelf) return;

        Vector3 chaseCenter = GetChaseTargetCenter();
        Vector3 targetPos = new Vector3(
            chaseCenter.x + (usePersonalChaseOffset ? _personalChaseOffsetX : 0f),
            chaseCenter.y,
            chaseCenter.z);

        Vector3 direction  = transform.position - targetPos;
        float targetAngle  = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float newAngle     = Mathf.LerpAngle(_rb.rotation, targetAngle, turnSpeed * Time.fixedDeltaTime);
        _rb.MoveRotation(newAngle);

        float rad    = newAngle * Mathf.Deg2Rad;
        Vector2 facing = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        _rb.MovePosition(_rb.position - facing * moveSpeed * Time.fixedDeltaTime);
    }
}
