using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

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
    [Tooltip("Optional override: an artist-made animated hand prefab (with an Animator). When empty, the gizmo clones the scene's real XR hand mesh (Right/Left Hand Interaction Visual) and animates its skeleton. See TutorialGestureGizmo.")]
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
    private Image progressBarBg;
    private Image accentStrip;
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
        panelRoot.AddComponent<TrackedDeviceGraphicRaycaster>(); // hand-ray clicks on the Skip button

        RectTransform canvasRect = panelRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(560f, 340f);
        panelRoot.transform.localScale = Vector3.one * 0.001f; // project standard: 1px = 1mm

        // Background card
        Image bg = CreateChildImage(panelRoot.transform, "Background",
            new Color(0.07f, 0.09f, 0.13f, 0.88f));
        Stretch(bg.rectTransform, Vector2.zero, Vector2.zero);

        // Accent strip along the top (hidden if the project's panel style is adopted below)
        accentStrip = CreateChildImage(panelRoot.transform, "AccentStrip",
            new Color(0.35f, 0.78f, 0.95f, 1f));
        accentStrip.rectTransform.anchorMin = new Vector2(0f, 1f);
        accentStrip.rectTransform.anchorMax = new Vector2(1f, 1f);
        accentStrip.rectTransform.pivot = new Vector2(0.5f, 1f);
        accentStrip.rectTransform.anchoredPosition = Vector2.zero;
        accentStrip.rectTransform.sizeDelta = new Vector2(0f, 8f);

        // All text blocks share the same left/right margin (8%) so their edges line up with
        // each other and sit safely inside the background card's rounded/bordered artwork.
        const float marginL = 0.08f, marginR = 0.92f;

        titleLabel = CreateChildLabel(panelRoot.transform, "Title", 28f, FontStyles.Bold,
            new Color(0.92f, 0.96f, 1f, 1f), TextAlignmentOptions.Center);
        titleLabel.enableAutoSizing = true;
        titleLabel.fontSizeMin = 20f;
        titleLabel.fontSizeMax = 28f;
        Place(titleLabel.rectTransform, new Vector2(marginL, 0.76f), new Vector2(marginR, 0.96f));

        bodyLabel = CreateChildLabel(panelRoot.transform, "Body", 19f, FontStyles.Normal,
            new Color(0.88f, 0.9f, 0.94f, 1f), TextAlignmentOptions.Top);
        bodyLabel.enableAutoSizing = true;
        bodyLabel.fontSizeMin = 14f;
        bodyLabel.fontSizeMax = 19f;
        Place(bodyLabel.rectTransform, new Vector2(marginL, 0.24f), new Vector2(marginR, 0.74f));

        progressLabel = CreateChildLabel(panelRoot.transform, "ProgressLabel", 19f, FontStyles.Bold,
            new Color(0.55f, 0.9f, 1f, 1f), TextAlignmentOptions.Center);
        progressLabel.enableAutoSizing = true;
        progressLabel.fontSizeMin = 15f;
        progressLabel.fontSizeMax = 19f;
        Place(progressLabel.rectTransform, new Vector2(marginL, 0.13f), new Vector2(marginR, 0.24f));

        // Progress fill bar
        progressBarRoot = new GameObject("ProgressBar");
        progressBarRoot.transform.SetParent(panelRoot.transform, false);
        RectTransform barRect = progressBarRoot.AddComponent<RectTransform>();
        Place(barRect, new Vector2(marginL, 0.075f), new Vector2(marginR, 0.115f));

        progressBarBg = progressBarRoot.AddComponent<Image>();
        progressBarBg.color = new Color(1f, 1f, 1f, 0.12f);
        progressBarBg.raycastTarget = false;

        progressFill = CreateChildImage(progressBarRoot.transform, "Fill",
            new Color(0.35f, 0.85f, 0.55f, 1f));
        progressFill.rectTransform.anchorMin = Vector2.zero;
        progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        progressFill.rectTransform.offsetMin = Vector2.zero;
        progressFill.rectTransform.offsetMax = Vector2.zero;

        stepCounterLabel = CreateChildLabel(panelRoot.transform, "StepCounter", 15f, FontStyles.Normal,
            new Color(0.7f, 0.75f, 0.82f, 1f), TextAlignmentOptions.Center);
        stepCounterLabel.enableAutoSizing = true;
        stepCounterLabel.fontSizeMin = 12f;
        stepCounterLabel.fontSizeMax = 15f;
        Place(stepCounterLabel.rectTransform, new Vector2(marginL, 0.0f), new Vector2(marginR, 0.07f));

        // Adopt the project's UI design language (RoomListPanel card sprite + fonts) and
        // give the panel a Skip button cloned from the same panel's CloseButton.
        GameObject styleTemplate = FindSceneObjectByName("RoomListPanel");
        if (styleTemplate != null && ApplyTemplateStyle(styleTemplate, bg))
        {
            accentStrip.gameObject.SetActive(false);
        }
        BuildSkipButton(styleTemplate);
    }

    /// <summary>
    /// Copies the wrist panels' visual identity onto the tutorial panel: the rounded
    /// card background sprite (with its tint and material) and the TMP fonts/colors
    /// used by the room list's title and rows.
    /// </summary>
    private bool ApplyTemplateStyle(GameObject template, Image targetBackground)
    {
        // Background: panel-root image, else a child that looks like a background.
        Image srcBg = template.GetComponent<Image>();
        if (srcBg == null)
        {
            foreach (Image img in template.GetComponentsInChildren<Image>(true))
            {
                string n = img.name.ToLower();
                if (n == "image" || n == "bg" || n.Contains("background") || n.Contains("panel"))
                {
                    srcBg = img;
                    break;
                }
            }
        }
        if (srcBg == null) srcBg = template.GetComponentInChildren<Image>(true);

        if (srcBg != null && srcBg.sprite != null)
        {
            targetBackground.sprite = srcBg.sprite;
            targetBackground.type = srcBg.type;
            targetBackground.color = srcBg.color;
            targetBackground.material = srcBg.material;
            targetBackground.pixelsPerUnitMultiplier = srcBg.pixelsPerUnitMultiplier;
        }

        // Typography: title styling from RoomTitleText, body styling from any other label.
        TextMeshProUGUI srcTitle = null;
        Transform titleT = FindDeepChild(template.transform, "RoomTitleText");
        if (titleT != null) srcTitle = titleT.GetComponent<TextMeshProUGUI>();

        TextMeshProUGUI[] allLabels = template.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (srcTitle == null && allLabels.Length > 0) srcTitle = allLabels[0];

        TextMeshProUGUI srcBody = null;
        foreach (TextMeshProUGUI tmp in allLabels)
        {
            if (tmp != srcTitle) { srcBody = tmp; break; }
        }
        if (srcBody == null) srcBody = srcTitle;

        if (srcTitle != null)
        {
            titleLabel.font = srcTitle.font;
            titleLabel.color = srcTitle.color;
            progressLabel.font = srcTitle.font;
            progressLabel.color = srcTitle.color;
        }
        if (srcBody != null)
        {
            bodyLabel.font = srcBody.font;
            bodyLabel.color = srcBody.color;
            stepCounterLabel.font = srcBody.font;
            Color dim = srcBody.color;
            dim.a *= 0.65f;
            stepCounterLabel.color = dim;
        }

        return (srcBg != null && srcBg.sprite != null) || srcTitle != null;
    }

    /// <summary>
    /// Adds a Skip button to the panel's top-right corner. It clones the RoomListPanel's
    /// CloseButton so it looks and behaves exactly like every other close button in the
    /// app; if no template exists, a simple button in the house style is built instead.
    /// </summary>
    private void BuildSkipButton(GameObject template)
    {
        Transform closeT = template != null ? FindDeepChild(template.transform, "CloseButton") : null;
        GameObject btn;

        if (closeT != null)
        {
            btn = Instantiate(closeT.gameObject, panelRoot.transform);
            btn.name = "SkipButton";

            // Replace serialized/persistent click events (which still point at the ORIGINAL
            // room list panel) with a clean event that skips the tutorial. The click sound
            // is safe: XRButtonSelection plays it directly in OnSelectEntered.
            Button b = btn.GetComponentInChildren<Button>(true);
            if (b != null)
            {
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(SkipTutorial);
            }
            XRButtonSelection xr = btn.GetComponentInChildren<XRButtonSelection>(true);
            if (xr != null)
            {
                xr.onClick = new UnityEvent();
                xr.onClick.AddListener(SkipTutorial);

                // Hand-ray select needs a collider; add one if the clone lacks it and
                // bounce enabled so the interactable re-registers with the new collider.
                if (btn.GetComponentInChildren<Collider>(true) == null)
                {
                    xr.enabled = false;
                    BoxCollider box = xr.gameObject.AddComponent<BoxCollider>();
                    RectTransform xrRect = xr.GetComponent<RectTransform>();
                    if (xrRect != null)
                    {
                        box.size = new Vector3(Mathf.Max(xrRect.rect.width, 40f), Mathf.Max(xrRect.rect.height, 40f), 10f);
                    }
                    xr.colliders.Add(box);
                    xr.enabled = true;
                }
            }
            btn.SetActive(true);
        }
        else
        {
            btn = new GameObject("SkipButton");
            btn.transform.SetParent(panelRoot.transform, false);
            RectTransform fr = btn.AddComponent<RectTransform>();
            fr.sizeDelta = new Vector2(56f, 56f);

            Image img = btn.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.93f, 0.8f); // XRButtonSelection default normalColor

            BoxCollider box = btn.AddComponent<BoxCollider>();
            box.size = new Vector3(56f, 56f, 10f);

            XRButtonSelection xr = btn.AddComponent<XRButtonSelection>();
            xr.buttonImage = img;
            xr.onClick.AddListener(SkipTutorial);

            TextMeshProUGUI x = CreateChildLabel(btn.transform, "X", 30f, FontStyles.Bold,
                new Color(0.15f, 0.17f, 0.22f, 1f), TextAlignmentOptions.Center);
            Place(x.rectTransform, Vector2.zero, Vector2.one);
            x.text = "X";
        }

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-46f, -44f);
        }
    }

    /// <summary>Skips the rest of the tutorial (wired to the panel's Skip/close button).</summary>
    public void SkipTutorial()
    {
        if (!IsTutorialRunning) return;
        Debug.Log("[Tutorial] Skipped by player.");
        AbortTutorial();
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }
        return null;
    }

    private static GameObject FindSceneObjectByName(string name)
    {
        GameObject fallback = null;
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || t.name != name || !t.gameObject.scene.IsValid()) continue;
            if (t.gameObject.activeInHierarchy) return t.gameObject;
            if (fallback == null) fallback = t.gameObject;
        }
        return fallback;
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
