using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthController : MonoBehaviour
{
    [SerializeField] private int totalHealth;
    [SerializeField] private int xpReward = 100;
    [SerializeField] private GameObject deathEffect;
    [SerializeField] private EnemyDrops enemyDrops;

    [Header("Health Bar")]
    [Tooltip("CanvasGroup on the health bar canvas root — controls visibility.")]
    [SerializeField] private CanvasGroup healthBarGroup;
    [SerializeField] private Slider healthBarSlider;
    [Tooltip("Seconds after last hit before the health bar fades out.")]
    [SerializeField] private float hideDelay = 5f;
    [Tooltip("Seconds the fade-out animation takes.")]
    [SerializeField] private float fadeDuration = 0.4f;

    [Header("Damage Numbers")]
    [Tooltip("RectTransform of the DamageNumbers canvas (child of this enemy).")]
    [SerializeField] private RectTransform damageNumbersCanvas;
    [Tooltip("DamagePopup prefab — the Text object with DamagePopup script.")]
    [SerializeField] private DamagePopup damagePopupPrefab;

    private int _maxHealth;
    private bool _isDead;
    private HitFlash _hitFlash;
    private Coroutine _hideRoutine;

    void Awake()
    {
        if (enemyDrops == null)
            enemyDrops = GetComponent<EnemyDrops>();
        _hitFlash = GetComponent<HitFlash>();

        _maxHealth = totalHealth;

        if (healthBarSlider != null)
        {
            healthBarSlider.minValue = 0;
            healthBarSlider.maxValue = _maxHealth;
            healthBarSlider.value    = _maxHealth;
        }

        if (healthBarGroup != null)
            healthBarGroup.alpha = 0f;
    }

    // Returns true if this hit killed the enemy.
    public bool DamageEnemy(int damageAmount, bool isCrit = false)
    {
        if (_isDead) return true;
        _hitFlash?.Flash();

        totalHealth -= damageAmount;

        if (healthBarSlider != null)
            healthBarSlider.value = Mathf.Max(0, totalHealth);

        if (totalHealth <= 0)
        {
            _isDead = true;

            if (deathEffect != null)
                Instantiate(deathEffect, transform.position, transform.rotation);

            if (enemyDrops != null)
                enemyDrops.TrySpawnDrops(transform.position);

            RunLevelManager.instance?.AddXP(xpReward);

            Destroy(gameObject);
            return true;
        }

        ShowHealthBar();
        SpawnDamageNumber(damageAmount, isCrit);
        return false;
    }

    void SpawnDamageNumber(int damage, bool isCrit = false)
    {
        if (damagePopupPrefab == null || damageNumbersCanvas == null) return;

        Rect r      = damageNumbersCanvas.rect;
        float halfW = r.width  * 0.5f;
        float halfH = r.height * 0.5f;

        Vector2 randomPos = new Vector2(
            Random.Range(-halfW, halfW),
            Random.Range(-halfH, halfH)
        );

        DamagePopup popup = Instantiate(damagePopupPrefab, damageNumbersCanvas);
        popup.GetComponent<RectTransform>().anchoredPosition = randomPos;
        popup.Init(damage, isCrit);
    }

    void ShowHealthBar()
    {
        if (healthBarGroup == null) return;

        healthBarGroup.alpha = 1f;

        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (healthBarGroup != null)
                healthBarGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        if (healthBarGroup != null)
            healthBarGroup.alpha = 0f;

        _hideRoutine = null;
    }
}
