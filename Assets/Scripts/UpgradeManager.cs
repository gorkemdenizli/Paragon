using System.Collections.Generic;
using UnityEngine;

// Subscribes to RunLevelManager.OnLevelUp, rolls 3 unique upgrade offers,
// handles selection, skip and one-per-level-up reroll.
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager instance;

    [Tooltip("All 13 UpgradeDefinition assets.")]
    [SerializeField] private UpgradeDefinition[] allUpgrades;

    [SerializeField] private UpgradeCanvasController upgradeCanvas;

    [Header("Base Tier Chances (sum should equal 1)")]
    [SerializeField] private float baseLowChance  = 0.77f;
    [SerializeField] private float baseMidChance  = 0.20f;
    [SerializeField] private float baseHighChance = 0.03f;

    private List<UpgradeOffer>  _currentOffers    = new();
    private bool                _rerollUsed;
    private bool                _selectionActive;
    private readonly Queue<int> _pendingLevelUps  = new();

    public struct UpgradeOffer
    {
        public UpgradeDefinition definition;
        public UpgradeTier        tier;
    }

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (RunLevelManager.instance != null)
            RunLevelManager.instance.OnLevelUp += HandleLevelUp;
        else
            Debug.LogWarning("UpgradeManager: RunLevelManager.instance not found on Start.");
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (RunLevelManager.instance != null)
            RunLevelManager.instance.OnLevelUp -= HandleLevelUp;
    }

    private void HandleLevelUp(int newLevel)
    {
        _pendingLevelUps.Enqueue(newLevel);
        if (!_selectionActive)
            ShowNextPending();
    }

    private void ShowNextPending()
    {
        if (_pendingLevelUps.Count == 0) return;
        _pendingLevelUps.Dequeue();
        _rerollUsed      = false;
        _selectionActive = true;
        _currentOffers   = RollOffers();
        upgradeCanvas.Show(_currentOffers, rerollAvailable: true);
    }

    private void CloseAndContinue()
    {
        _selectionActive = false;
        upgradeCanvas.Hide(onComplete: () =>
        {
            if (_pendingLevelUps.Count > 0)
                ShowNextPending();
        });
    }

    // ── Public actions wired to canvas buttons ───────────────────────────────

    public void OnSelectUpgrade(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _currentOffers.Count) return;
        UpgradeOffer offer = _currentOffers[slotIndex];
        PlayerStats.instance?.ApplyUpgrade(offer.definition.statType, offer.definition.GetBonus(offer.tier));
        CloseAndContinue();
    }

    public void OnSkip()
    {
        CloseAndContinue();
    }

    public void OnReroll()
    {
        if (_rerollUsed) return;
        _rerollUsed    = true;
        _currentOffers = RollOffers();
        upgradeCanvas.Show(_currentOffers, rerollAvailable: false);
    }

    // ── Rolling logic ────────────────────────────────────────────────────────

    // Rolls 3 unique offers (unique by StatType, guaranteed no duplicates).
    private List<UpgradeOffer> RollOffers()
    {
        List<UpgradeDefinition> pool = new(allUpgrades);
        Shuffle(pool);

        var offers = new List<UpgradeOffer>(3);
        var usedTypes = new HashSet<StatType>();

        foreach (UpgradeDefinition def in pool)
        {
            if (def == null) continue;
            if (usedTypes.Contains(def.statType)) continue;

            offers.Add(new UpgradeOffer { definition = def, tier = RollTier() });
            usedTypes.Add(def.statType);

            if (offers.Count == 3) break;
        }

        return offers;
    }

    // Tier probabilities scale with the player's Luck stat.
    // highChance = baseHigh * luck
    // midChance  = baseMid  * luck
    // lowChance  = remainder (clamped ≥ 0)
    private UpgradeTier RollTier()
    {
        float luck = PlayerStats.instance != null ? PlayerStats.instance.Luck : 1f;

        float high = Mathf.Clamp01(baseHighChance * luck);
        float mid  = Mathf.Clamp01(baseMidChance  * luck);
        // Ensure total does not exceed 1
        if (high + mid > 1f)
        {
            float scale = 1f / (high + mid);
            high *= scale;
            mid  *= scale;
        }

        float roll = Random.value;
        if (roll < high)        return UpgradeTier.High;
        if (roll < high + mid)  return UpgradeTier.Mid;
        return UpgradeTier.Low;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
