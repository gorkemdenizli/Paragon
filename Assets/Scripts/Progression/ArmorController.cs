using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ArmorController : MonoBehaviour
{
    public enum ArmorType { None, Light, Medium, Heavy }

    [System.Serializable]
    public struct ArmorStats
    {
        [Tooltip("Maximum armor points.")]
        public int maxArmor;
        [Tooltip("Seconds after last hit before armor starts refilling.")]
        public float rechargeDelay;
        [Tooltip("Seconds it takes to go from 0 to full armor once refill begins.")]
        public float refillDuration;
        [Tooltip("Multiplier applied to player walk and run speed (1 = no change).")]
        public float speedMultiplier;
    }

    public static ArmorController instance;

    [Header("Armor Type")]
    [SerializeField] private ArmorType armorType = ArmorType.Light;

    [Header("Stats Per Type")]
    [SerializeField] private ArmorStats lightStats  = new() { maxArmor = 50,  rechargeDelay = 2f, refillDuration = 1.0f, speedMultiplier = 1.2f };
    [SerializeField] private ArmorStats mediumStats = new() { maxArmor = 75,  rechargeDelay = 3f, refillDuration = 1.5f, speedMultiplier = 1.0f };
    [SerializeField] private ArmorStats heavyStats  = new() { maxArmor = 100, rechargeDelay = 4f, refillDuration = 2.0f, speedMultiplier = 0.8f };

    [Header("UI")]
    [SerializeField] private Slider armorSlider;
    [SerializeField] private TMP_Text armorText;

    // Public read-only access to per-type stats (used by LoadoutController for display).
    public ArmorStats LightStats  => lightStats;
    public ArmorStats MediumStats => mediumStats;
    public ArmorStats HeavyStats  => heavyStats;

    // Read by PlayerController to scale walk/run speed.
    public float SpeedMultiplier => armorType == ArmorType.None ? 1f : CurrentStats.speedMultiplier;

    public int CurrentArmor  => _currentArmor;
    public int MaxArmor      => armorType == ArmorType.None ? 0
        : Mathf.RoundToInt(CurrentStats.maxArmor * (PlayerStats.instance?.MaxArmorMultiplier ?? 1f));

    private ArmorStats CurrentStats => armorType switch
    {
        ArmorType.Light  => lightStats,
        ArmorType.Medium => mediumStats,
        ArmorType.Heavy  => heavyStats,
        _                => default
    };

    private int   _currentArmor;
    private float _armorFloat;    // fractional armor used during gradual refill
    private float _rechargeTimer; // countdown until refill begins

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

    private void Start()
    {
        if (LoadoutManager.instance != null)
            armorType = LoadoutManager.instance.EquippedArmorType;

        PlayerStats.instance?.RegisterBaseValue(StatType.MaxArmor, CurrentStats.maxArmor);

        if (PlayerStats.instance != null)
            PlayerStats.instance.OnStatsChanged += OnStatUpgraded;

        FillArmor();
    }

    private void OnDestroy()
    {
        if (PlayerStats.instance != null)
            PlayerStats.instance.OnStatsChanged -= OnStatUpgraded;
    }

    private void OnStatUpgraded()
    {
        // Re-clamp current armor to the new (higher) max without resetting it.
        int newMax = MaxArmor;
        if (_currentArmor > newMax) _currentArmor = newMax;
        _armorFloat = Mathf.Max(_armorFloat, _currentArmor);
        UpdateUI();
    }

    private void Update()
    {
        int max = MaxArmor;
        if (armorType == ArmorType.None || _currentArmor >= max)
            return;

        // Phase 1 – waiting after last hit.
        if (_rechargeTimer > 0f)
        {
            _rechargeTimer -= Time.deltaTime;
            return;
        }

        // Phase 2 – gradually fill armor.
        float rate = max / Mathf.Max(0.01f, CurrentStats.refillDuration);
        _armorFloat = Mathf.Min(max, _armorFloat + rate * Time.deltaTime);

        int newArmor = Mathf.FloorToInt(_armorFloat);
        if (newArmor != _currentArmor)
        {
            _currentArmor = newArmor;
            UpdateUI();
        }
    }

    // Called by PlayerHealthController before applying damage to health.
    // Returns the leftover damage that should reduce health.
    public int ProcessDamage(int damage)
    {
        if (armorType == ArmorType.None)
            return damage;

        // Always restart the recharge countdown (interrupts refill too).
        _rechargeTimer = CurrentStats.rechargeDelay;

        if (_currentArmor <= 0)
            return damage;

        int absorbed = Mathf.Min(_currentArmor, damage);
        _currentArmor -= absorbed;
        _armorFloat    = _currentArmor; // sync float so refill resumes from here
        UpdateUI();

        return damage - absorbed;
    }

    // Instantly fills armor to max (useful on respawn or equip).
    public void FillArmor()
    {
        _currentArmor  = armorType == ArmorType.None ? 0 : MaxArmor;
        _armorFloat    = _currentArmor;
        _rechargeTimer = 0f;
        UpdateUI();
    }

    // Swap armor type at runtime (e.g. from inventory/shop).
    public void ChangeArmorType(ArmorType newType)
    {
        armorType = newType;
        FillArmor();
    }

    private void UpdateUI()
    {
        int max = MaxArmor;

        if (armorSlider != null)
        {
            armorSlider.maxValue = max;
            armorSlider.value    = _currentArmor;
        }

        if (armorText != null)
            armorText.text = $"{_currentArmor} / {max}";
    }
}
