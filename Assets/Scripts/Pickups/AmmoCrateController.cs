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
    private bool _started;
    private bool _playerInside;

    public void Initialize(Weapon weapon)
    {
        _weapon = weapon;
    }

    void Awake()
    {
        if (triggerZone != null)
            triggerZone.enabled = false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (_landed) return;
        if ((groundLayer.value & (1 << collision.gameObject.layer)) == 0) return;

        _landed = true;

        if (triggerZone != null)
            triggerZone.enabled = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (_weapon == null)
            _weapon = FindFirstObjectByType<Weapon>();

        if (_weapon == null)
        {
            Debug.LogWarning("AmmoCrateController: Weapon bulunamadı, mermi verilemez.");
            return;
        }

        _playerInside = true;

        if (!_started)
        {
            _started = true;
            StartCoroutine(GiveAmmoRoutine());
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = false;
    }

    IEnumerator GiveAmmoRoutine()
    {
        int ticksLeft = totalTicks;

        while (ticksLeft > 0)
        {
            // Oyuncu içeride değilse veya aktif silahın mermisi sonsuzsa bekle.
            yield return new WaitUntil(() => _playerInside && !_weapon.ActiveWeaponHasInfiniteAmmo);

            _weapon.AddReserveAmmo(ammoPerTick);
            ticksLeft--;

            if (ticksLeft > 0)
                yield return new WaitForSeconds(tickInterval);
        }

        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }
}
