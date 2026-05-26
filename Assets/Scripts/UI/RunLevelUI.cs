using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Listens to RunLevelManager events and updates the level text and progress slider.
// Attach to the UI Canvas GameObject that contains the level display.
public class RunLevelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Slider   xpProgressSlider;

    private void Start()
    {
        if (RunLevelManager.instance != null)
        {
            RunLevelManager.instance.OnXPGained += HandleXPGained;
            RunLevelManager.instance.OnLevelUp  += HandleLevelUp;
        }
        else
        {
            Debug.LogWarning("RunLevelUI: RunLevelManager.instance not found on Start.");
        }

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (RunLevelManager.instance != null)
        {
            RunLevelManager.instance.OnXPGained -= HandleXPGained;
            RunLevelManager.instance.OnLevelUp  -= HandleLevelUp;
        }
    }

    private void HandleXPGained(int amount)
    {
        UpdateSlider();
    }

    private void HandleLevelUp(int newLevel)
    {
        UpdateLevelText();
        UpdateSlider();
    }

    private void RefreshAll()
    {
        UpdateLevelText();
        UpdateSlider();
    }

    private void UpdateLevelText()
    {
        if (levelText == null || RunLevelManager.instance == null) return;
        levelText.text = RunLevelManager.instance.CurrentLevel.ToString();
    }

    private void UpdateSlider()
    {
        if (xpProgressSlider == null || RunLevelManager.instance == null) return;
        xpProgressSlider.value = RunLevelManager.instance.LevelProgress;
    }
}
