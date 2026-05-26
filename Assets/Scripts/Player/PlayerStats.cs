using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatType
{
    MaxHealth, MaxArmor, MovementSpeed, JumpForce,
    Damage, RateOfFire, Accuracy, MagazineSize, ReloadSpeed,
    Luck, CritChance, CritDamage, XPGain
}

// Stores all run-time stat bonuses accumulated through upgrades.
// Singleton scoped to the game scene — resets each run (not DontDestroyOnLoad).
//
// Stacking rules:
//   Additive  (MaxHealth, MaxArmor, MovementSpeed, JumpForce):
//       multiplier = 1 + totalBonusPercent / 100
//   Multiplicative (everything else):
//       multiplier compounds: current *= (1 + bonus/100)
//   Luck / XPGain bonuses are raw fractions (0.2 = ×1.2 per upgrade), not percent.
public class PlayerStats : MonoBehaviour
{
    public static PlayerStats instance;

    [Header("Base Values (Inspector-editable)")]
    [Tooltip("Starting luck multiplier applied to tier chances.")]
    [SerializeField] private float baseLuck       = 1f;
    [Tooltip("Starting crit chance (0 = no crits by default).")]
    [SerializeField] private float baseCritChance = 0f;
    [Tooltip("Starting crit damage multiplier (1.0 = 100%, no bonus — crits deal normal damage).")]
    [SerializeField] private float baseCritDamage = 1f;
    [Tooltip("Starting XP gain multiplier (1 = no bonus).")]
    [SerializeField] private float baseXPGain     = 1f;

    // ── Additive accumulators (percent points, e.g. 5 = +5%) ────────────────
    private float _maxHealthBonus;
    private float _maxArmorBonus;
    private float _movementSpeedBonus;
    private float _jumpForceBonus;

    // ── Multiplicative running values ────────────────────────────────────────
    private float _damageMult       = 1f;
    private float _rateOfFireMult   = 1f;
    private float _accuracyMult     = 1f;
    private float _magazineSizeMult = 1f;
    private float _reloadSpeedMult  = 1f;

    // ── Absolute compounding stats ───────────────────────────────────────────
    public float Luck            { get; private set; }
    public float CritChance      { get; private set; }
    public float CritDamage      { get; private set; }
    public float XPGainMultiplier{ get; private set; }

    // ── Computed multiplier properties ───────────────────────────────────────
    public float MaxHealthMultiplier     => 1f + _maxHealthBonus     / 100f;
    public float MaxArmorMultiplier      => 1f + _maxArmorBonus      / 100f;
    public float MovementSpeedMultiplier => 1f + _movementSpeedBonus / 100f;
    public float JumpForceMultiplier     => 1f + _jumpForceBonus     / 100f;
    public float DamageMultiplier        => _damageMult;
    public float RateOfFireMultiplier    => _rateOfFireMult;
    public float AccuracyMultiplier      => _accuracyMult;
    public float MagazineSizeMultiplier  => _magazineSizeMult;
    public float ReloadSpeedMultiplier   => _reloadSpeedMult;

    // Registered base values from individual components for display purposes.
    private readonly Dictionary<StatType, float> _baseValues = new();

    public event Action OnStatsChanged;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Luck             = baseLuck;
            CritChance       = baseCritChance;
            CritDamage       = baseCritDamage;
            XPGainMultiplier = baseXPGain;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Called by individual components (Health, Weapon, etc.) so upgrade cards can
    // display actual values instead of raw multipliers.
    public void RegisterBaseValue(StatType stat, float value)
    {
        _baseValues[stat] = value;
    }

