using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string newGameScene;

    [Tooltip("The root panel of the main menu (shown by default).")]
    [SerializeField] private GameObject mainMenuCanvas;

    [Tooltip("The Loadout canvas (starts inactive; opened via OpenLoadout).")]
    [SerializeField] private GameObject loadoutCanvas;

    [Tooltip("SpriteRenderer in the main menu that displays the currently equipped primary weapon.")]
    [SerializeField] private SpriteRenderer mainMenuWeaponRenderer;

    private void Start()
    {
        RefreshWeaponImage();
    }

    public void RefreshWeaponImage()
    {
        if (mainMenuWeaponRenderer == null || LoadoutManager.instance == null) return;

        WeaponData equipped = LoadoutManager.instance.EquippedPrimaryWeapon;
        if (equipped != null && equipped.weaponSprite != null)
            mainMenuWeaponRenderer.sprite = equipped.weaponSprite;
    }

    public void NewGame()
    {
        SceneManager.LoadScene(newGameScene);
    }

    public void OpenLoadout()
    {
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);
        if (loadoutCanvas != null)  loadoutCanvas.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();

        Debug.Log("Quitting game");
    }
}
