using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthController : MonoBehaviour
{
    public static PlayerHealthController instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        } 
        else 
        {
            Destroy(gameObject);
        }
    }

    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private int maxHealth;
    public int currentHealth;

    private int _baseMaxHealth;
    [SerializeField] private float invincibilityLength;
    private float invincibilityCounter;
    [SerializeField] private float flashLength = 0.1f;
    private float flashCounter;
    [SerializeField] private SpriteRenderer[] playerSprites;

    [Header("Armor Hit Flash")]
    [SerializeField] private float armorFlashHold = 0.05f;
    [SerializeField] private float armorFlashFade = 0.12f;
    private static readonly Color ArmorFlashColor = new Color(0f, 0.831f, 1f, 1f); // #00D4FF
    private Color[]   _spriteOriginalColors;
    private Coroutine _armorFlashRoutine;

    [Header("Ammo orb homing")]
    [Tooltip("Orbs fly toward this point (torso center). Falls back to player collider center.")]
    [SerializeField] private Transform ammoAbsorbPoint;

    public Transform AmmoAbsorbPoint
    {
        get
        {
            if (ammoAbsorbPoint != null)
                return ammoAbsorbPoint;

            Collider2D col = GetComponentInChildren<Collider2D>();
            return col != null ? col.transform : transform;
        }
    }

    // Drives the flash visual independently from invincibility iframes.
    // Set by StartFlash(); checked each Update to toggle sprites.
    private float _flashActiveTimer;

    void Start()
    {
        _baseMaxHealth = maxHealth;
        currentHealth  = maxHealth; // initialize before ApplyStatMultipliers reads it

        // Auto-find sprites on the same GameObject if none assigned in Inspector.
        if (playerSprites == null || playerSprites.Length == 0)
            playerSprites = GetComponentsInChildren<SpriteRenderer>();

        _spriteOriginalColors = new Color[playerSprites.Length];
        for (int i = 0; i < playerSprites.Length; i++)
            _spriteOriginalColors[i] = playerSprites[i].color;

        PlayerStats.instance?.RegisterBaseValue(StatType.MaxHealth, _baseMaxHealth);
        ApplyStatMultipliers();

        if (PlayerStats.instance != null)
            PlayerStats.instance.OnStatsChanged += ApplyStatMultipliers;
    }

    private void OnDestroy()
    {
        if (PlayerStats.instance != null)
            PlayerStats.instance.OnStatsChanged -= ApplyStatMultipliers;
    }

    private void ApplyStatMultipliers()
    {
        float mult    = PlayerStats.instance != null ? PlayerStats.instance.MaxHealthMultiplier : 1f;
        int newMax    = Mathf.RoundToInt(_baseMaxHealth * mult);
        int delta     = newMax - maxHealth;
        maxHealth     = newMax;
        currentHealth = Mathf.Clamp(currentHealth + delta, 1, maxHealth);
        UpdateHealthSlider(currentHealth, maxHealth);
    }

    void Update()
    {
        if (invincibilityCounter > 0)
            invincibilityCounter -= Time.deltaTime;

        if (_flashActiveTimer <= 0f)
            return;

        _flashActiveTimer -= Time.deltaTime;
        flashCounter     -= Time.deltaTime;

        if (flashCounter <= 0f)
        {
            float interval = Mathf.Max(0.05f, flashLength);
            foreach (SpriteRenderer sr in playerSprites)
                sr.enabled = !sr.enabled;
            flashCounter = interval;
        }

        if (_flashActiveTimer <= 0f)
        {
            foreach (SpriteRenderer sr in playerSprites)
                sr.enabled = true;
            flashCounter = 0f;
        }
    }

    void StartArmorFlash()
    {
        if (_armorFlashRoutine != null)
            StopCoroutine(_armorFlashRoutine);
        _armorFlashRoutine = StartCoroutine(ArmorFlashRoutine());
    }

    IEnumerator ArmorFlashRoutine()
    {
        foreach (var sr in playerSprites)
            if (sr != null) sr.color = ArmorFlashColor;

        yield return new WaitForSeconds(armorFlashHold);

        float elapsed = 0f;
        while (elapsed < armorFlashFade)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / armorFlashFade);
            for (int i = 0; i < playerSprites.Length; i++)
                if (playerSprites[i] != null)
                    playerSprites[i].color = Color.Lerp(ArmorFlashColor, _spriteOriginalColors[i], t);
            yield return null;
        }

        for (int i = 0; i < playerSprites.Length; i++)
            if (playerSprites[i] != null)
                playerSprites[i].color = _spriteOriginalColors[i];

        _armorFlashRoutine = null;
    }

    // Starts the sprite flash. Triggers on health damage only.
    void StartFlash()
    {
        _flashActiveTimer = Mathf.Max(0.1f, invincibilityLength);
        flashCounter      = 0f;

        foreach (SpriteRenderer sr in playerSprites)
            sr.enabled = false;
    }

    public void UpdateHealthSlider(int currentHealth, int maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (healthText != null)
        {
            healthText.text = currentHealth + " / " + maxHealth;
        }
    }

    public void DamagePlayer(int damageAmount)
    {
        if (invincibilityCounter > 0)
            return;

        // Armor absorbs damage first; remainder hits health.
        int healthDamage = ArmorController.instance != null
            ? ArmorController.instance.ProcessDamage(damageAmount)
            : damageAmount;

        if (ArmorController.instance != null && healthDamage < damageAmount)
            StartArmorFlash();

        if (healthDamage <= 0)
            return; // armor absorbed all — no health damage, no flash, no iframes

        StartFlash();
        currentHealth -= healthDamage;
        invincibilityCounter = invincibilityLength;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            RespawnController.instance.Respawn();
            return;
        }

        UpdateHealthSlider(currentHealth, maxHealth);
    }

    public void fillHealth()
    {
        currentHealth = maxHealth;

        UpdateHealthSlider(currentHealth, maxHealth);
    }

    public void HealPlayer(int healAmount)
    {
        currentHealth += healAmount;

        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        UpdateHealthSlider(currentHealth, maxHealth);
    }
}
