using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DifficultyRunUI : MonoBehaviour
{
    [SerializeField] private TMP_Text difficultyLevelText;
    [SerializeField] private Slider   killProgressSlider;
    [SerializeField] private TMP_Text timerText;

    private float _elapsedSeconds;

    void Update()
    {
        // Time.deltaTime returns 0 when timeScale = 0 (pause / upgrade menu).
        _elapsedSeconds += Time.deltaTime;

        if (RunDifficultyManager.instance == null) return;

        int level    = RunDifficultyManager.instance.CurrentDifficultyLevel;
        int kills    = RunDifficultyManager.instance.KillsSinceLastIncrease;
        int required = RunDifficultyManager.instance.CurrentKillsRequired;

        if (difficultyLevelText != null)
            difficultyLevelText.text = level.ToString();

        if (killProgressSlider != null)
        {
            killProgressSlider.maxValue = required;
            killProgressSlider.value    = Mathf.Min(kills, required);
        }

        if (timerText != null)
        {
            int total      = Mathf.FloorToInt(_elapsedSeconds);
            timerText.text = $"{total / 60:00}:{total % 60:00}";
        }
    }
}
