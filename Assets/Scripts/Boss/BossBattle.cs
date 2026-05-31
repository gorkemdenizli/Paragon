using UnityEngine;

public class BossBattle : MonoBehaviour
{
    [SerializeField] private int treshold1;
    [SerializeField] private int treshold2;
    [SerializeField] private float activeTime;
    [SerializeField] private float fadeOutTime;
    [SerializeField] private float inactiveTime;
    [SerializeField] private float moveSpeed;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Animator anim;
    [SerializeField] private Transform theBoss;
    [SerializeField] private float timeBetweenShots1;
    [SerializeField] private float timeBetweenShots2;
    [SerializeField] private GameObject bullet;
    [SerializeField] private Transform shotPoint;

    private float shotCounter;
    private float activeCounter;
    private float fadeOutCounter;
    private float inactiveCounter;
    private Transform targetPoint;

    // Set by BossSpawnerController at spawn time
    private int _phase2Threshold;      // ~83% of scaled health → Phase 1→2 boundary
    private int _phase3Threshold;      // ~33% of scaled health → Phase 2→3 boundary
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

    void Update()
    {
        int hp = BossHealthController.instance.currentHealth;

        // If SetupDifficulty was not called, fall back to original treshold1 for Phase 1 boundary
        bool inPhase1 = _phase2Threshold > 0 ? hp > _phase2Threshold : hp > treshold1;
        bool inPhase3 = _phase2Threshold > 0 && hp <= _phase3Threshold;

        if (inPhase1)
        {
            Phase1Update();
        }
        else
        {
            Phase23Update(inPhase3);
        }
    }

    void Phase1Update()
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
                shotCounter = timeBetweenShots1;
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
                theBoss.position  = spawnPoints[Random.Range(0, spawnPoints.Length)].position;
                theBoss.gameObject.SetActive(true);
                activeCounter = activeTime;
                shotCounter   = timeBetweenShots1;
            }
        }
    }

    void Phase23Update(bool inPhase3)
    {
        // Phase 3 always uses fast fire rate; Phase 2 depends on player health
        float shotInterval = (inPhase3 || PlayerHealthController.instance.currentHealth <= treshold2)
            ? timeBetweenShots2
            : timeBetweenShots1;

        if (targetPoint == null)
        {
            targetPoint    = theBoss;
            fadeOutCounter = fadeOutTime;
            anim.SetTrigger("vanish");
        }
        else
        {
            if (Vector3.Distance(theBoss.position, targetPoint.position) > 0.2f)
            {
                theBoss.position = Vector3.MoveTowards(
                    theBoss.position, targetPoint.position, moveSpeed * Time.deltaTime);

                if (Vector3.Distance(theBoss.position, targetPoint.position) <= 0.2f)
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
                    theBoss.position = spawnPoints[Random.Range(0, spawnPoints.Length)].position;

                    targetPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                    int whileBreaker = 0;
                    while (targetPoint.position == theBoss.position && whileBreaker < 100)
                    {
                        targetPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                        whileBreaker++;
                    }

                    theBoss.gameObject.SetActive(true);
                    shotCounter = shotInterval;
                }
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
