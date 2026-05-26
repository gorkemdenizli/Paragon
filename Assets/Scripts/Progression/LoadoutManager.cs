using UnityEngine;

// Persistent singleton that stores the player's loadout choices across scenes.
// Lives on its own GameObject in the Main Menu scene; survives scene loads via DontDestroyOnLoad.
public class LoadoutManager : MonoBehaviour
{
    public static LoadoutManager instance;

    private const string PrefKeyPrimary   = "EquippedPrimaryWeaponIndex";
    private const string PrefKeyEquipment = "EquippedEquipmentIndex";
    private const string PrefKeyArmor     = "EquippedArmorIndex";

    [Tooltip("Primary weapon options in order: index 0 = Assault Rifle, 1 = SMG, 2 = Shotgun.")]
    public WeaponData[] primaryWeapons;

    [Tooltip("Equipment options in order: index 0 = Ammo Crate, 1 = Stim Shot.")]
    public EquipmentData[] equipmentOptions;

    public int EquippedPrimaryIndex   { get; private set; } = 0;
    public int EquippedEquipmentIndex { get; private set; } = 0;
    public int EquippedArmorIndex     { get; private set; } = 0;

    // Converts stored armor index to the ArmorController enum value.
    // Button order: 0 = Medium, 1 = Heavy, 2 = Light
    public ArmorController.ArmorType EquippedArmorType => EquippedArmorIndex switch
    {
        0 => ArmorController.ArmorType.Medium,
        1 => ArmorController.ArmorType.Heavy,
        _ => ArmorController.ArmorType.Light
    };

    public WeaponData EquippedPrimaryWeapon
    {
        get
        {
            if (primaryWeapons == null || primaryWeapons.Length == 0) return null;
            return primaryWeapons[Mathf.Clamp(EquippedPrimaryIndex, 0, primaryWeapons.Length - 1)];
        }
    }

    public EquipmentData EquippedEquipment
    {
        get
        {
            if (equipmentOptions == null || equipmentOptions.Length == 0) return null;
            return equipmentOptions[Mathf.Clamp(EquippedEquipmentIndex, 0, equipmentOptions.Length - 1)];
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadFromPrefs();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetEquippedPrimary(int index)
    {
        if (primaryWeapons == null || primaryWeapons.Length == 0) return;
        EquippedPrimaryIndex = Mathf.Clamp(index, 0, primaryWeapons.Length - 1);
        PlayerPrefs.SetInt(PrefKeyPrimary, EquippedPrimaryIndex);
        PlayerPrefs.Save();
    }

    public void SetEquippedEquipment(int index)
    {
        if (equipmentOptions == null || equipmentOptions.Length == 0) return;
        EquippedEquipmentIndex = Mathf.Clamp(index, 0, equipmentOptions.Length - 1);
        PlayerPrefs.SetInt(PrefKeyEquipment, EquippedEquipmentIndex);
        PlayerPrefs.Save();
    }

    public void SetEquippedArmor(int index)
    {
        EquippedArmorIndex = Mathf.Clamp(index, 0, 2);
        PlayerPrefs.SetInt(PrefKeyArmor, EquippedArmorIndex);
        PlayerPrefs.Save();
    }

    private void LoadFromPrefs()
    {
        EquippedPrimaryIndex   = PlayerPrefs.GetInt(PrefKeyPrimary, 0);
        EquippedEquipmentIndex = PlayerPrefs.GetInt(PrefKeyEquipment, 0);
        EquippedArmorIndex     = PlayerPrefs.GetInt(PrefKeyArmor, 0);
    }
}
