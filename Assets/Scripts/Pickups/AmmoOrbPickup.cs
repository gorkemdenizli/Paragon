using UnityEngine;

// Homing ammo orb spawned on enemy death. Bursts outward, waits, then glides to the player.
public class AmmoOrbPickup : MonoBehaviour
{
    private enum OrbPhase { Burst, WaitForHoming, Homing }

    [Header("Motion")]
    [SerializeField] private float waveAmplitude = 0.25f;
    [SerializeField] private float waveFrequency = 6f;
    [SerializeField] private float hoverAmplitude = 0.1f;
    [SerializeField] private float hoverFrequency = 3.5f;
    [SerializeField] private float waveDampenDistance = 1.5f;

    [Header("Idle hover (after burst, before homing)")]
    [SerializeField] private float idleHoverAmplitude = 0.08f;
    [SerializeField] private float idleHoverFrequency = 4f;
    [SerializeField] private float idleDriftAmplitude = 0.06f;
    [SerializeField] private float idleDriftFrequency = 2.5f;

    private int _ammoAmount;
    private Transform _absorbTarget;
    private float _homingSpeed = 10f;
    private float _absorbDistance = 0.35f;
    private float _wavePhase;

    private OrbPhase _phase;
    private Vector3 _burstTarget;
    private Vector3 _idleAnchor;
    private float _burstSpeed = 6f;
    private float _homingStartTime;
    private bool _absorbed;

    public void Init(
        int ammo,
        Transform absorbTarget,
        float homingSpeed,
        float absorbDistance,
        Vector3 burstOrigin,
        float burstRadius,
        float burstSpeed,
        float homingDelay)
    {
        _ammoAmount     = ammo;
        _absorbTarget   = absorbTarget;
        _homingSpeed    = homingSpeed;
        _absorbDistance = absorbDistance;
        _burstSpeed     = burstSpeed;
        _wavePhase      = Random.Range(0f, Mathf.PI * 2f);
        _homingStartTime = Time.time + homingDelay;

        transform.position = burstOrigin;
        _burstTarget = burstOrigin + (Vector3)(Random.insideUnitCircle * burstRadius);
        _phase = OrbPhase.Burst;
    }

    void Update()
    {
        if (_absorbed)
            return;

        switch (_phase)
        {
            case OrbPhase.Burst:
                UpdateBurst();
                break;
            case OrbPhase.WaitForHoming:
                UpdateWait();
                break;
            case OrbPhase.Homing:
                UpdateHoming();
                break;
        }
    }

    void UpdateBurst()
    {
        transform.position = Vector3.MoveTowards(
            transform.position, _burstTarget, _burstSpeed * Time.deltaTime);

        if ((transform.position - _burstTarget).sqrMagnitude <= 0.0025f)
        {
            _idleAnchor = transform.position;
            _phase = OrbPhase.WaitForHoming;
        }
    }

    void UpdateWait()
    {
        transform.position = _idleAnchor + GetIdleHoverOffset();

        if (Time.time >= _homingStartTime)
            _phase = OrbPhase.Homing;
    }

    Vector3 GetIdleHoverOffset()
    {
        float t = Time.time;
        return new Vector3(
            Mathf.Sin(t * idleDriftFrequency + _wavePhase) * idleDriftAmplitude,
            Mathf.Sin(t * idleHoverFrequency + _wavePhase * 1.2f) * idleHoverAmplitude,
            0f);
    }

    void UpdateHoming()
    {
        ResolveAbsorbTarget();

        if (_absorbTarget == null)
            return;

        Vector3 target   = GetHomingTarget();
        Vector3 toTarget = target - transform.position;
        float dist       = toTarget.magnitude;

        if (dist <= _absorbDistance)
        {
            Absorb();
            return;
        }

        Vector3 dir  = toTarget / dist;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        float damp   = Mathf.Clamp01(dist / waveDampenDistance);

        float wave  = Mathf.Sin(Time.time * waveFrequency + _wavePhase) * waveAmplitude * damp;
        float hover = Mathf.Sin(Time.time * hoverFrequency + _wavePhase * 1.3f) * hoverAmplitude * damp;

        Vector3 velocity = dir * _homingSpeed + perp * wave + Vector3.up * hover;
        transform.position += velocity * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_absorbed || _phase != OrbPhase.Homing || !other.CompareTag("Player"))
            return;

        Absorb();
    }

    void ResolveAbsorbTarget()
    {
        if (_absorbTarget != null)
            return;

        if (PlayerHealthController.instance != null)
            _absorbTarget = PlayerHealthController.instance.AmmoAbsorbPoint;
    }

    Vector3 GetHomingTarget()
    {
        if (_absorbTarget != null)
            return _absorbTarget.position;

        if (PlayerHealthController.instance != null)
        {
            Collider2D col = PlayerHealthController.instance.GetComponentInChildren<Collider2D>();
            if (col != null)
                return col.bounds.center;
        }

        return transform.position;
    }

    void Absorb()
    {
        if (_absorbed)
            return;

        _absorbed = true;

        Transform playerRoot = _absorbTarget != null
            ? _absorbTarget.root
            : PlayerHealthController.instance != null
                ? PlayerHealthController.instance.transform
                : null;

        Weapon weapon = playerRoot != null
            ? playerRoot.GetComponentInChildren<Weapon>()
            : null;

        if (weapon == null)
            weapon = FindFirstObjectByType<Weapon>();

        if (weapon != null && !weapon.ActiveWeaponHasInfiniteAmmo)
            weapon.AddReserveAmmo(_ammoAmount);

        Destroy(gameObject);
    }
}
