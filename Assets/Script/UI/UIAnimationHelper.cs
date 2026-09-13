using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight, high-performance UI animation helper for VR/MR.
/// Provides smooth fade-in and scale-pop transitions on screen changes and
/// seamless crossfade dissolves on theme swaps.
/// Uses native CanvasGroup and unscaled delta time (72/90/120 FPS VR safe).
/// </summary>
public class UIAnimationHelper : MonoBehaviour
{
    private static UIAnimationHelper instance;
    public static UIAnimationHelper Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("UIAnimationHelper");
                instance = go.AddComponent<UIAnimationHelper>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private readonly Dictionary<GameObject, Coroutine> runningAnimations = new Dictionary<GameObject, Coroutine>();
    private readonly Dictionary<GameObject, Vector3> cachedBaseScales = new Dictionary<GameObject, Vector3>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Smoothly fades in a panel with an optional subtle scale pop (e.g. 0.93x -> 1.0x).
    /// </summary>
    public static void FadeIn(GameObject panel, float duration = 0.20f, bool withScale = true, Action onComplete = null)
    {
        if (panel == null) return;
        Instance.StartFadeIn(panel, duration, withScale, onComplete);
    }

    /// <summary>
    /// Smoothly fades out a panel before setting it inactive.
    /// </summary>
    public static void FadeOut(GameObject panel, float duration = 0.15f, Action onComplete = null)
    {
        if (panel == null || !panel.activeSelf) return;
        Instance.StartFadeOut(panel, duration, onComplete);
    }

    /// <summary>
    /// Smooth crossfade for theme transitions across given active panels.
    /// Dips alpha to 0.15, invokes onMidpoint to swap materials/colors/sprites,
    /// then blooms back up to 1.0.
    /// </summary>
    public static void CrossfadeTheme(IEnumerable<GameObject> panels, Action onMidpoint, float halfDuration = 0.12f)
    {
        Instance.StartCrossfadeTheme(panels, onMidpoint, halfDuration);
    }

    /// <summary>
    /// Animates a 180-degree spin and punch scale on a theme toggle icon.
    /// </summary>
    public static void AnimateThemeIcon(Transform iconTransform, float duration = 0.28f)
    {
        if (iconTransform == null) return;
        Instance.StartCoroutine(Instance.AnimateIconRoutine(iconTransform, duration));
    }

    private void StartFadeIn(GameObject panel, float duration, bool withScale, Action onComplete)
    {
        StopRunningAnimation(panel);

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        if (!cachedBaseScales.TryGetValue(panel, out Vector3 baseScale))
        {
            baseScale = panel.transform.localScale;
            if (baseScale == Vector3.zero) baseScale = Vector3.one;
            cachedBaseScales[panel] = baseScale;
        }

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        if (withScale)
        {
            panel.transform.localScale = baseScale * 0.93f;
        }

        panel.SetActive(true);

        Coroutine c = StartCoroutine(FadeInRoutine(panel, cg, baseScale, duration, withScale, onComplete));
        runningAnimations[panel] = c;
    }

    private IEnumerator FadeInRoutine(GameObject panel, CanvasGroup cg, Vector3 baseScale, float duration, bool withScale, Action onComplete)
    {
        float elapsed = 0f;
        Vector3 startScale = baseScale * 0.93f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / duration);

            // Cubic ease-out: 1 - (1 - p)^3
            float ease = 1f - Mathf.Pow(1f - p, 3f);

            if (cg != null) cg.alpha = ease;
            if (withScale && panel != null)
            {
                panel.transform.localScale = Vector3.LerpUnclamped(startScale, baseScale, ease);
            }

            yield return null;
        }

        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        if (withScale && panel != null)
        {
            panel.transform.localScale = baseScale;
        }

        if (panel != null && runningAnimations.ContainsKey(panel))
        {
            runningAnimations.Remove(panel);
        }

        onComplete?.Invoke();
    }

    private void StartFadeOut(GameObject panel, float duration, Action onComplete)
    {
        StopRunningAnimation(panel);

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        cg.interactable = false;
        cg.blocksRaycasts = false;

        Coroutine c = StartCoroutine(FadeOutRoutine(panel, cg, duration, onComplete));
        runningAnimations[panel] = c;
    }

    private IEnumerator FadeOutRoutine(GameObject panel, CanvasGroup cg, float duration, Action onComplete)
    {
        float elapsed = 0f;
        float startAlpha = cg != null ? cg.alpha : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            float ease = p * p; // Quadratic ease-in for quick fade-out

            if (cg != null)
            {
                cg.alpha = Mathf.Lerp(startAlpha, 0f, ease);
            }

            yield return null;
        }

        if (cg != null)
        {
            cg.alpha = 1f; // Reset for subsequent displays
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        if (panel != null)
        {
            panel.SetActive(false);
            if (runningAnimations.ContainsKey(panel))
            {
                runningAnimations.Remove(panel);
            }
        }

        onComplete?.Invoke();
    }

    private void StartCrossfadeTheme(IEnumerable<GameObject> panels, Action onMidpoint, float halfDuration)
    {
        StartCoroutine(CrossfadeThemeRoutine(panels, onMidpoint, halfDuration));
    }

    private IEnumerator CrossfadeThemeRoutine(IEnumerable<GameObject> panels, Action onMidpoint, float halfDuration)
    {
        List<CanvasGroup> groups = new List<CanvasGroup>();
        if (panels != null)
        {
            foreach (GameObject p in panels)
            {
                if (p == null || !p.activeInHierarchy) continue;
                CanvasGroup cg = p.GetComponent<CanvasGroup>();
                if (cg == null) cg = p.AddComponent<CanvasGroup>();
                groups.Add(cg);
            }
        }

        if (groups.Count == 0)
        {
            onMidpoint?.Invoke();
            yield break;
        }

        // Phase 1: Dissolve alpha from current down to 0.15f
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / halfDuration);
            float a = Mathf.Lerp(1f, 0.15f, p * p);

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null) groups[i].alpha = a;
            }
            yield return null;
        }

        // Midpoint: Swap materials, colors, sprites at low alpha
        onMidpoint?.Invoke();

        // Phase 2: Reveal alpha smoothly back to 1.0f
        elapsed = 0f;
        float revealDuration = halfDuration * 1.25f;
        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / revealDuration);
            float ease = 1f - Mathf.Pow(1f - p, 2f); // Smooth ease-out
            float a = Mathf.Lerp(0.15f, 1f, ease);

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null) groups[i].alpha = a;
            }
            yield return null;
        }

        // Finalize
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null) groups[i].alpha = 1f;
        }
    }

    private IEnumerator AnimateIconRoutine(Transform iconTransform, float duration)
    {
        if (iconTransform == null) yield break;

        Vector3 baseScale = iconTransform.localScale;
        Quaternion startRot = iconTransform.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, 180f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / duration);

            // Sine pulse for scale (1.0 -> 1.25 -> 1.0)
            float scaleMultiplier = 1f + Mathf.Sin(p * Mathf.PI) * 0.25f;
            iconTransform.localScale = baseScale * scaleMultiplier;

            // Smooth rotation
            float rotEase = Mathf.SmoothStep(0f, 1f, p);
            iconTransform.localRotation = Quaternion.Slerp(startRot, endRot, rotEase);

            yield return null;
        }

        iconTransform.localScale = baseScale;
        iconTransform.localRotation = endRot;
    }

    private void StopRunningAnimation(GameObject panel)
    {
        if (panel != null && runningAnimations.TryGetValue(panel, out Coroutine c))
        {
            if (c != null) StopCoroutine(c);
            runningAnimations.Remove(panel);
        }
    }
}