    public void ApplyUpgrade(StatType stat, float bonusValue)
    {
        switch (stat)
        {
            // Additive: accumulate percent points
            case StatType.MaxHealth:     _maxHealthBonus      += bonusValue; break;
            case StatType.MaxArmor:      _maxArmorBonus       += bonusValue; break;
            case StatType.MovementSpeed: _movementSpeedBonus  += bonusValue; break;
            case StatType.JumpForce:     _jumpForceBonus      += bonusValue; break;

            // Multiplicative: compound
            case StatType.Damage:        _damageMult       *= 1f + bonusValue / 100f; break;
            case StatType.RateOfFire:    _rateOfFireMult   *= 1f + bonusValue / 100f; break;
            case StatType.Accuracy:      _accuracyMult     *= 1f + bonusValue / 100f; break;
            case StatType.MagazineSize:  _magazineSizeMult *= 1f + bonusValue / 100f; break;
            case StatType.ReloadSpeed:   _reloadSpeedMult  *= 1f - bonusValue / 100f; break;

            // Luck / XPGain: flat additive (0.2 → Luck += 0.2)
            case StatType.Luck:   Luck             += bonusValue; break;
            case StatType.XPGain: XPGainMultiplier += bonusValue; break;

            // CritChance / CritDamage: flat additive (each upgrade adds to the total)
            case StatType.CritChance: CritChance += bonusValue / 100f; break;
            case StatType.CritDamage: CritDamage += bonusValue / 100f; break;
        }

        OnStatsChanged?.Invoke();
    }

    // Returns the current actual value of a stat (base × multiplier where applicable).
    public float GetCurrentValue(StatType stat)
    {
        return stat switch
        {
            // Absolute stats returned directly
            StatType.Luck       => Luck,
            StatType.XPGain     => XPGainMultiplier,
            StatType.CritChance => CritChance * 100f,
            StatType.CritDamage => CritDamage * 100f,
            // Base-multiplied stats
            _ => _baseValues.TryGetValue(stat, out float b) ? b * GetMultiplier(stat) : GetMultiplier(stat)
        };
    }

    // Returns the projected actual value after applying bonusValue for the given stat.
    public float GetProjectedValue(StatType stat, float bonusValue)
    {
        return stat switch
        {
            StatType.Luck       => Luck + bonusValue,
            StatType.XPGain     => XPGainMultiplier + bonusValue,
            StatType.CritChance => (CritChance + bonusValue / 100f) * 100f,
            StatType.CritDamage => (CritDamage + bonusValue / 100f) * 100f,
            _ => _baseValues.TryGetValue(stat, out float b)
                ? b * GetProjectedMultiplier(stat, bonusValue)
                : GetProjectedMultiplier(stat, bonusValue)
        };
    }

    private float GetMultiplier(StatType stat) => stat switch
    {
        StatType.MaxHealth     => MaxHealthMultiplier,
        StatType.MaxArmor      => MaxArmorMultiplier,
        StatType.MovementSpeed => MovementSpeedMultiplier,
        StatType.JumpForce     => JumpForceMultiplier,
        StatType.Damage        => _damageMult,
        StatType.RateOfFire    => _rateOfFireMult,
        StatType.Accuracy      => _accuracyMult,
        StatType.MagazineSize  => _magazineSizeMult,
        StatType.ReloadSpeed   => _reloadSpeedMult,
        _                      => 1f
    };

    private float GetProjectedMultiplier(StatType stat, float bonusValue) => stat switch
    {
        StatType.MaxHealth     => 1f + (_maxHealthBonus     + bonusValue) / 100f,
        StatType.MaxArmor      => 1f + (_maxArmorBonus      + bonusValue) / 100f,
        StatType.MovementSpeed => 1f + (_movementSpeedBonus + bonusValue) / 100f,
        StatType.JumpForce     => 1f + (_jumpForceBonus     + bonusValue) / 100f,
        StatType.Damage        => _damageMult       * (1f + bonusValue / 100f),
        StatType.RateOfFire    => _rateOfFireMult   * (1f + bonusValue / 100f),
        StatType.Accuracy      => _accuracyMult     * (1f + bonusValue / 100f),
        StatType.MagazineSize  => _magazineSizeMult * (1f + bonusValue / 100f),
        StatType.ReloadSpeed   => _reloadSpeedMult  * (1f - bonusValue / 100f),
        _                      => 1f
    };
}
