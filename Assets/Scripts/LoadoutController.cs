using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Controls the Loadout canvas: tab switching, weapon selection, stat display, equip & save.
// Attach to the root of the Loadout Canvas GameObject.
public class LoadoutController : MonoBehaviour
{
    [Header("Tab Content Panels")]
    [Tooltip("GameObject containing the Primary Weapon selection UI.")]
    [SerializeField] private GameObject primaryWeaponHorizontal;
    [Tooltip("GameObject containing the Equipment selection UI.")]
    [SerializeField] private GameObject equipmentHorizontal;
    [Tooltip("GameObject containing the Armor selection UI.")]
    [SerializeField] private GameObject armorHorizontal;

    [Header("Primary Weapon — Weapon Buttons")]
    [Tooltip("Ordered list of weapon select buttons: 0=AR, 1=SMG, 2=Shotgun.")]
    [SerializeField] private Button[] weaponButtons;

    [Header("Primary Weapon — Center Display")]
    [SerializeField] private TMP_Text weaponNameText;
    [SerializeField] private Image weaponDisplayImage;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text rateOfFireText;
    [SerializeField] private TMP_Text magazineSizeText;
    [SerializeField] private TMP_Text reloadSpeedText;

    [Header("Player Preview")]
    [Tooltip("Image in the right panel that shows the currently equipped weapon on the player sprite.")]
    [SerializeField] private Image playerWeaponPreviewImage;

    [Header("Equipment — Equipment Buttons")]
    [Tooltip("Ordered list of equipment select buttons: 0=Ammo Crate, 1=Stim Shot.")]
    [SerializeField] private Button[] equipmentButtons;

    [Header("Equipment — Center Display")]
    [SerializeField] private TMP_Text equipmentNameText;
    [SerializeField] private Image equipmentDisplayImage;
    [Tooltip("Entire row GameObject for Total Ammo stat (hidden when Stim Shot selected).")]
    [SerializeField] private GameObject totalAmmoRow;
    [SerializeField] private TMP_Text totalAmmoText;
    [Tooltip("Entire row GameObject for Health Amount stat (hidden when Ammo Crate selected).")]
    [SerializeField] private GameObject healthAmountRow;
    [SerializeField] private TMP_Text healthAmountText;
    [SerializeField] private TMP_Text equipmentCooldownText;

    [Header("Armor — Armor Buttons")]
    [Tooltip("Ordered list of armor select buttons: 0=Medium, 1=Heavy, 2=Light.")]
    [SerializeField] private Button[] armorButtons;

    [Header("Armor — Stats (filled from ArmorController Inspector values)")]
    [SerializeField] private ArmorController.ArmorStats mediumArmorStats;
    [SerializeField] private ArmorController.ArmorStats heavyArmorStats;
    [SerializeField] private ArmorController.ArmorStats lightArmorStats;

    [Header("Armor — Center Display")]
    [SerializeField] private TMP_Text armorNameText;
    [SerializeField] private TMP_Text maxArmorText;
    [SerializeField] private TMP_Text rechargeDelayText;
    [SerializeField] private TMP_Text refillDurationText;
    [SerializeField] private TMP_Text speedMultiplierText;

    [Header("Navigation")]
    [Tooltip("The Main Menu canvas to re-enable when Back is pressed.")]
    [SerializeField] private GameObject mainMenuCanvas;

    // Which weapon/equipment/armor is highlighted (not yet saved).
    private int _selectedWeaponIndex    = -1;
    private int _selectedEquipmentIndex = -1;
    private int _selectedArmorIndex     = -1;

    private void OnEnable()
    {
        // Refresh UI every time the canvas is shown so it reflects any saved state.
        RestoreSavedState();
    }

    // Called by the three tab buttons via UnityEvent; pass 0, 1 or 2.
    public void SwitchTab(int tabIndex)
    {
        if (primaryWeaponHorizontal != null)
            primaryWeaponHorizontal.SetActive(tabIndex == 0);
        if (equipmentHorizontal != null)
            equipmentHorizontal.SetActive(tabIndex == 1);
        if (armorHorizontal != null)
            armorHorizontal.SetActive(tabIndex == 2);
    }

    // Called by the three weapon buttons via UnityEvent; pass 0, 1 or 2.
    public void SelectWeapon(int weaponIndex)
    {
        if (LoadoutManager.instance == null) return;

        WeaponData[] weapons = LoadoutManager.instance.primaryWeapons;
        if (weapons == null || weaponIndex < 0 || weaponIndex >= weapons.Length) return;

        _selectedWeaponIndex = weaponIndex;
        WeaponData data = weapons[weaponIndex];

        if (data == null) return;

        // Center display — name
        if (weaponNameText != null)
            weaponNameText.text = string.IsNullOrEmpty(data.weaponName) ? data.name : data.weaponName;

        // Center display — weapon image
        if (weaponDisplayImage != null)
        {
            weaponDisplayImage.sprite = data.weaponSprite;
            weaponDisplayImage.enabled = data.weaponSprite != null;
        }

        // Stats
        if (damageText != null)       damageText.text       = data.damage.ToString();
        if (accuracyText != null)     accuracyText.text     = data.accuracy.ToString("0.#");
        if (rateOfFireText != null)   rateOfFireText.text   = data.fireRate.ToString("0.#");
        if (magazineSizeText != null) magazineSizeText.text = data.magazineSize.ToString();
        if (reloadSpeedText != null)  reloadSpeedText.text  = data.reloadSpeed.ToString("0.##") + "s";
    }

