using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Mini-Game 3: "Urutkan Timeline Sejarah".
/// Inherits from BaseGame.
/// Supports a list of questions (e.g. 3 or 4 questions total).
/// On game start:
/// 1. Questions are played in RANDOM order. Title shows progress counter (e.g. "(1/4) Susunkan garis masa Sejarah Terengganu").
/// 2. For each question, card text is updated on the TMP_Text component inside each process card (Process1..Process5), and card layout is randomized.
/// 3. Correct guesses advance to the next question in the queue, or finish the game to display the Leaderboard. Incorrect guesses display the wrong text warning so the player can retry.
/// </summary>
public class Game3OrderTimeline : BaseGame
{
    [System.Serializable]
    public struct TimelineItem
    {
        [Tooltip("Dynamic text displayed on this card's Text (TMP).")]
        [TextArea(2, 4)]
        public string text;

        [Tooltip("Target slot index for this card (1-based [1..5] or 0-based [0..4]).")]
        public int correctSlotIndex;
    }

    [System.Serializable]
    public struct TimelineQuestion
    {
        [Tooltip("Question topic/title (e.g. 'Sejarah Terengganu', 'Ekonomi'). 'Susunkan garis masa ' is prepended automatically.")]
        public string questionTitle;

        [Tooltip("The 5 timeline items for this question.")]
        public TimelineItem[] timelineItems;
    }

    [Header("Questions Configuration (Randomized Order on Start)")]
    public TimelineQuestion[] questions = new TimelineQuestion[]
    {
        new TimelineQuestion
        {
            questionTitle = "Sejarah Terengganu",
            timelineItems = new TimelineItem[]
            {
                new TimelineItem { text = "Batu Bersurat Terengganu\n(1303 M)", correctSlotIndex = 1 },
                new TimelineItem { text = "Pemerintahan Sultan Zainal Abidin I\n(1708 M)", correctSlotIndex = 2 },
                new TimelineItem { text = "Perjanjian Bangkok\n(1909 M)", correctSlotIndex = 3 },
                new TimelineItem { text = "Penentangan Haji Abdul Rahman Limbong\n(1928 M)", correctSlotIndex = 4 },
                new TimelineItem { text = "Kemerdekaan & Era Moden\n(1957 M)", correctSlotIndex = 5 }
            }
        },
        new TimelineQuestion
        {
            questionTitle = "Ekonomi",
            timelineItems = new TimelineItem[]
            {
                new TimelineItem { text = "Sistem Barter & Mata Wang Kerang", correctSlotIndex = 1 },
                new TimelineItem { text = "Pengeluaran Mata Wang Logam & Koin", correctSlotIndex = 2 },
                new TimelineItem { text = "Pengenalan Wang Kertas & Bank", correctSlotIndex = 3 },
                new TimelineItem { text = "Perbankan Elektronik & Kad Kredit", correctSlotIndex = 4 },
                new TimelineItem { text = "Mata Wang Digital & Transaksi Dalam Talian", correctSlotIndex = 5 }
            }
        },
        new TimelineQuestion
        {
            questionTitle = "Peristiwa Proklamasi Kemerdekaan",
            timelineItems = new TimelineItem[]
            {
                new TimelineItem { text = "Pembentukan BPUPKI\n(1 Mac 1945)", correctSlotIndex = 1 },
                new TimelineItem { text = "Pembentukan PPKI\n(7 Ogos 1945)", correctSlotIndex = 2 },
                new TimelineItem { text = "Peristiwa Rengasdengklok\n(16 Ogos 1945)", correctSlotIndex = 3 },
                new TimelineItem { text = "Pembacaan Teks Proklamasi\n(17 Ogos 1945)", correctSlotIndex = 4 },
                new TimelineItem { text = "Pengesahan UUD 1945\n(18 Ogos 1945)", correctSlotIndex = 5 }
            }
        }
    };

    [Header("UI References (Assign in Inspector or Auto-Wired)")]
    [Tooltip("Displays question description and header (e.g. TaskText).")]
    public TMP_Text taskText;

    [Tooltip("Process card GameObjects (Process1, Process2, Process3, Process4, Process5).")]
    public GameObject[] processObjects;

    [Tooltip("Parent transform containing Process cards (ProcessLayout).")]
    public Transform processLayout;

