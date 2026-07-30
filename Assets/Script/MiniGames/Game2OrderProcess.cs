using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Mini-Game 2: "Urutkan Proses Pembuatan Batik".
/// Inherits from BaseGame.
/// All UI references (processObjects, slotObjects, processLayout, numberSlotPanel, checkAnswerButton, wrongText) 
/// are assigned via the Inspector (similar to Game 1).
/// On start or enable, the process layout buttons are reordered at random.
/// The player drags and drops cards to the correct slots, then presses "Periksa Jawaban".
/// If correct, the game ends (calls OnClose to return to menu).
/// If wrong, displays the wrong text and allows the player to reorder and try again.
/// </summary>
public class Game2OrderProcess : BaseGame
{
    [Header("UI References (Assign in Inspector)")]
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

    private List<DraggableProcessCard> cards = new List<DraggableProcessCard>();
    private Vector3[] slotLocalPositions = new Vector3[5];
    private Vector3[] bottomBankLocalPositions = new Vector3[5];
    private DraggableProcessCard[] slotAssignments = new DraggableProcessCard[5];

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle & BaseGame Overrides
    // ─────────────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        AutoFindUIReferences();
        InitializeCardsAndSlots();
        WireCheckButton();
    }

    /// <summary>
    /// Overridden from BaseGame. Triggered automatically on OnEnable.
    /// Re-randomizes card layout at the bottom every time the game panel is enabled.
    /// </summary>
    public override void OnGameStart()
    {
        base.OnGameStart();

        // Hide wrong text on start
        if (wrongText != null)
        {
            wrongText.SetActive(false);
        }

        // Enable check button
        if (checkAnswerButton != null)
        {
            checkAnswerButton.interactable = true;
        }

        // Randomize card slots at bottom
        RandomizeCardPositions();
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
    // Initialization & Setup
    // ─────────────────────────────────────────────────────────────────────────

    private void AutoFindUIReferences()
    {
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
            Transform t = FindDeepChild(transform, "CheckButton");
            if (t == null) t = FindDeepChild(transform, "CheckAnswerButton");
            if (t == null) t = FindDeepChild(transform, "ButtonCheck");
            if (t != null) checkAnswerButton = t.GetComponent<Button>();
        }

        if (wrongText == null)
        {
            Transform t = FindDeepChild(transform, "Wrong Text");
            if (t == null) t = FindDeepChild(transform, "WrongText");
            if (t == null) t = FindDeepChild(transform, "TextWrong");
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

                DraggableProcessCard cardComp = obj.GetComponent<DraggableProcessCard>();
                if (cardComp == null)
                {
                    cardComp = obj.AddComponent<DraggableProcessCard>();
                }

                TMP_Text label = obj.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 8f;
                    label.fontSizeMax = 18f;
                    label.enableWordWrapping = true;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.alignment = TextAlignmentOptions.Center;
                }

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
            for (int i = 0; i < childCount; i++)
            {
                Transform child = processLayout.GetChild(i);
                DraggableProcessCard cardComp = child.GetComponent<DraggableProcessCard>();
                if (cardComp == null)
                {
                    cardComp = child.gameObject.AddComponent<DraggableProcessCard>();
                }

                int stepIdx = GetStepIndexFromName(child.name, i);
                cardComp.Setup(this, stepIdx);
                cards.Add(cardComp);
            }
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
        // 3. Fallback
        else
        {
            for (int i = 0; i < count; i++)
            {
                slotLocalPositions[i] = cards[i].transform.localPosition;
            }
        }

        // Sort slotLocalPositions strictly left-to-right by X coordinate
        System.Array.Sort(slotLocalPositions, (a, b) => a.x.CompareTo(b.x));

        // 2. Calculate bottom bank positions below top slots
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

        // Snapping threshold distance to top slot (using wider 250f snap radius)
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
    // Check Answer
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
            Debug.Log("Game2OrderProcess: Player guessed CORRECT! Game ending.");
            if (wrongText != null) wrongText.SetActive(false);

            // Show Game 2 Leaderboard with player completion time!
            FinishGameAndShowLeaderboard("game_2", "Susun Proses Pembuatan Batik");
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
        // Visual press start if needed
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
