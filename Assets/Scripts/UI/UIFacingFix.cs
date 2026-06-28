using UnityEngine;

// Attach to any World Space canvas that is a child of a sprite flipped via localScale.x.
// Counters the parent's X-scale flip each frame so the UI always faces the camera correctly.
public class UIFacingFix : MonoBehaviour
{
    private float _originalAbsScaleX;

    void Awake()
    {
        _originalAbsScaleX = Mathf.Abs(transform.localScale.x);
    }

    void LateUpdate()
    {
        Transform p = transform.parent;
        if (p == null) return;

        // When the parent (or any ancestor) has a negative world-space X scale, counter-flip
        // this object so its own world-space X stays positive (readable).
        float sign = p.lossyScale.x < 0f ? -1f : 1f;

        Vector3 ls = transform.localScale;
        ls.x = _originalAbsScaleX * sign;
        transform.localScale = ls;
    }
}
