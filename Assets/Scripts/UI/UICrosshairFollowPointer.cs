using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Crosshair panelini (altındaki reticle çizgileriyle birlikte) fare / pointer ekran konumuna taşır.
/// Bu bileşeni Canvas hiyerarşisindeki Crosshair kök <see cref="RectTransform"/> üzerine ekleyin.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UICrosshairFollowPointer : MonoBehaviour
{
    RectTransform _rect;
    Canvas _canvas;
    RectTransform _canvasRect;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
            _canvasRect = _canvas.transform as RectTransform;
    }

    void LateUpdate()
    {
        if (_canvas == null || _canvasRect == null)
            return;

        if (!TryReadPointerScreenPosition(out Vector2 screen))
            return;

        Camera eventCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screen,
                eventCam,
                out Vector2 local))
        {
            _rect.localPosition = local;
        }
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
