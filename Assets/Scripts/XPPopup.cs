using System.Collections;
using UnityEngine;
using TMPro;

// Displays a stacking XP gain popup. Fade in instantly on first gain,
// accumulate additional gains within the stack window, then fade out.
// Attach to the XP popup GameObject (should start inactive or at alpha 0).
public class XPPopup : MonoBehaviour
{
    [Tooltip("Text that displays the accumulated XP amount (e.g. '+200 XP').")]
    [SerializeField] private TMP_Text xpText;

    [Tooltip("Seconds after the last XP gain before the popup fades out.")]
    [SerializeField] private float stackWindow = 1.5f;

    [Tooltip("Seconds for the fade-out animation.")]
    [SerializeField] private float fadeDuration = 0.4f;

    [Tooltip("CanvasGroup used for fade. If null, the component tries to find one on this GameObject.")]
    [SerializeField] private CanvasGroup canvasGroup;

    private int       _accumulatedXP;
    private float     _stackTimer;
    private bool      _active;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void Start()
    {
        if (RunLevelManager.instance != null)
            RunLevelManager.instance.OnXPGained += HandleXPGained;
        else
            Debug.LogWarning("XPPopup: RunLevelManager.instance not found on Start.");
    }

    private void OnDestroy()
    {
        if (RunLevelManager.instance != null)
            RunLevelManager.instance.OnXPGained -= HandleXPGained;
    }

    private void Update()
    {
        if (!_active) return;

        _stackTimer -= Time.deltaTime;

        if (_stackTimer <= 0f)
        {
            _active = false;
            StartFadeOut();
        }
    }

    private void HandleXPGained(int amount)
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        _accumulatedXP += amount;
        _stackTimer     = stackWindow;
        _active         = true;

        // Snap visible instantly.
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        UpdateText();
    }

    private void UpdateText()
    {
        if (xpText != null)
            xpText.text = "+" + _accumulatedXP + " XP";
    }

    private void StartFadeOut()
    {
        _fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        _accumulatedXP    = 0;
        _fadeRoutine      = null;
    }
}
