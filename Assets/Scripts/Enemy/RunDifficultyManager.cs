using System;
using System.Collections.Generic;
using UnityEngine;

public struct RunDifficultySnapshot
{
    public int   difficultyLevel;
    public int   totalKills;
    public int   killsSinceLastIncrease;
    public int   currentKillsRequired;
    public float healthMultiplier;
    public float damageMultiplier;
    public float speedMultiplier;
    public float spawnRateMultiplier;
    public int   currentMaxAliveEnemies;
}

public class RunDifficultyManager : MonoBehaviour
{
    public static RunDifficultyManager instance;

    [Header("Difficulty Progression")]
    [SerializeField] private int   baseKillsRequired                = 30;
    [SerializeField] private float killRequirementGrowth            = 1.2f;
    [SerializeField] private bool  infiniteScaling                  = false;
    [SerializeField] private int   maxDifficultyLevel               = 10;

    [Header("Enemy Stat Scaling")]
    [SerializeField] private float healthGrowthPerDifficulty        = 1.10f;
    [SerializeField] private float damageGrowthPerDifficulty        = 1.07f;
    [SerializeField] private float speedGrowthPerDifficulty         = 1.03f;
    [SerializeField] private float maxHealthMultiplier              = 5.0f;
    [SerializeField] private float maxDamageMultiplier              = 2.5f;
    [SerializeField] private float maxSpeedMultiplier               = 1.35f;

    [Header("Spawn Scaling")]
    [SerializeField] private float spawnRateGrowthPerDifficulty     = 1.08f;
    [SerializeField] private int   spawnCountIncreaseEveryNLevels   = 3;
    [SerializeField] private int   spawnCountIncreaseAmount         = 1;
    [Tooltip("Minimum seconds between individual enemy spawns across all spawn points.")]
    [SerializeField] private float minSingleSpawnInterval           = 0.6f;
    [Header("Walker Alive Cap")]
    [SerializeField] private int   baseMaxAliveWalkers           = 10;
    [SerializeField] private int   maxAliveWalkersPerDifficulty  = 1;
    [SerializeField] private int   absoluteMaxAliveWalkers       = 30;

    [Header("Flyer Alive Cap")]
    [SerializeField] private int   baseMaxAliveFlyers            = 10;
    [SerializeField] private int   maxAliveFlyersPerDifficulty   = 1;
    [SerializeField] private int   absoluteMaxAliveFlyers        = 30;

    [Header("Scaling Whitelist")]
    [Tooltip("Bu listedeki prefab'lardan spawn edilen enemy'lere difficulty scaling uygulanır.")]
    [SerializeField] private List<GameObject> scaledEnemyPrefabs = new();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Events
    public event Action<int>                   OnDifficultyIncreased;
    public event Action<RunDifficultySnapshot> OnDifficultyStatsChanged;

    // Runtime state
    private int _currentDifficultyLevel;
    private int _totalKills;
    private int _killsSinceLastIncrease;
    private int _currentKillsRequired;

    // Cached computed values
    public float HealthMultiplier       { get; private set; } = 1f;
    public float DamageMultiplier       { get; private set; } = 1f;
    public float SpeedMultiplier        { get; private set; } = 1f;
    public float SpawnRateMultiplier    { get; private set; } = 1f;
    public int   CurrentMaxAliveEnemies { get; private set; }
    public int   CurrentMaxAliveWalkers { get; private set; }
    public int   CurrentMaxAliveFlyers  { get; private set; }

    // Per-point spawn helpers (consumed by EnemySpawnPoint.GetCurrentSingleInterval)
    public float MinSingleSpawnInterval => minSingleSpawnInterval;
    public int   SpawnCountBonus        => spawnCountIncreaseEveryNLevels > 0
        ? (CurrentDifficultyLevel / spawnCountIncreaseEveryNLevels) * spawnCountIncreaseAmount
        : 0;

