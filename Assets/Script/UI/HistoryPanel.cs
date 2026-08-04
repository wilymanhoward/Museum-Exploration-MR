using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Controls the HistoryPanel UI for presenting historical information.
/// Implements 4-step video clip trigger interaction:
/// 1. Starts off hiding the video panel (Video container GameObject).
/// 2. Holding down on the videoTriggerArea (BoxCollider) for > 1.0 second hides the display image and reveals the video panel, playing the video clip.
/// 3. Upon finishing the video clip OR releasing the trigger, it immediately reverts back to the display image and hides the video panel.
/// 4. Accepts a BoxCollider directly for videoTriggerArea without adding or modifying any BoxCollider.
/// </summary>
public class HistoryPanel : MonoBehaviour
{
    public static HistoryPanel Instance { get; private set; }

    [Header("Header UI References")]
    [Tooltip("Main title text (e.g. 'Artefak' or 'Sejarah').")]
    public TMP_Text topTitleText;

    [Tooltip("Category subtitle text (e.g. 'Gamelan').")]
    public TMP_Text categoryText;

    [Header("Left Media Display UI References")]
    [Tooltip("Text label above image (e.g. 'Hold to Play Clip').")]
    public TMP_Text holdToPlayText;

    [Tooltip("Main static display image component (DisplayImage).")]
    public Image displayImage;

    [Tooltip("Container GameObject for video player and raw image (Video panel, starts hidden).")]
    public GameObject videoPanel;

    [Tooltip("RawImage used to render video clip when playing.")]
    public RawImage displayVideoRawImage;

    [Tooltip("VideoPlayer component that plays the 3-5 second clip.")]
    public VideoPlayer videoPlayer;

    [Tooltip("Trigger BoxCollider component on VideoTriggerArea.")]
    public BoxCollider videoTriggerArea;

    [Tooltip("Fallback text displayed when no image sprite is available.")]
    public GameObject noImageTextObj;

    [Tooltip("Event / Story title displayed below the media box (e.g. 'Pertarungan Megat Panji Alam').")]
    public TMP_Text eventTitleText;

    [Header("Right Card 1: Detail Sejarah UI References")]
    [Tooltip("Displays time period / year (e.g. '1879').")]
    public TMP_Text timePeriodText;

    [Tooltip("Displays location / origin (e.g. 'Trengganu').")]
    public TMP_Text locationText;

    [Header("Right Card 2: About / Description UI References")]
    [Tooltip("Body description paragraph text.")]
    public TMP_Text descriptionText;

    [Header("Control Buttons")]
    [Tooltip("Restart audio narration button.")]
    public Button restartButton;
    public XRButtonSelection restartButtonXR;

    [Tooltip("Play / Pause audio narration button.")]
    public Button playButton;
    public XRButtonSelection playButtonXR;
    public GameObject playIconObj;
    public GameObject pauseIconObj;

    [Tooltip("Back button to return to previous menu.")]
    public Button backButton;
    public XRButtonSelection backButtonXR;

    [Tooltip("Close button ('X') to hide the panel.")]
    public Button closeButton;
    public XRButtonSelection closeButtonXR;

    [Header("Active Data")]
    public HistoryData activeHistoryData;

