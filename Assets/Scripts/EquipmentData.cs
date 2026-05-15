using UnityEngine;

// Loadout menüsünde gösterilecek ekipman bilgileri.
[CreateAssetMenu(fileName = "EquipmentData", menuName = "Game/Equipment Data", order = 1)]
public class EquipmentData : ScriptableObject
{
    [Tooltip("Loadout menüsünde gösterilecek isim.")]
    public string equipmentName;

    public Sprite equipmentSprite;

    [Tooltip("Bu veri hangi ekipman tipine karşılık gelir.")]
    public EquipmentController.EquipmentType equipmentType;

    [Header("Stats")]
    [Tooltip("Ammo Crate için verilen toplam mermi miktarı. Stim Shot için kullanılmaz.")]
    public int totalAmmo;

    [Tooltip("Stim Shot için iyileşme miktarı (HP). Ammo Crate için kullanılmaz.")]
    public int healthAmount;

    [Tooltip("Yeniden kullanım süresi (saniye).")]
    public float cooldown;
}
