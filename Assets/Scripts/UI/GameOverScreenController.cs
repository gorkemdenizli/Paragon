using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverScreenController : MonoBehaviour
{
    public static GameOverScreenController instance;

    [SerializeField] private GameObject screenRoot;
    [SerializeField] private TMP_Text   resultText;
    [SerializeField] private Button     mainMenuButton;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        if (screenRoot != null)     screenRoot.SetActive(false);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void ShowLoss()    => Show("Loss",    Color.red);
    public void ShowVictory() => Show("Victory", Color.green);

    // Boss ölüm animasyonu için gecikmeli zafer ekranı. ShowVictory Time.timeScale=0 yaptığından
    // bekleme realtime; GameOverScreenController persistent olduğundan coroutine güvenle çalışır.
    public void ShowVictoryAfter(float seconds)
    {
        if (seconds <= 0f) { ShowVictory(); return; }
        StartCoroutine(VictoryDelay(seconds));
    }

    IEnumerator VictoryDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        ShowVictory();
    }

    void Show(string text, Color color)
    {
        Time.timeScale = 0f;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
        if (screenRoot != null) screenRoot.SetActive(true);
        if (resultText != null) { resultText.text = text; resultText.color = color; }
    }

    void OnMainMenu()
    {
        UIController.instance?.GoToMainMenu();
    }
}
