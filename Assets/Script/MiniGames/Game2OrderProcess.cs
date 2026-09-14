using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Mini-Game 2: "Susun Langkah & Kisah" (Combined Mini-Game with 5 questions).
/// Inherits from BaseGame.
/// 
/// Question 1 (1/5): "Proses Pembuatan Batik" (5 illustration cards: 1 1.png .. 5 1.png).
/// Questions 2-5 (2/5 .. 5/5): Timeline History questions with dynamic text cards:
/// - Question 2 (2/5): "Ekonomi Trengganu"
/// - Question 3 (3/5): "Tragedi Megat Panji Alam"
/// - Question 4 (4/5): "Asal Usul 'Taring Anu'"
/// - Question 5 (5/5): "Rombongan Kelantan"
/// 
/// Correctly ordering all 5 cards in each question advances to the next question.
/// Completing Question 5 ends the game and shows the Leaderboard for game_2.
/// </summary>
public class Game2OrderProcess : BaseGame
{
    [System.Serializable]
    public class TimelineItem
    {
        [Tooltip("Dynamic text displayed on this card's Text (TMP).")]
        [TextArea(2, 4)]
        public string text;

        [Tooltip("Target slot index for this card (1-based [1..5] or 0-based [0..4]).")]
        public int correctSlotIndex;
    }

    [System.Serializable]
    public class GameQuestion
    {
        [Tooltip("Question topic/title (e.g. 'Proses Pembuatan Batik', 'Ekonomi Trengganu').")]
        public string questionTitle;

        [Tooltip("If true, cards use Batik process illustration sprites. If false, cards display dynamic text.")]
        public bool isImageQuestion;

        [Tooltip("Timeline items for text-based questions (Questions 2..5).")]
        public TimelineItem[] timelineItems;
    }

    [Header("Questions Configuration (5 Questions Total)")]
    public GameQuestion[] questions = new GameQuestion[]
    {
        // Question 1 (1/5)
        new GameQuestion
        {
            questionTitle = "Proses Pembuatan Batik",
            isImageQuestion = true,
            timelineItems = new TimelineItem[]
            {
                new TimelineItem { text = "Melukis corak lilin", correctSlotIndex = 1 },
                new TimelineItem { text = "Mewarna corak", correctSlotIndex = 2 },
                new TimelineItem { text = "Mencelup warna latar", correctSlotIndex = 3 },
                new TimelineItem { text = "Merebus membuang lilin", correctSlotIndex = 4 },
                new TimelineItem { text = "Membasuh & menjemur", correctSlotIndex = 5 }
            }
        },
        // Question 2 (2/5)
        new GameQuestion
        {
            questionTitle = "Ekonomi Trengganu",
            isImageQuestion = false,
            timelineItems = new TimelineItem[]
            {
                new TimelineItem { text = "Bandar pelabuhan & hasil hutan merancakkan ekonomi.", correctSlotIndex = 1 },
                new TimelineItem { text = "Terengganu jadi pengeluar utama lada hitam.", correctSlotIndex = 2 },
                new TimelineItem { text = "Galakan tanaman kopi, tebu & galian emas/timah.", correctSlotIndex = 3 },
                new TimelineItem { text = "Tambang bijih timah & besi mula dibuka.", correctSlotIndex = 4 },
                new TimelineItem { text = "Penemuan minyak tingkatkan ekonomi negeri.", correctSlotIndex = 5 }
            }
        },
        // Question 3 (3/5)
        new GameQuestion
        {
            questionTitle = "Tragedi Megat Panji Alam",
            isImageQuestion = false,
            timelineItems = new TimelineItem[]
            {
                new TimelineItem { text = "Hang Tuah bawa Tun Teja dari Pahang ke Melaka.", correctSlotIndex = 1 },
                new TimelineItem { text = "Megat Panji Alam tahu tunangnya dilarikan.", correctSlotIndex = 2 },
                new TimelineItem { text = "Beliau ke Pahang untuk menentang Hang Tuah.", correctSlotIndex = 3 },
                new TimelineItem { text = "Beliau ditikam Hang Jebat & Kasturi di tangga istana.", correctSlotIndex = 4 },
                new TimelineItem { text = "Jenazah Megat Panji Alam dimakamkan di Terengganu sebagai pahlawan negeri.", correctSlotIndex = 5 }
            }
        },
        // Question 4 (4/5)
        new GameQuestion
        {
            questionTitle = "Asal Usul \"Taring Anu\"",
            isImageQuestion = false,
            timelineItems = new TimelineItem[]
            {
                new TimelineItem { text = "Pemburu Pahang berburu di Hulu Terengganu.", correctSlotIndex = 1 },
                new TimelineItem { text = "Taring haiwan misteri dijumpai di sungai.", correctSlotIndex = 2 },
                new TimelineItem { text = "Pemburu panggil tempat itu \"Taring Anu\".", correctSlotIndex = 3 },
                new TimelineItem { text = "Mereka sebut \"Taring Anu\" bila ditanya lokasi.", correctSlotIndex = 4 },
                new TimelineItem { text = "\"Taring Anu\" berubah jadi Terengganu.", correctSlotIndex = 5 }
            }
        },
        // Question 5 (5/5)
        new GameQuestion
        {
            questionTitle = "Rombongan Kelantan",
            isImageQuestion = false,
            timelineItems = new TimelineItem[]
            {
                new TimelineItem { text = "Rombongan Kelantan belayar waktu hujan renyai.", correctSlotIndex = 1 },
                new TimelineItem { text = "Cahaya terang di langit dipanggil \"Ular Mayang\".", correctSlotIndex = 2 },
                new TimelineItem { text = "Penduduk tempatan panggil kawasan itu \"Ganu\".", correctSlotIndex = 3 },
                new TimelineItem { text = "Rombongan kata \"Terang sungguh Ganu di sini\".", correctSlotIndex = 4 },
                new TimelineItem { text = "Perkataan \"Terangnya Ganu\" menjadi Terengganu.", correctSlotIndex = 5 }
            }
        }
    };

