using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
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

    [Header("Scene-Authored Panel")]
    [Tooltip("Optional: a TutorialPanel living in the scene hierarchy (Tools > Museum MR > Build Tutorial Panel In Scene builds one). When set - or when a GameObject named 'TutorialPanel' exists in the scene - its layout and sizes are used as-is and only the texts are driven at runtime. Children are matched by name: Title, Body, ProgressLabel, ProgressBar (with child Fill), StepCounter, SkipButton.")]
    public GameObject sceneAuthoredPanel;

    [Header("Panel Copy")]
    public string panelHeader = "Tutorial";
    [TextArea] public string praiseText = "Bagus! / Well done!";
    [TextArea] public string completionText = "Tutorial selesai! Selamat meneroka muzium.\nTutorial complete - enjoy exploring the museum!";

    [Header("Narration (optional voice-over)")]
    [Tooltip("Played once when the tutorial starts, before the first step - a general welcome plus how the hand-ray/pinch controls work. Leave empty to skip.")]
    public AudioClip introNarrationClip;
    [Tooltip("Extra pause after the intro narration finishes before the first step begins, in seconds.")]
    public float introNarrationGap = 0.5f;
    [Tooltip("Played once when the whole tutorial completes. Leave empty to skip.")]
    public AudioClip completionNarrationClip;

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
    private bool panelIsSceneAuthored;
    private GameObject skipNarrationButtonRoot;
    private bool skipNarrationRequested;
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

        // The authored panel is a design-time preview; keep it hidden until the tutorial runs.
        if (sceneAuthoredPanel != null) sceneAuthoredPanel.SetActive(false);

        ConfigureHandRayVisuals();
    }

    /// <summary>
    /// Visible length of the hand-ray line, in meters (~5 inches). This clamps ONLY the
    /// rendered line: XRI computes the render points after hit processing, so the raycast,
    /// hover, clicking and the HandRayReticle cursor dot all still work at full range -
    /// the player sees a short stub at the hand plus the dot on whatever they point at.
    /// </summary>
    private const float HandRayVisibleLength = 0.12f;

    /// <summary>
    /// App-wide hand-ray polish, styled after the Meta Quest home rays:
    ///  - The rendered line is PHYSICALLY clamped to a short stub near the hand via
    ///    overrideInteractorLineLength. XRI applies this clamp to the render points
    ///    after hit processing, so the raycast, hover, clicking and the reticle all
    ///    still reach panels at any distance. (Physical length is used because the
    ///    line material ignores gradient alpha, so fade-based approaches render as a
    ///    full-length beam.)
    ///  - A Quest-style cursor dot (HandRayReticle) at the exact point the ray hits a
    ///    panel/button/object, so the player always sees precisely where they point.
    ///    The line visual positions, orients and toggles the dot from the raycast hit,
    ///    independent of the rendered line length.
    ///  Teleport/gaze rays are left untouched - they need their full visuals.
    /// </summary>
    private static void ConfigureHandRayVisuals()
    {
        foreach (XRInteractorLineVisual line in FindObjectsOfType<XRInteractorLineVisual>(true))
        {
            string objName = line.gameObject.name.ToLower();
            if (objName.Contains("teleport") || objName.Contains("gaze")) continue;

            line.autoAdjustLineLength = false;
            line.overrideInteractorLineLength = true;
            line.lineLength = HandRayVisibleLength;

            if (line.reticle == null)
            {
                line.reticle = HandRayReticle.Create();
            }
        }
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
            UpdateSkipNarrationButtonVisibility();
        }
    }

    /// <summary>
    /// Shows the Skip button throughout the tutorial (intro narration and every step),
    /// so the player can jump ahead at any point - not just while the instructor happens
    /// to be talking. Hidden once the last step is done (currentStepIndex has advanced
    /// past the end), since there's nothing left to skip on the completion screen.
    /// </summary>
    private void UpdateSkipNarrationButtonVisibility()
    {
        if (skipNarrationButtonRoot == null) return;
        bool shouldShow = currentStepIndex < steps.Count;
        if (skipNarrationButtonRoot.activeSelf != shouldShow)
        {
            skipNarrationButtonRoot.SetActive(shouldShow);
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
        EnsureSkipNarrationButton();
        ConfigureHandRayVisuals(); // again here: hand rigs can activate after Awake
        HideWristWatch();

        onTutorialStarted.Invoke();
        currentStepIndex = -1;
        StartCoroutine(PlayIntroThenAdvance());
        Debug.Log($"[Tutorial] Started with {steps.Count} steps.");
    }

    /// <summary>
    /// Plays the welcome/controls narration (if assigned) and waits for it to finish
    /// before the first step begins, so the two never talk over each other. The wait is
    /// polled frame-by-frame (not a flat WaitForSeconds) so SkipNarration can cut it short.
    /// </summary>
    private IEnumerator PlayIntroThenAdvance()
    {
        skipNarrationRequested = false;

        if (introNarrationClip != null && Audio != null)
        {
            Audio.PlayNarration(introNarrationClip);

            float duration = introNarrationClip.length + introNarrationGap;
            float elapsed = 0f;
            while (elapsed < duration && !skipNarrationRequested)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        skipNarrationRequested = false;
        AdvanceToNextStep();
    }

    /// <summary>
    /// Wired to the panel's round Skip button (visible for the whole tutorial, not just
    /// while narrating). Always stops any playing narration line, and:
    ///  - During the intro wait (before step 1 has begun): unblocks that wait so step 1
    ///    appears immediately, same as before.
    ///  - During an active step (e.g. "Pinch to Click"): skips the step's gesture
    ///    requirement entirely and advances straight to the next one (e.g. "Pinch & Hold
    ///    to Rotate"), instead of only silencing that step's narration and leaving the
    ///    player stuck performing the same gesture. Goes through the step's normal
    ///    Completed event, so the usual praise-then-advance flow still plays.
    /// </summary>
    public void SkipNarration()
    {
        if (!IsTutorialRunning) return;
        if (Audio != null) Audio.StopNarration();

        // Still waiting on the intro narration - no step has started yet, just unblock it.
        if (currentStepIndex < 0)
        {
            skipNarrationRequested = true;
            return;
        }

        // Mid-step: force it to report completion immediately.
        if (currentStepIndex < steps.Count)
        {
            TutorialStep step = steps[currentStepIndex];
            if (step != null && step.IsRunning && !step.IsCompleted)
            {
                step.SkipStep();
            }
        }
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

        // A half-armed skip from the previous step must not carry into this one.
        skipArmed = false;
        if (skipDisarmCoroutine != null)
        {
            StopCoroutine(skipDisarmCoroutine);
            skipDisarmCoroutine = null;
        }

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

        // A step that fails to start must not strand the tutorial on a dead panel
        // (no practice object would ever report completion) - move on instead.
        try
        {
            step.Begin(this);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Tutorial] Step '{step.stepTitle}' failed to start - skipping it. {e}");
            AdvanceToNextStep();
            return;
        }

        if (Audio != null && step.narrationClip != null)
        {
            Audio.PlayNarration(step.narrationClip);
        }

        Debug.Log($"[Tutorial] Step {currentStepIndex + 1}/{steps.Count} started: {step.stepTitle}");
    }

    private void OnStepCompleted(TutorialStep step)
    {
        if (!IsTutorialRunning) return;
        StartCoroutine(CelebrateThenAdvance(step));
    }

    private IEnumerator CelebrateThenAdvance(TutorialStep step)
    {
        // Wait a frame so the select event that completed the step fully finishes before
        // its practice object is destroyed (tearing down an interactable mid-event can
        // leave the ray interactor in a broken state).
        yield return null;
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
        if (Audio != null && completionNarrationClip != null) Audio.PlayNarration(completionNarrationClip);

        PlayerPrefs.SetInt(CompletedPrefsKey, 1);
        PlayerPrefs.Save();
        onTutorialCompleted.Invoke();

        // Keep the completion panel up long enough for the narration to finish, if any.
        float displaySeconds = completionNarrationClip != null
            ? Mathf.Max(3.5f, completionNarrationClip.length + 0.5f)
            : 3.5f;
        yield return new WaitForSeconds(displaySeconds);
        Cleanup();
        Debug.Log("[Tutorial] Completed.");
    }

    private void Cleanup()
    {
        StopAllCoroutines();
        IsTutorialRunning = false;
        currentStepIndex = -1;
        skipNarrationRequested = false;
        if (Audio != null) Audio.StopNarration();
        ShowWristWatch();
        if (panelRoot != null)
        {
            // An authored panel belongs to the scene - hide it so it can be reused/edited.
            if (panelIsSceneAuthored)
            {
                panelRoot.SetActive(false);
                // skipNarrationButtonRoot is a child of the persisted authored panel -
                // keep the reference so the next run doesn't build a duplicate.
            }
            else
            {
                Destroy(panelRoot);
                skipNarrationButtonRoot = null; // destroyed along with panelRoot
            }
        }
        if (gizmo != null) Destroy(gizmo.gameObject);
        panelRoot = null;
        panelIsSceneAuthored = false;
        skipArmed = false;
        skipDisarmCoroutine = null;
        gizmo = null;
    }

    private void HideWristWatch()
    {
        if (WristWatch.Instance != null) WristWatch.Instance.forceHidden = true;
    }

    private void ShowWristWatch()
    {
        if (WristWatch.Instance != null)
        {
            WristWatch.Instance.forceHidden = false;
            WristWatch.Instance.EnsureWatchButtonVisible();
        }
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

    /// <summary>
    /// Builds the "Skip Narration" pill button once (idempotent: no-ops on repeat calls
    /// while a reference is already held) and parents it just below the panel, so it works
    /// the same whether the panel is scene-authored or code-built without requiring any
    /// manual wiring. UpdateSkipNarrationButtonVisibility toggles it on/off each frame based
    /// on whether the instructor is currently talking.
    /// </summary>
    private void EnsureSkipNarrationButton()
    {
        if (skipNarrationButtonRoot != null || panelRoot == null) return;

        // Cloned from IntroVideoPanel's own Skip button - a small ROUND button with a
        // forward-skip icon (matches "skip ahead", unlike an X which reads as "close").
        // That icon is deliberately kept HERE and NOT on the abort/close SkipButton below,
        // so the two buttons never look interchangeable: round skip-forward icon = skip
        // this narration line, square X = close the whole tutorial.
        Transform introSkip = FindIntroSkipButtonTemplate();

        GameObject btn;
        if (introSkip != null)
        {
            btn = Instantiate(introSkip.gameObject, panelRoot.transform);
            btn.name = "SkipNarrationButton";

            // Clear the cloned events (they point at the ORIGINAL intro video panel).
            Button b = btn.GetComponentInChildren<Button>(true);
            if (b != null)
            {
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(SkipNarration);
            }
            XRButtonSelection xr = btn.GetComponentInChildren<XRButtonSelection>(true);
            if (xr != null)
            {
                xr.onClick = new UnityEvent();
                xr.onClick.AddListener(SkipNarration);

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
        }
        else
        {
            // No template found: a plain round button with a skip-forward glyph.
            btn = new GameObject("SkipNarrationButton");
            btn.transform.SetParent(panelRoot.transform, false);
            btn.AddComponent<RectTransform>();

            Image img = btn.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.93f, 0.85f);

            BoxCollider box = btn.AddComponent<BoxCollider>();
            box.size = new Vector3(64f, 64f, 10f);

            XRButtonSelection xr = btn.AddComponent<XRButtonSelection>();
            xr.buttonImage = img;
            xr.onClick.AddListener(SkipNarration);

            TextMeshProUGUI label = CreateChildLabel(btn.transform, "Label", 22f, FontStyles.Bold,
                new Color(0.15f, 0.17f, 0.22f, 1f), TextAlignmentOptions.Center);
            label.text = "▶";
            Place(label.rectTransform, Vector2.zero, Vector2.one);
        }

        // Fixed spot just below the panel's bottom edge, centered - works regardless of
        // whatever layout the panel's own content uses, authored or code-built. Forced to a
        // small square/round footprint (not a wide pill) regardless of the source template.
        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -16f);
        rt.sizeDelta = new Vector2(64f, 64f);

        btn.SetActive(false); // UpdateSkipNarrationButtonVisibility shows it only while narrating
        skipNarrationButtonRoot = btn;
    }

    /// <summary>
    /// Binds to a panel authored in the scene hierarchy instead of building one from code,
    /// so designers can edit the layout, sizes and buttons in the Editor. Children are
    /// matched by name: Title, Body, ProgressLabel, ProgressBar (with child Fill),
    /// StepCounter, SkipButton. Returns false (caller builds the panel from code) when no
    /// authored panel exists or it lacks the essential labels.
    /// </summary>
    private bool TryBindAuthoredPanel()
    {
        GameObject authored = sceneAuthoredPanel != null ? sceneAuthoredPanel : FindSceneObjectByName("TutorialPanel");
        if (authored == null) return false;

        panelRoot = authored;
        panelIsSceneAuthored = true;

        if (panelRoot.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
        {
            panelRoot.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        // Make sure the panel's background actually catches the hand ray. An authored
        // panel built before this was enforced (or with the checkbox off by hand) would
        // otherwise have a hole in the middle: pointing at a button works, but pointing at
        // plain background or text registers no hit at all, so the cursor dot silently
        // never appears there. Panel-level root Image only, not every button's own
        // decorative art, so this can't steal clicks from anything already working.
        Image authoredBg = panelRoot.GetComponent<Image>();
        if (authoredBg == null)
        {
            Transform bgT = FindDeepChild(panelRoot.transform, "Background");
            authoredBg = bgT != null ? bgT.GetComponent<Image>() : null;
        }
        if (authoredBg != null) authoredBg.raycastTarget = true;

        titleLabel = FindAuthoredLabel("Title");
        bodyLabel = FindAuthoredLabel("Body");
        progressLabel = FindAuthoredLabel("ProgressLabel");
        stepCounterLabel = FindAuthoredLabel("StepCounter");

        // The authored preview text ("Tutorial") is much shorter than the real step titles
        // and instructions, so a label that looks fine in the Editor can overflow at runtime.
        // Treat the authored font size as the MAXIMUM and let long copy shrink to fit the
        // authored rect instead of spilling out of it.
        EnsureLabelFits(titleLabel);
        EnsureLabelFits(bodyLabel);
        EnsureLabelFits(progressLabel);
        EnsureLabelFits(stepCounterLabel);

        Transform bar = FindDeepChild(panelRoot.transform, "ProgressBar");
        progressBarRoot = bar != null ? bar.gameObject : null;
        progressBarBg = bar != null ? bar.GetComponent<Image>() : null;
        Transform fill = bar != null ? FindDeepChild(bar, "Fill") : null;
        progressFill = fill != null ? fill.GetComponent<Image>() : null;

        if (titleLabel == null || bodyLabel == null)
        {
            Debug.LogWarning("[Tutorial] Scene-authored TutorialPanel is missing a 'Title' or 'Body' label - building the panel from code instead.");
            panelRoot = null;
            panelIsSceneAuthored = false;
            return false;
        }

        // Wire the authored Skip button. Remove-then-add keeps this idempotent across
        // tutorial restarts, and leaves any designer-added persistent events untouched.
        Transform skip = FindDeepChild(panelRoot.transform, "SkipButton");
        if (skip != null)
        {
            // Force an X/close icon (from RoomListPanel's CloseButton) so this button always
            // reads as "close the tutorial" - kept visually distinct from the round
            // skip-forward icon on the separate Skip Narration button, so the two can't be
            // mistaken for each other.
            RestyleSkipButtonToCloseIcon(skip);

            Button b = skip.GetComponentInChildren<Button>(true);
            if (b != null)
            {
                b.onClick.RemoveListener(SkipTutorial);
                b.onClick.AddListener(SkipTutorial);
            }
            XRButtonSelection xr = skip.GetComponentInChildren<XRButtonSelection>(true);
            if (xr != null)
            {
                xr.onClick.RemoveListener(SkipTutorial);
                xr.onClick.AddListener(SkipTutorial);
            }
        }

        panelRoot.SetActive(true);
        Debug.Log("[Tutorial] Using the scene-authored TutorialPanel.");
        return true;
    }

    private TextMeshProUGUI FindAuthoredLabel(string childName)
    {
        Transform t = FindDeepChild(panelRoot.transform, childName);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    /// <summary>
    /// Keeps runtime text inside the authored rect: the size set in the Editor becomes the
    /// font ceiling, auto-sizing shrinks long copy down (to 45% at worst), wrapping is on,
    /// and anything still too long is ellipsed rather than overflowing the panel.
    /// </summary>
    private static void EnsureLabelFits(TextMeshProUGUI label)
    {
        if (label == null) return;
        float authoredSize = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
        if (authoredSize <= 0f) authoredSize = 19f;
        label.enableAutoSizing = true;
        label.fontSizeMax = authoredSize;
        label.fontSizeMin = authoredSize * 0.45f;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void BuildPanel()
    {
        if (panelRoot != null) return;
        if (TryBindAuthoredPanel()) return;
        panelIsSceneAuthored = false;

        panelRoot = new GameObject("TutorialPanel");
        Canvas canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;
        panelRoot.AddComponent<TrackedDeviceGraphicRaycaster>(); // hand-ray clicks on the Skip button

        RectTransform canvasRect = panelRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(560f, 340f);
        panelRoot.transform.localScale = Vector3.one * 0.001f; // project standard: 1px = 1mm

        // Background card. Raycastable (unlike every other decorative element built via
        // CreateChildImage) so the hand ray registers a hit - and the cursor dot appears -
        // anywhere over the panel, not only when pointing exactly at a button. Title/body
        // text stay non-raycastable, so the ray simply "sees through" them to this
        // background layer underneath, same as pointing at empty panel space.
        Image bg = CreateChildImage(panelRoot.transform, "Background",
            new Color(0.07f, 0.09f, 0.13f, 0.88f));
        bg.raycastTarget = true;
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
    /// Adds a Skip (close/abort) button to the panel's top-right corner. It clones the
    /// RoomListPanel's CloseButton so it looks and behaves exactly like every other close
    /// button in the app - a plain X, deliberately NOT the round skip-forward icon used by
    /// the separate Skip Narration button, so the two are never visually interchangeable.
    /// Falls back to a simple house-style X button if no template exists.
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

    private float lastSkipPressTime = -10f;
    private bool skipArmed;
    private Coroutine skipDisarmCoroutine;

    /// <summary>
    /// Skips the rest of the tutorial (wired to the panel's Skip/close button).
    /// Requires TWO deliberate presses: a stray pinch near the panel during gesture
    /// practice was silently closing the whole tutorial, which players experienced as
    /// "the next step never showed up". The first press shows a confirmation hint; a
    /// second press within a few seconds actually skips.
    /// </summary>
    public void SkipTutorial()
    {
        if (!IsTutorialRunning) return;

        // A single pinch can fire this twice (UI pointer-down + XR select); collapse
        // rapid repeats into one press so the confirmation can't self-confirm.
        if (Time.unscaledTime - lastSkipPressTime < 0.4f) return;
        lastSkipPressTime = Time.unscaledTime;

        if (!skipArmed)
        {
            skipArmed = true;
            if (stepCounterLabel != null)
            {
                stepCounterLabel.text = "Tekan sekali lagi untuk langkau / Press again to skip";
            }
            if (skipDisarmCoroutine != null) StopCoroutine(skipDisarmCoroutine);
            skipDisarmCoroutine = StartCoroutine(DisarmSkipAfterDelay(3f));
            return;
        }

        Debug.Log("[Tutorial] Skipped by player.");
        AbortTutorial();
    }

    private IEnumerator DisarmSkipAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        skipArmed = false;
        skipDisarmCoroutine = null;
        if (stepCounterLabel != null && currentStepIndex >= 0 && currentStepIndex < steps.Count)
        {
            stepCounterLabel.text = $"Langkah {currentStepIndex + 1} / {steps.Count}";
        }
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

    /// <summary>
    /// Finds the main menu's "SkipButton" specifically inside "IntoVideoPanel" - the button
    /// that skips the intro cinematic. Scoped to that panel (not a scene-wide name search)
    /// because the tutorial's own Skip button is also named "SkipButton" once built, and a
    /// global lookup could find that one instead of the real template.
    /// </summary>
    private static Transform FindIntroSkipButtonTemplate()
    {
        GameObject introPanel = FindSceneObjectByName("IntoVideoPanel");
        return introPanel != null ? FindDeepChild(introPanel.transform, "SkipButton") : null;
    }

    /// <summary>
    /// Copies the RoomListPanel CloseButton's visuals (background sprite/tint + X icon
    /// sprite) onto an existing "SkipButton" - used for a scene-authored panel, whose
    /// SkipButton may have picked up a different icon from an earlier authoring pass. Only
    /// the visuals are touched; the authored RectTransform (size/position) is left exactly
    /// as designed. Deliberately an X, not the round skip-forward icon used by the separate
    /// Skip Narration button, so the two can never be mistaken for each other.
    /// </summary>
    private static void RestyleSkipButtonToCloseIcon(Transform target)
    {
        GameObject roomListPanel = FindSceneObjectByName("RoomListPanel");
        Transform closeTemplate = roomListPanel != null ? FindDeepChild(roomListPanel.transform, "CloseButton") : null;
        if (closeTemplate == null || target == null) return;

        Image srcBg = closeTemplate.GetComponent<Image>();
        Image dstBg = target.GetComponent<Image>();
        if (srcBg != null && dstBg != null)
        {
            dstBg.sprite = srcBg.sprite;
            dstBg.type = srcBg.type;
            dstBg.color = srcBg.color;
            dstBg.material = srcBg.material;
            dstBg.pixelsPerUnitMultiplier = srcBg.pixelsPerUnitMultiplier;
        }

        Image srcIcon = null;
        foreach (Image img in closeTemplate.GetComponentsInChildren<Image>(true))
        {
            if (img.transform != closeTemplate) { srcIcon = img; break; }
        }
        Image dstIcon = null;
        foreach (Image img in target.GetComponentsInChildren<Image>(true))
        {
            if (img.transform != target) { dstIcon = img; break; }
        }
        if (srcIcon != null && dstIcon != null)
        {
            dstIcon.sprite = srcIcon.sprite;
            dstIcon.color = srcIcon.color;
            dstIcon.material = srcIcon.material;
        }
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