    [Tooltip("Parent transform containing 1, 2, 3, 4, 5 slot indicator objects (NumberSlotPanel).")]
    public Transform numberSlotPanel;

    [Tooltip("Slot indicator GameObjects or Transforms. Optional.")]
    public Transform[] slotObjects;

    [Tooltip("'Periksa Jawaban' Check Button (CheckButton).")]
    public Button checkAnswerButton;

    [Tooltip("Wrong answer warning text object (Wrong Text).")]
    public GameObject wrongText;

    [Header("Animation & Drag Settings")]
    [Tooltip("Smooth lerp speed when snapping cards into slots.")]
    public float snapSpeed = 15f;

    // ─────────────────────────────────────────────────────────────────────────
    // Internal State
    // ─────────────────────────────────────────────────────────────────────────

    private int currentRoundIndex = 0;
    private List<int> shuffledQuestionIndices = new List<int>();
    private List<DraggableTimelineCard> cards = new List<DraggableTimelineCard>();
    private Vector3[] slotLocalPositions = new Vector3[5];
    private Vector3[] bottomBankLocalPositions = new Vector3[5];
    private DraggableTimelineCard[] slotAssignments = new DraggableTimelineCard[5];

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle & BaseGame Overrides
    // ─────────────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        AutoFindUIReferences();
        InitializeCardComponents();
        WireCheckButton();
    }

    /// <summary>
    /// Overridden from BaseGame. Triggered automatically on OnEnable.
    /// Shuffles questions randomly and starts at round 0.
    /// </summary>
    public override void OnGameStart()
    {
        base.OnGameStart();

        InitializeQuestionQueue();
        currentRoundIndex = 0;

        if (shuffledQuestionIndices.Count > 0)
        {
            LoadQuestion(shuffledQuestionIndices[currentRoundIndex]);
        }
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
    // Question Shuffling & Setup
    // ─────────────────────────────────────────────────────────────────────────

    private void InitializeQuestionQueue()
    {
        shuffledQuestionIndices.Clear();
        if (questions == null || questions.Length == 0) return;

        for (int i = 0; i < questions.Length; i++)
        {
            shuffledQuestionIndices.Add(i);
        }

        // Fisher-Yates shuffle question sequence
        for (int i = 0; i < shuffledQuestionIndices.Count; i++)
        {
            int rnd = Random.Range(i, shuffledQuestionIndices.Count);
            int temp = shuffledQuestionIndices[i];
            shuffledQuestionIndices[i] = shuffledQuestionIndices[rnd];
            shuffledQuestionIndices[rnd] = temp;
        }
    }

    private void LoadQuestion(int questionDataIdx)
    {
        if (wrongText != null) wrongText.SetActive(false);
        if (checkAnswerButton != null) checkAnswerButton.interactable = true;

        if (questions == null || questions.Length == 0) return;

        questionDataIdx = Mathf.Clamp(questionDataIdx, 0, questions.Length - 1);
        TimelineQuestion qData = questions[questionDataIdx];

        int displayRoundNum = currentRoundIndex + 1;
        int totalQuestions = questions.Length;

        // 1. Update TaskText with (1/4) progress counter and Malay prefix
        if (taskText != null)
        {
            string title = string.IsNullOrWhiteSpace(qData.questionTitle) ? "Sejarah" : qData.questionTitle.Trim();
            string formattedTitle = title.StartsWith("Susunkan", System.StringComparison.OrdinalIgnoreCase)
                ? title
                : $"Susunkan garis masa {title}";

            taskText.text = $"({displayRoundNum}/{totalQuestions}) {formattedTitle}";
            taskText.enableAutoSizing = true;
            taskText.fontSizeMin = 10f;
            taskText.fontSizeMax = 22f;
        }

        // 2. Setup Cards with Question Data (updates TMP_Text inside process cards)
        int itemCount = Mathf.Min(cards.Count, qData.timelineItems != null ? qData.timelineItems.Length : 0);
        for (int i = 0; i < cards.Count; i++)
        {
            DraggableTimelineCard card = cards[i];
            if (card == null) continue;

            if (i < itemCount)
            {
                TimelineItem item = qData.timelineItems[i];

                // Convert 1-based index (1..5) or 0-based index (0..4) to 0-based
                int stepIdx = (item.correctSlotIndex >= 1 && item.correctSlotIndex <= 5)
                    ? item.correctSlotIndex - 1
                    : Mathf.Clamp(item.correctSlotIndex, 0, 4);

                card.Setup(this, stepIdx);

                // Set dynamic card text on the TMP_Text component inside the process card
                TMP_Text txtComp = card.GetComponentInChildren<TMP_Text>(true);
                if (txtComp != null)
                {
                    txtComp.text = item.text;
                    txtComp.enableAutoSizing = true;
                    txtComp.fontSizeMin = 8f;
                    txtComp.fontSizeMax = 18f;
                    txtComp.enableWordWrapping = true;
                    txtComp.overflowMode = TextOverflowModes.Ellipsis;
                    txtComp.alignment = TextAlignmentOptions.Center;
                }
            }
        }

        // 3. Re-calculate slot positions and randomize card positions into bottom bank
        CalculateSlotPositions();
        RandomizeCardPositions();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auto-Wiring & Initialization
    // ─────────────────────────────────────────────────────────────────────────

    private void AutoFindUIReferences()
    {
        if (taskText == null)
        {
            Transform t = FindDeepChild(transform, "TaskText") ?? FindDeepChild(transform, "TitleText");
            if (t != null) taskText = t.GetComponent<TMP_Text>();
        }

        if (processLayout == null)
        {
            Transform t = FindDeepChild(transform, "ProcessLayout") ?? FindDeepChild(transform, "TimelineLayout");
            if (t != null) processLayout = t;
        }

        if (numberSlotPanel == null)
        {
            Transform t = FindDeepChild(transform, "NumberSlotPanel") ?? FindDeepChild(transform, "SlotPanel");
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

    private void InitializeCardComponents()
    {
        cards.Clear();

        // 1. Try processObjects array if assigned
        if (processObjects != null && processObjects.Length > 0)
        {
            for (int i = 0; i < processObjects.Length; i++)
            {
                GameObject obj = processObjects[i];
                if (obj == null) continue;

                DraggableTimelineCard cardComp = obj.GetComponent<DraggableTimelineCard>() ?? obj.AddComponent<DraggableTimelineCard>();
                cards.Add(cardComp);

                if (processLayout == null && obj.transform.parent != null)
                {
                    processLayout = obj.transform.parent;
                }
            }
        }
        // 2. Fallback: gather from processLayout children
        else if (processLayout != null)
        {
            int childCount = processLayout.childCount;
            List<GameObject> objs = new List<GameObject>();
            for (int i = 0; i < childCount; i++)
            {
                Transform child = processLayout.GetChild(i);
                DraggableTimelineCard cardComp = child.GetComponent<DraggableTimelineCard>() ?? child.gameObject.AddComponent<DraggableTimelineCard>();
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
        if (totalCount > 0 && slotLocalPositions.Length != totalCount)
        {
            slotLocalPositions = new Vector3[totalCount];
            bottomBankLocalPositions = new Vector3[totalCount];
            slotAssignments = new DraggableTimelineCard[totalCount];
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
            slotAssignments = new DraggableTimelineCard[count];
        }

        // 1. Try slotObjects array first
        if (slotObjects != null && slotObjects.Length >= count)
        {
            for (int i = 0; i < count; i++)
            {
                Transform slotChild = slotObjects[i];
                if (slotChild != null && processLayout != null)
                {
                    Vector3 worldPos = slotChild.position;
                    Vector3 localPos = processLayout.InverseTransformPoint(worldPos);
                    localPos.z = cards[i].transform.localPosition.z;
                    slotLocalPositions[i] = localPos;
                }
                else if (slotChild != null)
                {
                    slotLocalPositions[i] = slotChild.localPosition;
                }
            }
        }
        // 2. Try numberSlotPanel children second
        else if (numberSlotPanel != null && numberSlotPanel.childCount >= count)
        {
            for (int i = 0; i < count; i++)
            {
                Transform slotChild = numberSlotPanel.GetChild(i);
                if (processLayout != null)
                {
                    Vector3 worldPos = slotChild.position;
                    Vector3 localPos = processLayout.InverseTransformPoint(worldPos);
                    localPos.z = cards[i].transform.localPosition.z;
                    slotLocalPositions[i] = localPos;
                }
                else
                {
                    slotLocalPositions[i] = slotChild.localPosition;
                }
            }
        }
        // 3. Fallback position calculation
        else
        {
            for (int i = 0; i < count; i++)
            {
                slotLocalPositions[i] = cards[i].transform.localPosition;
            }
        }

        // Sort slotLocalPositions strictly left-to-right by X coordinate
        System.Array.Sort(slotLocalPositions, (a, b) => a.x.CompareTo(b.x));

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

        XRButtonSelection xr = checkAnswerButton.GetComponent<XRButtonSelection>();
        if (xr != null)
        {
            xr.onClick.RemoveAllListeners();
            xr.onClick.AddListener(OnCheckAnswerPressed);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Randomization & Game Logic
    // ─────────────────────────────────────────────────────────────────────────

    private void RandomizeCardPositions()
    {
        int count = cards.Count;
        if (count == 0) return;

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
            cards[i].AssignedSlotIndex = -1; // -1 = unassigned, sitting at bottom bank
            cards[i].BottomBankIndex = bankIdx;
            cards[i].TargetLocalPosition = bottomBankLocalPositions[bankIdx];
            cards[i].transform.localPosition = bottomBankLocalPositions[bankIdx];
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drag & Drop Callbacks
    // ─────────────────────────────────────────────────────────────────────────

    public void OnCardBeginDrag(DraggableTimelineCard draggedCard)
    {
        if (wrongText != null)
        {
            wrongText.SetActive(false);
        }
    }

    public void OnCardReleased(DraggableTimelineCard droppedCard)
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

        // Snapping threshold distance to top slot
        if (minDistance < 250f)
        {
            DraggableTimelineCard occupantCard = slotAssignments[closestSlotIndex];

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
    // Check Answer & Multi-Question Flow
    // ─────────────────────────────────────────────────────────────────────────

    public void OnCheckAnswerPressed()
    {
        int count = cards.Count;
        if (count == 0) return;

        bool isCorrect = true;
        for (int slotIdx = 0; slotIdx < count; slotIdx++)
        {
            DraggableTimelineCard cardInSlot = slotAssignments[slotIdx];
            if (cardInSlot == null)
            {
                Debug.Log($"Game3OrderTimeline: Slot {slotIdx} is empty.");
                isCorrect = false;
                break;
            }
            else if (cardInSlot.CorrectStepIndex != slotIdx)
            {
                Debug.Log($"Game3OrderTimeline: Slot {slotIdx} contains card '{cardInSlot.gameObject.name}' (StepIndex={cardInSlot.CorrectStepIndex}), expected={slotIdx}.");
                isCorrect = false;
                break;
            }
        }

        if (isCorrect)
        {
            Debug.Log($"Game3OrderTimeline: Question {currentRoundIndex + 1}/{shuffledQuestionIndices.Count} CORRECT!");

            if (wrongText != null) wrongText.SetActive(false);

            // Advance to next random question in queue or finish game
            if (currentRoundIndex + 1 < shuffledQuestionIndices.Count)
            {
                currentRoundIndex++;
                LoadQuestion(shuffledQuestionIndices[currentRoundIndex]);
            }
            else
            {
                Debug.Log("Game3OrderTimeline: All questions completed! Game ending.");
                FinishGameAndShowLeaderboard("game_3", "Urutkan Timeline Sejarah");
            }
        }
        else
        {
            Debug.Log("Game3OrderTimeline: Player guessed WRONG. Displaying wrong text.");
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
/// Attached to individual process/timeline card items under ProcessLayout.
/// Handles touch, mouse, and XR ray pointer dragging across Unity Canvas UI.
/// </summary>
public class DraggableTimelineCard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    public int CorrectStepIndex { get; private set; }
    public int AssignedSlotIndex { get; set; }
    public int BottomBankIndex { get; set; }
    public Vector3 TargetLocalPosition { get; set; }

    private Game3OrderTimeline controller;
    private RectTransform rectTransform;
    private RectTransform parentRectTransform;
    private bool isDragging = false;
    private Vector2 grabOffsetLocal;

    public void Setup(Game3OrderTimeline gameController, int stepIndex)
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
