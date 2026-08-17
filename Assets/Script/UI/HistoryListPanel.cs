using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the HistoryListPanel UI hierarchy:
/// Displays the 7 Sejarah Terengganu topics in a compact, balanced 2-column grid layout.
/// Clicking any topic opens the HistoryDetailPanel (HistoryPanel).
/// Automatically auto-loads all HistoryData assets from Resources/MuseumData/DataSejarah.
/// </summary>
public class HistoryListPanel : MonoBehaviour
{
    public static HistoryListPanel Instance { get; private set; }

    [Header("Header Text Fields")]
    [Tooltip("Main room title (e.g. 'Ruang Sejarah').")]
    public TMP_Text roomTitleText;

    [Tooltip("Room subtitle (e.g. 'Sejarah Terengganu').")]
    public TMP_Text roomSubtitleText;

    [Tooltip("Section header text (e.g. 'Informasi Sejarah di Ruangan ini').")]
    public TMP_Text sectionHeaderText;

    [Tooltip("Item count text (e.g. 'Jumlah: 7').")]
    public TMP_Text artifactCountText;

    [Header("Hierarchy References")]
    [Tooltip("The Layout Group container (ArtifactList) where item buttons are spawned.")]
    public Transform artifactListContainer;

    [Tooltip("Prefab instantiated for each history entry in the list.")]
    public GameObject historyItemPrefab;

    [Header("Detail Panel Transition")]
    [Tooltip("Reference to the HistoryDetailPanel (HistoryPanel component).")]
    public HistoryPanel historyDetailPanel;

    [Header("Control Buttons")]
    public Button closeButton;
    public XRButtonSelection closeButtonXR;
    public Button backButton;
    public XRButtonSelection backButtonXR;

