using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Controls the upgrade selection canvas: fade in, time-pause, slot population, fade out.
// Attach to the root of the upgrade canvas GameObject.
public class UpgradeCanvasController : MonoBehaviour
{
    public static UpgradeCanvasController instance;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInDuration  = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.15f;

    [SerializeField] private UpgradeButton[] slots;   // exactly 3
    [SerializeField] private Button rerollButton;
    [SerializeField] private Button skipButton;

    private Coroutine _fadeRoutine;

    public bool IsOpen => gameObject.activeInHierarchy;

    private void Awake()
    {
        instance = this;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        // Do NOT call SetActive(false) here — the canvas starts inactive in the scene hierarchy.
        // Calling SetActive(false) in Awake would re-trigger Awake on the next SetActive(true),
        // causing StartCoroutine to fail because the object briefly becomes inactive again.
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void Show(List<UpgradeManager.UpgradeOffer> offers, bool rerollAvailable)
    {
        // Populate slots
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            if (i < offers.Count)
            {
                int captured = i; // capture for lambda
                slots[i].gameObject.SetActive(true);
                slots[i].Populate(offers[i].definition, offers[i].tier,
                    () => UpgradeManager.instance?.OnSelectUpgrade(captured));
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }

        if (rerollButton != null) rerollButton.interactable = rerollAvailable;
        if (skipButton   != null) skipButton.interactable   = true;

        gameObject.SetActive(true);
        Time.timeScale = 0f;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(0f, 1f, fadeInDuration));
    }

    public void Hide(Action onComplete = null)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutAndDeactivate(onComplete));
    }

    public void SetRerollInteractable(bool on)
    {
        if (rerollButton != null)
            rerollButton.interactable = on;
    }

    private IEnumerator FadeOutAndDeactivate(Action onComplete = null)
    {
        yield return FadeRoutine(canvasGroup != null ? canvasGroup.alpha : 1f, 0f, fadeOutDuration);
        Time.timeScale   = 1f;
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Confined;
        gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    // Uses unscaled time so the fade works while timeScale == 0.
    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }
}
