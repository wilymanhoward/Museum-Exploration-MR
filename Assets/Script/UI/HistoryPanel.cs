using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
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

    [Header("Photo Gallery UI (auto-found by name, or built at runtime if absent)")]
    [Tooltip("Goes to the previous photo. Auto-found by name ('PreviousImageButton') or built at runtime next to the display image.")]
    public Button previousImageButton;
    public XRButtonSelection previousImageButtonXR;
    [Tooltip("Goes to the next photo. Auto-found by name ('NextImageButton') or built at runtime next to the display image.")]
    public Button nextImageButton;
    public XRButtonSelection nextImageButtonXR;
    [Tooltip("Shows '2 / 5' etc. Auto-found by name ('ImageCounterText') or built at runtime. Hidden for topics with 0 or 1 photo.")]
    public TMP_Text imageCounterText;

    [Header("Active Data")]
    public HistoryData activeHistoryData;

    private AudioSource audioSource;
    private Action onCloseCallback;
    private Coroutine holdCheckCoroutine;
    private bool isHoldingTrigger = false;
    private bool isStaringOrHolding = false;
    private bool isVideoPlaying = false;
    private float currentGazeTimer = 0f;
    private const float RequiredGazeDuration = 1.5f;
    private CanvasGroup displayImageCanvasGroup;
    private CanvasGroup photoCanvasGroup;
    private CanvasGroup videoPanelCanvasGroup;
    private CanvasGroup holdHintCanvasGroup;
    private Coroutine crossfadeCoroutine;
    private Coroutine videoMonitorCoroutine;
    private int currentImageIndex = 0;
    private GameObject builtGalleryNavRoot;
    private ScrollRect descriptionScrollRect;
    private ScrollRect photoScrollRect;
    private RectTransform photoContentRect;
    private PhotoSnapScroller photoSnapScroller;

    private struct RectTransformSnapshot
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 pivot;

        public static RectTransformSnapshot Capture(RectTransform rt)
        {
            if (rt == null) return default;
            return new RectTransformSnapshot
            {
                anchorMin = rt.anchorMin,
                anchorMax = rt.anchorMax,
                anchoredPosition = rt.anchoredPosition,
                sizeDelta = rt.sizeDelta,
                pivot = rt.pivot
            };
        }

        public void Restore(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.pivot = pivot;
        }
    }

    private bool hasCapturedOriginalLayout = false;
    private RectTransformSnapshot origMediaSlot;
    private RectTransformSnapshot origDetailCard;
    private RectTransformSnapshot origDescCard;
    private RectTransformSnapshot origEventTitle;
    private RectTransform mediaSlotTransform;
    private RectTransform detailCardTransform;
    private RectTransform descCardTransform;

    private void CaptureOriginalLayout()
    {
        if (hasCapturedOriginalLayout) return;

        if (displayImage != null)
        {
            mediaSlotTransform = displayImage.transform.parent as RectTransform;
        }
        if (mediaSlotTransform == null)
        {
            Transform t = FindDeepChildTransform(transform, "2DViewPanel");
            if (t != null) mediaSlotTransform = t as RectTransform;
        }

        Transform detailT = FindDeepChildTransform(transform, "DetailArtefakCard");
        if (detailT != null) detailCardTransform = detailT as RectTransform;
        else if (timePeriodText != null) detailCardTransform = timePeriodText.transform.parent as RectTransform;

        Transform descT = FindDeepChildTransform(transform, "TentangArtefakCard");
        if (descT != null) descCardTransform = descT as RectTransform;

        origMediaSlot = RectTransformSnapshot.Capture(mediaSlotTransform);
        origDetailCard = RectTransformSnapshot.Capture(detailCardTransform);
        origDescCard = RectTransformSnapshot.Capture(descCardTransform);
        origEventTitle = RectTransformSnapshot.Capture(eventTitleText != null ? eventTitleText.rectTransform : null);

        hasCapturedOriginalLayout = true;
    }

    private void UpdatePanelLayout(bool hasImages)
    {
        CaptureOriginalLayout();

        // 1. Permanently hide "No Image" text placeholder
        if (noImageTextObj != null)
        {
            noImageTextObj.SetActive(false);
        }

        // Hide any texts in the panel that contain "no image" / "tidak ada gambar"
        TMP_Text[] allTexts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in allTexts)
        {
            if (t != null && t != eventTitleText && t != topTitleText && t != categoryText && t != timePeriodText && t != locationText && t != descriptionText)
            {
                string s = t.text.ToLower();
                if (s.Contains("no image") || s.Contains("tidak ada gambar") || s.Contains("no images"))
                {
                    t.gameObject.SetActive(false);
                }
            }
        }

        if (hasImages)
        {
            // --- 2-COLUMN LAYOUT (With Photo Gallery / Video) ---
            if (mediaSlotTransform != null)
            {
                origMediaSlot.Restore(mediaSlotTransform);
                mediaSlotTransform.gameObject.SetActive(true);
            }
            if (detailCardTransform != null)
            {
                origDetailCard.Restore(detailCardTransform);
                detailCardTransform.gameObject.SetActive(true);
            }
            if (descCardTransform != null)
            {
                origDescCard.Restore(descCardTransform);
                descCardTransform.gameObject.SetActive(true);
            }
            if (eventTitleText != null)
            {
                origEventTitle.Restore(eventTitleText.rectTransform);
                eventTitleText.gameObject.SetActive(true);
            }
            if (photoScrollRect != null)
            {
                photoScrollRect.gameObject.SetActive(true);
            }

            AlignCardElements(false);
        }
        else
        {
            // --- FULL-WIDTH TEXT ONLY LAYOUT (e.g. Asal Usul Nama Terengganu) ---
            // Hide left photo slot / media container completely
            if (mediaSlotTransform != null)
            {
                mediaSlotTransform.gameObject.SetActive(false);
            }
            if (photoScrollRect != null)
            {
                photoScrollRect.gameObject.SetActive(false);
            }
            if (holdToPlayText != null)
            {
                holdToPlayText.gameObject.SetActive(false);
            }
            if (videoPanel != null)
            {
                videoPanel.SetActive(false);
            }

            // Expand DetailArtefakCard across full width under header
            if (detailCardTransform != null)
            {
                detailCardTransform.gameObject.SetActive(true);
                detailCardTransform.anchorMin = new Vector2(0.05f, 0.58f);
                detailCardTransform.anchorMax = new Vector2(0.95f, 0.78f);
                detailCardTransform.anchoredPosition = Vector2.zero;
                detailCardTransform.sizeDelta = Vector2.zero;
            }

            // Expand TentangArtefakCard (Description Card) across full width and height
            if (descCardTransform != null)
            {
                descCardTransform.gameObject.SetActive(true);
                descCardTransform.anchorMin = new Vector2(0.05f, 0.05f);
                descCardTransform.anchorMax = new Vector2(0.95f, 0.56f);
                descCardTransform.anchoredPosition = Vector2.zero;
                descCardTransform.sizeDelta = Vector2.zero;
            }

            // Position eventTitleText cleanly across top, aligning flush with Sejarah Terengganu
            if (eventTitleText != null)
            {
                eventTitleText.gameObject.SetActive(true);
                eventTitleText.rectTransform.anchorMin = new Vector2(0.13f, 0.79f);
                eventTitleText.rectTransform.anchorMax = new Vector2(0.68f, 0.86f);
                eventTitleText.rectTransform.pivot = new Vector2(0f, 0.5f);
                eventTitleText.rectTransform.anchoredPosition = Vector2.zero;
                eventTitleText.rectTransform.sizeDelta = Vector2.zero;
                eventTitleText.alignment = TextAlignmentOptions.MidlineLeft;
            }

            AlignCardElements(true);
        }

        Canvas.ForceUpdateCanvases();
        if (descCardTransform != null) LayoutRebuilder.ForceRebuildLayoutImmediate(descCardTransform);
        if (detailCardTransform != null) LayoutRebuilder.ForceRebuildLayoutImmediate(detailCardTransform);
    }

    /// <summary>
    /// Aligns all icons, labels, values, and separator lines inside DetailArtefakCard and TentangArtefakCard
    /// with deterministic top-anchored vertical rows so icons and lines never overlap.
    /// </summary>
    private void AlignCardElements(bool isFullText)
    {
        // 1. Align DetailArtefakCard child elements
        if (detailCardTransform != null)
        {
            // Direct child lookups by name
            Transform tSparkle = detailCardTransform.Find("Image (4)");
            Transform tCalendar = detailCardTransform.Find("Image");
            Transform tPin = detailCardTransform.Find("Image (1)");
            Transform tHeader = detailCardTransform.Find("Header");
            Transform tLabelTempoh = detailCardTransform.Find("Label_0");
            Transform tValueTempoh = detailCardTransform.Find("Value_0");
            Transform tLabelLokasi = detailCardTransform.Find("Label_1");
            Transform tValueLokasi = detailCardTransform.Find("Value_1");
            Transform tSeparators = detailCardTransform.Find("Separators");

            // Fallback lookups if names changed
            if (tHeader == null)
            {
                foreach (var t in detailCardTransform.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.text.ToLower().Contains("detail")) { tHeader = t.transform; break; }
                }
            }
            if (tLabelTempoh == null)
            {
                foreach (var t in detailCardTransform.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.text.ToLower().Contains("tempoh") || t.text.ToLower().Contains("masa")) { tLabelTempoh = t.transform; break; }
                }
            }
            if (tLabelLokasi == null)
            {
                foreach (var t in detailCardTransform.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.text.ToLower().Contains("lokasi")) { tLabelLokasi = t.transform; break; }
                }
            }
            if (tValueTempoh == null && timePeriodText != null) tValueTempoh = timePeriodText.transform;
            if (tValueLokasi == null && locationText != null) tValueLokasi = locationText.transform;

            // (a) Header Row: y = -14px
            if (tSparkle != null)
            {
                RectTransform rt = tSparkle as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(16f, -14f);
                    rt.sizeDelta = new Vector2(18f, 18f);
                }
            }
            if (tHeader != null)
            {
                RectTransform rt = tHeader as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(40f, -14f);
                    rt.sizeDelta = new Vector2(-56f, 24f);
                }
                TMP_Text tmp = tHeader.GetComponent<TMP_Text>();
                if (tmp != null) tmp.alignment = TextAlignmentOptions.MidlineLeft;
            }

            // (b) Divider Lines
            if (tSeparators != null)
            {
                RectTransform rtSep = tSeparators as RectTransform;
                if (rtSep != null)
                {
                    rtSep.anchorMin = Vector2.zero;
                    rtSep.anchorMax = Vector2.one;
                    rtSep.pivot = new Vector2(0.5f, 0.5f);
                    rtSep.anchoredPosition = Vector2.zero;
                    rtSep.sizeDelta = Vector2.zero;
                }

                Transform tLine1 = tSeparators.Find("Separator") ?? (tSeparators.childCount > 0 ? tSeparators.GetChild(0) : null);
                Transform tLine2 = tSeparators.Find("Line (1)") ?? (tSeparators.childCount > 1 ? tSeparators.GetChild(1) : null);

                if (tLine1 != null)
                {
                    RectTransform rt = tLine1 as RectTransform;
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0.02f, 1f);
                        rt.anchorMax = new Vector2(0.98f, 1f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = new Vector2(0f, -28f);
                        rt.sizeDelta = new Vector2(0f, 1.5f);
                    }
                }

                if (tLine2 != null)
                {
                    RectTransform rt = tLine2 as RectTransform;
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0.02f, 1f);
                        rt.anchorMax = new Vector2(0.98f, 1f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = new Vector2(0f, -62f);
                        rt.sizeDelta = new Vector2(0f, 1f);
                    }
                }
            }

            // (c) Row 1: Tempoh Masa (y = -45px)
            if (tCalendar != null)
            {
                RectTransform rt = tCalendar as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(16f, -45f);
                    rt.sizeDelta = new Vector2(18f, 18f);
                }
            }
            if (tLabelTempoh != null)
            {
                RectTransform rt = tLabelTempoh as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0.40f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(40f, -45f);
                    rt.sizeDelta = new Vector2(-40f, 22f);
                }
                TMP_Text tmp = tLabelTempoh.GetComponent<TMP_Text>();
                if (tmp != null) tmp.alignment = TextAlignmentOptions.MidlineLeft;
            }
            if (tValueTempoh != null)
            {
                RectTransform rt = tValueTempoh as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.40f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(-16f, -45f);
                    rt.sizeDelta = new Vector2(-16f, 22f);
                }
                TMP_Text tmp = tValueTempoh.GetComponent<TMP_Text>();
                if (tmp != null) tmp.alignment = TextAlignmentOptions.MidlineRight;
            }

            // (d) Row 2: Lokasi (y = -78px)
            if (tPin != null)
            {
                RectTransform rt = tPin as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(16f, -78f);
                    rt.sizeDelta = new Vector2(18f, 18f);
                }
            }
            if (tLabelLokasi != null)
            {
                RectTransform rt = tLabelLokasi as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0.40f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(40f, -78f);
                    rt.sizeDelta = new Vector2(-40f, 22f);
                }
                TMP_Text tmp = tLabelLokasi.GetComponent<TMP_Text>();
                if (tmp != null) tmp.alignment = TextAlignmentOptions.MidlineLeft;
            }
            if (tValueLokasi != null)
            {
                RectTransform rt = tValueLokasi as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.40f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(-16f, -78f);
                    rt.sizeDelta = new Vector2(-16f, 22f);
                }
                TMP_Text tmp = tValueLokasi.GetComponent<TMP_Text>();
                if (tmp != null) tmp.alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        // 2. Align TentangArtefakCard child elements
        if (descCardTransform != null)
        {
            Transform tHeader = descCardTransform.Find("Header") ?? (descCardTransform.childCount > 0 ? descCardTransform.GetChild(0) : null);

            if (tHeader != null)
            {
                RectTransform hRect = tHeader as RectTransform;
                if (hRect != null)
                {
                    hRect.anchorMin = new Vector2(0f, 1f);
                    hRect.anchorMax = new Vector2(1f, 1f);
                    hRect.pivot = new Vector2(0.5f, 1f);
                    hRect.anchoredPosition = Vector2.zero;
                    hRect.sizeDelta = new Vector2(0f, 28f);
                }

                Transform tInfoIcon = tHeader.Find("HeaderIcon") ?? tHeader.Find("Image") ?? (tHeader.childCount > 0 ? tHeader.GetChild(0) : null);
                Transform tInfoText = tHeader.Find("HeaderText") ?? tHeader.Find("Text") ?? (tHeader.childCount > 1 ? tHeader.GetChild(1) : null);

                if (tInfoIcon != null)
                {
                    RectTransform rt = tInfoIcon as RectTransform;
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 0.5f);
                        rt.anchorMax = new Vector2(0f, 0.5f);
                        rt.pivot = new Vector2(0f, 0.5f);
                        rt.anchoredPosition = new Vector2(16f, 0f);
                        rt.sizeDelta = new Vector2(18f, 18f);
                    }
                }
                if (tInfoText != null)
                {
                    RectTransform rt = tInfoText as RectTransform;
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 0f);
                        rt.anchorMax = new Vector2(1f, 1f);
                        rt.pivot = new Vector2(0f, 0.5f);
                        rt.anchoredPosition = new Vector2(40f, 0f);
                        rt.sizeDelta = new Vector2(-56f, 0f);
                    }
                    TMP_Text tmp = tInfoText.GetComponent<TMP_Text>();
                    if (tmp != null) tmp.alignment = TextAlignmentOptions.MidlineLeft;
                }
            }

            // Divider Line
            Transform tLine = descCardTransform.Find("Line") ?? descCardTransform.Find("Separator") ?? descCardTransform.Find("Image (1)");
            if (tLine != null)
            {
                RectTransform rt = tLine as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.02f, 1f);
                    rt.anchorMax = new Vector2(0.98f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, -28f);
                    rt.sizeDelta = new Vector2(0f, 1.5f);
                }
            }

            // DescriptionScrollView
            if (descriptionScrollRect != null)
            {
                RectTransform sRect = descriptionScrollRect.transform as RectTransform;
                if (sRect != null)
                {
                    sRect.anchorMin = new Vector2(0f, 0f);
                    sRect.anchorMax = new Vector2(1f, 1f);
                    sRect.pivot = new Vector2(0.5f, 0.5f);
                    sRect.offsetMin = new Vector2(0f, 12f);
                    sRect.offsetMax = new Vector2(0f, -36f);
                }

                if (descriptionScrollRect.viewport != null)
                {
                    RectTransform vRect = descriptionScrollRect.viewport;
                    vRect.offsetMin = new Vector2(16f, 0f);
                    vRect.offsetMax = new Vector2(-28f, 0f);
                }
            }
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Detach from wrist canvas at runtime so panel stays fixed and draggable in world space
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
        }
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

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
        EnsurePhotoScrollView();
        EnsureDescriptionScrollView();
        EnsureGrabbablePanel();
    }

    /// <summary>
    /// Makes the panel pinch-and-hold draggable, and magnetically snappable onto a nearby
    /// real-world wall, exactly like the Artifact Detail Panel - reuses the same
    /// ArtifactPanelDragger component rather than duplicating its drag/wall-snap logic.
    /// </summary>
    private void EnsureGrabbablePanel()
    {
        XRGrabInteractable oldGrab = GetComponent<XRGrabInteractable>();
        if (oldGrab != null && !(oldGrab is ArtifactPanelDragger))
        {
            Destroy(oldGrab);
        }
        if (GetComponent<ArtifactPanelDragger>() == null)
        {
            gameObject.AddComponent<ArtifactPanelDragger>();
        }
    }

    /// <summary>
    /// Converts the photo container into a horizontal snapping photo gallery (PhotoScrollView)
    /// with a horizontal scrollbar, full-mode pictures, and automatic page snapping.
    /// </summary>
    private void EnsurePhotoScrollView()
    {
        if (photoScrollRect != null) return;

        Transform parentSlot = null;
        if (displayImage != null)
        {
            parentSlot = displayImage.transform.parent;
            displayImage.gameObject.SetActive(false); // Hide single legacy image component
        }
        if (parentSlot == null) parentSlot = transform;

        // Clean up all stray BoxColliders and XRSimpleInteractables in photo area so XR Raycast targets UGUI directly
        Collider[] allColliders = parentSlot.GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            Destroy(col);
        }
        XRSimpleInteractable[] allInteractables = parentSlot.GetComponentsInChildren<XRSimpleInteractable>(true);
        foreach (var inter in allInteractables)
        {
            if (!(inter is PhotoSnapScroller) && !(inter is ScrollbarSnapHook))
            {
                Destroy(inter);
            }
        }

        // 1. Root ScrollView container
        GameObject scrollGo = new GameObject("PhotoScrollView");
        RectTransform scrollRootRect = scrollGo.AddComponent<RectTransform>();
        scrollGo.transform.SetParent(parentSlot, false);
        scrollRootRect.anchorMin = new Vector2(0.02f, 0.02f);
        scrollRootRect.anchorMax = new Vector2(0.98f, 0.98f);
        scrollRootRect.anchoredPosition = Vector2.zero;
        scrollRootRect.sizeDelta = Vector2.zero;
        scrollRootRect.pivot = new Vector2(0.5f, 0.5f);

        photoCanvasGroup = scrollGo.GetComponent<CanvasGroup>();
        if (photoCanvasGroup == null) photoCanvasGroup = scrollGo.AddComponent<CanvasGroup>();

        Image scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.color = new Color(1f, 1f, 1f, 0.001f);
        scrollBg.raycastTarget = true;

        // 2. Viewport - leaves 16px bottom margin for the horizontal scrollbar
        GameObject viewportGo = new GameObject("Viewport");
        RectTransform viewportRect = viewportGo.AddComponent<RectTransform>();
        viewportGo.transform.SetParent(scrollGo.transform, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(0f, 16f);
        viewportRect.offsetMax = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();
        Image viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImg.raycastTarget = true;

        // 3. Content - horizontal layout
        GameObject contentGo = new GameObject("Content");
        photoContentRect = contentGo.AddComponent<RectTransform>();
        contentGo.transform.SetParent(viewportGo.transform, false);
        photoContentRect.anchorMin = new Vector2(0f, 0f);
        photoContentRect.anchorMax = new Vector2(0f, 1f);
        photoContentRect.pivot = new Vector2(0f, 0.5f);
        photoContentRect.anchoredPosition = Vector2.zero;
        photoContentRect.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup hlg = contentGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 0f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 4. ScrollRect - horizontal only with snappy deceleration
        photoScrollRect = scrollGo.AddComponent<ScrollRect>();
        photoScrollRect.content = photoContentRect;
        photoScrollRect.viewport = viewportRect;
        photoScrollRect.horizontal = true;
        photoScrollRect.vertical = false;
        photoScrollRect.movementType = ScrollRect.MovementType.Elastic;
        photoScrollRect.elasticity = 0.1f;
        photoScrollRect.inertia = true;
        photoScrollRect.decelerationRate = 0.135f;
        photoScrollRect.scrollSensitivity = 25f;

        // 5. Page Snap Controller
        photoSnapScroller = scrollGo.AddComponent<PhotoSnapScroller>();
        photoSnapScroller.scrollRect = photoScrollRect;

        // 6. Build horizontal scrollbar indicator
        BuildHorizontalScrollbarForScrollRect(photoScrollRect);
    }

    /// <summary>
    /// Populates the scrollable photo gallery with full-mode pictures and configures page snapping.
    /// </summary>
    private void PopulatePhotoScrollView(HistoryData data)
    {
        EnsurePhotoScrollView();
        if (photoContentRect == null) return;

        HistoryImage[] images = GetCurrentImages();
        bool hasImages = images != null && images.Length > 0;

        UpdatePanelLayout(hasImages);

        if (!hasImages)
        {
            if (photoSnapScroller != null) photoSnapScroller.totalPages = 1;
            return;
        }

        // Clear existing photo cards
        for (int i = photoContentRect.childCount - 1; i >= 0; i--)
        {
            Destroy(photoContentRect.GetChild(i).gameObject);
        }

        RectTransform viewportRect = photoScrollRect != null ? photoScrollRect.viewport : null;
        float viewportWidth = (viewportRect != null && viewportRect.rect.width > 50f) ? viewportRect.rect.width : 246f;

        int validCount = 0;
        foreach (var historyImg in images)
        {
            if (historyImg.sprite == null) continue;
            validCount++;

            Sprite s = historyImg.sprite;
            GameObject cardGo = new GameObject("PhotoPage_" + validCount);
            cardGo.transform.SetParent(photoContentRect, false);

            RectTransform cardRect = cardGo.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 0f);
            cardRect.anchorMax = new Vector2(0f, 1f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(viewportWidth, 0f);

            LayoutElement le = cardGo.AddComponent<LayoutElement>();
            le.preferredWidth = viewportWidth;
            le.flexibleHeight = 1f;

            // Full-mode photo image container
            GameObject imgGo = new GameObject("Photo");
            imgGo.transform.SetParent(cardGo.transform, false);
            RectTransform imgRt = imgGo.AddComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.pivot = new Vector2(0.5f, 0.5f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;

            Image imgComp = imgGo.AddComponent<Image>();
            imgComp.sprite = s;
            imgComp.color = Color.white;
            imgComp.preserveAspect = true;
            imgComp.raycastTarget = true;

            AspectRatioFitter fitter = imgGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            if (s.rect.height > 0f)
            {
                fitter.aspectRatio = (float)s.rect.width / (float)s.rect.height;
            }

            // Caption overlay (if present)
            if (!string.IsNullOrEmpty(historyImg.caption))
            {
                GameObject capGo = new GameObject("Caption");
                capGo.transform.SetParent(cardGo.transform, false);
                RectTransform capRt = capGo.AddComponent<RectTransform>();
                capRt.anchorMin = new Vector2(0f, 0f);
                capRt.anchorMax = new Vector2(1f, 0f);
                capRt.pivot = new Vector2(0.5f, 0f);
                capRt.anchoredPosition = new Vector2(0f, 4f);
                capRt.sizeDelta = new Vector2(-10f, 22f);

                Image capBg = capGo.AddComponent<Image>();
                capBg.color = new Color(0f, 0f, 0f, 0.55f);
                capBg.raycastTarget = false;

                GameObject capTextGo = new GameObject("Text");
                capTextGo.transform.SetParent(capGo.transform, false);
                RectTransform textRt = capTextGo.AddComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI capTmp = capTextGo.AddComponent<TextMeshProUGUI>();
                capTmp.text = historyImg.caption;
                capTmp.fontSize = 11f;
                capTmp.alignment = TextAlignmentOptions.Center;
                capTmp.color = Color.white;
                capTmp.enableWordWrapping = false;
                capTmp.overflowMode = TextOverflowModes.Ellipsis;
                capTmp.raycastTarget = false;
            }
        }

        if (photoSnapScroller != null)
        {
            photoSnapScroller.totalPages = Mathf.Max(1, validCount);
            photoSnapScroller.currentPage = 0;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(photoContentRect);

        if (photoScrollRect != null)
        {
            photoScrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    /// <summary>
    /// Wraps descriptionText in a standard Viewport/Content/ScrollRect so a description too
    /// long to fit its box (e.g. "Tragedi Megat Panji Alam"'s multi-paragraph legend) can be
    /// pinch-held and dragged to scroll instead of just running past the panel's edge. Reuses
    /// Unity's built-in ScrollRect rather than custom drag code - it drives the exact same
    /// IBeginDragHandler/IDragHandler/IEndDragHandler interfaces that TrackedDeviceGraphicRaycaster
    /// already pumps hand-ray pinch input through elsewhere in this project (ArtifactPanelDragger,
    /// the timeline mini-game's draggable cards), so pinch-hold-drag scrolling works for free.
    /// Runs once in Awake, before Setup() ever assigns text, and re-parents descriptionText's
    /// own GameObject rather than replacing it, so every other reference to descriptionText
    /// (Setup/SetSmallerText) keeps working unchanged.
    /// </summary>
    private void EnsureDescriptionScrollView()
    {
        if (descriptionText == null) return;

        descriptionScrollRect = descriptionText.GetComponentInParent<ScrollRect>();
        if (descriptionScrollRect != null)
        {
            // Already wrapped - ensure raycast target is active on text so UI raycasts hit
            descriptionText.raycastTarget = true;
            // Remove any ContentSizeFitter on Content that could collapse contentRect height
            Transform currentContent = descriptionText.transform.parent;
            if (currentContent != null)
            {
                ContentSizeFitter csf = currentContent.GetComponent<ContentSizeFitter>();
                if (csf != null) Destroy(csf);
            }

            if (descriptionScrollRect.verticalScrollbar == null)
            {
                BuildScrollbarForScrollRect(descriptionScrollRect);
            }
            return;
        }

        Transform originalParent = descriptionText.transform.parent;
        RectTransform textRect = descriptionText.rectTransform;

        // Capture the description's original slot - the scroll view takes it over so nothing
        // else in the panel needs to know this restructuring happened.
        Vector2 anchorMin = textRect.anchorMin;
        Vector2 anchorMax = textRect.anchorMax;
        Vector2 anchoredPosition = textRect.anchoredPosition;
        Vector2 sizeDelta = textRect.sizeDelta;
        Vector2 pivot = textRect.pivot;
        int siblingIndex = textRect.GetSiblingIndex();

        // 1. Scroll view root - the visible, clipped window; occupies the description's old spot.
        GameObject scrollGo = new GameObject("DescriptionScrollView");
        RectTransform scrollRootRect = scrollGo.AddComponent<RectTransform>();
        scrollGo.transform.SetParent(originalParent, false);
        scrollRootRect.anchorMin = anchorMin;
        scrollRootRect.anchorMax = anchorMax;
        scrollRootRect.anchoredPosition = anchoredPosition;
        scrollRootRect.sizeDelta = sizeDelta;
        scrollRootRect.pivot = pivot;
        scrollRootRect.SetSiblingIndex(siblingIndex);

        // Invisible background image on root to ensure raycasts anywhere in the box hit the ScrollRect
        Image scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.color = new Color(1f, 1f, 1f, 0.001f);
        scrollBg.raycastTarget = true;

        // 2. Viewport - clips content to the visible window and leaves 12px right margin for scrollbar track
        GameObject viewportGo = new GameObject("Viewport");
        RectTransform viewportRect = viewportGo.AddComponent<RectTransform>();
        viewportGo.transform.SetParent(scrollGo.transform, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-12f, 0f);
        viewportGo.AddComponent<RectMask2D>();
        Image viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 0.001f); // invisible but still raycastable
        viewportImg.raycastTarget = true;

        // 3. Content - grows to the text's full unclipped height; this is what scrolls.
        GameObject contentGo = new GameObject("Content");
        RectTransform contentRect = contentGo.AddComponent<RectTransform>();
        contentGo.transform.SetParent(viewportGo.transform, false);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, sizeDelta.y);

        // 4. Re-parent the description text itself into Content, stretched to Content's width
        descriptionText.transform.SetParent(contentGo.transform, false);
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        descriptionText.raycastTarget = true;

        // 5. Wire the ScrollRect.
        descriptionScrollRect = scrollGo.AddComponent<ScrollRect>();
        descriptionScrollRect.content = contentRect;
        descriptionScrollRect.viewport = viewportRect;
        descriptionScrollRect.horizontal = false;
        descriptionScrollRect.vertical = true;
        descriptionScrollRect.movementType = ScrollRect.MovementType.Clamped;
        descriptionScrollRect.scrollSensitivity = 35f;

        // 6. Build sleek vertical Scrollbar indicator
        BuildScrollbarForScrollRect(descriptionScrollRect);
    }

    private static Sprite cachedRoundedRectSprite;

    private static Sprite GetOrCreateRoundedRectSprite()
    {
        if (cachedRoundedRectSprite != null) return cachedRoundedRectSprite;

        int size = 64;
        float cornerRadius = 10f; // 10px corner radius on 64x64 texture
        float halfSize = size / 2f; // 32f
        float innerHalf = halfSize - cornerRadius; // 22f

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs((x + 0.5f) - halfSize);
                float py = Mathf.Abs((y + 0.5f) - halfSize);

                float dx = Mathf.Max(px - innerHalf, 0f);
                float dy = Mathf.Max(py - innerHalf, 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(cornerRadius - dist + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        // 9-slice borders: 14px on all 4 sides -> clean, symmetrical rounded rectangular corners
        Vector4 border = new Vector4(14, 14, 14, 14);
        cachedRoundedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        return cachedRoundedRectSprite;
    }

    private void BuildScrollbarForScrollRect(ScrollRect scrollRect)
    {
        if (scrollRect == null || scrollRect.gameObject == null) return;

        Sprite roundedRect = GetOrCreateRoundedRectSprite();

        // Check if scrollbar already exists
        Scrollbar existingSb = scrollRect.gameObject.GetComponentInChildren<Scrollbar>(true);
        if (existingSb != null)
        {
            scrollRect.verticalScrollbar = existingSb;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            if (existingSb.targetGraphic is Image existingHandleImg)
            {
                existingHandleImg.sprite = roundedRect;
                existingHandleImg.type = Image.Type.Sliced;
            }
            Image existingTrackImg = existingSb.GetComponent<Image>();
            if (existingTrackImg != null)
            {
                existingTrackImg.sprite = roundedRect;
                existingTrackImg.type = Image.Type.Sliced;
            }
            return;
        }

        GameObject scrollbarGo = new GameObject("ScrollbarVertical");
        scrollbarGo.transform.SetParent(scrollRect.transform, false);

        RectTransform sbRect = scrollbarGo.AddComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(1f, 0f);
        sbRect.anchorMax = new Vector2(1f, 1f);
        sbRect.pivot = new Vector2(1f, 0.5f);
        sbRect.anchoredPosition = Vector2.zero;
        sbRect.sizeDelta = new Vector2(7f, 0f); // Sleek 7px wide rounded rectangular scrollbar

        Image trackImg = scrollbarGo.AddComponent<Image>();
        trackImg.sprite = roundedRect;
        trackImg.type = Image.Type.Sliced;
        trackImg.color = new Color(1f, 1f, 1f, 0.15f); // Translucent track
        trackImg.raycastTarget = true;

        Scrollbar sbComp = scrollbarGo.AddComponent<Scrollbar>();
        sbComp.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingAreaGo = new GameObject("Sliding Area");
        slidingAreaGo.transform.SetParent(scrollbarGo.transform, false);
        RectTransform slidingRect = slidingAreaGo.AddComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.sizeDelta = Vector2.zero;

        GameObject handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(slidingAreaGo.transform, false);
        RectTransform handleRect = handleGo.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.sizeDelta = Vector2.zero;

        Image handleImg = handleGo.AddComponent<Image>();
        handleImg.sprite = roundedRect;
        handleImg.type = Image.Type.Sliced;
        handleImg.color = new Color(0.90f, 0.93f, 0.63f, 0.85f); // Pale lime yellow accent (#E5EE9C)
        handleImg.raycastTarget = true;

        sbComp.targetGraphic = handleImg;
        sbComp.handleRect = handleRect;

        scrollRect.verticalScrollbar = sbComp;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = 4f;
    }

    private void BuildHorizontalScrollbarForScrollRect(ScrollRect scrollRect)
    {
        if (scrollRect == null || scrollRect.gameObject == null) return;

        Sprite roundedRect = GetOrCreateRoundedRectSprite();

        Scrollbar existingSb = scrollRect.gameObject.GetComponentInChildren<Scrollbar>(true);
        if (existingSb != null)
        {
            scrollRect.horizontalScrollbar = existingSb;
            scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return;
        }

        GameObject scrollbarGo = new GameObject("ScrollbarHorizontal");
        scrollbarGo.transform.SetParent(scrollRect.transform, false);

        RectTransform sbRect = scrollbarGo.AddComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(0.12f, 0f);
        sbRect.anchorMax = new Vector2(0.88f, 0f);
        sbRect.pivot = new Vector2(0.5f, 0f);
        sbRect.anchoredPosition = new Vector2(0f, 2f);
        sbRect.sizeDelta = new Vector2(0f, 6f); // Sleek 6px tall horizontal bar

        Image trackImg = scrollbarGo.AddComponent<Image>();
        trackImg.sprite = roundedRect;
        trackImg.type = Image.Type.Sliced;
        trackImg.color = new Color(1f, 1f, 1f, 0.15f); // Translucent track
        trackImg.raycastTarget = true;

        Scrollbar sbComp = scrollbarGo.AddComponent<Scrollbar>();
        sbComp.direction = Scrollbar.Direction.LeftToRight;

        GameObject slidingAreaGo = new GameObject("Sliding Area");
        slidingAreaGo.transform.SetParent(scrollbarGo.transform, false);
        RectTransform slidingRect = slidingAreaGo.AddComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.sizeDelta = Vector2.zero;

        GameObject handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(slidingAreaGo.transform, false);
        RectTransform handleRect = handleGo.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.sizeDelta = Vector2.zero;

        Image handleImg = handleGo.AddComponent<Image>();
        handleImg.sprite = roundedRect;
        handleImg.type = Image.Type.Sliced;
        handleImg.color = new Color(0.90f, 0.93f, 0.63f, 0.85f); // Pale lime yellow accent (#E5EE9C)
        handleImg.raycastTarget = true;

        sbComp.targetGraphic = handleImg;
        sbComp.handleRect = handleRect;

        scrollRect.horizontalScrollbar = sbComp;
        scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.horizontalScrollbarSpacing = 2f;

        ScrollbarSnapHook sbHook = scrollbarGo.AddComponent<ScrollbarSnapHook>();
        sbHook.snapScroller = photoSnapScroller;
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

        UpdateGazeDetection();
    }

    private void UpdateGazeDetection()
    {
        if (activeHistoryData == null || activeHistoryData.videoClip == null)
        {
            currentGazeTimer = 0f;
            return;
        }

        if (isVideoPlaying)
        {
            currentGazeTimer = 0f;
            return;
        }

        if (IsUserGazingAtPhoto())
        {
            currentGazeTimer += Time.deltaTime;
            if (currentGazeTimer >= RequiredGazeDuration)
            {
                currentGazeTimer = 0f;
                PlayVideoClipWithTransition();
            }
        }
        else
        {
            currentGazeTimer = Mathf.Max(0f, currentGazeTimer - Time.deltaTime * 2f);
        }
    }

    private bool IsUserGazingAtPhoto()
    {
        if (isStaringOrHolding) return true;
        if (activeHistoryData == null || activeHistoryData.videoClip == null) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        Transform targetT = photoScrollRect != null ? photoScrollRect.transform : (displayImage != null ? displayImage.transform : null);
        if (targetT == null || !targetT.gameObject.activeInHierarchy) return false;

        RectTransform rect = targetT as RectTransform;
        if (rect != null)
        {
            Ray headRay = new Ray(cam.transform.position, cam.transform.forward);
            Plane imgPlane = new Plane(-rect.forward, rect.position);
            if (imgPlane.Raycast(headRay, out float enterDist) && enterDist > 0.1f && enterDist <= 4f)
            {
                Vector3 hitPoint = headRay.GetPoint(enterDist);
                Vector3 localPoint = rect.InverseTransformPoint(hitPoint);
                if (rect.rect.Contains(localPoint))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Populate the HistoryPanel UI using the provided HistoryData ScriptableObject.
    /// </summary>
    public void Setup(HistoryData data, Action onClose = null)
    {
        activeHistoryData = data;
        onCloseCallback = onClose;
        currentImageIndex = 0;

        // Both callers (HistoryManager, HistoryListPanel) call Setup BEFORE OpenPanel, so
        // this can run while still inactive - activate early (after activeHistoryData is
        // already set, so OnEnable's own refresh below sees the right data) so
        // SetCappedTwoLineText's ForceMeshUpdate further down measures wrapped lines
        // against a live hierarchy. OpenPanel's own SetActive(true) right after this
        // returns is then just a no-op.
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        if (data == null) return;

        EnsureMediaLoaded(data);
        PopulatePhotoScrollView(data);

        // Header Title & Subtitle - capped at 2 lines (long titles like "Infrastruktur &
        // Pembangunan Terengganu" wrap once, then ellipsise, instead of running past the box).
        if (topTitleText != null) SetCappedTwoLineText(topTitleText, string.IsNullOrEmpty(data.topTitle) ? "Sejarah" : data.topTitle, 20f);
        if (categoryText != null) SetCappedTwoLineText(categoryText, string.IsNullOrEmpty(data.category) ? "" : data.category, 15f);

        // Event / Story Title
        if (eventTitleText != null) SetCappedTwoLineText(eventTitleText, string.IsNullOrEmpty(data.eventTitle) ? data.name : data.eventTitle, 16f);

        // Detail Sejarah (Time Period & Location)
        if (timePeriodText != null) SetCappedTwoLineText(timePeriodText, string.IsNullOrEmpty(data.timePeriod) ? "-" : data.timePeriod, 13f);
        if (locationText != null) SetCappedTwoLineText(locationText, string.IsNullOrEmpty(data.location) ? "-" : data.location, 13f);

        // Description is a full paragraph, not a label - it deliberately keeps normal
        // wrapping with NO line cap, since capping it at 2 lines would hide most of the
        // actual historical content instead of just fixing an overflowing title.
        if (descriptionText != null)
        {
            EnsureDescriptionScrollView();

            SetSmallerText(descriptionText, string.IsNullOrEmpty(data.description) ? "" : data.description, 13f);
            descriptionText.raycastTarget = true;

            // Force TextMeshPro layout update to calculate exact line wrapping height
            descriptionText.ForceMeshUpdate();

            RectTransform contentRect = descriptionText.transform.parent as RectTransform;
            if (contentRect != null)
            {
                float textHeight = descriptionText.preferredHeight;
                float viewportHeight = 120f;
                if (descriptionScrollRect != null && descriptionScrollRect.viewport != null)
                {
                    viewportHeight = descriptionScrollRect.viewport.rect.height;
                    if (viewportHeight <= 1f) viewportHeight = 120f;
                }

                float totalHeight = Mathf.Max(textHeight + 20f, viewportHeight);
                contentRect.sizeDelta = new Vector2(0f, totalHeight);
                descriptionText.rectTransform.sizeDelta = new Vector2(0f, totalHeight);

                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }

            if (descriptionScrollRect != null)
            {
                descriptionScrollRect.verticalNormalizedPosition = 1f; // Always reset to top when changing topics
            }
        }

        // Narration Audio Setup
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = data.narrationClip;
        }

        // Video Player Setup
        WireVideoTriggerArea();
        EnsureVideoPanelLayout();
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = data.videoClip;
            videoPlayer.isLooping = false;
        }

        if (holdToPlayText != null)
        {
            bool hasVideo = data.videoClip != null;
            holdToPlayText.gameObject.SetActive(hasVideo);
            if (hasVideo)
            {
                holdToPlayText.text = "✦ Tatap gambar untuk mainkan video langsung ✦";
            }
        }

        // Rule 1: Always start off hiding the video panel and revealing static image
        ResetMediaToImageState();
        UpdatePlayPauseIcons();
    }

    /// <summary>
    /// Fallback loader: ensures photo Sprite and VideoClip are loaded from Resources or
    /// AssetDatabase if serialized asset references are null or failed to deserialize.
    /// </summary>
    private void EnsureMediaLoaded(HistoryData data)
    {
        if (data == null) return;

        bool hasValidPhoto = false;
        if (data.images != null && data.images.Length > 0 && data.images[0].sprite != null)
        {
            try { hasValidPhoto = data.images[0].sprite.texture != null; } catch { hasValidPhoto = false; }
        }
        if (!hasValidPhoto && data.displaySprite != null)
        {
            try { hasValidPhoto = data.displaySprite.texture != null; } catch { hasValidPhoto = false; }
        }

        bool hasValidVideo = data.videoClip != null;

        if (!hasValidPhoto || !hasValidVideo)
        {
            string id = (data.historyId ?? "").ToLower();
            string name = (data.name ?? "").ToLower();
            string title = (data.eventTitle ?? "").ToLower();

            if (!hasValidPhoto)
            {
                Sprite loadedSprite = null;

                if (id.Contains("zaman") || name.Contains("zaman") || title.Contains("zaman"))
                {
                    loadedSprite = Resources.Load<Sprite>("MuseumData/DataSejarah/Media/ZamanPenjajahan/Zaman Penjajahan");
                    if (loadedSprite == null) loadedSprite = Resources.Load<Sprite>("Media/ZamanPenjajahan/Zaman Penjajahan");
                    if (loadedSprite == null)
                    {
                        Texture2D tex = Resources.Load<Texture2D>("MuseumData/DataSejarah/Media/ZamanPenjajahan/Zaman Penjajahan");
                        if (tex == null) tex = Resources.Load<Texture2D>("Media/ZamanPenjajahan/Zaman Penjajahan");
                        if (tex != null) loadedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
#if UNITY_EDITOR
                    if (loadedSprite == null) loadedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/Artifak Photo/Zaman Penjajahan.jpg");
                    if (loadedSprite == null)
                    {
                        Texture2D edTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Asset/Artifak Photo/Zaman Penjajahan.jpg");
                        if (edTex != null) loadedSprite = Sprite.Create(edTex, new Rect(0, 0, edTex.width, edTex.height), new Vector2(0.5f, 0.5f));
                    }
#endif
                }
                else if (id.Contains("tani") || name.Contains("tani") || title.Contains("tani") || id.Contains("pemberontakan") || title.Contains("pemberontakan"))
                {
                    loadedSprite = Resources.Load<Sprite>("MuseumData/DataSejarah/Media/PemberontakanTani/Pemberontakan Tani");
                    if (loadedSprite == null) loadedSprite = Resources.Load<Sprite>("Media/PemberontakanTani/Pemberontakan Tani");
                    if (loadedSprite == null)
                    {
                        Texture2D tex = Resources.Load<Texture2D>("MuseumData/DataSejarah/Media/PemberontakanTani/Pemberontakan Tani");
                        if (tex == null) tex = Resources.Load<Texture2D>("Media/PemberontakanTani/Pemberontakan Tani");
                        if (tex != null) loadedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
#if UNITY_EDITOR
                    if (loadedSprite == null) loadedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/Artifak Photo/Pemberontakan Tani.jpg");
                    if (loadedSprite == null)
                    {
                        Texture2D edTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Asset/Artifak Photo/Pemberontakan Tani.jpg");
                        if (edTex != null) loadedSprite = Sprite.Create(edTex, new Rect(0, 0, edTex.width, edTex.height), new Vector2(0.5f, 0.5f));
                    }
#endif
                }
                else if (id.Contains("megat") || name.Contains("megat") || title.Contains("megat"))
                {
                    loadedSprite = Resources.Load<Sprite>("MuseumData/DataSejarah/Media/MegatPanjiAlam/eFOTO-EF-260812-4E656F-24351");
                    if (loadedSprite == null) loadedSprite = Resources.Load<Sprite>("Media/MegatPanjiAlam/eFOTO-EF-260812-4E656F-24351");
                    if (loadedSprite == null)
                    {
                        Texture2D tex = Resources.Load<Texture2D>("MuseumData/DataSejarah/Media/MegatPanjiAlam/eFOTO-EF-260812-4E656F-24351");
                        if (tex == null) tex = Resources.Load<Texture2D>("Media/MegatPanjiAlam/eFOTO-EF-260812-4E656F-24351");
                        if (tex != null) loadedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
#if UNITY_EDITOR
                    if (loadedSprite == null) loadedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/Artifak Photo/Pembunuhan Megat Panji Alam/eFOTO-EF-260812-4E656F-24351.jpg");
                    if (loadedSprite == null)
                    {
                        Texture2D edTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Asset/Artifak Photo/Pembunuhan Megat Panji Alam/eFOTO-EF-260812-4E656F-24351.jpg");
                        if (edTex != null) loadedSprite = Sprite.Create(edTex, new Rect(0, 0, edTex.width, edTex.height), new Vector2(0.5f, 0.5f));
                    }
#endif
                }
                else if (id.Contains("infrastruktur") || name.Contains("infrastruktur") || title.Contains("infrastruktur") || id.Contains("pembangunan") || title.Contains("pembangunan"))
                {
                    System.Collections.Generic.List<HistoryImage> imgList = new System.Collections.Generic.List<HistoryImage>();
                    for (int i = 1; i <= 4; i++)
                    {
                        string resPath = $"MuseumData/DataSejarah/Media/InfrastrukturPembangunan/Infrastruktur & Pembangunan Terengganu {i}";
                        string resPath2 = $"Media/InfrastrukturPembangunan/Infrastruktur & Pembangunan Terengganu {i}";
                        string edPath = $"Assets/Asset/Artifak Photo/Infrastruktur & Pembangunan Terengganu {i}.jpg";

                        Sprite s = Resources.Load<Sprite>(resPath);
                        if (s == null) s = Resources.Load<Sprite>(resPath2);
                        if (s == null)
                        {
                            Texture2D tex = Resources.Load<Texture2D>(resPath);
                            if (tex == null) tex = Resources.Load<Texture2D>(resPath2);
                            if (tex != null) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        }
#if UNITY_EDITOR
                        if (s == null) s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(edPath);
                        if (s == null)
                        {
                            Texture2D edTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(edPath);
                            if (edTex != null) s = Sprite.Create(edTex, new Rect(0, 0, edTex.width, edTex.height), new Vector2(0.5f, 0.5f));
                        }
#endif
                        if (s != null)
                        {
                            imgList.Add(new HistoryImage { sprite = s, caption = $"Infrastruktur & Pembangunan Terengganu {i}" });
                        }
                    }
                    if (imgList.Count > 0)
                    {
                        data.images = imgList.ToArray();
                        data.displaySprite = imgList[0].sprite;
                        loadedSprite = imgList[0].sprite;
                    }
                }
                else if (id.Contains("ekonomi") || name.Contains("ekonomi") || title.Contains("ekonomi"))
                {
                    System.Collections.Generic.List<HistoryImage> imgList = new System.Collections.Generic.List<HistoryImage>();
                    for (int i = 1; i <= 3; i++)
                    {
                        string resPath = $"MuseumData/DataSejarah/Media/Ekonomi/Ekonomi Terengganu {i}";
                        string resPath2 = $"Media/Ekonomi/Ekonomi Terengganu {i}";
                        string edPath = $"Assets/Asset/Artifak Photo/Ekonomi Terengganu {i}.jpg";

                        Sprite s = Resources.Load<Sprite>(resPath);
                        if (s == null) s = Resources.Load<Sprite>(resPath2);
                        if (s == null)
                        {
                            Texture2D tex = Resources.Load<Texture2D>(resPath);
                            if (tex == null) tex = Resources.Load<Texture2D>(resPath2);
                            if (tex != null) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        }
#if UNITY_EDITOR
                        if (s == null) s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(edPath);
                        if (s == null)
                        {
                            Texture2D edTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(edPath);
                            if (edTex != null) s = Sprite.Create(edTex, new Rect(0, 0, edTex.width, edTex.height), new Vector2(0.5f, 0.5f));
                        }
#endif
                        if (s != null)
                        {
                            imgList.Add(new HistoryImage { sprite = s, caption = $"Ekonomi Terengganu {i}" });
                        }
                    }
                    if (imgList.Count > 0)
                    {
                        data.images = imgList.ToArray();
                        data.displaySprite = imgList[0].sprite;
                        loadedSprite = imgList[0].sprite;
                    }
                }

                if (loadedSprite != null)
                {
                    data.displaySprite = loadedSprite;
                    if (data.images == null || data.images.Length == 0 || data.images[0].sprite == null)
                    {
                        data.images = new HistoryImage[] { new HistoryImage { sprite = loadedSprite, caption = data.eventTitle } };
                    }
                    else
                    {
                        data.images[0].sprite = loadedSprite;
                    }
                }
            }

            if (!hasValidVideo && (id.Contains("megat") || name.Contains("megat") || title.Contains("megat")))
            {
                VideoClip loadedVideo = Resources.Load<VideoClip>("MuseumData/DataSejarah/Media/MegatPanjiAlam/PixVerse_V6_Image_Text_540P_Cinematic_animatio");
                if (loadedVideo == null) loadedVideo = Resources.Load<VideoClip>("Media/MegatPanjiAlam/PixVerse_V6_Image_Text_540P_Cinematic_animatio");
#if UNITY_EDITOR
                if (loadedVideo == null)
                {
                    loadedVideo = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Asset/Artifak Photo/Pembunuhan Megat Panji Alam/PixVerse_V6_Image_Text_540P_Cinematic_animatio.mp4");
                }
#endif
                if (loadedVideo != null)
                {
                    data.videoClip = loadedVideo;
                }
            }
        }
    }

    /// <summary>
    /// Sets text at a smaller fixed size than whatever the field was authored with, plus
    /// word-wrapping so long topic titles/descriptions wrap instead of running past their
    /// box. Deliberately a FIXED size, not auto-sizing: some of this panel's text fields
    /// have degenerate RectTransform bounds (same issue found on the list panel's item
    /// buttons), and TMP's shrink-to-fit auto-sizing collapses to invisible text against a
    /// box like that instead of just fixing the overflow.
    /// </summary>
    private static void SetSmallerText(TMP_Text label, string text, float fontSize)
    {
        label.text = text;
        label.enableAutoSizing = false;
        label.fontSize = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>
    /// Same as SetSmallerText, but also caps title/label fields at 2 wrapped lines,
    /// ellipsising anything beyond that ("Infrastruktur &amp; Pembangunan Terengganu…").
    /// Deliberately does NOT use TMP's maxVisibleLines/Ellipsis overflow mode - both need a
    /// real positive box HEIGHT to know how much text fits, and this panel's text fields
    /// have a degenerate (negative/near-zero) height baked into the source prefab, which
    /// made maxVisibleLines compute "zero lines fit" and hide the text completely. Instead
    /// this measures the WIDTH-based wrap result directly (ForceMeshUpdate + lineCount,
    /// which don't care about box height) and manually truncates the string to the first 2
    /// lines' worth of characters, so it can never disappear regardless of box height.
    /// </summary>
    private static void SetCappedTwoLineText(TMP_Text label, string text, float fontSize)
    {
        label.enableAutoSizing = false;
        label.fontSize = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
        label.maxVisibleLines = int.MaxValue;
        label.text = text ?? "";

        label.ForceMeshUpdate();
        TMP_TextInfo info = label.textInfo;
        if (info != null && info.lineCount > 2 && !string.IsNullOrEmpty(text))
        {
            int lastCharOfSecondLine = info.lineInfo[1].lastCharacterIndex;
            string clipped = text.Substring(0, Mathf.Clamp(lastCharOfSecondLine + 1, 0, text.Length)).TrimEnd();
            if (clipped.Length > 1) clipped = clipped.Substring(0, clipped.Length - 1).TrimEnd();
            label.text = clipped + "…";
            label.ForceMeshUpdate();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trigger Hold / Gaze-Stare Video Logic & Smooth Crossfade Transition
    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureCanvasGroups()
    {
        if (photoScrollRect != null && photoScrollRect.gameObject != null && photoCanvasGroup == null)
        {
            photoCanvasGroup = photoScrollRect.GetComponent<CanvasGroup>();
            if (photoCanvasGroup == null)
            {
                photoCanvasGroup = photoScrollRect.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (displayImage != null && displayImageCanvasGroup == null)
        {
            displayImageCanvasGroup = displayImage.GetComponent<CanvasGroup>();
            if (displayImageCanvasGroup == null)
            {
                displayImageCanvasGroup = displayImage.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (videoPanel != null && videoPanelCanvasGroup == null)
        {
            videoPanelCanvasGroup = videoPanel.GetComponent<CanvasGroup>();
            if (videoPanelCanvasGroup == null)
            {
                videoPanelCanvasGroup = videoPanel.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (holdToPlayText != null && holdHintCanvasGroup == null)
        {
            holdHintCanvasGroup = holdToPlayText.GetComponent<CanvasGroup>();
            if (holdHintCanvasGroup == null)
            {
                holdHintCanvasGroup = holdToPlayText.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void EnsureVideoPanelLayout()
    {
        AutoWireMediaFields();

        Transform parentSlot = null;
        if (photoScrollRect != null) parentSlot = photoScrollRect.transform.parent;
        else if (displayImage != null) parentSlot = displayImage.transform.parent;
        if (parentSlot == null) parentSlot = mediaSlotTransform != null ? mediaSlotTransform : transform;

        if (videoPanel == null)
        {
            GameObject vGo = new GameObject("VideoPanel");
            vGo.transform.SetParent(parentSlot, false);
            videoPanel = vGo;
        }
        else if (videoPanel.transform.parent != parentSlot)
        {
            videoPanel.transform.SetParent(parentSlot, false);
        }

        // Align VideoPanel RectTransform with PhotoScrollView exactly
        RectTransform vRt = videoPanel.GetComponent<RectTransform>();
        if (vRt == null) vRt = videoPanel.AddComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0.02f, 0.02f);
        vRt.anchorMax = new Vector2(0.98f, 0.98f);
        vRt.anchoredPosition = Vector2.zero;
        vRt.sizeDelta = Vector2.zero;
        vRt.offsetMin = new Vector2(0f, 16f); // match photo viewport above scrollbar
        vRt.offsetMax = Vector2.zero;
        vRt.pivot = new Vector2(0.5f, 0.5f);
        vRt.localScale = Vector3.one;
        vRt.localRotation = Quaternion.identity;

        // Ensure displayVideoRawImage fills VideoPanel
        if (displayVideoRawImage == null)
        {
            GameObject rawGo = new GameObject("DisplayVideoRawImage");
            rawGo.transform.SetParent(videoPanel.transform, false);
            displayVideoRawImage = rawGo.AddComponent<RawImage>();
        }
        else if (displayVideoRawImage.transform.parent != videoPanel.transform)
        {
            displayVideoRawImage.transform.SetParent(videoPanel.transform, false);
        }

        RectTransform rRt = displayVideoRawImage.rectTransform;
        rRt.anchorMin = Vector2.zero;
        rRt.anchorMax = Vector2.one;
        rRt.anchoredPosition = Vector2.zero;
        rRt.sizeDelta = Vector2.zero;
        rRt.pivot = new Vector2(0.5f, 0.5f);
        rRt.localScale = Vector3.one;
        rRt.localRotation = Quaternion.identity;

        // Connect RenderTexture
        RenderTexture videoRT = videoPlayer != null && videoPlayer.targetTexture != null ? videoPlayer.targetTexture : Resources.Load<RenderTexture>("SejarahVideo/Video");
        if (videoPlayer != null && videoRT != null)
        {
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRT;
        }
        if (displayVideoRawImage != null && videoRT != null)
        {
            displayVideoRawImage.texture = videoRT;
            displayVideoRawImage.color = Color.white;
            displayVideoRawImage.enabled = true;
        }

        // Put video panel in front of photo viewport so it crossfades smoothly on top
        videoPanel.transform.SetAsLastSibling();

        EnsureCanvasGroups();
    }

    public void OnPointerEnterTrigger()
    {
        if (activeHistoryData == null || activeHistoryData.videoClip == null) return;
        if (isVideoPlaying) return;

        isStaringOrHolding = true;
        if (holdCheckCoroutine != null) StopCoroutine(holdCheckCoroutine);
        holdCheckCoroutine = StartCoroutine(StareTimerCoroutine());
    }

    public void OnPointerExitTrigger()
    {
        if (!isVideoPlaying)
        {
            isStaringOrHolding = false;
            if (holdCheckCoroutine != null)
            {
                StopCoroutine(holdCheckCoroutine);
                holdCheckCoroutine = null;
            }
        }
    }

    public void OnPointerDownTrigger()
    {
        if (activeHistoryData == null || activeHistoryData.videoClip == null) return;
        if (isVideoPlaying) return;

        isStaringOrHolding = true;
        if (holdCheckCoroutine != null) StopCoroutine(holdCheckCoroutine);
        holdCheckCoroutine = StartCoroutine(StareTimerCoroutine());
    }

    public void OnPointerUpTrigger()
    {
        if (!isVideoPlaying)
        {
            isStaringOrHolding = false;
            if (holdCheckCoroutine != null)
            {
                StopCoroutine(holdCheckCoroutine);
                holdCheckCoroutine = null;
            }
        }
    }

    private IEnumerator StareTimerCoroutine()
    {
        yield return new WaitForSeconds(1.5f);

        if (isStaringOrHolding && activeHistoryData != null && activeHistoryData.videoClip != null && !isVideoPlaying)
        {
            PlayVideoClipWithTransition();
        }
    }

    private void PlayVideoClipWithTransition()
    {
        if (activeHistoryData == null || activeHistoryData.videoClip == null) return;

        isVideoPlaying = true;
        currentGazeTimer = 0f;
        SetGalleryNavVisible(false);

        EnsureVideoPanelLayout();
        EnsureCanvasGroups();

        if (videoPanel != null)
        {
            videoPanel.SetActive(true);
            videoPanel.transform.SetAsLastSibling();
        }

        if (displayVideoRawImage != null)
        {
            if (displayVideoRawImage.texture == null)
            {
                RenderTexture rt = (videoPlayer != null && videoPlayer.targetTexture != null) ? videoPlayer.targetTexture : Resources.Load<RenderTexture>("SejarahVideo/Video");
                if (rt != null) displayVideoRawImage.texture = rt;
            }
            displayVideoRawImage.color = Color.white;
            displayVideoRawImage.enabled = true;
        }

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = activeHistoryData.videoClip;
            videoPlayer.time = 0;
            videoPlayer.Play();
        }

        if (videoMonitorCoroutine != null) StopCoroutine(videoMonitorCoroutine);
        videoMonitorCoroutine = StartCoroutine(VideoMonitorCoroutine());

        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        crossfadeCoroutine = StartCoroutine(CrossfadeLivePhotoCoroutine(toVideo: true, duration: 0.65f, () =>
        {
            if (photoScrollRect != null) photoScrollRect.gameObject.SetActive(false);
            if (displayImage != null) displayImage.gameObject.SetActive(false);
            if (noImageTextObj != null) noImageTextObj.SetActive(false);
        }));
    }

    private IEnumerator VideoMonitorCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        while (isVideoPlaying && videoPlayer != null && (videoPlayer.isPlaying || videoPlayer.isPrepared))
        {
            if (videoPlayer.length > 0 && videoPlayer.time >= videoPlayer.length - 0.2f)
            {
                OnVideoLoopPointReached(videoPlayer);
                yield break;
            }
            yield return null;
        }
    }

    public void ResetMediaToImageState(bool animate = false)
    {
        isHoldingTrigger = false;
        isStaringOrHolding = false;
        isVideoPlaying = false;
        currentGazeTimer = 0f;

        if (holdCheckCoroutine != null)
        {
            StopCoroutine(holdCheckCoroutine);
            holdCheckCoroutine = null;
        }

        if (videoMonitorCoroutine != null)
        {
            StopCoroutine(videoMonitorCoroutine);
            videoMonitorCoroutine = null;
        }

        EnsureCanvasGroups();

        if (animate && videoPanel != null && videoPanel.activeSelf)
        {
            if (photoScrollRect != null)
            {
                photoScrollRect.gameObject.SetActive(true);
            }
            if (displayImage != null)
            {
                displayImage.gameObject.SetActive(true);
            }

            if (photoCanvasGroup != null) photoCanvasGroup.alpha = 0f;
            if (displayImageCanvasGroup != null) displayImageCanvasGroup.alpha = 0f;
            SetGalleryNavVisible(false);

            if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = StartCoroutine(CrossfadeLivePhotoCoroutine(toVideo: false, duration: 0.65f, () =>
            {
                if (videoPlayer != null && videoPlayer.isPlaying)
                {
                    videoPlayer.Stop();
                }
                if (videoPanel != null) videoPanel.SetActive(false);
                UpdateImageUI();
            }));
        }
        else
        {
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
                crossfadeCoroutine = null;
            }

            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }

            if (videoPanel != null) videoPanel.SetActive(false);
            if (videoPanelCanvasGroup != null) videoPanelCanvasGroup.alpha = 0f;

            if (photoScrollRect != null) photoScrollRect.gameObject.SetActive(true);
            if (photoCanvasGroup != null) photoCanvasGroup.alpha = 1f;
            if (displayImageCanvasGroup != null) displayImageCanvasGroup.alpha = 1f;
            if (holdHintCanvasGroup != null) holdHintCanvasGroup.alpha = 1f;

            UpdateImageUI();
        }
    }

    private IEnumerator CrossfadeLivePhotoCoroutine(bool toVideo, float duration, Action onComplete = null)
    {
        float elapsed = 0f;

        // Start alphas
        float startPhotoAlpha = toVideo ? 1f : 0f;
        float targetPhotoAlpha = toVideo ? 0f : 1f;

        float startVideoAlpha = toVideo ? 0f : 1f;
        float targetVideoAlpha = toVideo ? 1f : 0f;

        // Ensure targets active before crossfading
        if (toVideo)
        {
            if (videoPanel != null) videoPanel.SetActive(true);
            if (videoPanelCanvasGroup != null) videoPanelCanvasGroup.alpha = 0f;
        }
        else
        {
            if (photoScrollRect != null) photoScrollRect.gameObject.SetActive(true);
            if (photoCanvasGroup != null) photoCanvasGroup.alpha = 0f;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            float curPhotoAlpha = Mathf.Lerp(startPhotoAlpha, targetPhotoAlpha, smoothT);
            float curVideoAlpha = Mathf.Lerp(startVideoAlpha, targetVideoAlpha, smoothT);

            if (photoCanvasGroup != null) photoCanvasGroup.alpha = curPhotoAlpha;
            if (displayImageCanvasGroup != null) displayImageCanvasGroup.alpha = curPhotoAlpha;
            if (videoPanelCanvasGroup != null) videoPanelCanvasGroup.alpha = curVideoAlpha;
            if (holdHintCanvasGroup != null) holdHintCanvasGroup.alpha = curPhotoAlpha;

            yield return null;
        }

        if (photoCanvasGroup != null) photoCanvasGroup.alpha = targetPhotoAlpha;
        if (displayImageCanvasGroup != null) displayImageCanvasGroup.alpha = targetPhotoAlpha;
        if (videoPanelCanvasGroup != null) videoPanelCanvasGroup.alpha = targetVideoAlpha;
        if (holdHintCanvasGroup != null) holdHintCanvasGroup.alpha = targetPhotoAlpha;

        onComplete?.Invoke();
        crossfadeCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Photo Gallery (multiple photos, or none, per topic)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The effective photo list for the active topic: the new 'images' array when it has
    /// entries, else the single legacy 'displaySprite' wrapped as a one-photo gallery, else
    /// empty (topic has no photo at all - a fully supported, expected case).
    /// </summary>
    private HistoryImage[] GetCurrentImages()
    {
        if (activeHistoryData == null) return System.Array.Empty<HistoryImage>();
        EnsureMediaLoaded(activeHistoryData);

        if (activeHistoryData.images != null && activeHistoryData.images.Length > 0)
        {
            System.Collections.Generic.List<HistoryImage> validList = new System.Collections.Generic.List<HistoryImage>();
            foreach (var img in activeHistoryData.images)
            {
                if (img.sprite != null)
                {
                    try
                    {
                        if (img.sprite.texture != null) validList.Add(img);
                    }
                    catch { }
                }
            }
            if (validList.Count > 0) return validList.ToArray();
        }

        if (activeHistoryData.displaySprite != null)
        {
            try
            {
                if (activeHistoryData.displaySprite.texture != null)
                {
                    return new[] { new HistoryImage { sprite = activeHistoryData.displaySprite, caption = activeHistoryData.eventTitle } };
                }
            }
            catch { }
        }

        return System.Array.Empty<HistoryImage>();
    }

    // Gallery buttons carry both a UGUI Button and an XRButtonSelection (for hand-ray/poke
    // support, same as every other button in the app) - a single pinch can fire both paths
    // for one press, which without a guard skips an extra photo. Debounce each direction
    // independently, matching the fix already applied to MiniGameMenuPanel's Next/Previous.
    private const float GalleryNavDebounceSeconds = 0.25f;
    private float lastNextImageTime = -10f;
    private float lastPreviousImageTime = -10f;

    private void UpdateImageUI()
    {
        // Handled dynamically by PopulatePhotoScrollView
        if (activeHistoryData != null)
        {
            PopulatePhotoScrollView(activeHistoryData);
        }
    }

    private void SetGalleryNavVisible(bool visible)
    {
        if (previousImageButton != null) previousImageButton.gameObject.SetActive(false);
        if (nextImageButton != null) nextImageButton.gameObject.SetActive(false);
    }

    private static Button BuildChevronButton(Transform parent, string name, string glyph, Vector2 anchor, Vector2 offset)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = new Vector2(36f, 44f);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.4f);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(36f, 44f, 10f);

        XRButtonSelection xr = go.AddComponent<XRButtonSelection>();
        xr.buttonImage = img;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject labelGo = new GameObject("Glyph");
        labelGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = glyph;
        label.fontSize = 24f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        return btn;
    }

    private static Transform FindDeepChildTransform(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name == childName) return t;
        }
        return null;
    }

    private void OnVideoLoopPointReached(VideoPlayer source)
    {
        // Smoothly return to photo when video finishes
        ResetMediaToImageState(animate: true);
    }

    private void WireVideoTriggerArea()
    {
        AutoWireMediaFields();

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            videoPlayer.loopPointReached += OnVideoLoopPointReached;
        }
    }

    private void AutoWireMediaFields()
    {
        if (displayImage == null)
        {
            Transform t = FindDeepChildTransform(transform, "DisplayImage");
            if (t != null) displayImage = t.GetComponent<Image>();
            if (displayImage == null)
            {
                Image[] imgs = GetComponentsInChildren<Image>(true);
                foreach (var img in imgs)
                {
                    if (img.gameObject.name.ToLower().Contains("display") || img.gameObject.name.ToLower().Contains("photo") || img.gameObject.name.ToLower().Contains("image"))
                    {
                        displayImage = img;
                        break;
                    }
                }
            }
        }

        if (videoPanel == null)
        {
            Transform t = FindDeepChildTransform(transform, "VideoPanel") ?? FindDeepChildTransform(transform, "VideoContainer") ?? FindDeepChildTransform(transform, "Video");
            if (t != null) videoPanel = t.gameObject;
        }

        if (displayVideoRawImage == null)
        {
            if (videoPanel != null) displayVideoRawImage = videoPanel.GetComponentInChildren<RawImage>(true);
            if (displayVideoRawImage == null) displayVideoRawImage = GetComponentInChildren<RawImage>(true);
        }

        if (videoPanel == null && displayVideoRawImage != null)
        {
            videoPanel = displayVideoRawImage.gameObject;
        }

        if (videoPlayer == null)
        {
            if (videoPanel != null) videoPlayer = videoPanel.GetComponentInChildren<VideoPlayer>(true);
            if (videoPlayer == null) videoPlayer = GetComponentInChildren<VideoPlayer>(true);
        }

        // Connect the existing project RenderTexture (SejarahVideo/Video.renderTexture)
        RenderTexture videoRT = null;
        if (videoPlayer != null && videoPlayer.targetTexture != null)
        {
            videoRT = videoPlayer.targetTexture;
        }
        else
        {
            videoRT = Resources.Load<RenderTexture>("SejarahVideo/Video");
            if (videoPlayer != null && videoRT != null)
            {
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                videoPlayer.targetTexture = videoRT;
            }
        }

        if (displayVideoRawImage != null && videoRT != null)
        {
            displayVideoRawImage.texture = videoRT;
            displayVideoRawImage.color = Color.white;
            displayVideoRawImage.enabled = true;
        }

        if (holdToPlayText == null)
        {
            Transform t = FindDeepChildTransform(transform, "HoldToPlayText") ?? FindDeepChildTransform(transform, "HoldText") ?? FindDeepChildTransform(transform, "VideoHint");
            if (t != null) holdToPlayText = t.GetComponent<TMP_Text>();
        }

        if (noImageTextObj == null)
        {
            Transform t = FindDeepChildTransform(transform, "NoImageTextObj") ?? FindDeepChildTransform(transform, "NoImageText");
            if (t != null) noImageTextObj = t.gameObject;
        }

        if (videoTriggerArea == null)
        {
            Transform t = FindDeepChildTransform(transform, "VideoTriggerArea");
            if (t != null) videoTriggerArea = t.GetComponent<BoxCollider>();
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

    private bool wasAudioPlaying = false;

    private void UpdatePlayPauseIcons()
    {
        bool isPlaying = audioSource != null && audioSource.isPlaying;

        if (playIconObj != null) playIconObj.SetActive(!isPlaying);
        if (pauseIconObj != null) pauseIconObj.SetActive(isPlaying);

        if (isPlaying != wasAudioPlaying)
        {
            wasAudioPlaying = isPlaying;
            BGMManager.Instance?.SetDucked(isPlaying);
        }
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
        // Clears the dragger's "user moved" bookkeeping since this call IS the deliberate
        // reset-to-in-front-of-player action - matches Artifact.cs.PositionInFrontOfUser.
        ArtifactPanelDragger dragger = GetComponent<ArtifactPanelDragger>();
        if (dragger != null) dragger.ResetUserMoved();

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

    public void PositionInFrontOfUser()
    {
        PositionInFrontOfPlayer();
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
/// Handles smooth snapping to each full-mode photo page so the picture never stops halfway between 2 images.
/// </summary>
public class PhotoSnapScroller : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IPointerUpHandler
{
    public ScrollRect scrollRect;
    public int totalPages = 1;
    public int currentPage = 0;
    private Coroutine snapCoroutine;
    private bool isDragging = false;

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        if (snapCoroutine != null)
        {
            StopCoroutine(snapCoroutine);
            snapCoroutine = null;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        SnapToNearestPage();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
        {
            SnapToNearestPage();
        }
    }

    public void SnapToNearestPage()
    {
        if (totalPages <= 1 || scrollRect == null) return;

        float pos = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition);
        float step = 1f / (totalPages - 1);
        int targetPage = Mathf.RoundToInt(pos / step);
        targetPage = Mathf.Clamp(targetPage, 0, totalPages - 1);
        currentPage = targetPage;

        float targetPos = targetPage * step;
        if (snapCoroutine != null) StopCoroutine(snapCoroutine);
        snapCoroutine = StartCoroutine(LerpToPage(targetPos));
    }

    private IEnumerator LerpToPage(float targetPos)
    {
        float startPos = scrollRect.horizontalNormalizedPosition;
        float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (isDragging) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(startPos, targetPos, t);
            yield return null;
        }

        scrollRect.horizontalNormalizedPosition = targetPos;
        snapCoroutine = null;
    }
}

/// <summary>
/// Catches pointer release events on the horizontal scrollbar to trigger page snapping.
/// </summary>
public class ScrollbarSnapHook : MonoBehaviour, IPointerUpHandler
{
    public PhotoSnapScroller snapScroller;

    public void OnPointerUp(PointerEventData eventData)
    {
        snapScroller?.SnapToNearestPage();
    }
}

