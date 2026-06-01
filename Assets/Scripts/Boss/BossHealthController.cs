using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthController : MonoBehaviour
{
    public static BossHealthController instance;

    private HitFlash _hitFlash;

    private void Awake()
    {
        instance = this;
        _hitFlash = GetComponent<HitFlash>();

        // Damage number canvas'ını her zaman aktif kök objeye taşı. Aksi halde boss ışınlanırken
        // (BossBattle, Sprite Boss child'ını SetActive(false) yapıyor) canvas deaktive olup
        // içindeki DamagePopup coroutine'leri ölüyor → popuplar donup boss üstünde birikiyor.
        if (damageNumbersCanvas != null)
            damageNumbersCanvas.SetParent(transform, worldPositionStays: true);
    }

    [SerializeField] private Slider bossHealthSlider;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] public int currentHealth;
    [SerializeField] private int maxHealth;
    [SerializeField] private BossBattle theBoss;
    [Tooltip("Boss ölünce zafer ekranı bu kadar saniye sonra gelir (ölüm animasyonuna pay).")]
    [SerializeField] private float victoryDelay = 1.5f;

    [Header("Damage Numbers")]
    [Tooltip("World Space Canvas (RectTransform) on the boss — damage popups spawn here.")]
    [SerializeField] private RectTransform damageNumbersCanvas;
    [Tooltip("DamagePopup prefab — same as used by EnemyHealthController.")]
    [SerializeField] private DamagePopup   damagePopupPrefab;

    private bool _initialized;

    public void Initialize(int health, Slider slider, TMP_Text text)
    {
        _initialized     = true;
        bossHealthSlider = slider;
        healthText       = text;
        maxHealth        = health;
        currentHealth    = health;
        UpdateHealthSlider(currentHealth, maxHealth);
    }

    void Start()
    {
        if (_initialized) return;
        currentHealth = maxHealth;
        UpdateHealthSlider(currentHealth, maxHealth);
    }

    public void UpdateHealthSlider(int currentHealth, int maxHealth)
    {
        if (bossHealthSlider != null)
        {
            bossHealthSlider.maxValue = maxHealth;
            bossHealthSlider.value = currentHealth;
        }

        if (healthText != null)
        {
            healthText.text = currentHealth + " / " + maxHealth;
        }
    }

    // Returns true if this hit killed the boss.
    public bool DamageBoss(int damageAmount)
    {
        _hitFlash?.Flash();
        SpawnDamageNumber(damageAmount);

        currentHealth -= damageAmount;

        bool killed = false;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            theBoss.EndBattle();
            GameOverScreenController.instance?.ShowVictoryAfter(victoryDelay);
            killed = true;
        }

        UpdateHealthSlider(currentHealth, maxHealth);
        return killed;
    }

    void SpawnDamageNumber(int damage)
    {
        if (damagePopupPrefab == null || damageNumbersCanvas == null) return;

        DamagePopup popup = Instantiate(damagePopupPrefab, damageNumbersCanvas);
        var rt = popup.GetComponent<RectTransform>();

        // Canvas artık kök objede (hep aktif). Popup'ı boss'un anlık görsel konumunda doğur,
        // sonra küçük rastgele bir yayılma ekle. Popup bağımsız süzülüp kendini yok eder.
        Transform vis = theBoss != null ? theBoss.Visual : transform;
        rt.position = vis.position;

        Rect r = damageNumbersCanvas.rect;
        rt.anchoredPosition += new Vector2(
            Random.Range(-r.width  * 0.5f, r.width  * 0.5f),
            Random.Range(-r.height * 0.5f, r.height * 0.5f));

        popup.Init(damage);
    }
}