    [Header("Batik Process Sprites (Auto-Cached from cards if empty)")]
    [Tooltip("The 5 process illustration sprites (1 1.png .. 5 1.png).")]
    public Sprite[] batikSprites = new Sprite[5];

    [Header("UI References (Assign in Inspector or Auto-Found)")]
    [Tooltip("Task title / progress header text (e.g. TaskText).")]
    public TMP_Text taskText;

    [Tooltip("Process card GameObjects in their correct order (Process1, Process2, Process3, Process4, Process5).")]
    public GameObject[] processObjects;

    [Tooltip("Parent transform containing Process1 .. Process5 buttons.")]
    public Transform processLayout;

    [Tooltip("Slot indicator GameObjects or Transforms (Slot 1, Slot 2, Slot 3, Slot 4, Slot 5). Optional.")]
    public Transform[] slotObjects;

    [Tooltip("Parent transform containing 1, 2, 3, 4, 5 slot indicator objects.")]
    public Transform numberSlotPanel;

    [Tooltip("'Periksa Jawaban' Check Button.")]
    public Button checkAnswerButton;

    [Tooltip("Wrong answer warning text object (e.g. 'Wrong Text').")]
    public GameObject wrongText;

    [Header("Animation & Drag Settings")]
    [Tooltip("Smooth lerp speed when snapping cards into slots.")]
    public float snapSpeed = 15f;

    // ─────────────────────────────────────────────────────────────────────────
    // Internal State
    // ─────────────────────────────────────────────────────────────────────────

    private int currentRoundIndex = 0;
    private List<DraggableProcessCard> cards = new List<DraggableProcessCard>();
    private Vector3[] slotLocalPositions = new Vector3[5];
    private Vector3[] bottomBankLocalPositions = new Vector3[5];
    private DraggableProcessCard[] slotAssignments = new DraggableProcessCard[5];

