using System.Collections;
using TMPro;
using UnityEngine;

// Attach to the damage number Text prefab.
// Spawned by EnemyHealthController; floats up and fades out then self-destructs.
[RequireComponent(typeof(TMP_Text))]
public class DamagePopup : MonoBehaviour
{
    [Tooltip("Units per second the popup drifts upward (RectTransform space).")]
    [SerializeField] private float moveSpeed = 40f;

    [Tooltip("Seconds the popup floats at full opacity before fading.")]
    [SerializeField] private float holdDuration = 0.5f;

    [Tooltip("Seconds the fade-out takes after the hold phase.")]
    [SerializeField] private float fadeDuration = 0.4f;

    private TMP_Text _text;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    [Tooltip("Color used for critical hit numbers.")]
    [SerializeField] private Color critColor = Color.red;

    // Called by the spawner immediately after instantiation.
    public void Init(int damage, bool isCrit = false)
    {
        _text.text  = damage.ToString();
        if (isCrit) _text.color = critColor;
        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        RectTransform rt    = GetComponent<RectTransform>();
        Color         start = _text.color;

        // Phase 1: float up at full opacity
        float elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.deltaTime;
            rt.anchoredPosition += Vector2.up * moveSpeed * Time.deltaTime;
            yield return null;
        }

        // Phase 2: continue floating up while fading out
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            rt.anchoredPosition += Vector2.up * moveSpeed * Time.deltaTime;
            _text.color = new Color(start.r, start.g, start.b, 1f - Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        Destroy(gameObject);
    }
}
