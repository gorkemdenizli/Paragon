using UnityEngine;

public class BombController : MonoBehaviour
{
    [SerializeField] private float timeToExplode = .5f;
    [SerializeField] private GameObject explosion;
    [SerializeField] private float blastRange = 2f;
    [SerializeField] private float damageAmount = 50f;
    [Tooltip("Patlama sınırındaki hedeflere verilecek hasarın oranı (0-1). 0.5 = yarım hasar.")]
    [SerializeField] [Range(0f, 1f)] private float edgeDamageRatio = 0.5f;
    [SerializeField] private LayerMask whatIsDamageable;

    void Update()
    {
        timeToExplode -= Time.deltaTime;

        if (timeToExplode <= 0)
        {
            Explode();
        }
    }

    void Explode()
    {
        if (explosion != null)
            Instantiate(explosion, transform.position, transform.rotation);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, blastRange, whatIsDamageable);

        foreach (Collider2D col in hits)
        {
            EnemyHealthController enemyHealth = col.GetComponent<EnemyHealthController>();
            if (enemyHealth == null)
                continue;

            float distance = Vector2.Distance(transform.position, col.bounds.center);
            float normalizedDist = blastRange > 0f ? Mathf.Clamp01(distance / blastRange) : 0f;
            float damageMult = Mathf.Lerp(1f, edgeDamageRatio, normalizedDist);
            int finalDamage = Mathf.RoundToInt(damageAmount * damageMult);

            enemyHealth.DamageEnemy(finalDamage);
        }

        Destroy(gameObject);
    }
}
