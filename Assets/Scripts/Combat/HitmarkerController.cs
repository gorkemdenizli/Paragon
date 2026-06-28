using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Attach to the hitmarker root GameObject.
// The root must have a CanvasGroup component (add one in the Inspector).
// All 4 line Images should be direct children of the same root.
public class HitmarkerController : MonoBehaviour
{
    public static HitmarkerController instance;

    [Tooltip("CanvasGroup on the hitmarker root — controls visibility.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("How long the hitmarker stays fully visible (seconds).")]
    [SerializeField] private float displayDuration = 0.12f;

    [Tooltip("How long it takes to fade out after displayDuration.")]
    [SerializeField] private float fadeDuration = 0.08f;

    [SerializeField] private Color hitColor  = Color.white;
    [SerializeField] private Color killColor = Color.red;

    private Image[] _lines;
    private Coroutine _hideRoutine;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        _lines = GetComponentsInChildren<Image>(true);

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    // Call this whenever a bullet hits an enemy. killed = true turns the marker red.
    public void ShowHit(bool killed)
    {
        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);

        Color c = killed ? killColor : hitColor;
        foreach (var img in _lines)
            img.color = c;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        _hideRoutine = StartCoroutine(HideRoutine());
    }

    IEnumerator HideRoutine()
    {
        yield return new WaitForSecondsRealtime(displayDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        _hideRoutine = null;
    }
}