    private AudioSource audioSource;
    private Action onCloseCallback;
    private Coroutine holdCheckCoroutine;
    private bool isHoldingTrigger = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        WireVideoTriggerArea();
        AutoWireButtons();
    }

    private void OnEnable()
    {
        AutoWireButtons();
        UpdatePlayPauseIcons();
        ResetMediaToImageState();
    }

    private void Update()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            UpdatePlayPauseIcons();
        }
    }

    /// <summary>
    /// Populate the HistoryPanel UI using the provided HistoryData ScriptableObject.
    /// </summary>
    public void Setup(HistoryData data, Action onClose = null)
    {
        activeHistoryData = data;
        onCloseCallback = onClose;

        if (data == null) return;

        // Header Title & Subtitle
        if (topTitleText != null) topTitleText.text = string.IsNullOrEmpty(data.topTitle) ? "Sejarah" : data.topTitle;
        if (categoryText != null) categoryText.text = string.IsNullOrEmpty(data.category) ? "" : data.category;

        // Event / Story Title
        if (eventTitleText != null) eventTitleText.text = string.IsNullOrEmpty(data.eventTitle) ? data.name : data.eventTitle;

        // Detail Sejarah (Time Period & Location)
        if (timePeriodText != null) timePeriodText.text = string.IsNullOrEmpty(data.timePeriod) ? "-" : data.timePeriod;
        if (locationText != null) locationText.text = string.IsNullOrEmpty(data.location) ? "-" : data.location;

        // Description
        if (descriptionText != null) descriptionText.text = string.IsNullOrEmpty(data.description) ? "" : data.description;

        // Narration Audio Setup
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = data.narrationClip;
        }

        // Video Player Setup
        WireVideoTriggerArea();
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = data.videoClip;
            videoPlayer.isLooping = false;
        }

        // Rule 1: Always start off hiding the video panel and revealing static image
        ResetMediaToImageState();
        UpdatePlayPauseIcons();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trigger Hold-To-Play Video Logic
    // ─────────────────────────────────────────────────────────────────────────

    public void OnPointerDownTrigger()
    {
        if (activeHistoryData == null || activeHistoryData.videoClip == null) return;

        isHoldingTrigger = true;
        if (holdCheckCoroutine != null) StopCoroutine(holdCheckCoroutine);
        holdCheckCoroutine = StartCoroutine(HoldTimerCoroutine());
    }

    public void OnPointerUpTrigger()
    {
        isHoldingTrigger = false;
        if (holdCheckCoroutine != null)
        {
            StopCoroutine(holdCheckCoroutine);
            holdCheckCoroutine = null;
        }

        // Rule 3: Immediately return to image when letting go of trigger
        ResetMediaToImageState();
    }

    // Rule 2: If held for more than 1 second, hide image and reveal video panel to play video
    private IEnumerator HoldTimerCoroutine()
    {
        yield return new WaitForSeconds(1.0f);

        if (isHoldingTrigger && activeHistoryData != null && activeHistoryData.videoClip != null)
        {
            PlayVideoClip();
        }
    }

    private void PlayVideoClip()
    {
        // Hide display image and reveal video panel
        if (displayImage != null) displayImage.gameObject.SetActive(false);
        if (noImageTextObj != null) noImageTextObj.SetActive(false);
        if (videoPanel != null) videoPanel.SetActive(true);

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = activeHistoryData.videoClip;
            videoPlayer.time = 0;
            videoPlayer.Play();
        }
    }

    // Rule 1 & Rule 3: Hide video panel and immediately reveal display image
    public void ResetMediaToImageState()
    {
        isHoldingTrigger = false;

        if (holdCheckCoroutine != null)
        {
            StopCoroutine(holdCheckCoroutine);
            holdCheckCoroutine = null;
        }

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        // Hide video panel
        if (videoPanel != null) videoPanel.SetActive(false);

        // Reveal static image
        if (displayImage != null)
        {
            bool hasSprite = activeHistoryData != null && activeHistoryData.displaySprite != null;
            displayImage.gameObject.SetActive(hasSprite);
            if (noImageTextObj != null) noImageTextObj.SetActive(!hasSprite);
        }
    }

    private void OnVideoLoopPointReached(VideoPlayer source)
    {
        // Rule 3: When video finishes, immediately return to image
        ResetMediaToImageState();
    }

    // Accepts BoxCollider directly for videoTriggerArea
    private void WireVideoTriggerArea()
    {
        GameObject targetArea = videoTriggerArea != null ? videoTriggerArea.gameObject : (displayImage != null ? displayImage.gameObject : null);
        if (targetArea != null)
        {
            HoldMediaClipTrigger trigger = targetArea.GetComponent<HoldMediaClipTrigger>();
            if (trigger == null) trigger = targetArea.AddComponent<HoldMediaClipTrigger>();
            trigger.Setup(this);

            Image triggerImg = targetArea.GetComponent<Image>();
            if (triggerImg != null) triggerImg.raycastTarget = true;
        }

        if (videoPlayer == null)
        {
            videoPlayer = GetComponentInChildren<VideoPlayer>(true);
        }

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            videoPlayer.loopPointReached += OnVideoLoopPointReached;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Audio Controls
    // ─────────────────────────────────────────────────────────────────────────

    public void TogglePlayAudio()
    {
        if (audioSource == null || audioSource.clip == null) return;

        if (audioSource.isPlaying)
        {
            audioSource.Pause();
        }
        else
        {
            audioSource.Play();
        }

        UpdatePlayPauseIcons();
    }

    public void RestartAudio()
    {
        if (audioSource == null || audioSource.clip == null) return;

        audioSource.Stop();
        audioSource.time = 0f;
        audioSource.Play();

        UpdatePlayPauseIcons();
    }

    private void UpdatePlayPauseIcons()
    {
        bool isPlaying = audioSource != null && audioSource.isPlaying;

        if (playIconObj != null) playIconObj.SetActive(!isPlaying);
        if (pauseIconObj != null) pauseIconObj.SetActive(isPlaying);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Panel Navigation & Positioning
    // ─────────────────────────────────────────────────────────────────────────

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        PositionInFrontOfPlayer();
    }

    public void ClosePanel()
    {
        ResetMediaToImageState();

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        gameObject.SetActive(false);
        onCloseCallback?.Invoke();
    }

    public void OnBackButtonPressed()
    {
        ClosePanel();
    }

    public void PositionInFrontOfPlayer()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        if (forward == Vector3.zero) forward = Vector3.forward;

        transform.position = cam.position + forward * 0.7f - Vector3.up * 0.05f;
        Vector3 toPlayer = cam.position - transform.position;
        toPlayer.y = 0;
        if (toPlayer.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(-toPlayer, Vector3.up);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button Auto-Wiring
    // ─────────────────────────────────────────────────────────────────────────

    private void AutoWireButtons()
    {
        WireButton(restartButton, restartButtonXR, RestartAudio);
        WireButton(playButton, playButtonXR, TogglePlayAudio);
        WireButton(backButton, backButtonXR, OnBackButtonPressed);
        WireButton(closeButton, closeButtonXR, ClosePanel);
    }

    private static void WireButton(Button btn, XRButtonSelection xr, UnityEngine.Events.UnityAction action)
    {
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }
        if (xr != null)
        {
            xr.onClick.RemoveAllListeners();
            xr.onClick.AddListener(action);
        }
    }
}

/// <summary>
/// Attached to VideoTriggerArea BoxCollider.
/// Captures press & release triggers for mouse, touch, and XR pointers.
/// </summary>
public class HoldMediaClipTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private HistoryPanel panel;

    public void Setup(HistoryPanel historyPanel)
    {
        panel = historyPanel;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        panel?.OnPointerDownTrigger();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        panel?.OnPointerUpTrigger();
    }
}
