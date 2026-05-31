using UnityEngine;

public class BossBullet : MonoBehaviour
{
    [SerializeField] private float bulletSpeed;
    [SerializeField] private Rigidbody2D theRB;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private int damageAmount;
    [Tooltip("World-space offset added to the player's transform position before aiming. Increase Y to target the player's center or chest instead of feet.")]
    [SerializeField] private Vector2 aimOffset = Vector2.zero;

    void Start()
    {
        Vector3 target = PlayerHealthController.instance.transform.position + (Vector3)aimOffset;
        Vector3 directionToPlayer = transform.position - target;
        float angle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    // Update is called once per frame
    void Update()
    {
        theRB.linearVelocity = -transform.right * bulletSpeed;
    }

        public void SetDamage(int dmg) => damageAmount = dmg;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHealthController.instance.DamagePlayer(damageAmount);
        }

        if (impactEffect != null)
        {
            Instantiate(impactEffect, transform.position, transform.rotation);
        }

        Destroy(gameObject);
    }
}
