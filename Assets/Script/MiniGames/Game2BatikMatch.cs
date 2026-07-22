using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// "Susunkan Proses Pembuatan Batik" mini-game.
/// The player drags 5 photo cards from the tray into 5 numbered slots in the
/// correct order of the batik-making process, then presses "Periksa Jawaban".
/// On a win the solve time is shown and submitted to the Firestore leaderboard.
/// </summary>
public class Game2BatikMatch : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Draggable card component
    // ------------------------------------------------------------------
    public class BatikCard : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        public int correctStepIndex;   // which slot (0-4) this card belongs in
        public bool inSlot;            // currently parked in a slot (vs the tray)
        public int anchorIndex;        // index of the slot/tray column it occupies
        public Vector3 targetLocalPosition;
        public bool isGrabbed;

        private Game2BatikMatch controller;
        private Vector2 grabOffsetLocal;

        public void Setup(Game2BatikMatch gameController, int stepIndex, Vector2 cardSize)
        {
            controller = gameController;
            correctStepIndex = stepIndex;

            var boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(cardSize.x, cardSize.y, 14f);

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        public void DisableGrab()
        {
            // No-op: UI GraphicRaycaster handles pointer events cleanly
        }

        // --- Unity UI Drag Event Handlers ---
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            isGrabbed = true;
            transform.SetAsLastSibling();

            Vector3 worldHit = eventData.pointerCurrentRaycast.worldPosition;
            if (worldHit != Vector3.zero && transform.parent != null)
            {
                Vector3 localHit = transform.parent.InverseTransformPoint(worldHit);
                grabOffsetLocal = (Vector2)transform.localPosition - new Vector2(localHit.x, localHit.y);
            }
            else
            {
                grabOffsetLocal = Vector2.zero;
            }
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!isGrabbed || transform.parent == null) return;

            Vector3 worldHit = eventData.pointerCurrentRaycast.worldPosition;
            if (worldHit != Vector3.zero)
            {
                Vector3 localHit = transform.parent.InverseTransformPoint(worldHit);
                Vector2 targetPos = new Vector2(localHit.x, localHit.y) + grabOffsetLocal;

                targetPos.x = Mathf.Clamp(targetPos.x, -220f, 220f);
                targetPos.y = Mathf.Clamp(targetPos.y, -190f, 190f);
                transform.localPosition = new Vector3(targetPos.x, targetPos.y, CardZ);
            }
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!isGrabbed) return;
            isGrabbed = false;
            transform.SetAsLastSibling();
            controller.OnCardReleased(this);
        }

        private void Update()
        {
            Vector3 targetScale = isGrabbed ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 15f);

            if (!isGrabbed)
            {
                // Smoothly glide back to assigned slot or tray position
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, Time.deltaTime * 12f);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, Time.deltaTime * 12f);
            }
        }
    }

    private struct LeaderboardEntry
    {
        public string name;
        public float time;
    }

    // Firebase Firestore configuration (Lightweight REST API)
    private const string FirestoreUrl = "https://firestore.googleapis.com/v1/projects/museum-mixed-reality-app/databases/(default)/documents";

    // ------------------------------------------------------------------
    // Palette (matches exact sage green mockup aesthetic)
    // ------------------------------------------------------------------
    private static readonly Color SageBg = new Color(0.537f, 0.557f, 0.478f, 1f);   // sage panel background (#898E7A)
    private static readonly Color SageDark = new Color(0.424f, 0.447f, 0.373f, 1f); // slot interior (#6C725F)
    private static readonly Color Khaki = new Color(0.784f, 0.765f, 0.353f, 1f);    // chips, borders, buttons (#C8C35A)
    private static readonly Color KhakiHover = new Color(0.86f, 0.84f, 0.44f, 1f);
    private static readonly Color DarkOlive = new Color(0.22f, 0.235f, 0.16f, 1f);  // text on khaki, close button
    private static readonly Color DarkOliveHover = new Color(0.33f, 0.35f, 0.25f, 1f);
    private static readonly Color Cream = new Color(0.96f, 0.96f, 0.92f, 1f);    // title & button text

    // ------------------------------------------------------------------
    // Layout (canvas units; panel canvas is resized to 460x400)
    // ------------------------------------------------------------------
    private static readonly Vector2 PanelSize = new Vector2(460f, 400f);
    private static readonly Vector2 CardSize = new Vector2(74f, 120f);
    private const float SlotY = 85f;
    private const float TrayY = -85f;
    private const float CardZ = -6f;
    private static readonly float[] ColumnX = { -168f, -84f, 0f, 84f, 168f };

    // Steps in their CORRECT order (index == correct slot)
    private static readonly string[] StepNames = { "Melukis Corak", "Mencanting", "Mewarna", "Melorod", "Menjemur" };
    private static readonly string[] StepSpriteKeys = { "melukis_corak", "mencanting", "mewarna", "melorod", "menjemur" };
    private static readonly Color[] StepTopColors = {
        new Color(0.85f, 0.78f, 0.62f), new Color(0.75f, 0.52f, 0.32f), new Color(0.48f, 0.31f, 0.56f),
        new Color(0.49f, 0.53f, 0.58f), new Color(0.56f, 0.68f, 0.42f)
    };
    private static readonly Color[] StepBottomColors = {
        new Color(0.72f, 0.62f, 0.42f), new Color(0.54f, 0.35f, 0.20f), new Color(0.30f, 0.18f, 0.40f),
        new Color(0.30f, 0.33f, 0.38f), new Color(0.36f, 0.48f, 0.26f)
    };

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------
    private BatikCard[] slotCards = new BatikCard[5];
    private BatikCard[] trayCards = new BatikCard[5];
    private List<BatikCard> allCards = new List<BatikCard>();
    private GameObject boardRoot;
    private GameObject leaderboardRoot;
    private GameObject introRoot;
    private List<GameObject> spawnedButtons = new List<GameObject>();

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI feedbackText;
    private TextMeshProUGUI descText;

    // Countdown state (45 seconds for 5 cards + answer check)
    private const float TotalGameTime = 45.0f;
    private float remainingTime = TotalGameTime;
    private bool isGameOver = false;
    // The countdown/board only start once the player presses Mulai on the intro screen,
    // not the instant the game QR is scanned.
    private bool gameStarted = false;

    // Donut timer UI
    private GameObject donutContainer;
    private Image donutImage;
    private TextMeshProUGUI donutText;

    // Generated art assets (tracked for cleanup)
    private List<Texture2D> generatedTextures = new List<Texture2D>();
    private Sprite roundedFillSprite;
    private Sprite roundedBorderSprite;
    private Sprite dashedSlotBorderSprite;
    private Sprite circleSprite;

    [Header("Font Settings")]
    [Tooltip("Drag any Serif TMP Font Asset (e.g. Georgia, Times New Roman, Playfair Display) to match the mockup typography.")]
    public TMP_FontAsset customFont;

    [Header("Manual UI Inspector References (Optional)")]
    [Tooltip("Parent transform containing 5 Slot GameObjects. If assigned in Inspector, uses your custom slot layout.")]
    public Transform customSlotsContainer;

    [Tooltip("Parent transform containing 5 Tray anchor GameObjects. If assigned in Inspector, uses your custom tray anchors.")]
    public Transform customTrayContainer;

    [Tooltip("Custom Card UI Prefab. If assigned in Inspector, instantiated instead of procedural cards.")]
    public GameObject customCardPrefab;

    [Tooltip("Check Answer Button component in your custom UI.")]
    public UnityEngine.UI.Button customCheckAnswerButton;

    [Tooltip("Title Text component in your custom UI.")]
    public TextMeshProUGUI customTitleText;

    [Tooltip("Feedback Text component in your custom UI.")]
    public TextMeshProUGUI customFeedbackText;

    [Tooltip("Close Button component in your custom UI.")]
    public UnityEngine.UI.Button customCloseButton;

    private void Start()
    {
        // 0. Generate reusable high-precision rounded-corner sprites (24px radius)
        roundedFillSprite = MakeRoundedSprite(128, 128, 24, 0);
        roundedBorderSprite = MakeRoundedSprite(128, 128, 24, 6);
        circleSprite = MakeRoundedSprite(64, 64, 32, 0);
        // Dashed variant matches the mockup's dotted slot outline; 9-slicing would
        // stretch and distort a dash pattern, so it's rendered at the slots' exact
        // aspect ratio and drawn with Image.Type.Simple instead.
        dashedSlotBorderSprite = MakeDashedBorderSprite((int)CardSize.x * 2, (int)CardSize.y * 2, 28, 6, 22);

        // "Assets/Resources/Fonts & Materials/Georgia SDF.asset" was meant to give this
        // game the mockup's serif look, but it turns out to be an empty, never-generated
        // font stub - no material, no glyph/character table, no atlas texture. TMP can't
        // render anything with it (silently, no exception), which is why text disappeared
        // entirely when it was wired in. Only use it if it actually has a material - i.e.
        // once someone runs it through Window > TextMeshPro > Font Asset Creator in the
        // Editor - otherwise stay on TMP's default font, which already works everywhere
        // else in this project.
        if (customFont == null)
        {
            try
            {
                TMP_FontAsset loaded = Resources.Load<TMP_FontAsset>("Fonts & Materials/Georgia SDF");
                if (loaded != null && loaded.material != null)
                {
                    customFont = loaded;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Game2BatikMatch: Georgia SDF font failed to load, falling back to the default font. {e.Message}");
                customFont = null;
            }
        }

        // 1. Enlarge the panel canvas to a landscape layout and make it opaque sage
        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null) rootRect.sizeDelta = PanelSize;

        transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        Transform bg = transform.Find("Background");
        if (bg != null)
        {
            Image bgImage = bg.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.material = null;
                bgImage.sprite = roundedFillSprite;
                bgImage.type = Image.Type.Sliced;
                bgImage.color = SageBg;
            }
        }

        // 2. Resolve header text references now, but leave them hidden/unstyled until
        // the player actually presses "Mulai" on the intro screen (BeginGameplay).
        titleText = customTitleText != null ? customTitleText : transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        feedbackText = customFeedbackText != null ? customFeedbackText : transform.Find("ArtistYearText")?.GetComponent<TextMeshProUGUI>();
        descText = transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();

        if (titleText != null) titleText.gameObject.SetActive(false);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        if (descText != null) descText.gameObject.SetActive(false);

        // 3. Close button - available on both the intro screen and during gameplay
        if (customCloseButton != null)
        {
            customCloseButton.onClick.AddListener(() => MiniGameManager.Instance.CloseActiveGame());
        }
        else
        {
            CreateCloseButton();
        }

        // 4. Show the intro screen ("Main Game" pill, title, Mulai button). The donut
        // timer, board, and cards are only built once the player presses Mulai.
        CreateIntroScreen();
    }

    /// <summary>
    /// Called once, when the player presses "Mulai" on the intro screen. Tears down the
    /// intro UI and builds the actual game (header texts, countdown timer, board, cards).
    /// </summary>
    private void BeginGameplay()
    {
        if (gameStarted) return;
        gameStarted = true;

        if (introRoot != null)
        {
            Destroy(introRoot);
            introRoot = null;
        }

        // The Mulai button is parented directly to the panel root (like all other
        // buttons CreateStyledButton makes), not under introRoot, so it must be cleared
        // here explicitly rather than relying on introRoot's destruction to catch it.
        foreach (var btn in spawnedButtons) Destroy(btn);
        spawnedButtons.Clear();

        if (titleText != null && customTitleText == null)
        {
            titleText.gameObject.SetActive(true);
            SetupTextRect(titleText.rectTransform, new Vector2(-65f, 168f), new Vector2(300f, 36f));
            titleText.text = "Susunkan proses pembuatan batik";
            SafeSetFont(titleText);
            titleText.fontSize = 18;
            titleText.fontStyle = FontStyles.Normal;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.color = Cream;
        }
        if (feedbackText != null && customFeedbackText == null)
        {
            feedbackText.gameObject.SetActive(true);
            SetupTextRect(feedbackText.rectTransform, new Vector2(-50f, -172f), new Vector2(250f, 34f));
            feedbackText.text = "";
            SafeSetFont(feedbackText);
            feedbackText.fontSize = 11;
            feedbackText.fontStyle = FontStyles.Bold;
            feedbackText.alignment = TextAlignmentOptions.Left;
            feedbackText.color = Cream;
        }
        if (descText != null)
        {
            descText.gameObject.SetActive(true);
            SetupTextRect(descText.rectTransform, new Vector2(0f, 0f), new Vector2(420f, 300f));
            SafeSetFont(descText);
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = Cream;
            descText.text = "";
        }

        // Donut countdown timer (top-right)
        CreateDonutTimerUI();

        // Check Answer button
        if (customCheckAnswerButton != null)
        {
            customCheckAnswerButton.onClick.AddListener(CheckAnswer);
        }

        // Build board & cards
        BuildBoard();
    }

    /// <summary>
    /// Builds the intro screen shown right after scanning the game QR: a "Main Game"
    /// pill, the game's title, and a Mulai button. The countdown only starts once the
    /// player presses Mulai (BeginGameplay), not the moment the QR is scanned.
    /// </summary>
    private void CreateIntroScreen()
    {
        introRoot = new GameObject("IntroScreen");
        introRoot.transform.SetParent(transform, false);
        RectTransform introRect = introRoot.AddComponent<RectTransform>();
        introRect.anchorMin = new Vector2(0.5f, 0.5f);
        introRect.anchorMax = new Vector2(0.5f, 0.5f);
        introRect.anchoredPosition = Vector2.zero;
        introRect.sizeDelta = Vector2.zero;

        // "Main Game" pill with a simple procedural game-icon (rounded chip + two dots)
        GameObject pillObj = new GameObject("HeaderPill");
        pillObj.transform.SetParent(introRoot.transform, false);
        RectTransform pillRect = pillObj.AddComponent<RectTransform>();
        pillRect.sizeDelta = new Vector2(150f, 30f);
        pillRect.anchoredPosition = new Vector2(0f, 110f);
        Image pillImg = pillObj.AddComponent<Image>();
        pillImg.sprite = roundedFillSprite;
        pillImg.type = Image.Type.Sliced;
        pillImg.color = DarkOlive;

        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(pillObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(20f, 14f);
        iconRect.anchoredPosition = new Vector2(-50f, 0f);
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = roundedFillSprite;
        iconImg.type = Image.Type.Sliced;
        iconImg.color = Khaki;

        GameObject dot1 = new GameObject("Dot1");
        dot1.transform.SetParent(iconObj.transform, false);
        RectTransform dot1Rect = dot1.AddComponent<RectTransform>();
        dot1Rect.sizeDelta = new Vector2(4.5f, 4.5f);
        dot1Rect.anchoredPosition = new Vector2(-4f, 2f);
        Image dot1Img = dot1.AddComponent<Image>();
        dot1Img.sprite = circleSprite;
        dot1Img.color = DarkOlive;

        GameObject dot2 = new GameObject("Dot2");
        dot2.transform.SetParent(iconObj.transform, false);
        RectTransform dot2Rect = dot2.AddComponent<RectTransform>();
        dot2Rect.sizeDelta = new Vector2(4.5f, 4.5f);
        dot2Rect.anchoredPosition = new Vector2(5f, -2f);
        Image dot2Img = dot2.AddComponent<Image>();
        dot2Img.sprite = circleSprite;
        dot2Img.color = DarkOlive;

        GameObject pillTextObj = new GameObject("PillText");
        pillTextObj.transform.SetParent(pillObj.transform, false);
        RectTransform pillTextRect = pillTextObj.AddComponent<RectTransform>();
        pillTextRect.sizeDelta = new Vector2(105f, 30f);
        pillTextRect.anchoredPosition = new Vector2(14f, 0f);
        TextMeshProUGUI pillText = pillTextObj.AddComponent<TextMeshProUGUI>();
        pillText.text = "Main Game";
        SafeSetFont(pillText);
        pillText.fontSize = 12;
        pillText.fontStyle = FontStyles.Bold;
        pillText.alignment = TextAlignmentOptions.Center;
        pillText.color = Cream;

        // Big centered title
        GameObject introTitleObj = new GameObject("IntroTitle");
        introTitleObj.transform.SetParent(introRoot.transform, false);
        RectTransform introTitleRect = introTitleObj.AddComponent<RectTransform>();
        introTitleRect.sizeDelta = new Vector2(360f, 90f);
        introTitleRect.anchoredPosition = new Vector2(0f, 40f);
        TextMeshProUGUI introTitleText = introTitleObj.AddComponent<TextMeshProUGUI>();
        introTitleText.text = "Urutkan Proses\nPembuatan Batik";
        SafeSetFont(introTitleText);
        introTitleText.fontSize = 26;
        introTitleText.fontStyle = FontStyles.Bold;
        introTitleText.alignment = TextAlignmentOptions.Center;
        introTitleText.color = Cream;

        // Mulai (Start) button
        GameObject startBtn = CreateStyledButton("Mulai", new Vector2(0f, -60f), new Vector2(140f, 42f), BeginGameplay);
        spawnedButtons.Add(startBtn);
    }

    /// <summary>
    /// Assigns customFont to a text element, swallowing any exception. Georgia SDF has
    /// never actually been used in a live scene before this, so if it turns out to have
    /// an internal issue (incomplete atlas, missing material, etc.) this keeps that
    /// contained to "this one label stays in the default font" instead of aborting the
    /// rest of Start() and leaving the whole panel blank.
    /// </summary>
    private void SafeSetFont(TextMeshProUGUI text)
    {
        if (text == null || customFont == null) return;
        try
        {
            text.font = customFont;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Game2BatikMatch: Failed to apply customFont to '{text.name}', leaving default font. {e.Message}");
        }
    }

    private void SetupTextRect(RectTransform rect, Vector2 anchoredPos, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
    }

    // ------------------------------------------------------------------
    // Board construction
    // ------------------------------------------------------------------
    private void BuildBoard()
    {
        boardRoot = new GameObject("GameBoard");
        boardRoot.transform.SetParent(transform, false);
        RectTransform boardRect = boardRoot.AddComponent<RectTransform>();
        boardRect.anchorMin = new Vector2(0.5f, 0.5f);
        boardRect.anchorMax = new Vector2(0.5f, 0.5f);
        boardRect.anchoredPosition = Vector2.zero;
        boardRect.sizeDelta = Vector2.zero;

        // If manual slots container is assigned, sync ColumnX positions
        if (customSlotsContainer != null && customSlotsContainer.childCount >= 5)
        {
            for (int i = 0; i < 5; i++)
            {
                ColumnX[i] = customSlotsContainer.GetChild(i).localPosition.x;
            }
        }

        // Decorative horizontal divider line between slots and cards (if procedural)
        if (customSlotsContainer == null)
        {
            GameObject divObj = new GameObject("DividerLine");
            divObj.transform.SetParent(boardRoot.transform, false);
            RectTransform divRect = divObj.AddComponent<RectTransform>();
            divRect.sizeDelta = new Vector2(380f, 2f);
            divRect.anchoredPosition = new Vector2(0f, 0f);
            Image divImg = divObj.AddComponent<Image>();
            divImg.color = new Color(Khaki.r, Khaki.g, Khaki.b, 0.4f);

            GameObject diaObj = new GameObject("DividerDiamond");
            diaObj.transform.SetParent(boardRoot.transform, false);
            RectTransform diaRect = diaObj.AddComponent<RectTransform>();
            diaRect.sizeDelta = new Vector2(10f, 10f);
            diaRect.anchoredPosition = new Vector2(0f, 0f);
            diaRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image diaImg = diaObj.AddComponent<Image>();
            diaImg.color = Khaki;

            // --- Numbered slots (top row) ---
            for (int i = 0; i < 5; i++)
            {
                GameObject slotObj = new GameObject($"Slot_{i + 1}");
                slotObj.transform.SetParent(boardRoot.transform, false);
                RectTransform slotRect = slotObj.AddComponent<RectTransform>();
                slotRect.sizeDelta = CardSize;
                slotRect.anchoredPosition = new Vector2(ColumnX[i], SlotY);

                Image fill = slotObj.AddComponent<Image>();
                fill.sprite = roundedFillSprite;
                fill.type = Image.Type.Sliced;
                fill.color = SageDark;

                GameObject borderObj = new GameObject("Border");
                borderObj.transform.SetParent(slotObj.transform, false);
                RectTransform borderRect = borderObj.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.sizeDelta = Vector2.zero;
                Image border = borderObj.AddComponent<Image>();
                border.sprite = dashedSlotBorderSprite;
                // Simple, not Sliced: 9-slicing would stretch and distort the dash spacing.
                border.type = Image.Type.Simple;
                border.color = Khaki;

                GameObject numObj = new GameObject("Number");
                numObj.transform.SetParent(slotObj.transform, false);
                RectTransform numRect = numObj.AddComponent<RectTransform>();
                numRect.anchorMin = Vector2.zero;
                numRect.anchorMax = Vector2.one;
                numRect.sizeDelta = Vector2.zero;
                TextMeshProUGUI numText = numObj.AddComponent<TextMeshProUGUI>();
                numText.text = (i + 1).ToString();
                SafeSetFont(numText);
                numText.fontSize = 24;
                numText.fontStyle = FontStyles.Bold;
                numText.alignment = TextAlignmentOptions.Center;
                numText.color = new Color(Khaki.r, Khaki.g, Khaki.b, 0.75f);
            }

            // --- Chevrons between slots ---
            for (int i = 0; i < 4; i++)
            {
                float midX = (ColumnX[i] + ColumnX[i + 1]) * 0.5f;
                GameObject chevObj = new GameObject($"Chevron_{i}");
                chevObj.transform.SetParent(boardRoot.transform, false);
                RectTransform chevRect = chevObj.AddComponent<RectTransform>();
                chevRect.sizeDelta = new Vector2(24f, 44f);
                chevRect.anchoredPosition = new Vector2(midX, SlotY);
                TextMeshProUGUI chevText = chevObj.AddComponent<TextMeshProUGUI>();
                chevText.text = ">";
                SafeSetFont(chevText);
                chevText.fontSize = 24;
                chevText.fontStyle = FontStyles.Bold;
                chevText.alignment = TextAlignmentOptions.Center;
                chevText.color = Cream;
            }
        }

        // --- Shuffled photo cards (bottom tray) ---
        List<int> order = new List<int> { 0, 1, 2, 3, 4 };
        do { ShuffleList(order); } while (IsIndicesInCorrectOrder(order));

        for (int i = 0; i < 5; i++)
        {
            int stepIndex = order[i];
            BatikCard card = CreateCard(stepIndex);
            card.inSlot = false;
            card.anchorIndex = i;
            card.targetLocalPosition = new Vector3(ColumnX[i], TrayY, CardZ);
            card.transform.localPosition = card.targetLocalPosition;
            trayCards[i] = card;
            allCards.Add(card);
        }

        // --- "Periksa Jawaban ›" button (if procedural) ---
        if (customCheckAnswerButton == null)
        {
            GameObject checkBtn = CreateStyledButton("Periksa Jawaban  >", new Vector2(135f, -172f), new Vector2(170f, 38f), CheckAnswer);
            spawnedButtons.Add(checkBtn);
        }
    }

    private BatikCard CreateCard(int stepIndex)
    {
        GameObject cardObj;
        if (customCardPrefab != null)
        {
            cardObj = Instantiate(customCardPrefab, boardRoot.transform, false);
            cardObj.name = $"Card_{StepNames[stepIndex]}";

            Image photoImage = cardObj.transform.Find("Photo")?.GetComponent<Image>();
            if (photoImage != null)
            {
                Sprite photo = Resources.Load<Sprite>($"BatikSteps/{StepSpriteKeys[stepIndex]}");
                if (photo != null) photoImage.sprite = photo;
            }

            TextMeshProUGUI labelText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            if (labelText != null)
            {
                labelText.text = StepNames[stepIndex];
                SafeSetFont(labelText);
            }
        }
        else
        {
            cardObj = new GameObject($"Card_{StepNames[stepIndex]}");
            cardObj.transform.SetParent(boardRoot.transform, false);
            RectTransform cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.sizeDelta = CardSize;

            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.sprite = roundedFillSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = SageDark;

            // Masking component clips top photo & bottom chip perfectly to the rounded card corners
            Mask cardMask = cardObj.AddComponent<Mask>();
            cardMask.showMaskGraphic = true;

            GameObject photoObj = new GameObject("Photo");
            photoObj.transform.SetParent(cardObj.transform, false);
            RectTransform photoRect = photoObj.AddComponent<RectTransform>();
            photoRect.sizeDelta = new Vector2(CardSize.x, 82f);
            photoRect.anchoredPosition = new Vector2(0f, 19f);
            Image photoImage = photoObj.AddComponent<Image>();
            Sprite photo = Resources.Load<Sprite>($"BatikSteps/{StepSpriteKeys[stepIndex]}");
            if (photo != null)
            {
                photoImage.sprite = photo;
            }
            else
            {
                photoImage.sprite = MakeGradientSprite(64, 96, StepTopColors[stepIndex], StepBottomColors[stepIndex]);
            }
            photoImage.color = Color.white;

            GameObject chipObj = new GameObject("LabelChip");
            chipObj.transform.SetParent(cardObj.transform, false);
            RectTransform chipRect = chipObj.AddComponent<RectTransform>();
            chipRect.sizeDelta = new Vector2(CardSize.x, 38f);
            chipRect.anchoredPosition = new Vector2(0f, -41f);
            Image chipImage = chipObj.AddComponent<Image>();
            chipImage.sprite = roundedFillSprite;
            chipImage.type = Image.Type.Sliced;
            chipImage.color = Khaki;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(chipObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = StepNames[stepIndex];
            SafeSetFont(labelText);
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 6f;
            labelText.fontSizeMax = 9.5f;
            labelText.fontStyle = FontStyles.Normal;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Cream;
        }

        BatikCard card = cardObj.GetComponent<BatikCard>();
        if (card == null) card = cardObj.AddComponent<BatikCard>();
        card.Setup(this, stepIndex, CardSize);
        return card;
    }

    // ------------------------------------------------------------------
    // Drag & drop logic
    // ------------------------------------------------------------------
    public void OnCardReleased(BatikCard card)
    {
        // Find the nearest anchor (any slot or tray column) to the drop position
        Vector3 p = card.transform.localPosition;
        bool bestInSlot = false;
        int bestIdx = 0;
        float best = float.MaxValue;

        for (int i = 0; i < 5; i++)
        {
            float dSlot = Vector2.Distance(p, new Vector2(ColumnX[i], SlotY));
            if (dSlot < best) { best = dSlot; bestInSlot = true; bestIdx = i; }

            float dTray = Vector2.Distance(p, new Vector2(ColumnX[i], TrayY));
            if (dTray < best) { best = dTray; bestInSlot = false; bestIdx = i; }
        }

        MoveCardToAnchor(card, bestInSlot, bestIdx);
    }

    private void MoveCardToAnchor(BatikCard card, bool toSlot, int idx)
    {
        BatikCard occupant = toSlot ? slotCards[idx] : trayCards[idx];
        if (occupant == card)
        {
            card.targetLocalPosition = AnchorPosition(toSlot, idx);
            return;
        }

        bool oldInSlot = card.inSlot;
        int oldIdx = card.anchorIndex;

        // Vacate the card's old anchor
        if (oldInSlot) slotCards[oldIdx] = null; else trayCards[oldIdx] = null;

        // A displaced occupant swaps into the card's old anchor
        if (occupant != null)
        {
            if (toSlot) slotCards[idx] = null; else trayCards[idx] = null;
            if (oldInSlot) slotCards[oldIdx] = occupant; else trayCards[oldIdx] = occupant;
            occupant.inSlot = oldInSlot;
            occupant.anchorIndex = oldIdx;
            occupant.targetLocalPosition = AnchorPosition(oldInSlot, oldIdx);
        }

        if (toSlot) slotCards[idx] = card; else trayCards[idx] = card;
        card.inSlot = toSlot;
        card.anchorIndex = idx;
        card.targetLocalPosition = AnchorPosition(toSlot, idx);
    }

    private Vector3 AnchorPosition(bool inSlot, int idx)
    {
        return new Vector3(ColumnX[idx], inSlot ? SlotY : TrayY, CardZ);
    }

    // ------------------------------------------------------------------
    // Answer checking / win / loss
    // ------------------------------------------------------------------
    private void CheckAnswer()
    {
        if (isGameOver) return;

        for (int i = 0; i < 5; i++)
        {
            if (slotCards[i] == null)
            {
                if (feedbackText != null)
                {
                    feedbackText.text = "<color=#F0D060>Letakkan kesemua 5 kad ke dalam petak dahulu!</color>";
                }
                return;
            }
        }

        bool correct = true;
        for (int i = 0; i < 5; i++)
        {
            if (slotCards[i].correctStepIndex != i) { correct = false; break; }
        }

        if (correct)
        {
            TriggerWin();
        }
        else
        {
            if (feedbackText != null)
            {
                feedbackText.text = "<color=#FF6655>Susunan salah! Semak semula urutan proses.</color>";
            }
            PlayWrongBeep();
        }
    }

    private void TriggerWin()
    {
        isGameOver = true;
        float solveTime = TotalGameTime - remainingTime;

        if (feedbackText != null)
        {
            feedbackText.text = $"<color=#B8E986>★ Selesai! Masa anda: {solveTime:F1} saat! ★</color>";
        }

        // Hide the timer and the board to make space for the leaderboard
        if (donutContainer != null) donutContainer.SetActive(false);
        if (boardRoot != null) boardRoot.SetActive(false);

        SpawnConfetti();
        PlayCelebrationSound();

        // Submit score to Firebase & fetch the leaderboard
        string playerName = PlayerPrefs.GetString("PlayerName", "Pelawat");
        StartCoroutine(PostScoreAndLoadLeaderboard(playerName, solveTime));

        // Mark Batik Fabric completed on the checklist
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.MarkArtifactInteracted("artifact_batik");
        }

        ShowEndGameOptions();
    }

    private void TriggerLoss()
    {
        isGameOver = true;

        if (feedbackText != null)
        {
            feedbackText.text = "<color=#FF6655>Masa Tamat! Cuba lagi.</color>";
        }

        foreach (BatikCard card in allCards)
        {
            if (card != null) card.DisableGrab();
        }

        PlayLossSound();
        ShowEndGameOptions();
    }

    private void ShowEndGameOptions()
    {
        foreach (var btn in spawnedButtons) Destroy(btn);
        spawnedButtons.Clear();

        GameObject replayBtn = CreateStyledButton("Main Lagi", new Vector2(-80f, -172f), new Vector2(130f, 38f), ResetAndRestartGame);
        spawnedButtons.Add(replayBtn);

        GameObject closeBtn = CreateStyledButton("Tutup", new Vector2(80f, -172f), new Vector2(130f, 38f), () => MiniGameManager.Instance.CloseActiveGame());
        spawnedButtons.Add(closeBtn);
    }

    private void ResetAndRestartGame()
    {
        // Tear down the old board, leaderboard, and buttons
        if (boardRoot != null) Destroy(boardRoot);
        if (leaderboardRoot != null) Destroy(leaderboardRoot);
        foreach (var btn in spawnedButtons) Destroy(btn);
        spawnedButtons.Clear();
        slotCards = new BatikCard[5];
        trayCards = new BatikCard[5];
        allCards.Clear();

        // Reset UI state
        if (donutContainer != null) donutContainer.SetActive(true);
        if (descText != null) descText.text = "";
        if (titleText != null) titleText.text = "Susunkan Proses Pembuatan Batik";
        if (feedbackText != null) feedbackText.text = "Heret kad ke petak 1-5 mengikut urutan proses batik.";

        remainingTime = TotalGameTime;
        isGameOver = false;

        BuildBoard();
    }

    // ------------------------------------------------------------------
    // Countdown timer
    // ------------------------------------------------------------------
    private void CreateDonutTimerUI()
    {
        donutContainer = new GameObject("DonutTimer");
        donutContainer.transform.SetParent(transform, false);

        RectTransform donutRect = donutContainer.AddComponent<RectTransform>();
        donutRect.sizeDelta = new Vector2(38f, 38f);
        donutRect.anchoredPosition = new Vector2(152f, 168f); // top-right, left of the close button

        Texture2D ringTex = CreateRingTexture(128, 16);
        generatedTextures.Add(ringTex);
        Sprite ringSprite = Sprite.Create(ringTex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));

        // Dark backing ring
        GameObject bgCircle = new GameObject("BackgroundCircle");
        bgCircle.transform.SetParent(donutContainer.transform, false);
        RectTransform bgRect = bgCircle.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImage = bgCircle.AddComponent<Image>();
        bgImage.sprite = ringSprite;
        bgImage.color = new Color(0.16f, 0.17f, 0.12f, 0.4f);

        // Draining fill ring
        GameObject fillCircle = new GameObject("FillCircle");
        fillCircle.transform.SetParent(donutContainer.transform, false);
        RectTransform fillRect = fillCircle.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        donutImage = fillCircle.AddComponent<Image>();
        donutImage.sprite = ringSprite;
        donutImage.color = new Color(0.55f, 0.75f, 0.35f, 1f);
        donutImage.type = Image.Type.Filled;
        donutImage.fillMethod = Image.FillMethod.Radial360;
        donutImage.fillOrigin = (int)Image.Origin360.Top;
        donutImage.fillClockwise = false;

        // Seconds text in the center
        GameObject textObj = new GameObject("CountdownText");
        textObj.transform.SetParent(donutContainer.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        donutText = textObj.AddComponent<TextMeshProUGUI>();
        donutText.text = Mathf.CeilToInt(TotalGameTime).ToString();
        SafeSetFont(donutText);
        donutText.fontSize = 12;
        donutText.fontStyle = FontStyles.Bold;
        donutText.alignment = TextAlignmentOptions.Center;
        donutText.color = Cream;
    }

    private void Update()
    {
        if (!gameStarted || isGameOver) return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            TriggerLoss();
        }

        if (donutImage != null)
        {
            float ratio = remainingTime / TotalGameTime;
            donutImage.fillAmount = ratio;

            if (ratio > 0.5f)
            {
                donutImage.color = new Color(0.55f, 0.75f, 0.35f, 1f); // leafy green
            }
            else if (ratio > 0.2f)
            {
                donutImage.color = new Color(0.88f, 0.62f, 0.18f, 1f); // amber
            }
            else
            {
                donutImage.color = new Color(0.88f, 0.28f, 0.22f, 1f); // urgent red
            }
        }

        if (donutText != null)
        {
            donutText.text = Mathf.CeilToInt(remainingTime).ToString();
        }
    }

    // ------------------------------------------------------------------
    // Buttons
    // ------------------------------------------------------------------
    private void CreateCloseButton()
    {
        GameObject closeObj = new GameObject("CloseButton");
        closeObj.transform.SetParent(transform, false);
        RectTransform closeRect = closeObj.AddComponent<RectTransform>();
        closeRect.sizeDelta = new Vector2(40f, 40f);
        closeRect.anchoredPosition = new Vector2(203f, 168f);

        Image circleImage = closeObj.AddComponent<Image>();
        circleImage.sprite = circleSprite;
        circleImage.color = DarkOlive;

        GameObject xObj = new GameObject("X");
        xObj.transform.SetParent(closeObj.transform, false);
        RectTransform xRect = xObj.AddComponent<RectTransform>();
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI xText = xObj.AddComponent<TextMeshProUGUI>();
        xText.text = "X"; // plain ASCII: guaranteed glyph in the default TMP font
        SafeSetFont(xText);
        xText.fontSize = 16;
        xText.fontStyle = FontStyles.Bold;
        xText.alignment = TextAlignmentOptions.Center;
        xText.color = Cream;

        XRButtonSelection select = closeObj.AddComponent<XRButtonSelection>();
        select.buttonImage = circleImage;
        select.scaleTarget = closeObj.transform;
        select.normalColor = DarkOlive;
        select.hoverColor = DarkOliveHover;

        BoxCollider col = closeObj.AddComponent<BoxCollider>();
        col.size = new Vector3(40f, 40f, 15f);
        col.isTrigger = true;

        select.onClick.AddListener(() => MiniGameManager.Instance.CloseActiveGame());
    }

    private GameObject CreateStyledButton(string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject buttonObj = new GameObject($"Button_{label}");
        buttonObj.transform.SetParent(transform, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image img = buttonObj.AddComponent<Image>();
        img.sprite = roundedFillSprite;
        img.type = Image.Type.Sliced;
        img.color = Khaki;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        SafeSetFont(txt);
        txt.fontSize = 12;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = DarkOlive;

        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        XRButtonSelection select = buttonObj.AddComponent<XRButtonSelection>();
        select.buttonImage = img;
        select.scaleTarget = buttonObj.transform;
        select.normalColor = Khaki;
        select.hoverColor = KhakiHover;

        BoxCollider col = buttonObj.AddComponent<BoxCollider>();
        col.size = new Vector3(size.x, size.y, 15f);
        col.isTrigger = true;

        select.onClick.AddListener(onClickAction);
        return buttonObj;
    }

    // ------------------------------------------------------------------
    // Firestore leaderboard
    // ------------------------------------------------------------------
    private System.Collections.IEnumerator PostScoreAndLoadLeaderboard(string name, float time)
    {
        if (descText != null)
        {
            descText.text = "<align=center><color=#B8E986>Menghantar skor ke Firestore...</color></align>";
        }

        string postUrl = $"{FirestoreUrl}/leaderboard";

        // Firestore JSON document format: {"fields":{"name":{"stringValue":"..."},"time":{"doubleValue":...}}}
        string timeValue = time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        string safeName = string.IsNullOrWhiteSpace(name) ? "Pelawat" : name.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string json = $"{{\"fields\":{{\"name\":{{\"stringValue\":\"{safeName}\"}},\"time\":{{\"doubleValue\":{timeValue}}}}}}}";

        using (UnityWebRequest request = new UnityWebRequest(postUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
        }

        // Fetch scores directly via GET (avoids requiring a composite index in Firestore)
        if (descText != null)
        {
            descText.text = "<align=center><color=#B8D8F0>Memuatkan Papan Pendahulu...</color></align>";
        }

        string getUrl = $"{FirestoreUrl}/leaderboard";
        using (UnityWebRequest queryRequest = UnityWebRequest.Get(getUrl))
        {
            yield return queryRequest.SendWebRequest();

            if (queryRequest.result == UnityWebRequest.Result.Success)
            {
                string resultJson = queryRequest.downloadHandler.text;
                List<LeaderboardEntry> list = ParseLeaderboard(resultJson);
                DisplayLeaderboard(list);
            }
            else
            {
                if (descText != null)
                {
                    descText.text = $"<align=center><color=#FF6655>Gagal memuatkan leaderboard:\n{queryRequest.error}</color></align>";
                }
            }
        }
    }

    private List<LeaderboardEntry> ParseLeaderboard(string json)
    {
        List<LeaderboardEntry> list = new List<LeaderboardEntry>();
        if (string.IsNullOrEmpty(json) || json == "null" || json == "[]" || json == "{}") return list;

        // Split by "fields" to parse each document entry in Firestore response
        string[] docs = json.Split(new string[] { "\"fields\":" }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < docs.Length; i++)
        {
            string doc = docs[i];

            // 1. Extract Player Name
            string name = "";
            int nameIdx = doc.IndexOf("\"name\":");
            if (nameIdx != -1)
            {
                int strIdx = doc.IndexOf("\"stringValue\":", nameIdx);
                if (strIdx != -1)
                {
                    int start = doc.IndexOf("\"", strIdx + 14) + 1;
                    int end = doc.IndexOf("\"", start);
                    if (start > 0 && end > start)
                    {
                        name = doc.Substring(start, end - start);
                    }
                }
            }

            // 2. Extract Solve Time (handles doubleValue and integerValue formats)
            float time = 999f;
            int timeIdx = doc.IndexOf("\"time\":");
            if (timeIdx != -1)
            {
                int valIdx = doc.IndexOf("\"doubleValue\":", timeIdx);
                int prefixLen = 14;
                if (valIdx == -1)
                {
                    valIdx = doc.IndexOf("\"integerValue\":", timeIdx);
                    prefixLen = 15;
                }

                if (valIdx != -1)
                {
                    int start = valIdx + prefixLen;
                    while (start < doc.Length && (doc[start] == ' ' || doc[start] == '"' || doc[start] == ':')) start++;

                    int end = start;
                    while (end < doc.Length && (char.IsDigit(doc[end]) || doc[end] == '.' || doc[end] == ',')) end++;

                    if (end > start)
                    {
                        string timeStr = doc.Substring(start, end - start).Replace(',', '.');
                        float.TryParse(timeStr, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out time);
                    }
                }
            }

            if (!string.IsNullOrEmpty(name) && time < 990f)
            {
                list.Add(new LeaderboardEntry { name = name, time = time });
            }
        }

        // Sort by fastest time ascending
        list.Sort((a, b) => a.time.CompareTo(b.time));
        return list;
    }

    /// <summary>
    /// Builds a proper row-based leaderboard (rank badge, name, time) matching the
    /// game's sage/khaki design language, replacing the old single text-blob layout.
    /// </summary>
    private void DisplayLeaderboard(List<LeaderboardEntry> list)
    {
        if (titleText != null) titleText.text = "Papan Pendahulu";
        if (descText != null) descText.text = "";

        if (leaderboardRoot != null) Destroy(leaderboardRoot);
        leaderboardRoot = new GameObject("LeaderboardRoot");
        leaderboardRoot.transform.SetParent(transform, false);
        RectTransform lbRect = leaderboardRoot.AddComponent<RectTransform>();
        lbRect.anchorMin = new Vector2(0.5f, 0.5f);
        lbRect.anchorMax = new Vector2(0.5f, 0.5f);
        lbRect.anchoredPosition = Vector2.zero;
        lbRect.sizeDelta = Vector2.zero;

        // Header chip
        GameObject headerObj = new GameObject("HeaderChip");
        headerObj.transform.SetParent(leaderboardRoot.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(260f, 32f);
        headerRect.anchoredPosition = new Vector2(0f, 132f);
        Image headerImg = headerObj.AddComponent<Image>();
        headerImg.sprite = roundedFillSprite;
        headerImg.type = Image.Type.Sliced;
        headerImg.color = Khaki;

        GameObject headerTextObj = new GameObject("HeaderText");
        headerTextObj.transform.SetParent(headerObj.transform, false);
        RectTransform headerTextRect = headerTextObj.AddComponent<RectTransform>();
        headerTextRect.anchorMin = Vector2.zero;
        headerTextRect.anchorMax = Vector2.one;
        headerTextRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI headerText = headerTextObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "TURUTAN TERPANTAS";
        SafeSetFont(headerText);
        headerText.fontSize = 13;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = DarkOlive;

        if (list.Count == 0)
        {
            GameObject emptyObj = new GameObject("EmptyMessage");
            emptyObj.transform.SetParent(leaderboardRoot.transform, false);
            RectTransform emptyRect = emptyObj.AddComponent<RectTransform>();
            emptyRect.sizeDelta = new Vector2(380f, 60f);
            emptyRect.anchoredPosition = new Vector2(0f, 30f);
            TextMeshProUGUI emptyText = emptyObj.AddComponent<TextMeshProUGUI>();
            emptyText.text = "Tiada rekod lagi.\nJadilah yang pertama!";
            SafeSetFont(emptyText);
            emptyText.fontSize = 14;
            emptyText.alignment = TextAlignmentOptions.Center;
            emptyText.color = Cream;
            return;
        }

        string myName = PlayerPrefs.GetString("PlayerName", "Pelawat");
        bool foundMine = false;

        Color[] rankBadgeColors =
        {
            new Color(0.90f, 0.75f, 0.30f), // gold
            new Color(0.78f, 0.78f, 0.80f), // silver
            new Color(0.72f, 0.48f, 0.28f), // bronze
        };

        const float rowWidth = 400f;
        const float rowHeight = 40f;
        const float rowSpacing = 46f;
        const float startY = 95f;

        for (int i = 0; i < list.Count && i < 5; i++)
        {
            LeaderboardEntry entry = list[i];
            bool isTopThree = i < 3;
            bool isMine = !foundMine && entry.name == myName;
            if (isMine) foundMine = true;

            float y = startY - i * rowSpacing;

            GameObject rowObj = new GameObject($"Row_{i}");
            rowObj.transform.SetParent(leaderboardRoot.transform, false);
            RectTransform rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(rowWidth, rowHeight);
            rowRect.anchoredPosition = new Vector2(0f, y);
            Image rowImg = rowObj.AddComponent<Image>();
            rowImg.sprite = roundedFillSprite;
            rowImg.type = Image.Type.Sliced;
            rowImg.color = isTopThree
                ? new Color(Khaki.r, Khaki.g, Khaki.b, 0.22f)
                : new Color(SageDark.r, SageDark.g, SageDark.b, 0.9f);

            if (isMine)
            {
                GameObject outlineObj = new GameObject("MyHighlight");
                outlineObj.transform.SetParent(rowObj.transform, false);
                RectTransform outlineRect = outlineObj.AddComponent<RectTransform>();
                outlineRect.anchorMin = Vector2.zero;
                outlineRect.anchorMax = Vector2.one;
                outlineRect.sizeDelta = Vector2.zero;
                Image outlineImg = outlineObj.AddComponent<Image>();
                outlineImg.sprite = roundedBorderSprite;
                outlineImg.type = Image.Type.Sliced;
                outlineImg.color = Cream;
            }

            // Rank badge (circle)
            GameObject badgeObj = new GameObject("RankBadge");
            badgeObj.transform.SetParent(rowObj.transform, false);
            RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.sizeDelta = new Vector2(30f, 30f);
            badgeRect.anchoredPosition = new Vector2(-172f, 0f);
            Image badgeImg = badgeObj.AddComponent<Image>();
            badgeImg.sprite = circleSprite;
            badgeImg.color = isTopThree ? rankBadgeColors[i] : new Color(Khaki.r, Khaki.g, Khaki.b, 0.35f);

            GameObject badgeTextObj = new GameObject("RankNum");
            badgeTextObj.transform.SetParent(badgeObj.transform, false);
            RectTransform badgeTextRect = badgeTextObj.AddComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI badgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
            badgeText.text = (i + 1).ToString();
            SafeSetFont(badgeText);
            badgeText.fontSize = 15;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = isTopThree ? DarkOlive : Cream;

            // Player name
            GameObject nameObj = new GameObject("PlayerName");
            nameObj.transform.SetParent(rowObj.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(220f, 34f);
            nameRect.anchoredPosition = new Vector2(-30f, 0f);
            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = entry.name;
            SafeSetFont(nameText);
            nameText.fontSize = 13;
            nameText.fontStyle = isMine ? FontStyles.Bold : FontStyles.Normal;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = Cream;
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            // Time
            GameObject timeObj = new GameObject("Time");
            timeObj.transform.SetParent(rowObj.transform, false);
            RectTransform timeRect = timeObj.AddComponent<RectTransform>();
            timeRect.sizeDelta = new Vector2(80f, 34f);
            timeRect.anchoredPosition = new Vector2(150f, 0f);
            TextMeshProUGUI timeText = timeObj.AddComponent<TextMeshProUGUI>();
            timeText.text = $"{entry.time:F1}s";
            SafeSetFont(timeText);
            timeText.fontSize = 13;
            timeText.fontStyle = FontStyles.Bold;
            timeText.alignment = TextAlignmentOptions.Right;
            timeText.color = Khaki;
        }
    }

    // ------------------------------------------------------------------
    // Procedural sprites & textures
    // ------------------------------------------------------------------

    /// <summary>
    /// Generates a rounded-rectangle sprite (9-sliced). borderWidth 0 = filled shape,
    /// otherwise only an outline ring of that thickness is drawn.
    /// </summary>
    private Sprite MakeRoundedSprite(int w, int h, int radius, int borderWidth)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        generatedTextures.Add(tex);

        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float hw = w * 0.5f;
        float hh = h * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Signed-distance of a rounded box
                float dx = Mathf.Abs(x - cx) - (hw - radius);
                float dy = Mathf.Abs(y - cy) - (hh - radius);
                float outsideDist = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float dist = outsideDist + Mathf.Min(Mathf.Max(dx, dy), 0f);

                float outerAlpha = Mathf.Clamp01(radius - dist + 0.5f);
                float alpha = outerAlpha;
                if (borderWidth > 0)
                {
                    float innerAlpha = Mathf.Clamp01((radius - borderWidth) - dist + 0.5f);
                    alpha = Mathf.Clamp01(outerAlpha - innerAlpha);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        float b = radius + 2f; // 9-slice margins keep corners crisp at any size
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    /// <summary>
    /// A rounded-rect border broken into evenly-spaced dashes around its perimeter,
    /// matching the mockup's dotted slot outline. Uses an angle sweep normalized by the
    /// rect's half-width/half-height so the dashes stay roughly even around a
    /// non-square rect rather than bunching up on the short sides.
    /// </summary>
    private Sprite MakeDashedBorderSprite(int w, int h, int radius, int borderWidth, int dashCount)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        generatedTextures.Add(tex);

        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float hw = w * 0.5f;
        float hh = h * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x - cx) - (hw - radius);
                float dy = Mathf.Abs(y - cy) - (hh - radius);
                float outsideDist = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float dist = outsideDist + Mathf.Min(Mathf.Max(dx, dy), 0f);

                float outerAlpha = Mathf.Clamp01(radius - dist + 0.5f);
                float innerAlpha = Mathf.Clamp01((radius - borderWidth) - dist + 0.5f);
                float ringAlpha = Mathf.Clamp01(outerAlpha - innerAlpha);

                float angle = Mathf.Atan2((y - cy) / hh, (x - cx) / hw);
                float t = (angle + Mathf.PI) / (2f * Mathf.PI);
                float dashPhase = (t * dashCount) % 1f;
                float dashAlpha = dashPhase < 0.5f ? 1f : 0f;

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, ringAlpha * dashAlpha));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Vertical two-tone gradient used as a placeholder when no real step photo exists
    /// in Resources/BatikSteps.
    /// </summary>
    private Sprite MakeGradientSprite(int w, int h, Color top, Color bottom)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        generatedTextures.Add(tex);

        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color rowColor = Color.Lerp(bottom, top, t);
            for (int x = 0; x < w; x++)
            {
                // Subtle diagonal weave pattern for a fabric feel
                float weave = 0.03f * Mathf.Sin((x + y) * 0.55f);
                tex.SetPixel(x, y, new Color(
                    Mathf.Clamp01(rowColor.r + weave),
                    Mathf.Clamp01(rowColor.g + weave),
                    Mathf.Clamp01(rowColor.b + weave), 1f));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private Texture2D CreateRingTexture(int size, int thickness)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float outerRad = size * 0.5f;
        float innerRad = outerRad - thickness;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist >= innerRad && dist <= outerRad)
                {
                    // Basic antialiasing on edges
                    float alpha = 1.0f;
                    if (dist > outerRad - 1.5f) alpha = (outerRad - dist) / 1.5f;
                    else if (dist < innerRad + 1.5f) alpha = (dist - innerRad) / 1.5f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------
    // Celebration / failure effects
    // ------------------------------------------------------------------
    private void SpawnConfetti()
    {
        GameObject confettiObj = new GameObject("Confetti");
        confettiObj.transform.position = transform.position + new Vector3(0f, 0.15f, -0.05f); // Spawn slightly above and in front of the card

        ParticleSystem ps = confettiObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 3.0f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f); // small squares
        main.gravityModifier = 0.5f; // fall down slowly
        main.loop = false;
        main.playOnAwake = false;

        // Particle shape: Cone pointing upwards/forwards
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.1f;
        shape.rotation = new Vector3(-60f, 0f, 0f); // point forward/up

        // Emission: Burst of particles
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 80) });

        // Color over lifetime (colorful confetti!)
        var colorLifecycle = ps.colorOverLifetime;
        colorLifecycle.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.red, 0.0f),
                new GradientColorKey(Color.yellow, 0.25f),
                new GradientColorKey(Color.green, 0.5f),
                new GradientColorKey(Color.cyan, 0.75f),
                new GradientColorKey(Color.magenta, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.7f),
                new GradientAlphaKey(0.0f, 1.0f) // fade out at the end
            }
        );
        colorLifecycle.color = grad;

        // Noise to make it flutter!
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 1.5f;

        // Force Burst
        ps.Play();

        // Auto-destroy after 3 seconds
        Destroy(confettiObj, 4.0f);
    }

    private void PlayCelebrationSound()
    {
        GameObject audioObj = new GameObject("CelebrationSound");
        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.spatialBlend = 0.0f; // 2D sound for clarity
        source.volume = 0.6f;

        // Generate a beautiful C-Major chord chime (C5-E5-G5-C6) procedurally
        int sampleRate = 44100;
        float duration = 1.0f;
        int numSamples = (int)(sampleRate * duration);
        float[] samples = new float[numSamples];

        float[] freqs = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Clamp01(1.0f - t / duration);

            float val = 0f;
            foreach (float f in freqs)
            {
                val += Mathf.Sin(2f * Mathf.PI * f * t);
            }
            samples[i] = (val / freqs.Length) * envelope;
        }

        AudioClip clip = AudioClip.Create("Chime", numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);

        source.PlayOneShot(clip);
        Destroy(audioObj, 2.0f); // clean up
    }

    private void PlayLossSound()
    {
        GameObject audioObj = new GameObject("LossSound");
        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.spatialBlend = 0.0f;
        source.volume = 0.5f;

        // Generate a low-pitch failing buzz sound (sawtooth-like decay)
        int sampleRate = 44100;
        float duration = 0.6f;
        int numSamples = (int)(sampleRate * duration);
        float[] samples = new float[numSamples];

        float freq = 120.0f; // low buzz frequency (B2/C3 range)

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Clamp01(1.0f - t / duration);

            // Sawtooth wave approximation using basic math
            float val = 2.0f * (t * freq - Mathf.Floor(t * freq + 0.5f));
            samples[i] = val * envelope;
        }

        AudioClip clip = AudioClip.Create("Buzz", numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);

        source.PlayOneShot(clip);
        Destroy(audioObj, 1.5f);
    }

    private void PlayWrongBeep()
    {
        GameObject audioObj = new GameObject("WrongBeep");
        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.spatialBlend = 0.0f;
        source.volume = 0.35f;

        // Short, soft double-beep - a nudge rather than a failure buzz
        int sampleRate = 44100;
        float duration = 0.28f;
        int numSamples = (int)(sampleRate * duration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            // Two 0.1s beeps with a small gap between them
            bool inBeep = (t < 0.1f) || (t > 0.16f && t < 0.26f);
            float envelope = inBeep ? 1f : 0f;
            samples[i] = Mathf.Sin(2f * Mathf.PI * 240f * t) * envelope * 0.8f;
        }

        AudioClip clip = AudioClip.Create("WrongBeep", numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);

        source.PlayOneShot(clip);
        Destroy(audioObj, 1.0f);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[r];
            list[r] = tmp;
        }
    }

    private bool IsIndicesInCorrectOrder(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != i) return false;
        }
        return true;
    }

    private void OnDestroy()
    {
        foreach (Texture2D tex in generatedTextures)
        {
            if (tex != null) Destroy(tex);
        }
        generatedTextures.Clear();
    }
}
