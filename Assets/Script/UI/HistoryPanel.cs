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
    private const float RequiredGazeDuration = 2.0f;
    private CanvasGroup displayImageCanvasGroup;
    private CanvasGroup videoPanelCanvasGroup;
    private Coroutine crossfadeCoroutine;
    private Coroutine videoMonitorCoroutine;
    private int currentImageIndex = 0;
    private GameObject builtGalleryNavRoot;
    private ScrollRect descriptionScrollRect;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // This is a DETAIL view (one topic's full content) - like the Artifact Detail
        // Panel, it must stay fixed in world space and be movable by the player, not get
        // dragged around by the wrist. It was placed under ExplorationCanvas in the Editor
        // (alongside the room/topic LIST panels, which SHOULD follow the wrist), and
        // WristWatch.LateUpdate drags that whole canvas to the wrist every frame - its only
        // exemption checks for child names containing "ArtifactDetail"/"ArtifactUI", which
        // this panel doesn't match. Same fix WristWatch already uses for its own watch
        // button: detach at runtime so nothing above can drag it around.
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        // ExplorationCanvas (the panel's former parent) was the only Canvas in its
        // ancestry - detaching leaves it with nothing to render/raycast through unless it
        // gets its own, exactly like the standalone ArtifactPanelPrefab does.
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
        EnsureGalleryNavUI();
        EnsureGrabbablePanel();
        EnsureDescriptionScrollView();
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

        if (displayImage == null || !displayImage.gameObject.activeInHierarchy) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray headRay = new Ray(cam.transform.position, cam.transform.forward);

        // Check against videoTriggerArea BoxCollider
        if (videoTriggerArea != null && videoTriggerArea.Raycast(headRay, out _, 4f))
        {
            return true;
        }

        // Check against displayImage BoxCollider
        BoxCollider imgBox = displayImage.GetComponent<BoxCollider>();
        if (imgBox != null && imgBox.Raycast(headRay, out _, 4f))
        {
            return true;
        }

        // Check against displayImage RectTransform plane in world space
        RectTransform rect = displayImage.rectTransform;
        if (rect != null)
        {
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

            if (id.Contains("megat") || name.Contains("megat") || title.Contains("megat"))
            {
                if (!hasValidPhoto)
                {
                    Sprite loadedSprite = null;

                    // 1. Load Sprite directly from Resources
                    loadedSprite = Resources.Load<Sprite>("MuseumData/DataSejarah/Media/MegatPanjiAlam/eFOTO-EF-260812-4E656F-24351");
                    if (loadedSprite == null) loadedSprite = Resources.Load<Sprite>("Media/MegatPanjiAlam/eFOTO-EF-260812-4E656F-24351");

                    // 2. Load Texture2D from Resources and dynamically create Sprite
                    if (loadedSprite == null)
                    {
                        Texture2D tex = Resources.Load<Texture2D>("MuseumData/DataSejarah/Media/MegatPanjiAlam/eFOTO-EF-260812-4E656F-24351");
                        if (tex == null) tex = Resources.Load<Texture2D>("Media/MegatPanjiAlam/eFOTO-EF-260812-4E656F-24351");
                        if (tex != null)
                        {
                            loadedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        }
                    }

#if UNITY_EDITOR
                    // 3. Load from AssetDatabase in Unity Editor
                    if (loadedSprite == null)
                    {
                        loadedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/Artifak Photo/Pembunuhan Megat Panji Alam/eFOTO-EF-260812-4E656F-24351.jpg");
                    }
                    if (loadedSprite == null)
                    {
                        Texture2D edTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Asset/Artifak Photo/Pembunuhan Megat Panji Alam/eFOTO-EF-260812-4E656F-24351.jpg");
                        if (edTex != null)
                        {
                            loadedSprite = Sprite.Create(edTex, new Rect(0, 0, edTex.width, edTex.height), new Vector2(0.5f, 0.5f));
                        }
                    }
#endif

                    if (loadedSprite != null)
                    {
                        data.displaySprite = loadedSprite;
                        data.images = new HistoryImage[] { new HistoryImage { sprite = loadedSprite, caption = "" } };
                    }
                }

                if (!hasValidVideo)
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
        yield return new WaitForSeconds(2.0f);

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
        EnsureCanvasGroups();

        AutoWireMediaFields();

        if (videoPanel != null)
        {
            videoPanel.SetActive(true);
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
        crossfadeCoroutine = StartCoroutine(CrossfadeMediaCoroutine(displayImageCanvasGroup, videoPanelCanvasGroup, 0.6f, () =>
        {
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
            if (displayImage != null) displayImage.gameObject.SetActive(true);
            if (displayImageCanvasGroup != null) displayImageCanvasGroup.alpha = 0f;
            UpdateImageUI();
            SetGalleryNavVisible(false);

            if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = StartCoroutine(CrossfadeMediaCoroutine(videoPanelCanvasGroup, displayImageCanvasGroup, 0.6f, () =>
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
            if (displayImageCanvasGroup != null) displayImageCanvasGroup.alpha = 1f;
            if (videoPanelCanvasGroup != null) videoPanelCanvasGroup.alpha = 0f;

            UpdateImageUI();
        }
    }

    private IEnumerator CrossfadeMediaCoroutine(CanvasGroup fadeOutGroup, CanvasGroup fadeInGroup, float duration, Action onComplete = null)
    {
        float elapsed = 0f;

        if (fadeInGroup != null)
        {
            fadeInGroup.gameObject.SetActive(true);
        }

        float startFadeOutAlpha = fadeOutGroup != null ? fadeOutGroup.alpha : 1f;
        float startFadeInAlpha = fadeInGroup != null ? fadeInGroup.alpha : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Smoothstep curve (ease-in-out)
            t = t * t * (3f - 2f * t);

            if (fadeOutGroup != null) fadeOutGroup.alpha = Mathf.Lerp(startFadeOutAlpha, 0f, t);
            if (fadeInGroup != null) fadeInGroup.alpha = Mathf.Lerp(startFadeInAlpha, 1f, t);

            yield return null;
        }

        if (fadeOutGroup != null) fadeOutGroup.alpha = 0f;
        if (fadeInGroup != null) fadeInGroup.alpha = 1f;

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
            if (activeHistoryData.images[0].sprite != null)
            {
                try
                {
                    if (activeHistoryData.images[0].sprite.texture != null)
                        return activeHistoryData.images;
                }
                catch { }
            }
        }

        if (activeHistoryData.displaySprite != null)
        {
            try
            {
                if (activeHistoryData.displaySprite.texture != null)
                {
                    return new[] { new HistoryImage { sprite = activeHistoryData.displaySprite, caption = "" } };
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

    public void ShowNextImage()
    {
        if (Time.unscaledTime - lastNextImageTime < GalleryNavDebounceSeconds) return;
        lastNextImageTime = Time.unscaledTime;

        HistoryImage[] images = GetCurrentImages();
        if (images.Length == 0) return;
        currentImageIndex = (currentImageIndex + 1) % images.Length;
        UpdateImageUI();
    }

    public void ShowPreviousImage()
    {
        if (Time.unscaledTime - lastPreviousImageTime < GalleryNavDebounceSeconds) return;
        lastPreviousImageTime = Time.unscaledTime;

        HistoryImage[] images = GetCurrentImages();
        if (images.Length == 0) return;
        currentImageIndex = (currentImageIndex - 1 + images.Length) % images.Length;
        UpdateImageUI();
    }

    /// <summary>
    /// Shows the current gallery photo XOR the "no image" placeholder - never both, and
    /// never a blank box. Gallery arrows/counter only appear when there's more than one
    /// photo to page through; a single photo (or zero) shows no navigation chrome at all.
    /// </summary>
    private void UpdateImageUI()
    {
        HistoryImage[] images = GetCurrentImages();
        bool hasImage = images.Length > 0 && currentImageIndex >= 0 && currentImageIndex < images.Length
                        && images[currentImageIndex].sprite != null;

        if (displayImage != null)
        {
            displayImage.gameObject.SetActive(hasImage);
            if (hasImage)
            {
                AspectRatioFitter fitter = displayImage.GetComponent<AspectRatioFitter>();
                if (fitter != null) Destroy(fitter);

                displayImage.sprite = images[currentImageIndex].sprite;
                displayImage.color = Color.white;
                displayImage.preserveAspect = true;
            }
        }
        if (noImageTextObj != null) noImageTextObj.SetActive(!hasImage);

        SetGalleryNavVisible(hasImage && images.Length > 1);
        if (imageCounterText != null && images.Length > 1)
        {
            imageCounterText.text = $"{currentImageIndex + 1} / {images.Length}";
        }
    }

    private void SetGalleryNavVisible(bool visible)
    {
        if (previousImageButton != null) previousImageButton.gameObject.SetActive(visible);
        if (nextImageButton != null) nextImageButton.gameObject.SetActive(visible);
        if (imageCounterText != null) imageCounterText.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Finds PreviousImageButton/NextImageButton/ImageCounterText by name if the scene
    /// already has them (so a designer can customise their look), otherwise builds small
    /// chevron buttons and a counter label next to the display image at runtime. Either
    /// way the gallery works without any scene wiring.
    /// </summary>
    private void EnsureGalleryNavUI()
    {
        if (previousImageButton == null)
        {
            Transform t = FindDeepChildTransform(transform, "PreviousImageButton");
            if (t != null) previousImageButton = t.GetComponent<Button>();
        }
        if (nextImageButton == null)
        {
            Transform t = FindDeepChildTransform(transform, "NextImageButton");
            if (t != null) nextImageButton = t.GetComponent<Button>();
        }
        if (imageCounterText == null)
        {
            Transform t = FindDeepChildTransform(transform, "ImageCounterText");
            if (t != null) imageCounterText = t.GetComponent<TMP_Text>();
        }
        if (previousImageButtonXR == null && previousImageButton != null)
        {
            previousImageButtonXR = previousImageButton.GetComponent<XRButtonSelection>();
        }
        if (nextImageButtonXR == null && nextImageButton != null)
        {
            nextImageButtonXR = nextImageButton.GetComponent<XRButtonSelection>();
        }

        if ((previousImageButton == null || nextImageButton == null) && displayImage != null)
        {
            BuildGalleryNavFallback();
        }

        WireButton(previousImageButton, previousImageButtonXR, ShowPreviousImage);
        WireButton(nextImageButton, nextImageButtonXR, ShowNextImage);
    }

    /// <summary>
    /// Small "‹"/"›" chevron buttons straddling the display image, plus a counter label
    /// under it. Parented next to the image (not inside it) so they don't get hidden along
    /// with it when the image itself is toggled off.
    /// </summary>
    private void BuildGalleryNavFallback()
    {
        Transform parent = displayImage.transform.parent != null ? displayImage.transform.parent : transform;

        builtGalleryNavRoot = new GameObject("GalleryNav");
        builtGalleryNavRoot.transform.SetParent(parent, false);
        RectTransform navRect = builtGalleryNavRoot.AddComponent<RectTransform>();
        RectTransform imgRect = displayImage.rectTransform;
        navRect.anchorMin = imgRect.anchorMin;
        navRect.anchorMax = imgRect.anchorMax;
        navRect.anchoredPosition = imgRect.anchoredPosition;
        navRect.sizeDelta = imgRect.sizeDelta;
        navRect.pivot = imgRect.pivot;

        if (previousImageButton == null)
        {
            previousImageButton = BuildChevronButton(builtGalleryNavRoot.transform, "PreviousImageButton", "‹",
                new Vector2(0f, 0.5f), new Vector2(-18f, 0f));
            previousImageButtonXR = previousImageButton.GetComponent<XRButtonSelection>();
        }
        if (nextImageButton == null)
        {
            nextImageButton = BuildChevronButton(builtGalleryNavRoot.transform, "NextImageButton", "›",
                new Vector2(1f, 0.5f), new Vector2(18f, 0f));
            nextImageButtonXR = nextImageButton.GetComponent<XRButtonSelection>();
        }
        if (imageCounterText == null)
        {
            GameObject counterGo = new GameObject("ImageCounterText");
            counterGo.transform.SetParent(builtGalleryNavRoot.transform, false);
            imageCounterText = counterGo.AddComponent<TextMeshProUGUI>();
            imageCounterText.fontSize = 16f;
            imageCounterText.alignment = TextAlignmentOptions.Center;
            imageCounterText.color = new Color(1f, 1f, 1f, 0.85f);
            RectTransform counterRect = counterGo.GetComponent<RectTransform>();
            counterRect.anchorMin = new Vector2(0.5f, 0f);
            counterRect.anchorMax = new Vector2(0.5f, 0f);
            counterRect.pivot = new Vector2(0.5f, 1f);
            counterRect.anchoredPosition = new Vector2(0f, -6f);
            counterRect.sizeDelta = new Vector2(120f, 28f);
        }
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

    // Accepts BoxCollider directly for videoTriggerArea
    private void WireVideoTriggerArea()
    {
        AutoWireMediaFields();

        if (displayImage != null)
        {
            AttachMediaTrigger(displayImage.gameObject);
        }

        if (videoTriggerArea != null && videoTriggerArea.gameObject != displayImage?.gameObject)
        {
            AttachMediaTrigger(videoTriggerArea.gameObject);
        }

        if (videoPanel != null && videoPanel != displayImage?.gameObject && videoPanel != videoTriggerArea?.gameObject)
        {
            AttachMediaTrigger(videoPanel);
        }

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            videoPlayer.loopPointReached += OnVideoLoopPointReached;
        }
    }

    private void AttachMediaTrigger(GameObject targetObj)
    {
        if (targetObj == null) return;

        HoldMediaClipTrigger trigger = targetObj.GetComponent<HoldMediaClipTrigger>();
        if (trigger == null) trigger = targetObj.AddComponent<HoldMediaClipTrigger>();
        trigger.Setup(this);

        BoxCollider box = targetObj.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = targetObj.AddComponent<BoxCollider>();
            RectTransform rect = targetObj.GetComponent<RectTransform>();
            float w = (rect != null && rect.rect.width > 10f) ? rect.rect.width : 250f;
            float h = (rect != null && rect.rect.height > 10f) ? rect.rect.height : 250f;
            box.size = new Vector3(w, h, 10f);
        }

        Image img = targetObj.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        RawImage rawImg = targetObj.GetComponent<RawImage>();
        if (rawImg != null) rawImg.raycastTarget = true;
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
/// Attached to displayImage, videoTriggerArea, and videoPanel.
/// Captures press & release triggers, UGUI hover, as well as XR Ray / Head Gaze interactors via XRSimpleInteractable.
/// </summary>
public class HoldMediaClipTrigger : XRSimpleInteractable, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    private HistoryPanel panel;

    public void Setup(HistoryPanel historyPanel)
    {
        panel = historyPanel;
    }

    protected override void OnHoverEntered(HoverEnterEventArgs args)
    {
        base.OnHoverEntered(args);
        panel?.OnPointerEnterTrigger();
    }

    protected override void OnHoverExited(HoverExitEventArgs args)
    {
        base.OnHoverExited(args);
        panel?.OnPointerExitTrigger();
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        panel?.OnPointerDownTrigger();
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        panel?.OnPointerUpTrigger();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        panel?.OnPointerEnterTrigger();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        panel?.OnPointerExitTrigger();
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