    private Sprite darkTextCardSprite = null;
    private Sprite lightTextCardSprite = null;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle & BaseGame Overrides
    // ─────────────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        AutoFindUIReferences();
        CacheBatikSprites();
        InitializeCardsAndSlots();
        WireCheckButton();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ThemeManager.OnThemeChanged += HandleThemeChanged;
        StartCoroutine(DelayRecheckSlotPositions());
    }

    private System.Collections.IEnumerator DelayRecheckSlotPositions()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        CalculateSlotPositions();
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && !cards[i].IsDragging)
            {
                if (cards[i].AssignedSlotIndex >= 0 && cards[i].AssignedSlotIndex < slotLocalPositions.Length)
                {
                    cards[i].TargetLocalPosition = slotLocalPositions[cards[i].AssignedSlotIndex];
                }
                else if (cards[i].BottomBankIndex >= 0 && cards[i].BottomBankIndex < bottomBankLocalPositions.Length)
                {
                    cards[i].TargetLocalPosition = bottomBankLocalPositions[cards[i].BottomBankIndex];
                }
            }
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ThemeManager.OnThemeChanged -= HandleThemeChanged;
    }

    private void HandleThemeChanged(UIThemeMode newTheme)
    {
        ApplyCardTheme(newTheme);
    }

    /// <summary>
    /// Overridden from BaseGame. Triggered automatically on OnEnable.
    /// Resets to Question 1 (Batik) and shuffles cards.
    /// </summary>
    public override void OnGameStart()
    {
        base.OnGameStart();

        currentRoundIndex = 0;
        LoadQuestion(currentRoundIndex);
    }

    public override void OnGameEnd()
    {
        base.OnGameEnd();
        if (wrongText != null)
        {
            wrongText.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Question Loading & Card Styling
    // ─────────────────────────────────────────────────────────────────────────

    private void CacheBatikSprites()
    {
        bool hasAllSprites = true;
        if (batikSprites != null && batikSprites.Length >= 5)
        {
            for (int i = 0; i < 5; i++)
            {
                if (batikSprites[i] == null) { hasAllSprites = false; break; }
            }
        }
        else
        {
            hasAllSprites = false;
            batikSprites = new Sprite[5];
        }

        if (hasAllSprites) return;

        // Auto-cache from processObjects Image components
        if (processObjects != null && processObjects.Length >= 5)
        {
            for (int i = 0; i < 5; i++)
            {
                if (processObjects[i] != null && batikSprites[i] == null)
                {
                    Image img = processObjects[i].GetComponent<Image>();
                    if (img != null && img.sprite != null)
                    {
                        batikSprites[i] = img.sprite;
                    }
                }
            }
        }
    }

    private void LoadQuestion(int roundIdx)
    {
        if (wrongText != null) wrongText.SetActive(false);
        if (checkAnswerButton != null) checkAnswerButton.interactable = true;

        if (questions == null || questions.Length == 0) return;

        roundIdx = Mathf.Clamp(roundIdx, 0, questions.Length - 1);
        currentRoundIndex = roundIdx;
        GameQuestion qData = questions[roundIdx];

        int displayRoundNum = roundIdx + 1;
        int totalQuestions = questions.Length;

        // 1. Update TaskText with (X/5) progress counter
        if (taskText != null)
        {
            string title = string.IsNullOrWhiteSpace(qData.questionTitle) ? "Artefak" : qData.questionTitle.Trim();
            if (qData.isImageQuestion)
            {
                string formatted = title.StartsWith("Susun", System.StringComparison.OrdinalIgnoreCase)
                    ? title
                    : $"Susunkan {title}";
                taskText.text = $"({displayRoundNum}/{totalQuestions}) {formatted}";
            }
            else
            {
                string formatted = title.StartsWith("Susun", System.StringComparison.OrdinalIgnoreCase)
                    ? title
                    : $"Susunkan garis masa {title}";
                taskText.text = $"({displayRoundNum}/{totalQuestions}) {formatted}";
            }

            taskText.enableAutoSizing = true;
            taskText.fontSizeMin = 10f;
            taskText.fontSizeMax = 22f;
        }

        // 2. Setup visual presentation on each card
        int cardCount = cards.Count;
        for (int i = 0; i < cardCount; i++)
        {
            DraggableProcessCard cardComp = cards[i];
            if (cardComp == null) continue;

            Image img = cardComp.GetComponent<Image>();
            TMP_Text label = cardComp.GetComponentInChildren<TMP_Text>(true);

            if (qData.isImageQuestion)
            {
                // QUESTION 1: Batik Process Illustration
                cardComp.Setup(this, i); // Expected step index 0..4

                if (img != null)
                {
                    if (batikSprites != null && i < batikSprites.Length && batikSprites[i] != null)
                    {
                        img.sprite = batikSprites[i];
                    }
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                }

                if (label != null)
                {
                    label.text = "";
                    label.gameObject.SetActive(false);
                }
            }
            else
            {
                // QUESTIONS 2..5: Timeline History Texts
                int targetStepIdx = i;
                string itemText = "";

                if (qData.timelineItems != null && i < qData.timelineItems.Length)
                {
                    TimelineItem tItem = qData.timelineItems[i];
                    itemText = tItem.text;
                    targetStepIdx = (tItem.correctSlotIndex >= 1 && tItem.correctSlotIndex <= 5)
                        ? tItem.correctSlotIndex - 1
                        : Mathf.Clamp(tItem.correctSlotIndex, 0, 4);
                }

                cardComp.Setup(this, targetStepIdx);

                bool isDarkMode = ThemeManager.Instance == null || ThemeManager.Instance.currentTheme == UIThemeMode.Dark;
                Sprite cardSprite = isDarkMode ? GetOrCreateDarkCardSprite() : GetOrCreateLightCardSprite();
                Color textColor = isDarkMode ? new Color(0.97f, 0.98f, 0.96f, 1f) : new Color(0.11f, 0.13f, 0.10f, 1f);

                // Configure card background for text mode with smooth anti-aliased rounded corners
                if (img != null)
                {
                    img.sprite = cardSprite;
                    img.type = Image.Type.Sliced;
                    img.preserveAspect = false;
                    img.pixelsPerUnitMultiplier = 2.4f;
                    img.color = Color.white;
                }

                // Ensure child TMP_Text exists
                if (label == null)
                {
                    label = CreateCardTextChild(cardComp.gameObject);
                }

                if (label != null)
                {
                    label.gameObject.SetActive(true);
                    label.text = itemText;
                    label.color = textColor;
                    label.fontStyle = FontStyles.Bold;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 8f;
                    label.fontSizeMax = 15f;
                    label.enableWordWrapping = true;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.alignment = TextAlignmentOptions.Center;
                }
            }
        }

        // 3. Recalculate slots and randomize bottom bank
        CalculateSlotPositions();
        RandomizeCardPositions();
    }

    /// <summary>
    /// Applies theme-specific rounded card styling and typography to active text answer cards.
    /// </summary>
    public void ApplyCardTheme(UIThemeMode mode)
    {
        if (questions == null || currentRoundIndex < 0 || currentRoundIndex >= questions.Length) return;
        if (questions[currentRoundIndex].isImageQuestion) return;

        bool isDark = (mode == UIThemeMode.Dark);
        Sprite cardSprite = isDark ? GetOrCreateDarkCardSprite() : GetOrCreateLightCardSprite();
        Color textCol = isDark ? new Color(0.97f, 0.98f, 0.96f, 1f) : new Color(0.11f, 0.13f, 0.10f, 1f);

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;

            Image img = cards[i].GetComponent<Image>();
            if (img != null)
            {
                img.sprite = cardSprite;
                img.type = Image.Type.Sliced;
                img.preserveAspect = false;
                img.pixelsPerUnitMultiplier = 2.4f;
                img.color = Color.white;
            }

            TMP_Text label = cards[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = textCol;
                label.fontStyle = FontStyles.Bold;
            }
        }
    }

    private TMP_Text CreateCardTextChild(GameObject cardObj)
    {
        GameObject textObj = new GameObject("CardText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(cardObj.transform, false);

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(9f, 11f);
        rt.offsetMax = new Vector2(-9f, -11f);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;

        // Borrow font asset from an existing TMP in this panel
        if (taskText != null && taskText.font != null)
        {
            tmp.font = taskText.font;
        }
        else
        {
            TMP_Text sample = GetComponentInChildren<TMP_Text>(true);
            if (sample != null && sample.font != null)
            {
                tmp.font = sample.font;
            }
            else
            {
                TMP_FontAsset cardoFont = Resources.Load<TMP_FontAsset>("Fonts/Cardo-Regular SDF");
                if (cardoFont != null) tmp.font = cardoFont;
            }
        }

        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 8f;
        tmp.fontSizeMax = 15f;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return tmp;
    }

    private Sprite GetOrCreateDarkCardSprite()
    {
        if (darkTextCardSprite == null)
        {
            Color fillColor = new Color(0.086f, 0.102f, 0.094f, 0.92f); // #161A18 rich dark obsidian slate
            Color borderColor = new Color(0.749f, 0.733f, 0.400f, 0.85f); // #BFBB66 warm gold/olive metallic outline matching slot 11.png
            darkTextCardSprite = CreateCardBoxSprite(256, 256, 48f, 5f, fillColor, borderColor, new Vector4(48, 48, 48, 48), 100f);
            darkTextCardSprite.name = "DarkAnswerCard_Procedural";
        }
        return darkTextCardSprite;
    }

    private Sprite GetOrCreateLightCardSprite()
    {
        if (lightTextCardSprite == null)
        {
            Color fillColor = new Color(0.957f, 0.965f, 0.933f, 0.94f); // #F4F6EE frosted cream / light sage
            Color borderColor = new Color(0.647f, 0.698f, 0.596f, 0.88f); // #A5B298 soft sage-olive rim
            lightTextCardSprite = CreateCardBoxSprite(256, 256, 48f, 5f, fillColor, borderColor, new Vector4(48, 48, 48, 48), 100f);
            lightTextCardSprite.name = "LightAnswerCard_Procedural";
        }
        return lightTextCardSprite;
    }

    private static Sprite CreateCardBoxSprite(int width, int height, float radius, float borderWidth, Color fillColor, Color borderColor, Vector4 borderInset, float pixelsPerUnit = 100f)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[width * height];
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;
        float bX = halfW - radius;
        float bY = halfH - radius;

        byte fillR = (byte)Mathf.RoundToInt(fillColor.r * 255f);
        byte fillG = (byte)Mathf.RoundToInt(fillColor.g * 255f);
        byte fillB = (byte)Mathf.RoundToInt(fillColor.b * 255f);
        byte fillA = (byte)Mathf.RoundToInt(fillColor.a * 255f);

        byte borderR = (byte)Mathf.RoundToInt(borderColor.r * 255f);
        byte borderG = (byte)Mathf.RoundToInt(borderColor.g * 255f);
        byte borderB = (byte)Mathf.RoundToInt(borderColor.b * 255f);
        byte borderA = (byte)Mathf.RoundToInt(borderColor.a * 255f);

        for (int y = 0; y < height; y++)
        {
            float py = (y + 0.5f) - halfH;
            for (int x = 0; x < width; x++)
            {
                float px = (x + 0.5f) - halfW;

                float qx = Mathf.Abs(px) - bX;
                float qy = Mathf.Abs(py) - bY;
                float d = Mathf.Min(Mathf.Max(qx, qy), 0f) + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - radius;

                if (d > 0.5f)
                {
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    float outerAlpha = Mathf.Clamp01(0.5f - d);

                    if (d >= -borderWidth)
                    {
                        float t = Mathf.Clamp01((-d) / Mathf.Max(borderWidth, 0.001f));
                        byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(borderR, fillR, t * 0.4f));
                        byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(borderG, fillG, t * 0.4f));
                        byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(borderB, fillB, t * 0.4f));
                        byte a = (byte)Mathf.RoundToInt(Mathf.Lerp(borderA, fillA, t) * outerAlpha);
                        pixels[y * width + x] = new Color32(r, g, b, a);
                    }
                    else
                    {
                        byte a = (byte)Mathf.RoundToInt(fillA * outerAlpha);
                        pixels[y * width + x] = new Color32(fillR, fillG, fillB, a);
                    }
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, borderInset);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Initialization & Setup
    // ─────────────────────────────────────────────────────────────────────────

    private void AutoFindUIReferences()
    {
        if (taskText == null)
        {
            Transform t = FindDeepChild(transform, "TaskText") ?? FindDeepChild(transform, "TitleText") ?? FindDeepChild(transform, "HeaderTitle");
            if (t != null) taskText = t.GetComponent<TMP_Text>();
        }

        if (processLayout == null)
        {
            Transform t = FindDeepChild(transform, "ProcessLayout");
            if (t != null) processLayout = t;
        }

        if (numberSlotPanel == null)
        {
            Transform t = FindDeepChild(transform, "NumberSlotPanel");
            if (t != null) numberSlotPanel = t;
        }

        if (checkAnswerButton == null)
        {
            Transform t = FindDeepChild(transform, "CheckButton")
                        ?? FindDeepChild(transform, "CheckAnswerButton")
                        ?? FindDeepChild(transform, "ButtonCheck");
            if (t != null) checkAnswerButton = t.GetComponent<Button>();
        }

        if (wrongText == null)
        {
            Transform t = FindDeepChild(transform, "Wrong Text")
                        ?? FindDeepChild(transform, "WrongText")
                        ?? FindDeepChild(transform, "TextWrong");
            if (t != null) wrongText = t.gameObject;
        }
    }

    private int GetStepIndexFromName(string objName, int fallbackIndex)
    {
        if (string.IsNullOrEmpty(objName)) return fallbackIndex;

        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(objName, @"\d+");
        if (match.Success && int.TryParse(match.Value, out int num))
        {
            return num - 1;
        }

        return fallbackIndex;
    }

    private void InitializeCardsAndSlots()
    {
        cards.Clear();

        // 1. Gather cards from processObjects array if assigned in Inspector
        if (processObjects != null && processObjects.Length > 0)
        {
            for (int i = 0; i < processObjects.Length; i++)
            {
                GameObject obj = processObjects[i];
                if (obj == null) continue;

                DraggableProcessCard cardComp = obj.GetComponent<DraggableProcessCard>() ?? obj.AddComponent<DraggableProcessCard>();

                int stepIdx = GetStepIndexFromName(obj.name, i);
                cardComp.Setup(this, stepIdx);
                cards.Add(cardComp);

                if (processLayout == null && obj.transform.parent != null)
                {
                    processLayout = obj.transform.parent;
                }
            }
        }
        // Fallback: gather from processLayout children
        else if (processLayout != null)
        {
            int childCount = processLayout.childCount;
            List<GameObject> objs = new List<GameObject>();
            for (int i = 0; i < childCount; i++)
            {
                Transform child = processLayout.GetChild(i);
                DraggableProcessCard cardComp = child.GetComponent<DraggableProcessCard>() ?? child.gameObject.AddComponent<DraggableProcessCard>();

                int stepIdx = GetStepIndexFromName(child.name, i);
                cardComp.Setup(this, stepIdx);
                cards.Add(cardComp);
                objs.Add(child.gameObject);
            }
            processObjects = objs.ToArray();
        }

        if (processLayout != null)
        {
            LayoutGroup layoutGroup = processLayout.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.enabled = false;
            }
        }

        int totalCount = cards.Count;
        if (totalCount == 0)
        {
            Debug.LogWarning("Game2OrderProcess: No process cards/objects found to initialize.");
            return;
        }

        if (slotLocalPositions.Length != totalCount)
        {
            slotLocalPositions = new Vector3[totalCount];
            bottomBankLocalPositions = new Vector3[totalCount];
            slotAssignments = new DraggableProcessCard[totalCount];
        }

        CalculateSlotPositions();
    }

    private void CalculateSlotPositions()
    {
        int count = cards.Count;
        if (count == 0) return;

        if (slotLocalPositions.Length != count)
        {
            slotLocalPositions = new Vector3[count];
            bottomBankLocalPositions = new Vector3[count];
            slotAssignments = new DraggableProcessCard[count];
        }

        Canvas.ForceUpdateCanvases();

        bool positionsValid = false;

        // 1. Try slotObjects array first
        if (slotObjects != null && slotObjects.Length >= count)
        {
            bool allNotNull = true;
            for (int i = 0; i < count; i++)
            {
                if (slotObjects[i] == null) { allNotNull = false; break; }
            }

            if (allNotNull)
            {
                for (int i = 0; i < count; i++)
                {
                    Transform slotChild = slotObjects[i];
                    if (processLayout != null)
                    {
                        Vector3 worldPos = slotChild.position;
                        Vector3 localPos = processLayout.InverseTransformPoint(worldPos);
                        localPos.z = (i < cards.Count && cards[i] != null) ? cards[i].transform.localPosition.z : 0f;
                        slotLocalPositions[i] = localPos;
                    }
                    else
                    {
                        slotLocalPositions[i] = slotChild.localPosition;
                    }
                }
                positionsValid = true;
            }
        }
        // 2. Try numberSlotPanel children second
        if (!positionsValid && numberSlotPanel != null && numberSlotPanel.childCount >= count)
        {
            for (int i = 0; i < count; i++)
            {
                Transform slotChild = numberSlotPanel.GetChild(i);
                if (processLayout != null)
                {
                    Vector3 worldPos = slotChild.position;
                    Vector3 localPos = processLayout.InverseTransformPoint(worldPos);
                    localPos.z = (i < cards.Count && cards[i] != null) ? cards[i].transform.localPosition.z : 0f;
                    slotLocalPositions[i] = localPos;
                }
                else
                {
                    slotLocalPositions[i] = slotChild.localPosition;
                }
            }
            positionsValid = true;
        }

        // Sort slotLocalPositions strictly left-to-right by X coordinate
        if (positionsValid)
        {
            System.Array.Sort(slotLocalPositions, (a, b) => a.x.CompareTo(b.x));

            // Validate that positions are spread out properly (span > 150f and consecutive distance > 30f)
            float span = slotLocalPositions[count - 1].x - slotLocalPositions[0].x;
            if (span < 150f)
            {
                positionsValid = false;
            }
            else
            {
                for (int i = 0; i < count - 1; i++)
                {
                    if (slotLocalPositions[i + 1].x - slotLocalPositions[i].x < 30f)
                    {
                        positionsValid = false;
                        break;
                    }
                }
            }
        }

        // 3. Robust Canonical Fallback if layout pass was not ready or coordinates collapsed
        if (!positionsValid)
        {
            float[] canonicalX = new float[] { -164f, -85f, -4f, 76f, 157f };
            float topY = 116f;
            for (int i = 0; i < count; i++)
            {
                float cx = (i < canonicalX.Length) ? canonicalX[i] : (-164f + i * 80f);
                float cz = (i < cards.Count && cards[i] != null) ? cards[i].transform.localPosition.z : 0f;
                slotLocalPositions[i] = new Vector3(cx, topY, cz);
            }
        }

        // Calculate bottom bank positions below top slots
        for (int i = 0; i < count; i++)
        {
            Vector3 topPos = slotLocalPositions[i];
            bottomBankLocalPositions[i] = new Vector3(topPos.x, topPos.y - 140f, topPos.z);
        }
    }

    private void WireCheckButton()
    {
        if (checkAnswerButton == null) return;

        checkAnswerButton.onClick.RemoveAllListeners();
        checkAnswerButton.onClick.AddListener(OnCheckAnswerPressed);
        if (checkAnswerButton.targetGraphic != null) checkAnswerButton.targetGraphic.raycastTarget = true;

        XRButtonSelection xr = checkAnswerButton.GetComponent<XRButtonSelection>();
        if (xr != null)
        {
            xr.onClick.RemoveAllListeners();
            xr.onClick.AddListener(OnCheckAnswerPressed);
        }

        if (checkAnswerButton.GetComponent<UIButtonAudio>() == null)
        {
            checkAnswerButton.gameObject.AddComponent<UIButtonAudio>();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Randomization & Game Logic
    // ─────────────────────────────────────────────────────────────────────────

    private void RandomizeCardPositions()
    {
        int count = cards.Count;
        if (count == 0) return;

        CalculateSlotPositions();

        // All top slots start empty
        for (int i = 0; i < slotAssignments.Length; i++)
        {
            slotAssignments[i] = null;
        }

        List<int> availableBottomIndices = new List<int>();
        for (int i = 0; i < count; i++) availableBottomIndices.Add(i);

        // Shuffle bottom bank positions
        for (int i = 0; i < availableBottomIndices.Count; i++)
        {
            int rnd = Random.Range(i, availableBottomIndices.Count);
            int temp = availableBottomIndices[i];
            availableBottomIndices[i] = availableBottomIndices[rnd];
            availableBottomIndices[rnd] = temp;
        }

        for (int i = 0; i < count; i++)
        {
            int bankIdx = availableBottomIndices[i];
            cards[i].AssignedSlotIndex = -1; // -1 = unassigned, sitting at bottom
            cards[i].BottomBankIndex = bankIdx;
            cards[i].TargetLocalPosition = bottomBankLocalPositions[bankIdx];
            cards[i].transform.localPosition = bottomBankLocalPositions[bankIdx];
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drag & Drop Callbacks
    // ─────────────────────────────────────────────────────────────────────────

    public void OnCardBeginDrag(DraggableProcessCard draggedCard)
    {
        if (wrongText != null)
        {
            wrongText.SetActive(false);
        }
    }

    public void OnCardReleased(DraggableProcessCard droppedCard)
    {
        int totalSlots = slotLocalPositions.Length;
        if (totalSlots == 0) return;

        int closestSlotIndex = 0;
        float minDistance = float.MaxValue;
        Vector3 cardLocalPos = droppedCard.transform.localPosition;

        for (int i = 0; i < totalSlots; i++)
        {
            float dist = Vector3.Distance(cardLocalPos, slotLocalPositions[i]);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestSlotIndex = i;
            }
        }

        int oldSlotIndex = droppedCard.AssignedSlotIndex;

        // Snapping threshold distance to top slot (using wide 250f snap radius)
        if (minDistance < 250f)
        {
            DraggableProcessCard occupantCard = slotAssignments[closestSlotIndex];

            // Clear old top slot if card was previously in another top slot
            if (oldSlotIndex >= 0 && oldSlotIndex != closestSlotIndex)
            {
                slotAssignments[oldSlotIndex] = null;
            }

            // Assign droppedCard to closest top slot
            slotAssignments[closestSlotIndex] = droppedCard;
            droppedCard.AssignedSlotIndex = closestSlotIndex;
            droppedCard.TargetLocalPosition = slotLocalPositions[closestSlotIndex];

            // Handle occupant card swap or return to bottom
            if (occupantCard != null && occupantCard != droppedCard)
            {
                if (oldSlotIndex >= 0)
                {
                    slotAssignments[oldSlotIndex] = occupantCard;
                    occupantCard.AssignedSlotIndex = oldSlotIndex;
                    occupantCard.TargetLocalPosition = slotLocalPositions[oldSlotIndex];
                }
                else
                {
                    occupantCard.AssignedSlotIndex = -1;
                    occupantCard.TargetLocalPosition = bottomBankLocalPositions[occupantCard.BottomBankIndex];
                }
            }
        }
        else
        {
            // Dropped outside top slots -> return to bottom bank
            if (oldSlotIndex >= 0)
            {
                slotAssignments[oldSlotIndex] = null;
            }

            droppedCard.AssignedSlotIndex = -1;
            droppedCard.TargetLocalPosition = bottomBankLocalPositions[droppedCard.BottomBankIndex];
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Check Answer & Question Progression
    // ─────────────────────────────────────────────────────────────────────────

    public void OnCheckAnswerPressed()
    {
        int count = cards.Count;
        if (count == 0) return;

        bool isCorrect = true;
        for (int slotIdx = 0; slotIdx < count; slotIdx++)
        {
            DraggableProcessCard cardInSlot = slotAssignments[slotIdx];
            if (cardInSlot == null)
            {
                Debug.Log($"Game2OrderProcess: Slot {slotIdx} is empty.");
                isCorrect = false;
                break;
            }
            else if (cardInSlot.CorrectStepIndex != slotIdx)
            {
                Debug.Log($"Game2OrderProcess: Slot {slotIdx} contains card '{cardInSlot.gameObject.name}' (StepIndex={cardInSlot.CorrectStepIndex}), expected={slotIdx}.");
                isCorrect = false;
                break;
            }
        }

        if (isCorrect)
        {
            int currentQ = currentRoundIndex + 1;
            int totalQ = questions != null ? questions.Length : 5;
            Debug.Log($"Game2OrderProcess: Question {currentQ}/{totalQ} CORRECT!");

            if (wrongText != null) wrongText.SetActive(false);

            if (currentRoundIndex + 1 < totalQ)
            {
                // Advance to next question in the sequence
                currentRoundIndex++;
                LoadQuestion(currentRoundIndex);
            }
            else
            {
                // Completed all questions! Show Game 2 Leaderboard with player completion time
                Debug.Log("Game2OrderProcess: All 5 questions completed! Showing Leaderboard.");
                FinishGameAndShowLeaderboard("game_2", "Susun Langkah & Kisah");
            }
        }
        else
        {
            Debug.Log("Game2OrderProcess: Player guessed WRONG. Displaying wrong text.");
            if (wrongText != null)
            {
                wrongText.SetActive(true);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name == childName) return t;
        return null;
    }
}

/// <summary>
/// Attached to individual process card/button items under ProcessLayout.
/// Handles touch, mouse, and XR ray pointer dragging across Unity Canvas UI.
/// </summary>
public class DraggableProcessCard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    public int CorrectStepIndex { get; private set; }
    public int AssignedSlotIndex { get; set; }
    public int BottomBankIndex { get; set; }
    public Vector3 TargetLocalPosition { get; set; }
    public bool IsDragging => isDragging;

    private Game2OrderProcess controller;
    private RectTransform rectTransform;
    private RectTransform parentRectTransform;
    private bool isDragging = false;
    private Vector2 grabOffsetLocal;

    public void Setup(Game2OrderProcess gameController, int stepIndex)
    {
        controller = gameController;
        CorrectStepIndex = stepIndex;

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null && rectTransform.parent != null)
        {
            parentRectTransform = rectTransform.parent as RectTransform;
        }

        // Ensure raycast target is enabled on Image component
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = true;
        }

        // Ensure BoxCollider exists for XR ray interaction
        BoxCollider boxCol = GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider>();
        if (rectTransform != null)
        {
            Vector2 size = rectTransform.rect.size;
            if (size.x <= 1f || size.y <= 1f) size = new Vector2(74f, 120f);
            boxCol.size = new Vector3(size.x, size.y, 10f);
            boxCol.center = Vector3.zero;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        transform.SetAsLastSibling();

        if (parentRectTransform != null)
        {
            Vector3 worldHit = eventData.pointerCurrentRaycast.worldPosition;
            if (worldHit != Vector3.zero)
            {
                Vector3 localHit = parentRectTransform.InverseTransformPoint(worldHit);
                grabOffsetLocal = (Vector2)transform.localPosition - new Vector2(localHit.x, localHit.y);
            }
            else
            {
                grabOffsetLocal = Vector2.zero;
            }
        }

        controller?.OnCardBeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || parentRectTransform == null) return;

        Vector3 worldHit = eventData.pointerCurrentRaycast.worldPosition;
        if (worldHit != Vector3.zero)
        {
            Vector3 localHit = parentRectTransform.InverseTransformPoint(worldHit);
            Vector2 targetPos = new Vector2(localHit.x, localHit.y) + grabOffsetLocal;
            targetPos.x = Mathf.Clamp(targetPos.x, -230f, 230f);
            targetPos.y = Mathf.Clamp(targetPos.y, -200f, 200f);
            transform.localPosition = new Vector3(targetPos.x, targetPos.y, TargetLocalPosition.z);
        }
        else
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            {
                Vector2 targetPos = localPoint + grabOffsetLocal;
                targetPos.x = Mathf.Clamp(targetPos.x, -230f, 230f);
                targetPos.y = Mathf.Clamp(targetPos.y, -200f, 200f);
                transform.localPosition = new Vector3(targetPos.x, targetPos.y, TargetLocalPosition.z);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDragging)
        {
            isDragging = false;
            controller?.OnCardReleased(this);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isDragging)
        {
            isDragging = false;
            controller?.OnCardReleased(this);
        }
    }

    private void Update()
    {
        Vector3 targetScale = isDragging ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 15f);

        if (!isDragging)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, TargetLocalPosition, Time.deltaTime * (controller != null ? controller.snapSpeed : 15f));
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, Time.deltaTime * 15f);
        }
    }
}
