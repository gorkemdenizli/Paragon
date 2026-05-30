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
