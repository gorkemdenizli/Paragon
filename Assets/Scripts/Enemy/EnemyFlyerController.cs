using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyFlyerController : MonoBehaviour
{
    [SerializeField] private float rangeToStartChase;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float turnSpeed;
    [SerializeField] private Animator anim;

    public float BaseMoveSpeed => moveSpeed;
    public void SetMoveSpeed(float newSpeed) { moveSpeed = Mathf.Max(0f, newSpeed); }

    [Header("Chase Offset")]
    [SerializeField] private bool  usePersonalChaseOffset   = true;
    [SerializeField] private float minChaseOffsetFromPlayer = 0.35f;
    [SerializeField] private float maxChaseOffsetFromPlayer = 1.4f;
    private float _personalChaseOffsetX;

    private bool isChasing;
    private Transform player;
    private Rigidbody2D _rb;

    void Start()
    {
        _rb    = GetComponent<Rigidbody2D>();
        player = PlayerHealthController.instance.transform;

        if (usePersonalChaseOffset)
        {
            float r = Random.Range(minChaseOffsetFromPlayer, maxChaseOffsetFromPlayer);
            _personalChaseOffsetX = Random.value > 0.5f ? r : -r;
        }
    }

    void Update()
    {
        if (player == null) return;
        if (!isChasing)
        {
            if (Vector3.Distance(transform.position, player.position) < rangeToStartChase)
            {
                isChasing = true;
                anim.SetBool("isChasing", isChasing);
            }
        }
    }

    void FixedUpdate()
    {
        if (!isChasing || player == null || !player.gameObject.activeSelf) return;

        Vector3 targetPos = new Vector3(
            player.position.x + (usePersonalChaseOffset ? _personalChaseOffsetX : 0f),
            player.position.y,
            player.position.z);

        Vector3 direction  = transform.position - targetPos;
        float targetAngle  = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float newAngle     = Mathf.LerpAngle(_rb.rotation, targetAngle, turnSpeed * Time.fixedDeltaTime);
        _rb.MoveRotation(newAngle);

        float rad    = newAngle * Mathf.Deg2Rad;
        Vector2 facing = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        _rb.MovePosition(_rb.position - facing * moveSpeed * Time.fixedDeltaTime);
    }
}