    [Header("History Entries Data (Auto-populated if empty)")]
    [Tooltip("Leave empty to automatically auto-load all HistoryData entries from Resources.")]
    public List<HistoryData> historyItems = new List<HistoryData>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        AutoFindUIReferences();
        WireControlButtons();
        SetupTwoColumnLayout();
    }

    private void OnEnable()
    {
        AutoFindUIReferences();
        WireControlButtons();
        SetupTwoColumnLayout();
        PopulateHistoryList();
    }

    /// <summary>
    /// Populate the HistoryListPanel with a custom list of HistoryData entries.
    /// </summary>
    public void ShowList(List<HistoryData> items, string title = "Ruang Sejarah", string subtitle = "Sejarah Terengganu")
    {
        if (items != null && items.Count > 0)
        {
            historyItems = new List<HistoryData>(items);
        }
        else
        {
            EnsureHistoryItems();
        }

        gameObject.SetActive(true);
        SetupTwoColumnLayout();

        if (roomTitleText != null)
        {
            roomTitleText.text = title;
            roomTitleText.fontSize = 18f;
        }
        if (roomSubtitleText != null)
        {
            roomSubtitleText.text = subtitle;
            roomSubtitleText.fontSize = 13f;
        }

        PopulateHistoryList();
    }

    private void EnsureHistoryItems()
    {
        if (historyItems == null) historyItems = new List<HistoryData>();

        if (historyItems.Count == 0)
        {
            // 1. Try HistoryManager database first
            if (HistoryManager.Instance != null && HistoryManager.Instance.historyDatabase != null && HistoryManager.Instance.historyDatabase.Count > 0)
            {
                historyItems.AddRange(HistoryManager.Instance.historyDatabase);
            }
            else
            {
                // 2. Auto-load from Resources as fallback
                HistoryData[] resHistory = Resources.LoadAll<HistoryData>("MuseumData/DataSejarah");
                if (resHistory == null || resHistory.Length == 0) resHistory = Resources.LoadAll<HistoryData>("MuseumData/History");
                if (resHistory == null || resHistory.Length == 0) resHistory = Resources.LoadAll<HistoryData>("DataSejarah");
                if (resHistory == null || resHistory.Length == 0) resHistory = Resources.LoadAll<HistoryData>("MuseumData");
                if (resHistory == null || resHistory.Length == 0) resHistory = Resources.LoadAll<HistoryData>("");

                if (resHistory != null && resHistory.Length > 0)
                {
                    historyItems.AddRange(resHistory);
                }
            }
        }
    }

    /// <summary>
    /// Instantiate item buttons inside artifactListContainer for each HistoryData entry in 2 columns.
    /// </summary>
    public void PopulateHistoryList()
    {
        EnsureHistoryItems();
        SetupTwoColumnLayout();

        if (artifactCountText != null)
        {
            int count = historyItems != null ? historyItems.Count : 0;
            artifactCountText.text = $"Jumlah: {count}";
            artifactCountText.fontSize = 12f;
        }

        if (artifactListContainer == null) return;

        // Clear existing spawned items immediately to avoid layout ghosting
        for (int i = artifactListContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = artifactListContainer.GetChild(i);
            if (child != null)
            {
                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        if (historyItems == null || historyItems.Count == 0) return;

        for (int i = 0; i < historyItems.Count; i++)
        {
            HistoryData data = historyItems[i];
            if (data == null) continue;

            string formattedNum = (i + 1).ToString("D2");
            string displayText = string.IsNullOrEmpty(data.eventTitle) ? data.name : data.eventTitle;
            Sprite thumbSprite = GetHistoryThumbnail(data);

            GameObject itemObj = null;
            if (historyItemPrefab != null)
            {
                itemObj = Instantiate(historyItemPrefab, artifactListContainer);
                FormatHistoryCardItem(itemObj, data, i);
            }
            else
            {
                itemObj = CreateProceduralCard(displayText, formattedNum, thumbSprite);
                itemObj.transform.SetParent(artifactListContainer, false);
                WireCardEvents(itemObj, data);
            }
        }
    }

    /// <summary>
    /// Formats an instantiated item card to fit cleanly into the 2-column grid.
    /// </summary>
    private void FormatHistoryCardItem(GameObject itemObj, HistoryData data, int index)
    {
        if (itemObj == null) return;
        itemObj.SetActive(true);
        itemObj.transform.localScale = Vector3.one;

        RectTransform itemRt = itemObj.GetComponent<RectTransform>();
        if (itemRt != null)
        {
            itemRt.sizeDelta = new Vector2(260f, 52f);
        }

        LayoutElement le = itemObj.GetComponent<LayoutElement>();
        if (le == null) le = itemObj.AddComponent<LayoutElement>();
        le.preferredWidth = 260f;
        le.preferredHeight = 52f;
        le.minWidth = 240f;
        le.minHeight = 48f;

        string displayText = string.IsNullOrEmpty(data.eventTitle) ? data.name : data.eventTitle;
        string formattedNum = (index + 1).ToString("D2");

        // 1. Locate text fields
        TMP_Text numTmp = null;
        TMP_Text nameTmp = null;

        foreach (TMP_Text tmp in itemObj.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp == null) continue;
            string n = tmp.name.ToLower();
            string t = (tmp.text ?? "").ToLower();

            if (n.Contains("num") || n.Contains("number") || n.Contains("index") || t == "01" || t == "02")
            {
                numTmp = tmp;
            }
            else if (n.Contains("name") || n.Contains("title") || t.Contains("mona") || t.Contains("lisa") || n.Contains("text"))
            {
                nameTmp = tmp;
            }
        }

        // Align NumText ("01", "02") on the left
        if (numTmp != null)
        {
            numTmp.gameObject.SetActive(true);
            numTmp.text = formattedNum;
            numTmp.fontSize = 13f;
            numTmp.fontStyle = FontStyles.Bold;
            numTmp.color = new Color(0.95f, 0.85f, 0.45f, 1f); // Gold tint
            numTmp.alignment = TextAlignmentOptions.MidlineLeft;

            RectTransform nRt = numTmp.rectTransform;
            nRt.anchorMin = new Vector2(0f, 0.5f);
            nRt.anchorMax = new Vector2(0f, 0.5f);
            nRt.pivot = new Vector2(0f, 0.5f);
            nRt.anchoredPosition = new Vector2(10f, 0f);
            nRt.sizeDelta = new Vector2(24f, 30f);
        }

        // Align Thumbnail Image and Right Arrow Icon
        Sprite thumbSprite = GetHistoryThumbnail(data);
        bool hasThumb = (thumbSprite != null);
        float textLeft = hasThumb ? 76f : 38f;

        foreach (Image img in itemObj.GetComponentsInChildren<Image>(true))
        {
            if (img == null || img.gameObject == itemObj) continue;
            string n = img.gameObject.name.ToLower();

            if (n.Contains("icon") || n.Contains("arrow") || n.Contains("chevron"))
            {
                img.gameObject.SetActive(true);
                RectTransform aRt = img.rectTransform;
                aRt.anchorMin = new Vector2(1f, 0.5f);
                aRt.anchorMax = new Vector2(1f, 0.5f);
                aRt.pivot = new Vector2(1f, 0.5f);
                aRt.anchoredPosition = new Vector2(-8f, 0f);
                aRt.sizeDelta = new Vector2(16f, 16f);
            }
            else if (!n.Contains("bg") && !n.Contains("background"))
            {
                if (hasThumb)
                {
                    img.sprite = thumbSprite;
                    img.color = Color.white;
                    img.preserveAspect = true;
                    img.gameObject.SetActive(true);

                    RectTransform tRt = img.rectTransform;
                    tRt.anchorMin = new Vector2(0f, 0.5f);
                    tRt.anchorMax = new Vector2(0f, 0.5f);
                    tRt.pivot = new Vector2(0f, 0.5f);
                    tRt.anchoredPosition = new Vector2(36f, 0f);
                    tRt.sizeDelta = new Vector2(34f, 34f);
                }
                else
                {
                    img.gameObject.SetActive(false);
                }
            }
        }

        // Align NameText (Title) in the center with 2-line wrapping
        if (nameTmp != null)
        {
            nameTmp.gameObject.SetActive(true);
            nameTmp.text = displayText;
            nameTmp.fontSize = 11.5f;
            nameTmp.enableWordWrapping = true;
            nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
            nameTmp.color = Color.white;

            RectTransform nameRt = nameTmp.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 0.5f);
            nameRt.anchorMax = new Vector2(1f, 0.5f);
            nameRt.pivot = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = new Vector2(textLeft, 0f);
            nameRt.sizeDelta = new Vector2(-(textLeft + 26f), 48f);
        }

        WireCardEvents(itemObj, data);
    }

    private void WireCardEvents(GameObject itemObj, HistoryData data)
    {
        Button btn = itemObj.GetComponent<Button>();
        if (btn == null) btn = itemObj.AddComponent<Button>();

        HistoryData captured = data;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnHistoryItemClicked(captured));

        XRButtonSelection xr = itemObj.GetComponent<XRButtonSelection>();
        if (xr != null)
        {
            xr.scaleTarget = itemObj.GetComponent<RectTransform>();
            xr.onClick.RemoveAllListeners();
            xr.onClick.AddListener(() => OnHistoryItemClicked(captured));
        }
    }

    private GameObject CreateProceduralCard(string displayText, string formattedNum, Sprite thumbSprite)
    {
        GameObject card = new GameObject("HistoryCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(XRButtonSelection));
        RectTransform rt = card.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260f, 52f);

        Image bg = card.GetComponent<Image>();
        bg.color = new Color(0.15f, 0.17f, 0.22f, 0.95f);

        // Num text
        GameObject numObj = new GameObject("NumText", typeof(RectTransform), typeof(TextMeshProUGUI));
        numObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI numTmp = numObj.GetComponent<TextMeshProUGUI>();
        numTmp.text = formattedNum;
        numTmp.fontSize = 13f;
        numTmp.fontStyle = FontStyles.Bold;
        numTmp.color = new Color(0.95f, 0.85f, 0.45f, 1f);
        numTmp.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform nRt = numObj.GetComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0f, 0.5f);
        nRt.anchorMax = new Vector2(0f, 0.5f);
        nRt.pivot = new Vector2(0f, 0.5f);
        nRt.anchoredPosition = new Vector2(10f, 0f);
        nRt.sizeDelta = new Vector2(24f, 30f);

        float textLeft = (thumbSprite != null) ? 76f : 38f;

        if (thumbSprite != null)
        {
            GameObject imgObj = new GameObject("ThumbImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imgObj.transform.SetParent(card.transform, false);
            Image img = imgObj.GetComponent<Image>();
            img.sprite = thumbSprite;
            img.preserveAspect = true;
            RectTransform iRt = imgObj.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0f, 0.5f);
            iRt.anchorMax = new Vector2(0f, 0.5f);
            iRt.pivot = new Vector2(0f, 0.5f);
            iRt.anchoredPosition = new Vector2(36f, 0f);
            iRt.sizeDelta = new Vector2(34f, 34f);
        }

        // Title text
        GameObject txtObj = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI nameTmp = txtObj.GetComponent<TextMeshProUGUI>();
        nameTmp.text = displayText;
        nameTmp.fontSize = 11.5f;
        nameTmp.enableWordWrapping = true;
        nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
        nameTmp.color = Color.white;
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0.5f);
        tRt.anchorMax = new Vector2(1f, 0.5f);
        tRt.pivot = new Vector2(0f, 0.5f);
        tRt.anchoredPosition = new Vector2(textLeft, 0f);
        tRt.sizeDelta = new Vector2(-(textLeft + 26f), 48f);

        // Arrow icon text
        GameObject arrowObj = new GameObject("ArrowText", typeof(RectTransform), typeof(TextMeshProUGUI));
        arrowObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI arrowTmp = arrowObj.GetComponent<TextMeshProUGUI>();
        arrowTmp.text = "›";
        arrowTmp.fontSize = 18f;
        arrowTmp.color = new Color(0.8f, 0.8f, 0.9f, 0.7f);
        arrowTmp.alignment = TextAlignmentOptions.Center;
        RectTransform aRt = arrowObj.GetComponent<RectTransform>();
        aRt.anchorMin = new Vector2(1f, 0.5f);
        aRt.anchorMax = new Vector2(1f, 0.5f);
        aRt.pivot = new Vector2(1f, 0.5f);
        aRt.anchoredPosition = new Vector2(-10f, 0f);
        aRt.sizeDelta = new Vector2(16f, 24f);

        return card;
    }

    private Sprite GetHistoryThumbnail(HistoryData data)
    {
        if (data == null) return null;

        if (data.displaySprite != null)
        {
            try
            {
                if (data.displaySprite.texture != null) return data.displaySprite;
            }
            catch { }
        }

        if (data.images != null && data.images.Length > 0 && data.images[0].sprite != null)
        {
            try
            {
                if (data.images[0].sprite.texture != null) return data.images[0].sprite;
            }
            catch { }
        }

        string id = (data.historyId ?? data.name ?? "").ToLower();
        if (id.Contains("megat") || id.Contains("panji"))
        {
            Sprite s = Resources.Load<Sprite>("MuseumData/DataSejarah/Media/MegatPanjiAlam/eFOTO-EF-260812-4E656F-24351");
            if (s != null) return s;
            Texture2D tex = Resources.Load<Texture2D>("MuseumData/DataSejarah/Media/MegatPanjiAlam/eFOTO-EF-260812-4E656F-24351");
            if (tex != null) return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
        }

        return null;
    }

    public void OnHistoryItemClicked(HistoryData data)
    {
        if (data == null) return;

        if (historyDetailPanel == null)
        {
            historyDetailPanel = FindObjectOfType<HistoryPanel>(true);
        }

        if (historyDetailPanel != null)
        {
            gameObject.SetActive(false);
            historyDetailPanel.Setup(data, () =>
            {
                gameObject.SetActive(true);
            });
            historyDetailPanel.OpenPanel();
        }
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    public void OnBackPressed()
    {
        ClosePanel();
    }

    private void WireControlButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePanel);
        }
        if (closeButtonXR != null)
        {
            closeButtonXR.onClick.RemoveAllListeners();
            closeButtonXR.onClick.AddListener(ClosePanel);
        }
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBackPressed);
        }
        if (backButtonXR != null)
        {
            backButtonXR.onClick.RemoveAllListeners();
            backButtonXR.onClick.AddListener(OnBackPressed);
        }
    }

    private void AutoFindUIReferences()
    {
        if (roomTitleText == null) roomTitleText = FindDeepChild<TMP_Text>(transform, "RoomTitleText");
        if (roomSubtitleText == null) roomSubtitleText = FindDeepChild<TMP_Text>(transform, "RoomSubtitleText");
        if (sectionHeaderText == null) sectionHeaderText = FindDeepChild<TMP_Text>(transform, "SectionHeader");
        if (artifactCountText == null) artifactCountText = FindDeepChild<TMP_Text>(transform, "ArtifactCountText");

        if (artifactListContainer == null)
        {
            Transform t = transform.Find("ArtifactList");
            if (t == null) t = transform.Find("Content/ArtifactList");
            if (t == null) t = FindDeepChild<Transform>(transform, "ArtifactList");
            if (t != null) artifactListContainer = t;
        }

        if (historyDetailPanel == null)
        {
            historyDetailPanel = FindObjectOfType<HistoryPanel>(true);
        }

        if (closeButton == null)
        {
            Transform t = transform.Find("CloseButton");
            if (t != null) closeButton = t.GetComponent<Button>();
        }
        if (backButton == null)
        {
            Transform t = transform.Find("BackButton");
            if (t != null) backButton = t.GetComponent<Button>();
        }
    }

    /// <summary>
    /// Configures the HistoryListPanel and its children into a clean 2-column grid layout.
    /// </summary>
    private void SetupTwoColumnLayout()
    {
        if (artifactListContainer == null) return;

        // 1. HistoryListPanel root RectTransform
        RectTransform panelRt = GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(580f, 360f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.localScale = Vector3.one;
        }

        // 2. Panel background image
        Transform bgTransform = transform.Find("Background");
        if (bgTransform != null)
        {
            RectTransform bgRt = bgTransform.GetComponent<RectTransform>();
            if (bgRt != null)
            {
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.pivot = new Vector2(0.5f, 0.5f);
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;
                bgRt.localScale = Vector3.one;
            }
        }

        // 3. Disable VerticalLayoutGroup if present
        VerticalLayoutGroup vlg = artifactListContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.enabled = false;
        }

        // 4. Configure GridLayoutGroup for 2 columns
        GridLayoutGroup grid = artifactListContainer.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = artifactListContainer.gameObject.AddComponent<GridLayoutGroup>();
        }

        grid.enabled = true;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.cellSize = new Vector2(260f, 52f);
        grid.spacing = new Vector2(10f, 6f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(0, 0, 2, 2);

        // 5. Container RectTransform
        RectTransform listRt = artifactListContainer.GetComponent<RectTransform>();
        if (listRt != null)
        {
            listRt.anchorMin = Vector2.zero;
            listRt.anchorMax = Vector2.one;
            listRt.pivot = new Vector2(0.5f, 0.5f);
            listRt.offsetMin = new Vector2(18f, 14f);
            listRt.offsetMax = new Vector2(-18f, -78f);
            listRt.localScale = Vector3.one;
        }

        // 6. Header elements positions
        Transform headerIcon = transform.Find("HeaderIcon");
        if (headerIcon != null)
        {
            RectTransform rt = headerIcon.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(18f, -12f);
                rt.sizeDelta = new Vector2(30f, 30f);
            }
        }

        if (roomTitleText != null)
        {
            RectTransform rt = roomTitleText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(55f, -10f);
            rt.sizeDelta = new Vector2(320f, 28f);
        }

        if (roomSubtitleText != null)
        {
            RectTransform rt = roomSubtitleText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(55f, -34f);
            rt.sizeDelta = new Vector2(320f, 20f);
        }

        if (closeButton != null)
        {
            RectTransform rt = closeButton.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-18f, -12f);
                rt.sizeDelta = new Vector2(30f, 30f);
            }
        }

        if (backButton != null)
        {
            RectTransform rt = backButton.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-54f, -12f);
                rt.sizeDelta = new Vector2(30f, 30f);
            }
        }

        Transform separator = transform.Find("SeparatorLine");
        if (separator != null)
        {
            RectTransform rt = separator.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(18f, -58f);
                rt.offsetMax = new Vector2(-18f, -56f);
            }
        }

        if (sectionHeaderText != null)
        {
            RectTransform rt = sectionHeaderText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(18f, -62f);
            rt.sizeDelta = new Vector2(320f, 18f);
        }

        if (artifactCountText != null)
        {
            RectTransform rt = artifactCountText.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-18f, -62f);
            rt.sizeDelta = new Vector2(140f, 18f);
        }
    }

    private static T FindDeepChild<T>(Transform parent, string childName) where T : Component
    {
        if (parent == null) return null;
        foreach (T comp in parent.GetComponentsInChildren<T>(true))
        {
            if (comp != null && comp.name == childName) return comp;
        }
        return null;
    }
}
