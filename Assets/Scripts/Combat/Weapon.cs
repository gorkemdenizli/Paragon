using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
// Aim at mouse, fire bullets from magazine with spread, auto / manual reload coroutine.
public class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private PlayerController player;

    [Tooltip("World aim origin (e.g. player root Z plane). Defaults to player transform.")]
    [SerializeField] private Transform aimPlaneRoot;

    [Tooltip("Rotates to point at cursor (e.g. gun arm pivot).")]
    [SerializeField] private Transform weaponPivot;

    [Tooltip("Spawn point for bullets; aim ray uses muzzle -> cursor for direction.")]
    [SerializeField] private Transform muzzle;

    [Tooltip("Optional extra transform; if no SpriteRenderer is found for flip, scale.y is used only when muzzle is NOT a child of this.")]
    [SerializeField] private Transform weaponVisual;

    [Tooltip("Gun sprite; flipY used when aiming left (does not mirror child transforms — keep Shot Point under Pivot, not under this sprite if possible).")]
    [SerializeField] private SpriteRenderer weaponSpriteRenderer;

    [Header("Muzzle clearance (aim left)")]
    [Tooltip("Transform whose localPosition is nudged when aiming left; default = this Weapon root.")]
    [SerializeField] private Transform positionShiftTarget;

    [Tooltip("Added to cached base local pos when cursor is left of body (e.g. small negative X).")]
    [SerializeField] private Vector3 localOffsetWhenAimLeft;

    [Header("Ammo UI (TMP)")]
    [Tooltip("Şarjördeki mermi. Total TMP atanmadıysa eski gibi \"mag / total\" tek satırda burada gösterilir.")]
    [FormerlySerializedAs("combinedAmmoText")]
    [FormerlySerializedAs("ammoCountText")]
    [SerializeField] private TMP_Text ammoMagazineText;
    [Tooltip("Genelde sabit \"/\"; ayrı TMP istemezsen boş bırak.")]
    [SerializeField] private TMP_Text ammoSeparatorText;
    [Tooltip("Sağdaki toplam sayı (_displayTotalAmmo). Reload bitene kadar mag boşken gecikmeli güncellenme aynı.")]
    [SerializeField] private TMP_Text ammoTotalText;

    [Tooltip("Optional UI Image next to ammo text — shows WeaponData.weaponSprite (static icon).")]
    [SerializeField] private Image ammoHudWeaponImage;

    [Tooltip("Optional SpriteRenderer on HUD (if not using UI Image) — same sprite.")]
    [SerializeField] private SpriteRenderer ammoHudWeaponSprite;

    [Tooltip("Aktif olmayan (diğer) silahı gösteren HUD image.")]
    [SerializeField] private Image inactiveWeaponHudImage;

    [Tooltip("Aktif olmayan silah image'ının altındaki tuş etiketi (ör. '2').")]
    [SerializeField] private TMP_Text inactiveWeaponKeyText;

    [Header("Reload UI")]
    [Tooltip("Reload sırasında görünür; süre boyunca 0 → 1 dolar, bitince kapanır.")]
    [SerializeField] private Slider reloadProgressSlider;

    [Header("Weapon Slots")]
    [Tooltip("Sırayla 1, 2, 3… tuşlarına atanan silah verileri. Boş bırakılırsa tek silah modu.")]
    [SerializeField] private WeaponData[] weaponSlots;

    [Header("Kickback")]
    [Tooltip("Ateş edildiğinde silahın geri gideceği mesafe (pivot yerel X ekseninde). Örnek: 0.15–0.35.")]
    [SerializeField] private float kickbackDistance = 0.25f;
    [Tooltip("Ateş edildiğinde silahın geri teptiği açı (derece). 0 = sadece pozisyon, >0 = yukarı tepme eklenir.")]
    [SerializeField] private float kickbackAngle = 8f;
    [Tooltip("Geri tepkinin orijinal konuma dönüş hızı. Yüksek = sert/hızlı, düşük = yumuşak.")]
    [SerializeField] private float kickbackReturnSpeed = 14f;

    [Header("Precise Aim")]
    [Tooltip("Sağ tık precise aim bileşeni. Atanırsa tam ADS'te accuracy artar.")]
    [SerializeField] private PreciseAimController preciseAimController;
    [Tooltip("Precise aim tam açıkken accuracy'ye uygulanacak çarpan. 1.25 = %25 artış.")]
    [SerializeField] private float preciseAimAccuracyMultiplier = 1.25f;

    [Header("Aim Correction")]
    [Tooltip("Barrel sprite pivot +X'e tam hizalı değilse bu değerle düzelt (derece). Silah crosshair'ın üstüne bakıyorsa negatif yap.")]
    [SerializeField] private float aimAngleOffset = 0f;

    private float _recoilOffset;
    private float _recoilAngleDeg;
    private Vector3 _pivotBaseLocalPos;
    private bool _pivotBaseCached;

    // Ammo total text punch animation
    private float _ammoTextPunchTimer;
    private bool _ammoTextPunching;
    private Vector3 _ammoTextBaseScale;
    private const float AmmoTextPunchDuration = 0.25f;
    private const float AmmoTextPunchScale    = 1.45f;

    private int _magAmmo;
    private int _reserveAmmo;
    // Shown as right-hand number; decreases when reload finishes (by rounds moved from reserve), not each shot.
    private int _displayTotalAmmo;
    private float _nextShotTime;
    private bool _reloading;
    private Coroutine _reloadRoutine;
    private Vector3 _baseShiftLocalPos;

    // Weapon slot system
    private struct SlotState { public int mag, reserve, displayTotal; }
    private SlotState[] _slotStates;
    private int _activeSlot;
    private static readonly Key[] DigitKeys =
        { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

    // Cache starting ammo and sprite.
    void Awake()
    {
        if (aimPlaneRoot == null && player != null)
            aimPlaneRoot = player.transform;

        Transform shiftTarget = positionShiftTarget != null ? positionShiftTarget : transform;
        _baseShiftLocalPos = shiftTarget.localPosition;

        CachePivotBasePos();
        IgnorePlayerVsShotLayerCollision();

        _ammoTextBaseScale = ammoTotalText != null ? ammoTotalText.transform.localScale : Vector3.one;

        InitSlots();
        RefreshAmmoUi();

        if (reloadProgressSlider != null)
        {
            reloadProgressSlider.minValue = 0f;
            reloadProgressSlider.maxValue = 1f;
            reloadProgressSlider.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        RegisterWeaponBaseStats();

        if (PlayerStats.instance != null)
            PlayerStats.instance.OnStatsChanged += OnPlayerStatsChanged;
    }

    void OnDestroy()
    {
        if (PlayerStats.instance != null)
            PlayerStats.instance.OnStatsChanged -= OnPlayerStatsChanged;
    }

    // Re-compute slot capacities when an upgrade changes magazine size.
    void OnPlayerStatsChanged()
    {
        if (_slotStates == null || weaponSlots == null) return;

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null) continue;
            int newCap = EffectiveMagSize(weaponSlots[i]);

            // Keep current ammo in active slot, but expand/clamp the stored cap.
            if (i == _activeSlot)
            {
                // If mag was full before, fill to new cap too.
                if (_magAmmo == _slotStates[i].mag && _slotStates[i].mag == Mathf.RoundToInt(weaponSlots[i].magazineSize * 1f))
                    _magAmmo = newCap;
                _slotStates[i] = new SlotState { mag = _magAmmo, reserve = _reserveAmmo, displayTotal = _displayTotalAmmo };
            }
            else
            {
                // For inactive slots, scale stored mag proportionally.
                SlotState s   = _slotStates[i];
                int oldCap    = Mathf.Max(1, s.mag + s.reserve > 0 ? weaponSlots[i].magazineSize : 1);
                s.mag         = Mathf.Clamp(s.mag, 0, newCap);
                _slotStates[i] = s;
            }
        }

        RefreshAmmoUi();
    }

    // Returns the effective magazine capacity for a WeaponData, including PlayerStats multiplier.
    static int EffectiveMagSize(WeaponData d)
        => d == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(d.magazineSize * (PlayerStats.instance?.MagazineSizeMultiplier ?? 1f)));

    void RegisterWeaponBaseStats()
    {
        if (weaponData == null || PlayerStats.instance == null) return;
        PlayerStats.instance.RegisterBaseValue(StatType.Damage,       weaponData.damage);
        PlayerStats.instance.RegisterBaseValue(StatType.RateOfFire,   weaponData.fireRate);
        PlayerStats.instance.RegisterBaseValue(StatType.Accuracy,     weaponData.accuracy);
        PlayerStats.instance.RegisterBaseValue(StatType.MagazineSize, weaponData.magazineSize);
        PlayerStats.instance.RegisterBaseValue(StatType.ReloadSpeed,  weaponData.reloadSpeed);
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        HandleSlotSwitchInput();
        TickAmmoTextPunch();
    }

    void HandleSlotSwitchInput()
    {
        if (_slotStates == null || Keyboard.current == null)
            return;
        for (int i = 0; i < _slotStates.Length && i < DigitKeys.Length; i++)
        {
            if (i != _activeSlot && Keyboard.current[DigitKeys[i]].wasPressedThisFrame)
                SwitchToSlot(i);
        }
    }

    // Mermi alındığında total ammo text'i büyütüp küçülten animasyon.
    // AddReserveAmmo çağrısı timer'ı sıfırlayarak animasyonu yeniden başlatır.
    void TickAmmoTextPunch()
    {
        if (!_ammoTextPunching || ammoTotalText == null)
            return;

        _ammoTextPunchTimer -= Time.deltaTime;

        if (_ammoTextPunchTimer <= 0f)
        {
            _ammoTextPunching = false;
            ammoTotalText.transform.localScale = _ammoTextBaseScale;
            return;
        }

        // t: 1 → 0 olarak azalır; Sin(t*PI) ile 0→max→0 eğrisi oluşturur.
        float t = _ammoTextPunchTimer / AmmoTextPunchDuration;
        float scale = 1f + (AmmoTextPunchScale - 1f) * Mathf.Sin(t * Mathf.PI);
        ammoTotalText.transform.localScale = _ammoTextBaseScale * scale;
    }

    void OnDisable()
    {
        if (reloadProgressSlider != null)
            reloadProgressSlider.gameObject.SetActive(false);
    }

    // Slot 0'dan başlayarak tüm slotları başlatır; yoksa tek silah moduna düşer.
    void InitSlots()
    {
        // Apply the loadout-selected primary weapon to slot 0 if available.
        if (LoadoutManager.instance?.EquippedPrimaryWeapon != null
            && weaponSlots != null && weaponSlots.Length > 0)
        {
            weaponSlots[0] = LoadoutManager.instance.EquippedPrimaryWeapon;
        }

        bool hasSlots = weaponSlots != null && weaponSlots.Length > 0;
        if (hasSlots)
        {
            _activeSlot = 0;
            weaponData = weaponSlots[0];
            _slotStates = new SlotState[weaponSlots.Length];
            for (int i = 0; i < weaponSlots.Length; i++)
                ComputeSlotAmmo(i);
            LoadSlotAmmo(0);
        }
        else if (weaponData != null)
        {
            InitializeAmmoFromStartingTotal();
        }

        if (weaponData != null)
            ApplyWeaponSprite();
    }

    // WeaponData'dan başlangıç mermi değerini hesaplar ve ilgili slota yazar.
    void ComputeSlotAmmo(int index)
    {
        if (_slotStates == null || weaponSlots == null || index >= weaponSlots.Length) return;
        WeaponData d = weaponSlots[index];
        if (d == null) return;
        int cap   = Mathf.RoundToInt(d.magazineSize * (PlayerStats.instance?.MagazineSizeMultiplier ?? 1f));
        int total = Mathf.Max(0, d.startingTotalAmmo);
        int mag   = Mathf.Min(cap, total);
        _slotStates[index] = new SlotState { mag = mag, reserve = total - mag, displayTotal = total };
    }

    void SaveSlotAmmo(int index)
    {
        if (_slotStates == null || index < 0 || index >= _slotStates.Length) return;
        _slotStates[index] = new SlotState
            { mag = _magAmmo, reserve = _reserveAmmo, displayTotal = _displayTotalAmmo };
    }

    void LoadSlotAmmo(int index)
    {
        if (_slotStates == null || index < 0 || index >= _slotStates.Length) return;
        _magAmmo        = _slotStates[index].mag;
        _reserveAmmo    = _slotStates[index].reserve;
        _displayTotalAmmo = _slotStates[index].displayTotal;
    }

    // Aktif slotu kaydeder, yeni slota geçer ve HUD'ı günceller.
    public void SwitchToSlot(int index)
    {
        if (_slotStates == null || index < 0 || index >= _slotStates.Length) return;
        if (weaponSlots == null || index >= weaponSlots.Length || weaponSlots[index] == null) return;
        if (index == _activeSlot) return;

        SaveSlotAmmo(_activeSlot);

        // Devam eden reload'u iptal et.
        if (_reloadRoutine != null) { StopCoroutine(_reloadRoutine); _reloadRoutine = null; }
        _reloading = false;
        if (reloadProgressSlider != null) reloadProgressSlider.gameObject.SetActive(false);

        _activeSlot  = index;
        weaponData   = weaponSlots[index];
        _nextShotTime = 0f;

        LoadSlotAmmo(index);
        ApplyWeaponSprite();
        RefreshAmmoUi();
    }

    // Split starting total into magazine + reserve.
    void InitializeAmmoFromStartingTotal()
    {
        int cap = weaponData.magazineSize;
        int total = Mathf.Max(0, weaponData.startingTotalAmmo);
        _magAmmo = Mathf.Min(cap, total);
        _reserveAmmo = total - _magAmmo;
        _displayTotalAmmo = total;
    }

    // mag / total UI; total matches carried after reload, lags while mag is empty until reload completes.
    void RefreshAmmoUi()
    {
        if (weaponData == null)
            return;

        bool infinite = weaponData.infiniteAmmo;

        if (!infinite)
        {
            int carried = _magAmmo + _reserveAmmo;
            if (carried == 0)
                _displayTotalAmmo = 0;
        }

        string totalStr = infinite ? "\u221E" : _displayTotalAmmo.ToString();

        if (ammoMagazineText != null && ammoTotalText != null)
        {
            ammoMagazineText.text = _magAmmo.ToString();
            ammoTotalText.text = totalStr;
            if (ammoSeparatorText != null)
                ammoSeparatorText.text = "/";
        }
        else if (ammoMagazineText != null)
        {
            ammoMagazineText.text = _magAmmo + " / " + totalStr;
        }
        else if (ammoTotalText != null)
        {
            ammoTotalText.text = _magAmmo + " / " + totalStr;
        }
    }

    // Call from pickups, AmmoCrate, etc.
    public void AddReserveAmmo(int amount)
    {
        if (amount <= 0)
            return;
        _reserveAmmo += amount;
        _displayTotalAmmo += amount;
        RefreshAmmoUi();

        // Her mermi alımında total text'i büyüt-küçült (timer sıfırlanarak animasyon yenilenir).
        if (ammoTotalText != null)
        {
            _ammoTextPunchTimer = AmmoTextPunchDuration;
            _ammoTextPunching   = true;
        }
    }

    public int MagAmmo => _magAmmo;
    public int ReserveAmmo => _reserveAmmo;
    public int TotalAmmoRemaining => _magAmmo + _reserveAmmo;
    public bool ActiveWeaponHasInfiniteAmmo => weaponData != null && weaponData.infiniteAmmo;

    // Bullets on Shot layer should never collide with Player layer (spawn inside collider).
    static void IgnorePlayerVsShotLayerCollision()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int shotLayer = LayerMask.NameToLayer("Shot");
        if (playerLayer < 0 || shotLayer < 0)
            return;
        Physics2D.IgnoreLayerCollision(playerLayer, shotLayer, true);
    }

    // Left-aim mirror: use sprite flipY so muzzle children are not mirrored in world space.
    void ApplyLeftAimVisual(bool mouseLeftOfBody)
    {
        // When weaponPivot is present, localScale.y flip on the pivot mirrors both the sprite
        // and all child transforms (including muzzle) automatically — no flipY or manual offset needed.
        if (weaponPivot != null)
        {
            SpriteRenderer sr = weaponSpriteRenderer;
            if (sr == null && weaponVisual != null)
                sr = weaponVisual.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.flipY = false;
            if (weaponVisual != null)
                weaponVisual.localScale = Vector3.one;
            return;
        }

        // Fallback when there is no weaponPivot: flip sprite or visual scale directly.
        SpriteRenderer fallbackSr = weaponSpriteRenderer;
        if (fallbackSr == null && weaponVisual != null)
            fallbackSr = weaponVisual.GetComponent<SpriteRenderer>();

        if (fallbackSr != null)
        {
            fallbackSr.flipY = mouseLeftOfBody;
            fallbackSr.transform.localScale = Vector3.one;
            if (weaponVisual != null && weaponVisual != fallbackSr.transform)
                weaponVisual.localScale = Vector3.one;
            return;
        }

        if (weaponVisual == null)
            return;

        bool muzzleUnderVisual = muzzle != null && muzzle.transform.IsChildOf(weaponVisual);
        if (muzzleUnderVisual)
        {
            weaponVisual.localScale = Vector3.one;
            return;
        }

        weaponVisual.localScale = new Vector3(1f, mouseLeftOfBody ? -1f : 1f, 1f);
    }

    // Point gun at mouse, flip Y when cursor is left of body.
    void LateUpdate()
    {
        if (Time.timeScale == 0f) return;

        if (weaponData == null || player == null || aimPlaneRoot == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 mouseWorld = AimPlaneUtil.ScreenToWorldOnPlane(cam, aimPlaneRoot.position);
        Vector2 bodyPos = aimPlaneRoot.position;
        bool mouseLeftOfBody = mouseWorld.x < bodyPos.x;

        Transform shiftTarget = positionShiftTarget != null ? positionShiftTarget : transform;
        shiftTarget.localPosition = mouseLeftOfBody
            ? _baseShiftLocalPos + localOffsetWhenAimLeft
            : _baseShiftLocalPos;

        // Flip pivot Y scale so both the sprite and all child transforms (muzzle included) are
        // mirrored automatically when aiming left — no per-axis manual offset required.
        if (weaponPivot != null)
            weaponPivot.localScale = new Vector3(1f, mouseLeftOfBody ? -1f : 1f, 1f);

        ApplyLeftAimVisual(mouseLeftOfBody);

        if (weaponPivot != null)
        {
            CachePivotBasePos();

            // Sadece X eksenini yönet: Y'yi (PreciseAimController'ın lift değeri) koruyarak
            // pivot'u aim yönünün tersine kaydır. Sola nişanda işaret tersine döner.
            float recoilSign = mouseLeftOfBody ? 1f : -1f;
            Vector3 pivotPos = weaponPivot.localPosition;
            pivotPos.x = _pivotBaseLocalPos.x + recoilSign * _recoilOffset;
            weaponPivot.localPosition = pivotPos;

            Vector2 pivotWorldPos = weaponPivot.position;
            Vector2 toMouse = (Vector2)mouseWorld - pivotWorldPos;
            if (toMouse.sqrMagnitude > 1e-6f)
            {
                float aimAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
                // Y scale -1 (sol nişan) durumunda açıyı tersine çevirerek her yönde yukarı tepme sağlanır.
                float recoilContrib = mouseLeftOfBody ? -_recoilAngleDeg : _recoilAngleDeg;
                weaponPivot.rotation = Quaternion.Euler(0f, 0f, aimAngle + recoilContrib + aimAngleOffset);
            }
        }

        float decay = kickbackReturnSpeed * Time.deltaTime;
        _recoilOffset    = Mathf.Lerp(_recoilOffset,    0f, decay);
        _recoilAngleDeg  = Mathf.Lerp(_recoilAngleDeg,  0f, decay);
    }

    void CachePivotBasePos()
    {
        if (_pivotBaseCached || weaponPivot == null)
            return;
        _pivotBaseLocalPos = weaponPivot.localPosition;
        _pivotBaseCached = true;
    }

    // One shot if allowed; starts auto reload when magazine empties.
    public bool TryShoot()
    {
        if (weaponData == null || bulletPrefab == null || muzzle == null || player == null)
            return false;

        if (_reloading)
        {
            // Shell reload kesilirse yüklü shell'lerle ateş edilebilir.
            if (!weaponData.shellReload || _magAmmo <= 0)
                return false;

            if (_reloadRoutine != null) { StopCoroutine(_reloadRoutine); _reloadRoutine = null; }
            _reloading = false;
            if (reloadProgressSlider != null) reloadProgressSlider.gameObject.SetActive(false);
        }

        if (_magAmmo <= 0)
        {
            StartReloadIfNeeded(force: true);
            return false;
        }

        float rateOfFireMult = PlayerStats.instance?.RateOfFireMultiplier ?? 1f;
        float interval = 1f / Mathf.Max(0.01f, weaponData.fireRate * rateOfFireMult);
        if (Time.time < _nextShotTime)
            return false;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 mouseWorld = AimPlaneUtil.ScreenToWorldOnPlane(cam, aimPlaneRoot.position);
        // Mermi yönünü pivot'un mevcut rotation'ından al; bu sayede mouse çok yakında olsa bile
        // silahın baktığı yönde mermi çıkar (muzzle→mouse vektöründeki yakın mesafe sapması olmaz).
        Vector2 dir;
        if (weaponPivot != null)
        {
            dir = weaponPivot.rotation * Vector2.right;
        }
        else
        {
            Vector2 toCursor = (Vector2)mouseWorld - (Vector2)muzzle.position;
            dir = toCursor.sqrMagnitude > 1e-6f ? toCursor.normalized : Vector2.right;
        }
        float baseDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float adsBlend = preciseAimController != null ? preciseAimController.AimBlendNormalized : 0f;
        float accuracyMult = PlayerStats.instance?.AccuracyMultiplier ?? 1f;
        float effectiveAccuracy = weaponData.accuracy * accuracyMult * Mathf.Lerp(1f, preciseAimAccuracyMultiplier, adsBlend);
        effectiveAccuracy = Mathf.Min(effectiveAccuracy, 10f);
        float spreadHalf = 10f - effectiveAccuracy;

        float critChance  = PlayerStats.instance?.CritChance ?? 0.05f;
        float critDamage  = PlayerStats.instance?.CritDamage ?? 1.5f;
        bool  isCrit      = Random.value < critChance;
        int baseFinalDmg  = Mathf.RoundToInt(weaponData.damage * (PlayerStats.instance?.DamageMultiplier ?? 1f));
        int finalDamage   = isCrit ? Mathf.RoundToInt(baseFinalDmg * critDamage) : baseFinalDmg;

        int pellets = Mathf.Max(1, weaponData.pelletsPerShot);
        for (int p = 0; p < pellets; p++)
        {
            float spread = Random.Range(-spreadHalf, spreadHalf);
            Vector2 shootDir = (Quaternion.Euler(0f, 0f, baseDeg + spread) * Vector2.right).normalized;
            Bullet b = Instantiate(bulletPrefab, muzzle.position, Quaternion.identity);
            b.Initialize(shootDir, weaponData.bulletSpeed, finalDamage, isCrit);
        }

        _magAmmo--;
        _nextShotTime = Time.time + interval;
        _recoilOffset = kickbackDistance;
        _recoilAngleDeg = kickbackAngle;
        CameraShake.instance?.Shake(weaponData.shootShakeIntensity, weaponData.shootShakeDuration);
        RefreshAmmoUi();

        if (_magAmmo <= 0)
            StartReloadIfNeeded(force: true);

        return true;
    }

    // Player-requested reload; blocked if full or already reloading.
    public bool TryManualReload()
    {
        if (weaponData == null || _reloading)
            return false;
        if (_magAmmo >= EffectiveMagSize(weaponData))
            return false;
        if (!weaponData.infiniteAmmo && _reserveAmmo <= 0)
            return false;

        return StartReloadIfNeeded(force: false);
    }

    // World gun sprite + optional HUD icon from WeaponData.
    void ApplyWeaponSprite()
    {
        if (weaponData == null || weaponData.weaponSprite == null)
            return;

        if (weaponSpriteRenderer != null)
            weaponSpriteRenderer.sprite = weaponData.weaponSprite;

        if (ammoHudWeaponImage != null)
            ammoHudWeaponImage.sprite = weaponData.weaponSprite;

        if (ammoHudWeaponSprite != null)
            ammoHudWeaponSprite.sprite = weaponData.weaponSprite;

        RefreshInactiveWeaponHud();
    }

    // Aktif olmayan slotun image'ını ve tuş etiketini günceller.
    void RefreshInactiveWeaponHud()
    {
        if (weaponSlots == null || weaponSlots.Length < 2) return;

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (i == _activeSlot) continue;
            if (weaponSlots[i] == null) continue;

            if (inactiveWeaponHudImage != null)
                inactiveWeaponHudImage.sprite = weaponSlots[i].weaponSprite;

            if (inactiveWeaponKeyText != null)
                inactiveWeaponKeyText.text = (i + 1).ToString();

            break; // ilk aktif-olmayan slot yeterli (2 silah için)
        }
    }

    // Starts reload coroutine unless already running.
    bool StartReloadIfNeeded(bool force)
    {
        if (weaponData == null || _reloading)
            return false;
        int cap = EffectiveMagSize(weaponData);
        if (!force && _magAmmo >= cap)
            return false;

        int need = cap - _magAmmo;
        if (need <= 0)
            return false;
        if (!weaponData.infiniteAmmo && _reserveAmmo <= 0)
            return false;

        if (_reloadRoutine != null)
            StopCoroutine(_reloadRoutine);
        _reloadRoutine = weaponData.shellReload
            ? StartCoroutine(ShellReloadRoutine())
            : StartCoroutine(ReloadRoutine());
        return true;
    }

    // Waits reloadSpeed then fills magazine.
    IEnumerator ReloadRoutine()
    {
        _reloading = true;
        RefreshAmmoUi();

        float reloadMult = PlayerStats.instance?.ReloadSpeedMultiplier ?? 1f;
        float duration = Mathf.Max(0.0001f, weaponData.reloadSpeed * reloadMult);
        if (reloadProgressSlider != null)
        {
            reloadProgressSlider.gameObject.SetActive(true);
            reloadProgressSlider.value = 0f;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (reloadProgressSlider != null)
                reloadProgressSlider.value = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        if (reloadProgressSlider != null)
        {
            reloadProgressSlider.value = 1f;
            reloadProgressSlider.gameObject.SetActive(false);
        }

        int cap = EffectiveMagSize(weaponData);
        int need = cap - _magAmmo;
        bool infinite = weaponData.infiniteAmmo;
        int load = infinite ? need : Mathf.Min(need, _reserveAmmo);
        _magAmmo += load;
        if (!infinite)
        {
            _reserveAmmo -= load;
            _displayTotalAmmo -= load;
        }

        _reloading = false;
        _reloadRoutine = null;
        RefreshAmmoUi();
    }

    // Shell shell doldurma: her shell reloadSpeed kadar sürer, slider her shell için 0→1 döngüsü yapar.
    // Ateş edilince kesilir (TryShoot içinde _reloading=false yapılır), yüklü shell'lerle devam edilir.
    IEnumerator ShellReloadRoutine()
    {
        _reloading = true;
        RefreshAmmoUi();

        if (reloadProgressSlider != null)
            reloadProgressSlider.gameObject.SetActive(true);

        float reloadMult    = PlayerStats.instance?.ReloadSpeedMultiplier ?? 1f;
        float shellDuration = Mathf.Max(0.0001f, weaponData.reloadSpeed * reloadMult);
        int cap = Mathf.RoundToInt(weaponData.magazineSize * (PlayerStats.instance?.MagazineSizeMultiplier ?? 1f));
        bool infinite = weaponData.infiniteAmmo;

        while (_magAmmo < cap && (infinite || _reserveAmmo > 0))
        {
            float elapsed = 0f;
            if (reloadProgressSlider != null)
                reloadProgressSlider.value = 0f;

            while (elapsed < shellDuration)
            {
                elapsed += Time.deltaTime;
                if (reloadProgressSlider != null)
                    reloadProgressSlider.value = Mathf.Clamp01(elapsed / shellDuration);
                yield return null;
            }

            _magAmmo++;
            if (!infinite)
            {
                _reserveAmmo--;
                _displayTotalAmmo--;
            }
            RefreshAmmoUi();
        }

        if (reloadProgressSlider != null)
            reloadProgressSlider.gameObject.SetActive(false);

        _reloading = false;
        _reloadRoutine = null;
    }
}
