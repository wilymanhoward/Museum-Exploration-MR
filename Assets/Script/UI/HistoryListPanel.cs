using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the HistoryListPanel UI hierarchy shown in the editor screenshot:
/// RoomTitleText ("Ruang Sejarah"), RoomSubtitleText ("Sejarah Terengganu"), SectionHeader ("Informasi Sejarah di Ruangan ini"),
/// ArtifactCountText ("Jumlah: 5"), and populates ArtifactList with item buttons.
/// Clicking an item in the list opens HistoryDetailPanel (HistoryPanel).
/// Automatically auto-detects and populates HistoryData assets if historyItems is empty in Inspector.
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

    [Tooltip("Item count text (e.g. 'Jumlah: 5').")]
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
    [Tooltip("Leave empty to automatically auto-load all HistoryData entries from HistoryManager / Resources.")]
    public List<HistoryData> historyItems = new List<HistoryData>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        AutoFindUIReferences();
        WireControlButtons();
    }

    private void OnEnable()
    {
        AutoFindUIReferences();
        WireControlButtons();
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

        // Activate BEFORE measuring wrapped line counts below (ApplyCappedTwoLineStyle uses
        // TMP's ForceMeshUpdate, which needs the object live to lay text out correctly).
        gameObject.SetActive(true);

        if (roomTitleText != null) ApplyCappedTwoLineStyle(roomTitleText, title, 20f);
        if (roomSubtitleText != null) ApplyCappedTwoLineStyle(roomSubtitleText, subtitle, 14f);

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
    /// Instantiate item buttons inside artifactListContainer for each HistoryData entry.
    /// </summary>
    public void PopulateHistoryList()
    {
        EnsureHistoryItems();

        if (artifactCountText != null)
        {
            int count = historyItems != null ? historyItems.Count : 0;
            ApplyCappedTwoLineStyle(artifactCountText, $"Jumlah: {count}", 13f);
        }

        if (artifactListContainer == null) return;

        // Clear existing spawned items
        foreach (Transform child in artifactListContainer)
        {
            Destroy(child.gameObject);
        }

        if (historyItems == null || historyItems.Count == 0) return;

        for (int i = 0; i < historyItems.Count; i++)
        {
            HistoryData data = historyItems[i];
            if (data == null) continue;

            GameObject itemObj = null;
            if (historyItemPrefab != null)
            {
                itemObj = Instantiate(historyItemPrefab, artifactListContainer);
            }
            else
            {
                itemObj = CreateDefaultListItem(data.eventTitle);
                itemObj.transform.SetParent(artifactListContainer, false);
            }

            // Set button text. historyItemPrefab is the same ArtifactItem prefab the
            // regular artifact rooms use, which has TWO text fields: NumText ("01") and
            // NameText ("Mona Lisa" placeholder). Grabbing only the first TMP_Text (as
            // this used to do) lands on NumText and leaves NameText's "Mona Lisa"
            // placeholder showing untouched on every button - mirror Room.cs's matching
            // logic so both fields (and any other stray placeholder) get handled properly.
            string displayText = string.IsNullOrEmpty(data.eventTitle) ? data.name : data.eventTitle;
            string formattedNum = (i + 1).ToString("D2");

            bool titleUpdated = false;
            foreach (TMP_Text tmp in itemObj.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null) continue;
                string nameLower = tmp.name.ToLower();
                string textLower = (tmp.text ?? "").ToLower();

                if (!titleUpdated && (nameLower.Contains("name") || nameLower.Contains("title") || textLower.Contains("mona") || textLower.Contains("lisa")))
                {
                    tmp.gameObject.SetActive(true);
                    ApplyCappedTwoLineStyle(tmp, displayText, 12f);
                    titleUpdated = true;
                }
                else if (nameLower.Contains("num") || nameLower.Contains("number") || nameLower.Contains("index"))
                {
                    tmp.text = formattedNum;
                    tmp.enableAutoSizing = false;
                    tmp.fontSize = 12f;
                }
                else if (textLower.Contains("mona") || textLower.Contains("lisa"))
                {
                    // Any other leftover placeholder text object - hide it.
                    tmp.gameObject.SetActive(false);
                }
            }

            if (!titleUpdated)
            {
                TMP_Text fallback = itemObj.GetComponentInChildren<TMP_Text>(true);
                if (fallback != null)
                {
                    ApplyCappedTwoLineStyle(fallback, displayText, 12f);
                }
            }

            // Wire click event to open detail panel
            Button btn = itemObj.GetComponent<Button>();
            if (btn == null) btn = itemObj.AddComponent<Button>();

            HistoryData capturedData = data;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnHistoryItemClicked(capturedData));

            XRButtonSelection xr = itemObj.GetComponent<XRButtonSelection>();
            if (xr != null)
            {
                xr.onClick.RemoveAllListeners();
                xr.onClick.AddListener(() => OnHistoryItemClicked(capturedData));
            }
        }
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
                // On detail panel close -> return to list panel
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

    /// <summary>
    /// Wraps long text to at most 2 lines, ellipsising anything beyond that ("Infrastruktur
    /// &amp; Pembangunan Terengganu…"). Deliberately does NOT use TMP's maxVisibleLines/
    /// Ellipsis overflow mode - both need a real positive box HEIGHT to know how much text
    /// fits, and this prefab's text fields have a degenerate (negative/near-zero) height
    /// baked in, which made maxVisibleLines compute "zero lines fit" and hide the text
    /// completely. Instead this measures the WIDTH-based wrap result directly (via
    /// ForceMeshUpdate + textInfo.lineCount, which don't care about box height) and manually
    /// truncates the string to the first 2 lines' worth of characters - so it can never
    /// disappear regardless of whatever the container's height happens to be.
    /// </summary>
    private static void ApplyCappedTwoLineStyle(TMP_Text label, string text, float fontSize)
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

    private GameObject CreateDefaultListItem(string labelText)
    {
        GameObject item = new GameObject("HistoryListItem");
        RectTransform rt = item.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(480f, 45f);

        Image img = item.AddComponent<Image>();
        img.color = new Color(0.18f, 0.20f, 0.22f, 0.9f);
        item.AddComponent<Button>();

        GameObject txtObj = new GameObject("Text (TMP)");
        txtObj.transform.SetParent(item.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TMP_Text txt = txtObj.AddComponent<TMP_Text>();
        txt.text = labelText;
        txt.fontSize = 15;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.margin = new Vector4(20f, 0f, 0f, 0f);
        txt.color = Color.white;

        return item;
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
