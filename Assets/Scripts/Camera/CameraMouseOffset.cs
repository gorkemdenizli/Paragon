using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMouseOffset : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private CinemachinePositionComposer composer;
    [SerializeField] private Camera gameplayCamera;

    [Header("Dead Zone")]
    [Tooltip("Merkezden uzaklık (centered vektörü, tipik max ~1.41). 0.08 civarı uygundur. Eski sahnelerde 2 gibi büyük değerler tüm offseti sıfırlıyordu.")]
    [SerializeField] private float deadZoneRadius = 0.08f;

    [Header("Offset")]
    [SerializeField] private float maxOffsetX;
    [SerializeField] private float maxOffsetY;

    [Header("Smoothing")]
    [SerializeField] private float smooth;

    float _externalMaxOffsetXScale = 1f;
    float _externalMaxOffsetYScale = 1f;

    /// <summary>
    /// Harici sistemler (precise aim vb.) tarafından her kare veya hedef değişince ayarlanır; varsayılan 1.
    /// </summary>
    public void SetExternalMaxOffsetXScale(float scale)
    {
        _externalMaxOffsetXScale = Mathf.Max(0f, scale);
    }

    /// <summary>
    /// maxOffsetY (mouse dikey) ile çarpılır; varsayılan 1.
    /// </summary>
    public void SetExternalMaxOffsetYScale(float scale)
    {
        _externalMaxOffsetYScale = Mathf.Max(0f, scale);
    }

    void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player == null)
                return;
        }

        if (composer == null)
            return;

        Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;
        if (cam == null)
            return;

        if (!TryReadPointerScreenPosition(out Vector2 mouseScreen))
            return;

        // Viewport (0-1)
        Vector2 viewport = cam.ScreenToViewportPoint(mouseScreen);

        // Clamp → dışarı çıksa bile 0-1 arası kalır
        viewport.x = Mathf.Clamp01(viewport.x);
        viewport.y = Mathf.Clamp01(viewport.y);

        // -1 ile +1 arası merkez bazlı değer
        Vector2 centered = (viewport - new Vector2(0.5f, 0.5f)) * 2f;

        // Dead zone: centered magnitude en fazla ~sqrt(2) ≈ 1.41; 1'den büyük inspector değerleri eski hataydı
        float dz = deadZoneRadius > 1f ? 0.08f : Mathf.Max(0f, deadZoneRadius);
        dz = Mathf.Min(dz, 0.45f);
        if (centered.magnitude < dz)
            centered = Vector2.zero;

        float effMaxX = maxOffsetX * _externalMaxOffsetXScale;
        float effMaxY = maxOffsetY * _externalMaxOffsetYScale;
        Vector3 targetOffset = new Vector3(
            centered.x * effMaxX,
            centered.y * effMaxY * 0.5f,
            0f
        );

        float t = smooth > 0f ? 1f - Mathf.Exp(-smooth * Time.deltaTime) : 1f;
        composer.TargetOffset = Vector3.Lerp(composer.TargetOffset, targetOffset, t);
    }

    static bool TryReadPointerScreenPosition(out Vector2 screen)
    {
        if (Mouse.current != null)
        {
            screen = Mouse.current.position.ReadValue();
            return true;
        }

        if (Pointer.current != null)
        {
            screen = Pointer.current.position.ReadValue();
            return true;
        }

        screen = default;
        return false;
    }
}
