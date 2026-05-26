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
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        activeCounter = activeTime;

        shotCounter = timeBetweenShots1;
    }

    // Update is called once per frame
    void Update()
    {
        if (BossHealthController.instance.currentHealth > treshold1)
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
                    Instantiate(bullet, shotPoint.position, Quaternion.identity);
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
                    theBoss.gameObject.SetActive(true);

                    activeCounter = activeTime;

                    shotCounter = timeBetweenShots1;
                }
            }
        }
        else
        {
            if (targetPoint == null)
            {
                targetPoint = theBoss;
                fadeOutCounter = fadeOutTime;
                anim.SetTrigger("vanish");
            }
            else
            {
                if (Vector3.Distance(theBoss.position, targetPoint.position) > 0.2f)
                {
                    theBoss.position = Vector3.MoveTowards(theBoss.position, targetPoint.position, moveSpeed * Time.deltaTime);

                    if (Vector3.Distance(theBoss.position, targetPoint.position) <= 0.2f)
                    {
                        fadeOutCounter = fadeOutTime;
                        anim.SetTrigger("vanish");
                    }
                    
                    shotCounter -= Time.deltaTime;
                    if (shotCounter <= 0)
                    {
                        if (PlayerHealthController.instance.currentHealth > treshold2)
                        {
                            shotCounter = timeBetweenShots1;
                        }
                        else
                        {
                            shotCounter = timeBetweenShots2;
                        }

                        Instantiate(bullet, shotPoint.position, Quaternion.identity);
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

                        if (PlayerHealthController.instance.currentHealth > treshold2)
                        {
                            shotCounter = timeBetweenShots1;
                        }
                        else
                        {
                            shotCounter = timeBetweenShots2;
                        }
                    }
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
        if (bullets.Length > 0)
        {
            foreach (BossBullet bb in bullets)
            {
                Destroy(bb.gameObject);
            }
        }
    }
}
