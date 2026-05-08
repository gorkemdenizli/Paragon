using System.Collections;
using UnityEngine;

/// <summary>
/// Oyuncunun EquipmentController'ı aracılığıyla yere bıraktığı Ammo Crate.
///
/// Kurulum gereksinimleri (prefab üzerinde):
///   - Bu script (root obje)
///   - Root: BoxCollider2D (Is Trigger: kapalı) + Rigidbody2D  →  Layer: AmmoCrate
///   - Child "TriggerZone": BoxCollider2D (Is Trigger: açık)   →  Layer: Default
///   - triggerZone alanına child'daki trigger collider'ı sürükle
///   - groundLayer'a zemin layer'ını ata
///   - Oyuncu tag'ı "Player" olarak ayarlanmış olmalıdır.
/// </summary>
public class AmmoCrateController : MonoBehaviour
{
    [Header("Ammo Settings")]
    [Tooltip("Her tick'te verilecek mermi miktarı.")]
    [SerializeField] private int ammoPerTick = 20;
    [Tooltip("Tick'ler arası bekleme süresi (saniye).")]
    [SerializeField] private float tickInterval = 0.1f;
    [Tooltip("Toplam tick sayısı. Toplam mermi = ammoPerTick × totalTicks.")]
    [SerializeField] private int totalTicks = 10;
    [Tooltip("Tüm mermiler verildikten sonra objenin yok olana kadar bekleme süresi (saniye).")]
    [SerializeField] private float destroyDelay = 3f;

    [Header("Ground Detection")]
    [Tooltip("Child objedeki trigger collider. Yere değene kadar devre dışı tutulur.")]
    [SerializeField] private Collider2D triggerZone;
    [Tooltip("Zemin olarak kabul edilecek layer'lar.")]
    [SerializeField] private LayerMask groundLayer;

    private Weapon _weapon;
    private bool _landed;
    private bool _activated;

    public void Initialize(Weapon weapon)
    {
        _weapon = weapon;
    }

    void Awake()
    {
        // Yere değene kadar trigger zone kapalı — havadayken oyuncu algılanmasın.
        if (triggerZone != null)
            triggerZone.enabled = false;
    }

    // Root objedeki fizik collider zemini yakalar.
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (_landed) return;
        if ((groundLayer.value & (1 << collision.gameObject.layer)) == 0) return;

        _landed = true;

        if (triggerZone != null)
            triggerZone.enabled = true;
    }

    // Child trigger collider'dan gelen olay Rigidbody2D sayesinde buraya iletilir.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (_activated) return;
        if (!other.CompareTag("Player")) return;

        if (_weapon == null)
            _weapon = FindFirstObjectByType<Weapon>();

        if (_weapon == null)
        {
            Debug.LogWarning("AmmoCrateController: Weapon bulunamadı, mermi verilemez.");
            return;
        }

        if (_weapon.ActiveWeaponHasInfiniteAmmo)
            return;

        _activated = true;
        StartCoroutine(GiveAmmoRoutine());
    }

    IEnumerator GiveAmmoRoutine()
    {
        for (int i = 0; i < totalTicks; i++)
        {
            _weapon.AddReserveAmmo(ammoPerTick);
            yield return new WaitForSeconds(tickInterval);
        }

        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }
}
