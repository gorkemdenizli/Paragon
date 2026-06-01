using UnityEngine;

public class BossBattle : MonoBehaviour
{
    [SerializeField] private int treshold1;
    [SerializeField] private int treshold2;
    [SerializeField] private float activeTime;
    [SerializeField] private float fadeOutTime;
    [SerializeField] private float inactiveTime;
    [SerializeField] private float moveSpeed;
    [SerializeField] private Animator anim;
    [SerializeField] private Transform theBoss;
    [SerializeField] private float timeBetweenShots1;
    [SerializeField] private float timeBetweenShots2;
    [SerializeField] private GameObject bullet;
    [SerializeField] private Transform shotPoint;

    [Header("Player-Relative Positioning")]
    [Tooltip("Player'ın Y'sinin kaç birim üstüne minimum konumlanılsın.")]
    [SerializeField] private float spawnAbovePlayerYMin = 2f;
    [Tooltip("Player'ın Y'sinin kaç birim üstüne maksimum konumlanılsın.")]
    [SerializeField] private float spawnAbovePlayerYMax = 5f;
    [Tooltip("Player'a göre maksimum yatay (X) sapma.")]
    [SerializeField] private float spawnXRange = 4f;

    // Boss'un görsel (ışınlanırken hareket eden) transform'u. Damage number'lar bunun
    // anlık world konumunda doğsun diye dışarı açılır.
    public Transform Visual => theBoss;

    private float shotCounter;
    private float activeCounter;
    private float fadeOutCounter;
    private float inactiveCounter;

    // Phase 3 glide state
    private Vector3 _glideTarget;
    private bool _inPhase3;

    // Set by BossSpawnerController at spawn time
    private int _phase2Threshold;
    private int _phase3Threshold;
    private int _scaledBulletDamage;

    public void SetupDifficulty(int scaledMax, float phase2Frac, float phase3Frac, int bulletDmg)
    {
        _phase2Threshold    = Mathf.RoundToInt(scaledMax * phase2Frac);
        _phase3Threshold    = Mathf.RoundToInt(scaledMax * phase3Frac);
        _scaledBulletDamage = bulletDmg;
    }

    void Start()
    {
        activeCounter = activeTime;
        shotCounter   = timeBetweenShots1;
    }

    void SpawnBullet()
    {
        var go = Instantiate(bullet, shotPoint.position, Quaternion.identity);
        if (_scaledBulletDamage > 0)
            go.GetComponent<BossBullet>()?.SetDamage(_scaledBulletDamage);
    }

    Vector3 GetAbovePlayerPos()
    {
        if (PlayerHealthController.instance == null) return theBoss.position;
        Vector3 p = PlayerHealthController.instance.transform.position;
        return new Vector3(
            p.x + Random.Range(-spawnXRange, spawnXRange),
            p.y + Random.Range(spawnAbovePlayerYMin, spawnAbovePlayerYMax),
            theBoss.position.z);
    }

    void Update()
    {
        int hp = BossHealthController.instance.currentHealth;
        bool inPhase3 = _phase2Threshold > 0 && hp <= _phase3Threshold;
        bool inPhase2 = _phase2Threshold > 0 && hp <= _phase2Threshold && !inPhase3;

        if (inPhase3)
        {
            if (!_inPhase3) EnterPhase3();
            _inPhase3 = true;
            GlidePhaseUpdate();
        }
        else
        {
            _inPhase3 = false;
            TeleportPhaseUpdate(inPhase2);
        }
    }

    // Phase 1 + Phase 2: ışınlan → ateş et → ışınlan
    void TeleportPhaseUpdate(bool inPhase2)
    {
        float shotInterval = (inPhase2 && PlayerHealthController.instance.currentHealth <= treshold2)
            ? timeBetweenShots2 : timeBetweenShots1;

        if (activeCounter > 0)
        {
            activeCounter -= Time.deltaTime;
            if (activeCounter <= 0)
            {
                fadeOutCounter = fadeOutTime;
                anim.SetTrigger("vanish");
            }

            shotCounter -= Time.deltaTime;
            if (shotCounter <= 0)
            {
                shotCounter = shotInterval;
                SpawnBullet();
            }
        }
        else if (fadeOutCounter > 0)
        {
            fadeOutCounter -= Time.deltaTime;
            if (fadeOutCounter <= 0)
            {
                theBoss.gameObject.SetActive(false);
                inactiveCounter = inactiveTime;
            }
        }
        else if (inactiveCounter > 0)
        {
            inactiveCounter -= Time.deltaTime;
            if (inactiveCounter <= 0)
            {
                theBoss.position = GetAbovePlayerPos();
                theBoss.gameObject.SetActive(true);
                activeCounter = activeTime;
                shotCounter   = shotInterval;
            }
        }
    }

    // Phase 3'e ilk girişte çağrılır
    void EnterPhase3()
    {
        activeCounter   = 0f;
        fadeOutCounter  = 0f;
        inactiveCounter = 0f;
        if (!theBoss.gameObject.activeSelf) theBoss.gameObject.SetActive(true);
        _glideTarget = GetAbovePlayerPos();
    }

    // Phase 3: süzül (sessiz) → var → ateş et → fade → inactive → yeni hedef → tekrar
    void GlidePhaseUpdate()
    {
        if (activeCounter > 0)
        {
            activeCounter -= Time.deltaTime;
            if (activeCounter <= 0)
            {
                fadeOutCounter = fadeOutTime;
                anim.SetTrigger("vanish");
            }

            shotCounter -= Time.deltaTime;
            if (shotCounter <= 0)
            {
                shotCounter = timeBetweenShots2;
                SpawnBullet();
            }
        }
        else if (fadeOutCounter > 0)
        {
            fadeOutCounter -= Time.deltaTime;
            if (fadeOutCounter <= 0)
            {
                theBoss.gameObject.SetActive(false);
                inactiveCounter = inactiveTime;
            }
        }
        else if (inactiveCounter > 0)
        {
            inactiveCounter -= Time.deltaTime;
            if (inactiveCounter <= 0)
            {
                _glideTarget = GetAbovePlayerPos();
                theBoss.gameObject.SetActive(true);
            }
        }
        else
        {
            // Hedefe doğru süzülme — bu sırada ateş yok
            if (Vector3.Distance(theBoss.position, _glideTarget) > 0.2f)
            {
                theBoss.position = Vector3.MoveTowards(
                    theBoss.position, _glideTarget, moveSpeed * Time.deltaTime);
            }
            else
            {
                // Hedere vardı → ateş fazı başlar
                activeCounter = activeTime;
                shotCounter   = timeBetweenShots2;
            }
        }
    }

    public void EndBattle()
    {
        gameObject.SetActive(false);

        fadeOutCounter = fadeOutTime;
        anim.SetTrigger("vanish");
        theBoss.GetComponent<Collider2D>().enabled = false;

        BossBullet[] bullets = FindObjectsByType<BossBullet>(FindObjectsSortMode.None);
        foreach (BossBullet bb in bullets)
            Destroy(bb.gameObject);
    }
}