    // Called by the Primary Weapon Equip button via UnityEvent.
    public void EquipWeapon()
    {
        if (_selectedWeaponIndex < 0 || LoadoutManager.instance == null) return;

        LoadoutManager.instance.SetEquippedPrimary(_selectedWeaponIndex);
        UpdatePlayerPreview();
    }

    // Called by the two equipment buttons via UnityEvent; pass 0=Ammo Crate, 1=Stim Shot.
    public void SelectEquipment(int equipmentIndex)
    {
        if (LoadoutManager.instance == null) return;

        EquipmentData[] options = LoadoutManager.instance.equipmentOptions;
        if (options == null || equipmentIndex < 0 || equipmentIndex >= options.Length) return;

        _selectedEquipmentIndex = equipmentIndex;
        EquipmentData data = options[equipmentIndex];
        if (data == null) return;

        // Name
        if (equipmentNameText != null)
            equipmentNameText.text = string.IsNullOrEmpty(data.equipmentName) ? data.name : data.equipmentName;

        // Image
        if (equipmentDisplayImage != null)
        {
            equipmentDisplayImage.sprite  = data.equipmentSprite;
            equipmentDisplayImage.enabled = data.equipmentSprite != null;
        }

        // Ammo Crate: show totalAmmo row, hide healthAmount row.
        // Stim Shot:  hide totalAmmo row, show healthAmount row.
        bool isAmmoCrate = data.equipmentType == EquipmentController.EquipmentType.AmmoCrate;

        if (totalAmmoRow    != null) totalAmmoRow.SetActive(isAmmoCrate);
        if (healthAmountRow != null) healthAmountRow.SetActive(!isAmmoCrate);

        if (totalAmmoText    != null) totalAmmoText.text    = data.totalAmmo.ToString();
        if (healthAmountText != null) healthAmountText.text = data.healthAmount.ToString();
        if (equipmentCooldownText != null)
            equipmentCooldownText.text = data.cooldown.ToString("0.#") + "s";
    }

    // Called by the Equipment Equip button via UnityEvent.
    public void EquipEquipment()
    {
        if (_selectedEquipmentIndex < 0 || LoadoutManager.instance == null) return;
        LoadoutManager.instance.SetEquippedEquipment(_selectedEquipmentIndex);
    }

    // Called by the three armor buttons via UnityEvent; pass 0=Medium, 1=Heavy, 2=Light.
    public void SelectArmor(int armorIndex)
    {
        _selectedArmorIndex = armorIndex;

        ArmorController.ArmorStats stats = armorIndex switch
        {
            0 => mediumArmorStats,
            1 => heavyArmorStats,
            _ => lightArmorStats
        };

        string displayName = armorIndex switch
        {
            0 => "Medium Armor",
            1 => "Heavy Armor",
            _ => "Light Armor"
        };

        if (armorNameText        != null) armorNameText.text        = displayName;
        if (maxArmorText         != null) maxArmorText.text         = stats.maxArmor.ToString();
        if (rechargeDelayText    != null) rechargeDelayText.text    = stats.rechargeDelay.ToString("0.#") + "s";
        if (refillDurationText   != null) refillDurationText.text   = stats.refillDuration.ToString("0.#") + "s";
        if (speedMultiplierText  != null) speedMultiplierText.text  = stats.speedMultiplier.ToString("0.##") + "x";
    }

    // Called by the Armor Equip button via UnityEvent.
    public void EquipArmor()
    {
        if (_selectedArmorIndex < 0 || LoadoutManager.instance == null) return;

        LoadoutManager.instance.SetEquippedArmor(_selectedArmorIndex);
        ArmorController.instance?.ChangeArmorType(LoadoutManager.instance.EquippedArmorType);
    }

    // Called by the Back button via UnityEvent.
    public void BackToMainMenu()
    {
        gameObject.SetActive(false);
        if (mainMenuCanvas != null)
            mainMenuCanvas.SetActive(true);

        // Auto-find MainMenu so no extra Inspector assignment is needed.
        FindFirstObjectByType<MainMenu>()?.RefreshWeaponImage();
    }

    // Reads saved state from LoadoutManager and refreshes all tab UIs.
    private void RestoreSavedState()
    {
        if (LoadoutManager.instance == null) return;

        // Default to Primary Weapon tab on open.
        SwitchTab(0);

        // Weapon
        int savedWeapon = LoadoutManager.instance.EquippedPrimaryIndex;
        SelectWeapon(savedWeapon);
        _selectedWeaponIndex = savedWeapon;

        // Equipment
        int savedEquipment = LoadoutManager.instance.EquippedEquipmentIndex;
        SelectEquipment(savedEquipment);
        _selectedEquipmentIndex = savedEquipment;

        // Armor
        int savedArmor = LoadoutManager.instance.EquippedArmorIndex;
        SelectArmor(savedArmor);
        _selectedArmorIndex = savedArmor;

        UpdatePlayerPreview();
    }

    private void UpdatePlayerPreview()
    {
        if (playerWeaponPreviewImage == null || LoadoutManager.instance == null) return;

        WeaponData equipped = LoadoutManager.instance.EquippedPrimaryWeapon;
        if (equipped != null && equipped.weaponSprite != null)
        {
            playerWeaponPreviewImage.sprite = equipped.weaponSprite;
            playerWeaponPreviewImage.enabled = true;
        }
    }
}
