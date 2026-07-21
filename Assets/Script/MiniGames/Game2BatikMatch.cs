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
    public class BatikCard : MonoBehaviour
    {
        public int correctStepIndex;   // which slot (0-4) this card belongs in
        public bool inSlot;            // currently parked in a slot (vs the tray)
        public int anchorIndex;        // index of the slot/tray column it occupies
        public Vector3 targetLocalPosition;
        public bool isGrabbed;

        private XRGrabInteractable grabInteractable;
        private Game2BatikMatch controller;

        public void Setup(Game2BatikMatch gameController, int stepIndex, Vector2 cardSize)
        {
            controller = gameController;
            correctStepIndex = stepIndex;

            var boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(cardSize.x, cardSize.y, 14f);

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
            grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grabInteractable.trackPosition = true;
            grabInteractable.trackRotation = false; // keep the card upright/readable
            grabInteractable.useDynamicAttach = true;

            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        public void DisableGrab()
        {
            if (grabInteractable != null) Destroy(grabInteractable);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            isGrabbed = true;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            isGrabbed = false;
            controller.OnCardReleased(this);
        }

        private void Update()
        {
            if (!isGrabbed)
            {
                // Smoothly glide back to the assigned slot/tray anchor
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, Time.deltaTime * 10f);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, Time.deltaTime * 10f);
            }
        }

        private void OnDestroy()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                grabInteractable.selectExited.RemoveListener(OnReleased);
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
    // Palette (matches the sage/khaki mockup)
    // ------------------------------------------------------------------
    private static readonly Color SageBg = new Color(0.545f, 0.557f, 0.486f, 1f);   // opaque sage panel
    private static readonly Color SageDark = new Color(0.475f, 0.49f, 0.42f, 1f);   // slot interior
    private static readonly Color Khaki = new Color(0.788f, 0.765f, 0.353f, 1f);    // chips, borders, buttons
    private static readonly Color KhakiHover = new Color(0.87f, 0.85f, 0.48f, 1f);
    private static readonly Color DarkOlive = new Color(0.22f, 0.235f, 0.16f, 1f);  // text on khaki, close button
    private static readonly Color DarkOliveHover = new Color(0.33f, 0.35f, 0.25f, 1f);
    private static readonly Color Cream = new Color(0.955f, 0.945f, 0.895f, 1f);    // title/feedback text

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
    // Optional real photos: drop sprites into Assets/Resources/BatikSteps/<key>.png
    private static readonly string[] StepSpriteKeys = { "melukis_corak", "mencanting", "mewarna", "melorod", "menjemur" };
    // Fallback gradient tones per step (top, bottom) used when no photo sprite exists
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
    private List<GameObject> spawnedButtons = new List<GameObject>();

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI feedbackText;
    private TextMeshProUGUI descText;

    // Countdown state (45 seconds for 5 cards + answer check)
    private const float TotalGameTime = 45.0f;
    private float remainingTime = TotalGameTime;
    private bool isGameOver = false;

    // Donut timer UI
    private GameObject donutContainer;
    private Image donutImage;
    private TextMeshProUGUI donutText;

    // Generated art assets (tracked for cleanup)
    private List<Texture2D> generatedTextures = new List<Texture2D>();
    private Sprite roundedFillSprite;
    private Sprite roundedBorderSprite;
    private Sprite circleSprite;

    private void Start()
    {
        // 0. Generate reusable rounded-corner sprites
        roundedFillSprite = MakeRoundedSprite(64, 64, 14, 0);
        roundedBorderSprite = MakeRoundedSprite(64, 64, 14, 4);
        circleSprite = MakeRoundedSprite(48, 48, 24, 0);

        // 1. Enlarge the panel canvas to a landscape layout and make it opaque sage
        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null) rootRect.sizeDelta = PanelSize;

        Transform bg = transform.Find("Background");
        if (bg != null)
        {
            Image bgImage = bg.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.material = null;          // drop the translucent glassmorphism material
                bgImage.sprite = roundedFillSprite;
                bgImage.type = Image.Type.Sliced;
                bgImage.color = SageBg;           // fully opaque
            }
        }

        // 2. Restyle the header texts inherited from the panel prefab
        titleText = transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        feedbackText = transform.Find("ArtistYearText")?.GetComponent<TextMeshProUGUI>();
        descText = transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();

        if (titleText != null)
        {
            SetupTextRect(titleText.rectTransform, new Vector2(-40f, 172f), new Vector2(320f, 44f));
            titleText.text = "Susunkan Proses Pembuatan Batik";
            titleText.fontSize = 19;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.color = Cream;
        }
        if (feedbackText != null)
        {
            SetupTextRect(feedbackText.rectTransform, new Vector2(0f, -2f), new Vector2(430f, 26f));
            feedbackText.text = "Heret kad ke petak 1-5 mengikut urutan proses batik.";
            feedbackText.fontSize = 11;
            feedbackText.fontStyle = FontStyles.Normal;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.color = Cream;
        }
        if (descText != null)
        {
            SetupTextRect(descText.rectTransform, new Vector2(0f, -10f), new Vector2(330f, 300f));
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = Cream;
            descText.text = "";
        }

        // 3. Close button (dark circle with X, top-right like the mockup)
        CreateCloseButton();

        // 4. Donut countdown timer (top-right, left of the close button)
        CreateDonutTimerUI();

        // 5. Build slots, chevrons, cards and the check-answer button
        BuildBoard();
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

            // Khaki border ring on top of the fill
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(slotObj.transform, false);
            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            Image border = borderObj.AddComponent<Image>();
            border.sprite = roundedBorderSprite;
            border.type = Image.Type.Sliced;
            border.color = Khaki;

            // Big slot number
            GameObject numObj = new GameObject("Number");
            numObj.transform.SetParent(slotObj.transform, false);
            RectTransform numRect = numObj.AddComponent<RectTransform>();
            numRect.anchorMin = Vector2.zero;
            numRect.anchorMax = Vector2.one;
            numRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI numText = numObj.AddComponent<TextMeshProUGUI>();
            numText.text = (i + 1).ToString();
            numText.fontSize = 26;
            numText.fontStyle = FontStyles.Bold;
            numText.alignment = TextAlignmentOptions.Center;
            numText.color = Khaki;
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
            chevText.text = ">"; // plain ASCII: guaranteed glyph in the default TMP font
            chevText.fontSize = 30;
            chevText.fontStyle = FontStyles.Bold;
            chevText.alignment = TextAlignmentOptions.Center;
            chevText.color = Cream;
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

        // --- "Periksa Jawaban ›" button (bottom right) ---
        GameObject checkBtn = CreateStyledButton("Periksa Jawaban  >", new Vector2(135f, -172f), new Vector2(180f, 38f), CheckAnswer);
        spawnedButtons.Add(checkBtn);
    }

    private BatikCard CreateCard(int stepIndex)
    {
        GameObject cardObj = new GameObject($"Card_{StepNames[stepIndex]}");
        cardObj.transform.SetParent(boardRoot.transform, false);
        RectTransform cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.sizeDelta = CardSize;

        // Cream rounded card base
        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.sprite = roundedFillSprite;
        cardBg.type = Image.Type.Sliced;
        cardBg.color = Cream;

        // Photo area (real sprite from Resources/BatikSteps if present, else a themed gradient)
        GameObject photoObj = new GameObject("Photo");
        photoObj.transform.SetParent(cardObj.transform, false);
        RectTransform photoRect = photoObj.AddComponent<RectTransform>();
        photoRect.sizeDelta = new Vector2(CardSize.x - 10f, 76f);
        photoRect.anchoredPosition = new Vector2(0f, 17f);
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

        // Khaki label chip at the bottom of the card
        GameObject chipObj = new GameObject("LabelChip");
        chipObj.transform.SetParent(cardObj.transform, false);
        RectTransform chipRect = chipObj.AddComponent<RectTransform>();
        chipRect.sizeDelta = new Vector2(CardSize.x - 10f, 26f);
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
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 6f;
        labelText.fontSizeMax = 9.5f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = DarkOlive;

        BatikCard card = cardObj.AddComponent<BatikCard>();
        card.Setup(this, stepIndex, CardSize);
        return card;
    }

    // ------------------------------------------------------------------
    // Drag & drop logic
    // ------------------------------------------------------------------
    public void OnCardReleased(BatikCard card)
    {
        if (isGameOver)
        {
            card.targetLocalPosition = AnchorPosition(card.inSlot, card.anchorIndex);
            return;
        }

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
        // Tear down the old board and buttons
        if (boardRoot != null) Destroy(boardRoot);
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
        donutText.fontSize = 12;
        donutText.fontStyle = FontStyles.Bold;
        donutText.alignment = TextAlignmentOptions.Center;
        donutText.color = Cream;
    }

    private void Update()
    {
        if (isGameOver) return;

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
        // Force invariant culture so the decimal separator is always '.' - a comma-locale
        // headset would otherwise emit "12,34" and Firestore would reject the write as invalid JSON.
        string timeValue = time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        string safeName = name.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string json = $"{{\"fields\":{{\"name\":{{\"stringValue\":\"{safeName}\"}},\"time\":{{\"doubleValue\":{timeValue}}}}}}}";

        using (UnityWebRequest request = new UnityWebRequest(postUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
        }

        // Fetch top scores from Firestore using runQuery
        if (descText != null)
        {
            descText.text = "<align=center><color=#B8D8F0>Memuatkan Papan Pendahulu...</color></align>";
        }

        string queryUrl = "https://firestore.googleapis.com/v1/projects/museum-mixed-reality-app/databases/(default)/documents:runQuery";
        string queryJson = "{\"structuredQuery\":{\"from\":[{\"collectionId\":\"leaderboard\"}],\"orderBy\":[{\"field\":{\"fieldPath\":\"time\"},\"direction\":\"ASCENDING\"}],\"limit\":5}}";

        using (UnityWebRequest queryRequest = new UnityWebRequest(queryUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(queryJson);
            queryRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            queryRequest.downloadHandler = new DownloadHandlerBuffer();
            queryRequest.SetRequestHeader("Content-Type", "application/json");

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
        if (string.IsNullOrEmpty(json) || json == "null" || json == "[]") return list;

        // Parse Firestore documents response by searching for document boundaries
        string[] documents = json.Split(new string[] { "\"document\"" }, System.StringSplitOptions.None);
        foreach (string doc in documents)
        {
            string name = "";
            int nameIndex = doc.IndexOf("\"stringValue\":\"");
            if (nameIndex != -1)
            {
                int start = nameIndex + 15;
                int end = doc.IndexOf("\"", start);
                if (end != -1) name = doc.Substring(start, end - start);
            }

            float time = 999f;
            int timeIndex = doc.IndexOf("\"doubleValue\":");
            if (timeIndex != -1)
            {
                int start = timeIndex + 14;
                // Search for the closing brace/comma AFTER 'start'. Searching from index 0
                // would find the '}' that closes the earlier "stringValue" object (which sits
                // before "doubleValue"), giving a negative length and throwing in Substring -
                // that silently aborted the whole leaderboard parse and left the board blank.
                int end = doc.IndexOf("}", start);
                if (end == -1) end = doc.IndexOf(",", start);
                if (end != -1)
                {
                    string timeStr = doc.Substring(start, end - start).Trim().Replace("}", "");
                    // Firestore always returns '.' as the decimal separator, so parse with the
                    // invariant culture (a comma-locale headset would otherwise fail to parse).
                    float.TryParse(timeStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out time);
                }
            }

            if (!string.IsNullOrEmpty(name) && time < 990f)
            {
                list.Add(new LeaderboardEntry { name = name, time = time });
            }
        }

        // Sort by time ascending (Firestore query does this, but we sort locally just to be robust!)
        list.Sort((a, b) => a.time.CompareTo(b.time));
        return list;
    }

    private void DisplayLeaderboard(List<LeaderboardEntry> list)
    {
        if (descText == null) return;

        if (titleText != null) titleText.text = "Papan Pendahulu";

        // Gold/silver/bronze rank markers via color tags (emoji glyphs are missing
        // from the default TMP font and would render as empty boxes on the headset)
        string board = "<align=center><b>TURUTAN TERPANTAS</b>\n\n";
        for (int i = 0; i < list.Count && i < 5; i++)
        {
            string prefix =
                (i == 0) ? "<color=#E8C547><b>1.</b></color> " :
                (i == 1) ? "<color=#C0C0C0><b>2.</b></color> " :
                (i == 2) ? "<color=#CD7F32><b>3.</b></color> " :
                $"{i + 1}. ";
            board += $"<align=left>{prefix}{list[i].name} <line-height=0>\n<align=right>{list[i].time:F1}s<line-height=1em>\n";
        }

        if (list.Count == 0)
        {
            board += "\nTiada rekod lagi. Jadilah yang pertama!";
        }

        descText.text = board;
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
