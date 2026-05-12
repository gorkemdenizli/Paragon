using System.Collections;
using UnityEngine;

// Attach to enemy root. Flashes all child SpriteRenderers to a tint color then fades back,
// and briefly shows a pixel-perfect outline via the Unlit/PixelOutline shader.
// Call Flash() from EnemyHealthController or BossHealthController on damage.
public class HitFlash : MonoBehaviour
{
    [Header("Flash")]
    [Tooltip("Tint color applied on hit.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("Seconds to stay at flash color before fading back.")]
    [SerializeField] private float holdDuration = 0.05f;

    [Tooltip("Seconds to lerp back to original color.")]
    [SerializeField] private float fadeDuration = 0.1f;

    [Header("Hit Outline")]
    [Tooltip("Enable the pixel-perfect outline that appears on hit.")]
    [SerializeField] private bool useOutline = true;

    [Tooltip("Color of the outline ring.")]
    [SerializeField] private Color outlineColor = Color.white;

    [Tooltip("Shader _Radius value. Outline spread ≈ sqrt(radius) pixels. Try 4–9 for a visible rim.")]
    [SerializeField] [Range(1f, 10f)] private float outlineRadius = 4f;

    private SpriteRenderer[] _renderers;
    private Color[]           _originalColors;
    private GameObject[]      _outlineObjects;
    private Material[]        _outlineMaterials;
    private Coroutine         _routine;

    static readonly int PropColor  = Shader.PropertyToID("_Color");
    static readonly int PropRadius = Shader.PropertyToID("_Radius");

    void Awake()
    {
        _renderers      = GetComponentsInChildren<SpriteRenderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originalColors[i] = _renderers[i].color;

        if (useOutline)
            BuildOutlineRenderers();
    }

    void BuildOutlineRenderers()
    {
        Shader outlineShader = Shader.Find("Unlit/PixelOutline");
        if (outlineShader == null)
        {
            Debug.LogWarning("[HitFlash] Unlit/PixelOutline shader not found. Outline disabled.", this);
            return;
        }

        _outlineObjects   = new GameObject[_renderers.Length];
        _outlineMaterials = new Material[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            SpriteRenderer sr = _renderers[i];

            var go = new GameObject("_HitOutline");
            go.transform.SetParent(sr.transform, false);

            var osr = go.AddComponent<SpriteRenderer>();
            osr.sprite         = sr.sprite;
            osr.sortingLayerID = sr.sortingLayerID;
            // Render one order behind so the original sprite covers the sprite part of
            // the outline shader — only the outer rim pixels remain visible.
            osr.sortingOrder = sr.sortingOrder - 1;

            var mat = new Material(outlineShader);
            mat.SetColor(PropColor,  new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0f));
            mat.SetFloat(PropRadius, outlineRadius);
            osr.material = mat;

            go.SetActive(false);
            _outlineObjects[i]   = go;
            _outlineMaterials[i] = mat;
        }
    }

    public void Flash()
    {
        if (_routine != null)
            StopCoroutine(_routine);
        SyncOutlineSprites();
        _routine = StartCoroutine(FlashRoutine());
    }

    // Syncs the current animation frame sprite so the outline always matches.
    void SyncOutlineSprites()
    {
        if (_outlineObjects == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_outlineObjects[i] == null) continue;
            _outlineObjects[i].GetComponent<SpriteRenderer>().sprite = _renderers[i].sprite;
        }
    }

    IEnumerator FlashRoutine()
    {
        SetAll(flashColor);
        SetOutlineAlpha(1f);

        yield return new WaitForSeconds(holdDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null)
                    _renderers[i].color = Color.Lerp(flashColor, _originalColors[i], t);

            SetOutlineAlpha(1f - t);
            yield return null;
        }

        RestoreAll();
        SetOutlineAlpha(0f);
        _routine = null;
    }

    void SetOutlineAlpha(float alpha)
    {
        if (_outlineMaterials == null) return;
        bool show = alpha > 0f;
        for (int i = 0; i < _outlineMaterials.Length; i++)
        {
            if (_outlineObjects[i] == null) continue;
            _outlineObjects[i].SetActive(show);
            if (show)
                _outlineMaterials[i].SetColor(PropColor,
                    new Color(outlineColor.r, outlineColor.g, outlineColor.b, alpha));
        }
    }

    void SetAll(Color c)
    {
        foreach (var sr in _renderers)
            if (sr != null) sr.color = c;
    }

    void RestoreAll()
    {
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null)
                _renderers[i].color = _originalColors[i];
    }
}
