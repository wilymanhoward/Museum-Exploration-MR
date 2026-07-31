using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Sequences the interactive gesture tutorial that plays right after the player
/// leaves the main menu (presses MULAI / Start).
///
/// Setup: drop this component on an empty, always-active GameObject in the scene
/// (e.g. "TutorialSystem"). Everything else - instruction panel, practice objects,
/// ghost-hand gizmo, audio - is built at runtime, following the same pattern as
/// GameListMenu / MainMenu's runtime overlays.
///
/// Steps: add TutorialStep components (PinchClickStep, PinchDragRotateStep) to
/// this same GameObject to customise their text/thresholds in the Inspector, or
/// leave it bare and the default two steps are added automatically.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    private const string CompletedPrefsKey = "TutorialCompleted";

    [Header("Trigger")]
    [Tooltip("Automatically start the tutorial the moment MainMenu.IsExplorationStarted becomes true.")]
    public bool autoStartAfterMainMenu = true;

    [Tooltip("If true, the tutorial is skipped for a headset that has already completed it once (PlayerPrefs). Leave OFF for museum kiosks where every visitor should see it.")]
    public bool showOnlyOnce = false;

    [Tooltip("Delay after leaving the main menu before the tutorial appears, so the passthrough fade settles first.")]
    public float startDelaySeconds = 1.0f;

    [Header("Placement")]
    [Tooltip("Distance in front of the player's eyes for the instruction panel, in meters.")]
    public float panelDistance = 1.1f;
    [Tooltip("Vertical offset of the panel relative to eye level, in meters.")]
    public float panelHeightOffset = 0.12f;
    [Tooltip("Distance in front of the player for the practice objects, in meters.")]
    public float practiceDistance = 0.85f;
    [Tooltip("Vertical offset of practice objects relative to eye level, in meters (negative = below).")]
    public float practiceHeightOffset = -0.22f;

    [Header("Visual Gizmo")]
    [Tooltip("Optional: an artist-made animated hand prefab (with an Animator) used instead of the built-in procedural ghost hand. See TutorialGestureGizmo for the expected Animator triggers.")]
    public GameObject customHandGizmoPrefab;

    [Header("Panel Copy")]
    public string panelHeader = "Tutorial";
    [TextArea] public string praiseText = "Bagus! / Well done!";
    [TextArea] public string completionText = "Tutorial selesai! Selamat meneroka muzium.\nTutorial complete - enjoy exploring the museum!";

    [Header("Events")]
    public UnityEvent onTutorialStarted = new UnityEvent();
    public UnityEvent onTutorialCompleted = new UnityEvent();

    public TutorialAudioFeedback Audio { get; private set; }
    public bool IsTutorialRunning { get; private set; }

    private readonly List<TutorialStep> steps = new List<TutorialStep>();
    private int currentStepIndex = -1;
    private bool hasStarted = false;

    // Runtime-built UI
    private GameObject panelRoot;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI bodyLabel;
    private TextMeshProUGUI progressLabel;
    private TextMeshProUGUI stepCounterLabel;
    private Image progressFill;
    private GameObject progressBarRoot;
    private TutorialGestureGizmo gizmo;
    private Camera cachedCamera;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        Audio = GetComponent<TutorialAudioFeedback>();
        if (Audio == null) Audio = gameObject.AddComponent<TutorialAudioFeedback>();
    }

    private void Update()
    {
        // Zero-touch trigger: MainMenu.ProceedStartExploration() flips this static flag
        // when the player leaves the menu, so no edit to MainMenu is required.
        if (autoStartAfterMainMenu && !hasStarted && MainMenu.IsExplorationStarted)
        {
            if (showOnlyOnce && PlayerPrefs.GetInt(CompletedPrefsKey, 0) == 1)
            {
                hasStarted = true; // permanently skip this session
                return;
            }
            hasStarted = true;
            StartCoroutine(StartAfterDelay(startDelaySeconds));
        }

        if (IsTutorialRunning)
        {
            FacePanelTowardPlayer();
            UpdateProgressUI();
        }
    }

    private IEnumerator StartAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartTutorial();
    }

    /// <summary>Starts (or restarts) the tutorial from step one. Safe to call from a menu/debug button.</summary>
    public void StartTutorial()
    {
        if (IsTutorialRunning) return;
        hasStarted = true;
        IsTutorialRunning = true;

        CollectSteps();
        if (steps.Count == 0)
        {
            Debug.LogWarning("[Tutorial] No tutorial steps found or created - aborting.");
            IsTutorialRunning = false;
            return;
        }

        BuildPanel();
        PositionPanelInFrontOfPlayer();
        EnsureGizmo();

        onTutorialStarted.Invoke();
        currentStepIndex = -1;
        AdvanceToNextStep();
        Debug.Log($"[Tutorial] Started with {steps.Count} steps.");
    }

    /// <summary>Aborts the tutorial immediately (e.g. if the player scans a QR mid-tutorial).</summary>
    public void AbortTutorial()
    {
        if (!IsTutorialRunning) return;
        if (currentStepIndex >= 0 && currentStepIndex < steps.Count)
        {
            steps[currentStepIndex].End();
        }
        Cleanup();
    }

    private void CollectSteps()
    {
        steps.Clear();

        // Prefer steps authored in the Inspector on this GameObject (order = component order).
        GetComponents(steps);

        // Nothing authored: build the default two-step sequence.
        if (steps.Count == 0)
        {
            steps.Add(gameObject.AddComponent<PinchClickStep>());
            steps.Add(gameObject.AddComponent<PinchDragRotateStep>());
        }

        foreach (TutorialStep step in steps)
        {
            step.Completed -= OnStepCompleted;
            step.Completed += OnStepCompleted;
        }
    }

    private void AdvanceToNextStep()
    {
        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            StartCoroutine(FinishSequence());
            return;
        }

        TutorialStep step = steps[currentStepIndex];
        PositionPanelInFrontOfPlayer();

        if (titleLabel != null) titleLabel.text = step.stepTitle;
        if (bodyLabel != null) bodyLabel.text = step.instructionText;
        if (stepCounterLabel != null) stepCounterLabel.text = $"Langkah {currentStepIndex + 1} / {steps.Count}";

        if (gizmo != null)
        {
            Vector3 gizmoPos;
            Quaternion gizmoRot;
            GetPlacementPose(practiceDistance, practiceHeightOffset, out gizmoPos, out gizmoRot);
            // Ghost hand demonstrates just beside where the practice object spawns.
            gizmoPos += gizmoRot * new Vector3(0.28f, 0.02f, 0f);
            gizmo.transform.SetPositionAndRotation(gizmoPos, gizmoRot);
            gizmo.gameObject.SetActive(true);
            gizmo.SetMode(step.GizmoMode);
        }

        step.Begin(this);
        Debug.Log($"[Tutorial] Step {currentStepIndex + 1}/{steps.Count} started: {step.stepTitle}");
    }

    private void OnStepCompleted(TutorialStep step)
    {
        if (!IsTutorialRunning) return;
        StartCoroutine(CelebrateThenAdvance(step));
    }

    private IEnumerator CelebrateThenAdvance(TutorialStep step)
    {
        step.End();
        if (gizmo != null) gizmo.gameObject.SetActive(false);

        if (Audio != null) Audio.PlayStepComplete(panelRoot != null ? panelRoot.transform.position : transform.position);
        if (bodyLabel != null) bodyLabel.text = praiseText;
        if (progressLabel != null) progressLabel.text = "";
        if (progressBarRoot != null) progressBarRoot.SetActive(false);

        yield return new WaitForSeconds(1.6f);
        AdvanceToNextStep();
    }

    private IEnumerator FinishSequence()
    {
        if (titleLabel != null) titleLabel.text = panelHeader;
        if (bodyLabel != null) bodyLabel.text = completionText;
        if (stepCounterLabel != null) stepCounterLabel.text = "";
        if (Audio != null) Audio.PlayTutorialComplete(panelRoot != null ? panelRoot.transform.position : transform.position);

        PlayerPrefs.SetInt(CompletedPrefsKey, 1);
        PlayerPrefs.Save();
        onTutorialCompleted.Invoke();

        yield return new WaitForSeconds(3.5f);
        Cleanup();
        Debug.Log("[Tutorial] Completed.");
    }

    private void Cleanup()
    {
        StopAllCoroutines();
        IsTutorialRunning = false;
        currentStepIndex = -1;
        if (panelRoot != null) Destroy(panelRoot);
        if (gizmo != null) Destroy(gizmo.gameObject);
        panelRoot = null;
        gizmo = null;
    }

    #region Placement helpers

    private Transform ResolveCameraTransform()
    {
        if (Camera.main != null) return Camera.main.transform;
        if (cachedCamera == null) cachedCamera = FindObjectOfType<Camera>();
        return cachedCamera != null ? cachedCamera.transform : null;
    }

    /// <summary>
    /// World pose at 'distance' meters in front of the player's eyes (yaw only, so it
    /// never tilts with head pitch), offset vertically by 'heightOffset'.
    /// </summary>
    public void GetPlacementPose(float distance, float heightOffset, out Vector3 position, out Quaternion rotation)
    {
        Transform cam = ResolveCameraTransform();
        if (cam == null)
        {
            position = transform.position + Vector3.forward * distance;
            rotation = Quaternion.identity;
            return;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        position = cam.position + flatForward * distance + Vector3.up * heightOffset;
        rotation = Quaternion.LookRotation(flatForward, Vector3.up);
    }

    private void PositionPanelInFrontOfPlayer()
    {
        if (panelRoot == null) return;
        Vector3 pos;
        Quaternion rot;
        GetPlacementPose(panelDistance, panelHeightOffset, out pos, out rot);
        panelRoot.transform.SetPositionAndRotation(pos, rot);
    }

    private void FacePanelTowardPlayer()
    {
        // Same behaviour as MainMenu: position stays fixed, rotation tracks the player.
        if (panelRoot == null) return;
        Transform cam = ResolveCameraTransform();
        if (cam == null) return;

        Vector3 toPlayer = cam.position - panelRoot.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.0001f)
        {
            panelRoot.transform.rotation = Quaternion.LookRotation(-toPlayer, Vector3.up);
        }
    }

    #endregion

    #region Runtime UI construction

    private void EnsureGizmo()
    {
        if (gizmo != null) return;
        gizmo = TutorialGestureGizmo.Create(customHandGizmoPrefab);
    }

    private void BuildPanel()
    {
        if (panelRoot != null) return;

        panelRoot = new GameObject("TutorialPanel");
        Canvas canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;

        RectTransform canvasRect = panelRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(560f, 340f);
        panelRoot.transform.localScale = Vector3.one * 0.001f; // project standard: 1px = 1mm

        // Background card
        Image bg = CreateChildImage(panelRoot.transform, "Background",
            new Color(0.07f, 0.09f, 0.13f, 0.88f));
        Stretch(bg.rectTransform, Vector2.zero, Vector2.zero);

        // Accent strip along the top
        Image accent = CreateChildImage(panelRoot.transform, "AccentStrip",
            new Color(0.35f, 0.78f, 0.95f, 1f));
        accent.rectTransform.anchorMin = new Vector2(0f, 1f);
        accent.rectTransform.anchorMax = new Vector2(1f, 1f);
        accent.rectTransform.pivot = new Vector2(0.5f, 1f);
        accent.rectTransform.anchoredPosition = Vector2.zero;
        accent.rectTransform.sizeDelta = new Vector2(0f, 8f);

        titleLabel = CreateChildLabel(panelRoot.transform, "Title", 34f, FontStyles.Bold,
            new Color(0.92f, 0.96f, 1f, 1f), TextAlignmentOptions.Center);
        Place(titleLabel.rectTransform, new Vector2(0.03f, 0.76f), new Vector2(0.97f, 0.96f));

        bodyLabel = CreateChildLabel(panelRoot.transform, "Body", 24f, FontStyles.Normal,
            new Color(0.88f, 0.9f, 0.94f, 1f), TextAlignmentOptions.Top);
        Place(bodyLabel.rectTransform, new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.74f));

        progressLabel = CreateChildLabel(panelRoot.transform, "ProgressLabel", 24f, FontStyles.Bold,
            new Color(0.55f, 0.9f, 1f, 1f), TextAlignmentOptions.Center);
        Place(progressLabel.rectTransform, new Vector2(0.2f, 0.13f), new Vector2(0.8f, 0.24f));

        // Progress fill bar
        progressBarRoot = new GameObject("ProgressBar");
        progressBarRoot.transform.SetParent(panelRoot.transform, false);
        RectTransform barRect = progressBarRoot.AddComponent<RectTransform>();
        Place(barRect, new Vector2(0.2f, 0.075f), new Vector2(0.8f, 0.115f));

        Image barBg = progressBarRoot.AddComponent<Image>();
        barBg.color = new Color(1f, 1f, 1f, 0.12f);
        barBg.raycastTarget = false;

        progressFill = CreateChildImage(progressBarRoot.transform, "Fill",
            new Color(0.35f, 0.85f, 0.55f, 1f));
        progressFill.rectTransform.anchorMin = Vector2.zero;
        progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        progressFill.rectTransform.offsetMin = Vector2.zero;
        progressFill.rectTransform.offsetMax = Vector2.zero;

        stepCounterLabel = CreateChildLabel(panelRoot.transform, "StepCounter", 18f, FontStyles.Normal,
            new Color(0.7f, 0.75f, 0.82f, 1f), TextAlignmentOptions.Center);
        Place(stepCounterLabel.rectTransform, new Vector2(0.3f, 0.0f), new Vector2(0.7f, 0.07f));
    }

    private void UpdateProgressUI()
    {
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count) return;
        TutorialStep step = steps[currentStepIndex];
        if (!step.IsRunning) return;

        if (progressLabel != null)
        {
            string label = step.GetProgressLabel();
            progressLabel.text = string.IsNullOrEmpty(label) ? "" : label;
        }

        float normalized = step.GetProgressNormalized();
        bool showBar = normalized >= 0f;
        if (progressBarRoot != null && progressBarRoot.activeSelf != showBar)
        {
            progressBarRoot.SetActive(showBar);
        }
        if (showBar && progressFill != null)
        {
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
        }
    }

    private static Image CreateChildImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI CreateChildLabel(Transform parent, string name, float size,
        FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        return label;
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    #endregion
}
