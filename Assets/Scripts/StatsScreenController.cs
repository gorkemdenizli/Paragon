using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Tab-toggle stats overlay. Shows effective run stat values (base × upgrades).
// Attach to an always-active object (e.g. UI Canvas); statsPanel starts inactive in the scene.
public class StatsScreenController : MonoBehaviour
{
    public static StatsScreenController instance;

    [SerializeField] private GameObject statsPanel;
    [SerializeField] private Transform rowsContainer;

    private static readonly StatType[] StatOrder =
    {
        StatType.MaxHealth, StatType.MaxArmor, StatType.MovementSpeed, StatType.JumpForce,
        StatType.Damage, StatType.RateOfFire, StatType.Accuracy, StatType.MagazineSize,
        StatType.ReloadSpeed, StatType.CritChance, StatType.CritDamage, StatType.Luck, StatType.XPGain
    };

    private TMP_Text[] _valueTexts;
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
        {
            Destroy(this);
            return;
        }

        CacheValueTexts();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnEnable()
    {
        if (PlayerStats.instance != null)
            PlayerStats.instance.OnStatsChanged += RefreshAll;
    }

    private void OnDisable()
    {
        if (PlayerStats.instance != null)
            PlayerStats.instance.OnStatsChanged -= RefreshAll;
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame)
            return;

        if (IsUpgradeCanvasOpen())
            return;

        Toggle();
    }

    private void CacheValueTexts()
    {
        if (rowsContainer == null)
        {
            Debug.LogWarning("StatsScreenController: rowsContainer is not assigned.");
            return;
        }

        _valueTexts = new TMP_Text[StatOrder.Length];

        for (int i = 0; i < StatOrder.Length; i++)
        {
            int rowIndex = i + 1; // skip "STATS" header at index 0
            if (rowIndex >= rowsContainer.childCount)
            {
                Debug.LogWarning($"StatsScreenController: expected row {rowIndex}, only {rowsContainer.childCount} children.");
                continue;
            }

            Transform valueTransform = rowsContainer.GetChild(rowIndex).Find("StatValue");
            if (valueTransform != null)
                _valueTexts[i] = valueTransform.GetComponent<TMP_Text>();
            else
                Debug.LogWarning($"StatsScreenController: StatValue not found on row {rowIndex}.");
        }
    }

    public void Toggle()
    {
        if (_isOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (_isOpen) return;

        _isOpen = true;
        if (statsPanel != null)
            statsPanel.SetActive(true);
        RefreshAll();
        ApplyPauseState();
    }

    public void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        if (statsPanel != null)
            statsPanel.SetActive(false);
        RestoreTimeAndCursor();
    }

    public void RefreshAll()
    {
        if (_valueTexts == null || PlayerStats.instance == null)
            return;

        for (int i = 0; i < StatOrder.Length; i++)
        {
            if (_valueTexts[i] == null) continue;

            float value = PlayerStats.instance.GetCurrentValue(StatOrder[i]);
            _valueTexts[i].text = StatValueFormatter.Format(StatOrder[i], value);
        }
    }

    private static bool IsUpgradeCanvasOpen()
    {
        return UpgradeCanvasController.instance != null && UpgradeCanvasController.instance.IsOpen;
    }

    private void ApplyPauseState()
    {
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestoreTimeAndCursor()
    {
        if (IsUpgradeCanvasOpen() || IsPauseScreenOpen())
            return;

        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private static bool IsPauseScreenOpen()
    {
        return UIController.instance != null
            && UIController.instance.pauseScreen != null
            && UIController.instance.pauseScreen.activeSelf;
    }
}
