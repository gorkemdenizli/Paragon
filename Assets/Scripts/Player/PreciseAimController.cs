using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sağ tık "precise aim" — kamera max offset X/Y, crosshair boyutu ve silah local Y kaldırması arasında yumuşak geçiş.
/// İleride accuracy / handling alanları <see cref="PreciseAimPresentationProfile"/> üzerinden genişletilebilir.
/// </summary>
[DefaultExecutionOrder(-20)]
public class PreciseAimController : MonoBehaviour
{
    [Serializable]
    public struct PreciseAimPresentationProfile
    {
        [Tooltip("CameraMouseOffset maxOffsetX ile çarpılır (1 = normal, 1.5 = %50 artış).")]
        public float cameraMaxOffsetXMultiplier;

        [Tooltip("CameraMouseOffset maxOffsetY ile çarpılır (1 = normal, 1.5 = %50 artış). 0 = eski sahneler: X ile aynı kabul edilir.")]
        public float cameraMaxOffsetYMultiplier;

        public Vector2 crosshairSizeDelta;

        [Tooltip("weaponLiftRoot local uzayda +Y (ADS hissi).")]
        public float weaponLocalLiftY;

        // İleride: spread çarpanı, sway, ADS hızı vb. buraya eklenebilir.
    }

    [Header("Refs")]
    [SerializeField] private CameraMouseOffset cameraMouseOffset;
    [SerializeField] private RectTransform crosshairRect;
    [Tooltip("Silahı local Y ile kaldırır. Weapon'daki positionShiftTarget ile AYNI transformu verme (LateUpdate o transformu sıfırlar). Tercih: silah hiyerarşisinde Weapon'ın üstünde boş bir 'GunMount' parent.")]
    [SerializeField] private Transform weaponLiftRoot;

    [Header("Blend")]
    [Tooltip("Hip ↔ precise geçiş hızı (exponential). Yükselt = daha snappy (ör. 18–28), düşür = daha yumuşak (ör. 6–10).")]
    [SerializeField] private float blendSmooth = 16f;

    [Header("Hip-fire (sağ tık yok)")]
    [SerializeField] private PreciseAimPresentationProfile hip = new PreciseAimPresentationProfile
    {
        cameraMaxOffsetXMultiplier = 1f,
        cameraMaxOffsetYMultiplier = 1f,
        crosshairSizeDelta = new Vector2(75f, 75f),
        weaponLocalLiftY = 0f,
    };

    [Header("Precise (sağ tık)")]
    [SerializeField] private PreciseAimPresentationProfile precise = new PreciseAimPresentationProfile
    {
        cameraMaxOffsetXMultiplier = 1.5f,
        cameraMaxOffsetYMultiplier = 1.5f,
        crosshairSizeDelta = new Vector2(60f, 60f),
        weaponLocalLiftY = 0.12f,
    };

    float _blend;
    Vector3 _weaponBaseLocalPos;
    bool _weaponBaseCached;

    /// <summary>0 = hip, 1 = tam precise; accuracy / handling için dışarıdan okunabilir.</summary>
    public float AimBlendNormalized => _blend;

    void Awake()
    {
        if (crosshairRect != null)
            crosshairRect.sizeDelta = hip.crosshairSizeDelta;

        CacheWeaponBaseIfNeeded();
    }

    void CacheWeaponBaseIfNeeded()
    {
        if (weaponLiftRoot == null || _weaponBaseCached)
            return;
        _weaponBaseLocalPos = weaponLiftRoot.localPosition;
        _weaponBaseCached = true;
    }

    void Update()
    {
        float target = IsPreciseHeld() ? 1f : 0f;
        float t = blendSmooth > 0f ? 1f - Mathf.Exp(-blendSmooth * Time.deltaTime) : 1f;
        _blend = Mathf.Lerp(_blend, target, t);

        float camMulX = Mathf.Lerp(EffectiveCamXMul(hip), EffectiveCamXMul(precise), _blend);
        float camMulY = Mathf.Lerp(EffectiveCamYMul(hip), EffectiveCamYMul(precise), _blend);
        if (cameraMouseOffset != null)
        {
            cameraMouseOffset.SetExternalMaxOffsetXScale(camMulX);
            cameraMouseOffset.SetExternalMaxOffsetYScale(camMulY);
        }

        if (crosshairRect != null)
        {
            crosshairRect.sizeDelta = Vector2.Lerp(hip.crosshairSizeDelta, precise.crosshairSizeDelta, _blend);
        }

        ApplyWeaponLift();
    }

    void ApplyWeaponLift()
    {
        if (weaponLiftRoot == null)
            return;

        CacheWeaponBaseIfNeeded();

        float lift = Mathf.Lerp(hip.weaponLocalLiftY, precise.weaponLocalLiftY, _blend);
        weaponLiftRoot.localPosition = _weaponBaseLocalPos + Vector3.up * lift;
    }

    static bool IsPreciseHeld()
    {
        if (Mouse.current != null)
            return Mouse.current.rightButton.isPressed;
        return false;
    }

    static float EffectiveCamXMul(in PreciseAimPresentationProfile p) =>
        p.cameraMaxOffsetXMultiplier > 0f ? p.cameraMaxOffsetXMultiplier : 1f;

    static float EffectiveCamYMul(in PreciseAimPresentationProfile p)
    {
        if (p.cameraMaxOffsetYMultiplier > 0f)
            return p.cameraMaxOffsetYMultiplier;
        float x = p.cameraMaxOffsetXMultiplier;
        return x > 0f ? x : 1f;
    }

    void OnDisable()
    {
        if (cameraMouseOffset != null)
        {
            cameraMouseOffset.SetExternalMaxOffsetXScale(EffectiveCamXMul(hip));
            cameraMouseOffset.SetExternalMaxOffsetYScale(EffectiveCamYMul(hip));
        }

        if (crosshairRect != null)
            crosshairRect.sizeDelta = hip.crosshairSizeDelta;

        if (weaponLiftRoot != null && _weaponBaseCached)
            weaponLiftRoot.localPosition = _weaponBaseLocalPos;

        _blend = 0f;
    }
}
