using System;
using UnityEngine;

// Manages the run's XP and level progression.
// Singleton — persists for the duration of the run (not DontDestroyOnLoad; resets each run).
public class RunLevelManager : MonoBehaviour
{
    public static RunLevelManager instance;

    [Tooltip("XP required to reach level 2. Each subsequent level multiplies by xpScaleMultiplier.")]
    [SerializeField] private int baseXpToLevel = 1000;
    [Tooltip("Multiplier applied to XP threshold per level (e.g. 1.05 = 5% more each level).")]
    [SerializeField] private float xpScaleMultiplier = 1.05f;

    public int   CurrentLevel    { get; private set; } = 1;
    public int   CurrentXP       { get; private set; } = 0;
    public int   XPToNextLevel   { get; private set; }

    // Fired when XP is added: int = amount gained this call.
    public event Action<int> OnXPGained;
    // Fired when a level-up occurs: int = new level.
    public event Action<int> OnLevelUp;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        XPToNextLevel = baseXpToLevel;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        int effective = Mathf.RoundToInt(amount * (PlayerStats.instance?.XPGainMultiplier ?? 1f));
        CurrentXP += effective;
        OnXPGained?.Invoke(effective);

        // Handle one or more level-ups from a single XP gain.
        while (CurrentXP >= XPToNextLevel)
        {
            CurrentXP    -= XPToNextLevel;
            CurrentLevel += 1;
            XPToNextLevel = Mathf.RoundToInt(XPToNextLevel * xpScaleMultiplier);
            OnLevelUp?.Invoke(CurrentLevel);
        }
    }

    // Normalised progress towards the next level (0–1).
    public float LevelProgress => XPToNextLevel > 0 ? (float)CurrentXP / XPToNextLevel : 0f;
}
