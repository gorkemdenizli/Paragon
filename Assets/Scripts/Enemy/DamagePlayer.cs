using System.Collections;
using UnityEngine;

public class DamagePlayer : MonoBehaviour
{
    [SerializeField] private int damageAmount;
    [SerializeField] private bool destroyOnDamage;
    [SerializeField] private GameObject destroyEffect;
    [Tooltip("Seconds between damage ticks while the player stays inside the trigger.")]
    [SerializeField] private float damageTickInterval = 1f;

    private int _playerInsideCount;
    private Coroutine _damageLoop;

    public bool IsPlayerInContact => _playerInsideCount > 0;
    public int BaseDamageAmount => damageAmount;
    public void SetDamage(int newDamage) { damageAmount = Mathf.Max(0, newDamage); }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_playerInsideCount++ == 0)
            _damageLoop = StartCoroutine(DamageWhileInside());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (--_playerInsideCount > 0) return;

        if (_damageLoop != null)
        {
            StopCoroutine(_damageLoop);
            _damageLoop = null;
        }
    }

    private IEnumerator DamageWhileInside()
    {
        TryDamage();

        while (_playerInsideCount > 0)
        {
            yield return new WaitForSeconds(damageTickInterval);
            if (_playerInsideCount <= 0) break;
            TryDamage();
        }
    }

    private void TryDamage()
    {
        if (PlayerHealthController.instance == null) return;

        PlayerHealthController.instance.DamagePlayer(damageAmount);

        if (!destroyOnDamage) return;

        if (destroyEffect != null)
            Instantiate(destroyEffect, transform.position, transform.rotation);

        Destroy(gameObject);
    }
}
