using UnityEngine;

// Straight-line projectile; initialized by Weapon.TryShoot via Initialize().
public class Bullet : MonoBehaviour
{
    [SerializeField] private Rigidbody2D theRB;
    [SerializeField] private GameObject impactEffect;

    private Vector2 _dir;
    private float _speed;
    private int _damage;
    private bool _isCrit;

    // Sets direction, speed, damage from weapon; applies rotation and velocity.
    public void Initialize(Vector2 direction, float speed, int damage, bool isCrit = false)
    {
        _dir    = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _speed  = speed;
        _damage = Mathf.Max(1, damage);
        _isCrit = isCrit;

        float z = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, z);

        if (theRB != null)
            theRB.linearVelocity = _dir * _speed;
    }

    void FixedUpdate()
    {
        if (theRB == null)
            return;
        theRB.linearVelocity = _dir * _speed;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        bool hitEnemy = false;
        bool killed   = false;

        if (collision.CompareTag("Enemy"))
        {
            var ehc = collision.GetComponent<EnemyHealthController>();
            if (ehc != null)
            {
                killed   = ehc.DamageEnemy(_damage, _isCrit);
                hitEnemy = true;
            }

            if (!killed)
                collision.GetComponent<EnemyKnockback>()?.Apply(_dir);
        }

        if (collision.CompareTag("Boss") && BossHealthController.instance != null)
        {
            killed   = BossHealthController.instance.DamageBoss(_damage);
            hitEnemy = true;
        }

        if (hitEnemy)
            HitmarkerController.instance?.ShowHit(killed);

        if (impactEffect != null)
            Instantiate(impactEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    void OnBecameInvisible()
    {
        Destroy(gameObject);
    }
}
