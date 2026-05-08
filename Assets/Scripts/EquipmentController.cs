using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EquipmentController : MonoBehaviour
{
    public enum EquipmentType { StimShot, AmmoCrate }

    [Header("Equipment Selection")]
    [SerializeField] private EquipmentType selectedEquipment = EquipmentType.StimShot;

    [Header("Input")]
    [Tooltip("Equipment kullanma action'ı. Atanmazsa X tuşu fallback olarak kullanılır.")]
    [SerializeField] private InputActionReference useEquipmentAction;

    [Header("References")]
    [Tooltip("Weapon bileşeni; boş bırakılırsa alt objelerden otomatik bulunur.")]
    [SerializeField] private Weapon weapon;

    [Header("UI")]
    [Tooltip("Seçili ekipmanın ikonunu gösteren Image.")]
    [SerializeField] private Image equipmentIconImage;
    [Tooltip("Kalan cooldown'ı gösteren Slider. 1 = yeni kullanıldı, 0 = hazır.")]
    [SerializeField] private Slider cooldownSlider;
    [Tooltip("StimShot ikonu.")]
    [SerializeField] private Sprite stimShotSprite;
    [Tooltip("AmmoCrate ikonu.")]
    [SerializeField] private Sprite ammoCrateSprite;

    [Header("StimShot Settings")]
    [Tooltip("Toplam iyileşme miktarı (HP).")]
    [SerializeField] private int stimTotalHeal = 50;
    [Tooltip("İyileşmenin tamamlanacağı süre (saniye). Bu süre boyunca can smooth artar.")]
    [SerializeField] private float stimDuration = 3f;
    [Tooltip("StimShot'ın yeniden kullanılabilmesi için gereken bekleme süresi (saniye).")]
    [SerializeField] private float stimCooldown = 15f;
    [Tooltip("Stim kullanıldığında spawn edilecek particle efekti prefab'ı (ParticleSystem içeren obje).")]
    [SerializeField] private ParticleSystem stimParticleEffect;
    [Tooltip("Particle'ın spawn olacağı nokta. Boş bırakılırsa player pivot'u kullanılır.")]
    [SerializeField] private Transform stimSpawnPoint;

    [Header("AmmoCrate Settings")]
    [Tooltip("Ammo crate'in spawn olacağı nokta (oyuncunun altı).")]
    [SerializeField] private Transform equipmentPoint;
    [Tooltip("AmmoCrate prefab'ı.")]
    [SerializeField] private GameObject ammoCratePrefab;
    [Tooltip("AmmoCrate'in yeniden kullanılabilmesi için gereken bekleme süresi (saniye).")]
    [SerializeField] private float ammoCrateCooldown = 20f;

    private float _stimCooldownRemaining;
    private float _ammoCrateCooldownRemaining;
    private Coroutine _stimRoutine;
    private PlayerController _player;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        if (weapon == null)
            weapon = GetComponentInChildren<Weapon>(true);
    }

    void OnEnable()
    {
        if (useEquipmentAction != null)
            useEquipmentAction.action.performed += OnUseEquipment;
    }

    void OnDisable()
    {
        if (useEquipmentAction != null)
            useEquipmentAction.action.performed -= OnUseEquipment;
    }

    void Start()
    {
        RefreshUI();
    }

    void Update()
    {
        // Fallback keyboard input when no action reference is assigned.
        if (useEquipmentAction == null && Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
            TryUseEquipment();

        TickCooldowns();
        UpdateCooldownSlider();
    }

    void OnUseEquipment(InputAction.CallbackContext ctx) => TryUseEquipment();

    public void TryUseEquipment()
    {
        if (_player != null && !_player.canMove)
            return;

        switch (selectedEquipment)
        {
            case EquipmentType.StimShot:
                if (_stimCooldownRemaining > 0f) return;
                UseStimShot();
                break;
            case EquipmentType.AmmoCrate:
                if (_ammoCrateCooldownRemaining > 0f) return;
                UseAmmoCrate();
                break;
        }
    }

    // ── StimShot ─────────────────────────────────────────────────────────────

    void UseStimShot()
    {
        _stimCooldownRemaining = stimCooldown;

        if (stimParticleEffect != null)
        {
            Vector3 spawnPos = stimSpawnPoint != null ? stimSpawnPoint.position : transform.position;
            Instantiate(stimParticleEffect, spawnPos, Quaternion.identity);
        }

        if (_stimRoutine != null)
            StopCoroutine(_stimRoutine);
        _stimRoutine = StartCoroutine(StimHealRoutine());
    }

    IEnumerator StimHealRoutine()
    {
        float elapsed     = 0f;
        float accumulated = 0f;
        float healRate    = stimDuration > 0f ? (float)stimTotalHeal / stimDuration : stimTotalHeal;

        while (elapsed < stimDuration)
        {
            elapsed     += Time.deltaTime;
            accumulated += healRate * Time.deltaTime;

            // Her frame biriken kesirli canı tam sayıya dönüştürünce uygula.
            int toHeal = Mathf.FloorToInt(accumulated);
            if (toHeal > 0 && PlayerHealthController.instance != null)
            {
                PlayerHealthController.instance.HealPlayer(toHeal);
                accumulated -= toHeal;
            }

            yield return null;
        }

        // Kalan kesirli miktarı yuvarla ve uygula.
        int remainder = Mathf.RoundToInt(accumulated);
        if (remainder > 0)
            PlayerHealthController.instance?.HealPlayer(remainder);

        _stimRoutine = null;
    }

    // ── AmmoCrate ────────────────────────────────────────────────────────────

    void UseAmmoCrate()
    {
        if (ammoCratePrefab == null || equipmentPoint == null)
        {
            Debug.LogWarning("EquipmentController: ammoCratePrefab veya equipmentPoint atanmamış!");
            return;
        }

        _ammoCrateCooldownRemaining = ammoCrateCooldown;

        GameObject crateObj = Instantiate(ammoCratePrefab, equipmentPoint.position, Quaternion.identity);
        AmmoCrateController crate = crateObj.GetComponent<AmmoCrateController>();
        if (crate != null && weapon != null)
            crate.Initialize(weapon);
    }

    // ── Cooldown & UI ────────────────────────────────────────────────────────

    void TickCooldowns()
    {
        if (_stimCooldownRemaining > 0f)
            _stimCooldownRemaining = Mathf.Max(0f, _stimCooldownRemaining - Time.deltaTime);
        if (_ammoCrateCooldownRemaining > 0f)
            _ammoCrateCooldownRemaining = Mathf.Max(0f, _ammoCrateCooldownRemaining - Time.deltaTime);
    }

    void UpdateCooldownSlider()
    {
        if (cooldownSlider == null) return;

        float remaining = selectedEquipment == EquipmentType.StimShot
            ? _stimCooldownRemaining
            : _ammoCrateCooldownRemaining;

        float duration = selectedEquipment == EquipmentType.StimShot
            ? stimCooldown
            : ammoCrateCooldown;

        // 1 = yeni kullanıldı (tam dolu), 0 = hazır.
        cooldownSlider.value = duration > 0f ? remaining / duration : 0f;
    }

    void RefreshUI()
    {
        if (equipmentIconImage != null)
            equipmentIconImage.sprite = selectedEquipment == EquipmentType.StimShot
                ? stimShotSprite
                : ammoCrateSprite;

        UpdateCooldownSlider();
    }
}