    public int CurrentDifficultyLevel => _currentDifficultyLevel;
    public int TotalKills             => _totalKills;
    public int KillsSinceLastIncrease => _killsSinceLastIncrease;
    public int CurrentKillsRequired   => _currentKillsRequired;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _currentKillsRequired = baseKillsRequired;
        RecalculateStats();
    }

    public void RegisterEnemyKill()
    {
        _totalKills++;
        _killsSinceLastIncrease++;

        bool atCap = !infiniteScaling && _currentDifficultyLevel >= maxDifficultyLevel;
        if (atCap || _killsSinceLastIncrease < _currentKillsRequired) return;

        _killsSinceLastIncrease = 0;
        _currentKillsRequired   = Mathf.CeilToInt(_currentKillsRequired * killRequirementGrowth);
        _currentDifficultyLevel++;

        RecalculateStats();

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[Difficulty] Level {_currentDifficultyLevel} | " +
                $"HP: {HealthMultiplier:F2}x | DMG: {DamageMultiplier:F2}x | SPD: {SpeedMultiplier:F2}x | " +
                $"SpawnRateMult: {SpawnRateMultiplier:F2}x | SpawnCountBonus: +{SpawnCountBonus} | " +
                $"MaxAlive: {CurrentMaxAliveEnemies} | NextRequired: {_currentKillsRequired}");
        }

        OnDifficultyIncreased?.Invoke(_currentDifficultyLevel);
        OnDifficultyStatsChanged?.Invoke(BuildSnapshot());
    }

    private void RecalculateStats()
    {
        int lvl = _currentDifficultyLevel;

        HealthMultiplier    = Mathf.Min(Mathf.Pow(healthGrowthPerDifficulty, lvl), maxHealthMultiplier);
        DamageMultiplier    = Mathf.Min(Mathf.Pow(damageGrowthPerDifficulty, lvl), maxDamageMultiplier);
        SpeedMultiplier     = Mathf.Min(Mathf.Pow(speedGrowthPerDifficulty,  lvl), maxSpeedMultiplier);
        SpawnRateMultiplier = Mathf.Pow(spawnRateGrowthPerDifficulty, lvl);

        CurrentMaxAliveWalkers = Mathf.Min(baseMaxAliveWalkers + lvl * maxAliveWalkersPerDifficulty, absoluteMaxAliveWalkers);
        CurrentMaxAliveFlyers  = Mathf.Min(baseMaxAliveFlyers  + lvl * maxAliveFlyersPerDifficulty,  absoluteMaxAliveFlyers);
        CurrentMaxAliveEnemies = CurrentMaxAliveWalkers + CurrentMaxAliveFlyers;
    }

    public void ApplyScalingIfEligible(GameObject prefabSource, GameObject spawnedInstance)
    {
        if (prefabSource == null || spawnedInstance == null) return;
        if (!scaledEnemyPrefabs.Contains(prefabSource)) return;

        var scaler = spawnedInstance.GetComponent<IDifficultyScalableEnemy>();
        scaler?.ApplyDifficultyScaling(HealthMultiplier, DamageMultiplier, SpeedMultiplier);
    }

    public void ResetDifficulty()
    {
        _currentDifficultyLevel = 0;
        _totalKills             = 0;
        _killsSinceLastIncrease = 0;
        _currentKillsRequired   = baseKillsRequired;
        RecalculateStats();
        OnDifficultyStatsChanged?.Invoke(BuildSnapshot());
    }

    private RunDifficultySnapshot BuildSnapshot() => new RunDifficultySnapshot
    {
        difficultyLevel        = _currentDifficultyLevel,
        totalKills             = _totalKills,
        killsSinceLastIncrease = _killsSinceLastIncrease,
        currentKillsRequired   = _currentKillsRequired,
        healthMultiplier       = HealthMultiplier,
        damageMultiplier       = DamageMultiplier,
        speedMultiplier        = SpeedMultiplier,
        spawnRateMultiplier    = SpawnRateMultiplier,
        currentMaxAliveEnemies = CurrentMaxAliveEnemies,
    };

    private void OnValidate()
    {
        baseKillsRequired               = Mathf.Max(1, baseKillsRequired);
        killRequirementGrowth           = Mathf.Max(1f, killRequirementGrowth);
        maxDifficultyLevel              = Mathf.Max(1, maxDifficultyLevel);
        healthGrowthPerDifficulty       = Mathf.Max(1f, healthGrowthPerDifficulty);
        damageGrowthPerDifficulty       = Mathf.Max(1f, damageGrowthPerDifficulty);
        speedGrowthPerDifficulty        = Mathf.Max(1f, speedGrowthPerDifficulty);
        maxHealthMultiplier             = Mathf.Max(1f, maxHealthMultiplier);
        maxDamageMultiplier             = Mathf.Max(1f, maxDamageMultiplier);
        maxSpeedMultiplier              = Mathf.Max(1f, maxSpeedMultiplier);
        spawnRateGrowthPerDifficulty    = Mathf.Max(1f, spawnRateGrowthPerDifficulty);
        minSingleSpawnInterval          = Mathf.Max(0.1f, minSingleSpawnInterval);
        spawnCountIncreaseEveryNLevels  = Mathf.Max(1, spawnCountIncreaseEveryNLevels);
        spawnCountIncreaseAmount        = Mathf.Max(0, spawnCountIncreaseAmount);
        baseMaxAliveWalkers          = Mathf.Max(1, baseMaxAliveWalkers);
        maxAliveWalkersPerDifficulty = Mathf.Max(0, maxAliveWalkersPerDifficulty);
        absoluteMaxAliveWalkers      = Mathf.Max(baseMaxAliveWalkers, absoluteMaxAliveWalkers);
        baseMaxAliveFlyers           = Mathf.Max(1, baseMaxAliveFlyers);
        maxAliveFlyersPerDifficulty  = Mathf.Max(0, maxAliveFlyersPerDifficulty);
        absoluteMaxAliveFlyers       = Mathf.Max(baseMaxAliveFlyers, absoluteMaxAliveFlyers);
    }

    // ── Debug helpers ──────────────────────────────────────────────────────────

    [ContextMenu("Debug / Add 1 Kill")]
    public void Debug_AddKill() => RegisterEnemyKill();

    [ContextMenu("Debug / Add 10 Kills")]
    public void Debug_AddTenKills() => Debug_AddKills(10);

    [ContextMenu("Debug / Force Increase Difficulty")]
    public void Debug_ForceIncreaseDifficulty()
    {
        _killsSinceLastIncrease = _currentKillsRequired;
        RegisterEnemyKill();
    }

    [ContextMenu("Debug / Reset Difficulty")]
    public void Debug_ResetDifficulty() => ResetDifficulty();

    public void Debug_AddKills(int amount)
    {
        for (int i = 0; i < amount; i++)
            RegisterEnemyKill();
    }
}
