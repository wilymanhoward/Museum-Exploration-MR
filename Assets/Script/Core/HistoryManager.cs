using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton coordinator for History Panels in the scene.
/// Manages both HistoryListPanel (showing list of historical entries) and HistoryPanel (Detail Panel).
/// Automatically auto-detects and loads all HistoryData ScriptableObjects placed in Resources folders.
/// </summary>
public class HistoryManager : MonoBehaviour
{
    public static HistoryManager Instance { get; private set; }

    [Header("History Database (Auto-loaded from Resources if empty)")]
    [Tooltip("List of all HistoryData ScriptableObjects available in the game. Will auto-detect from Resources if left empty.")]
    public List<HistoryData> historyDatabase = new List<HistoryData>();

    [Header("UI Panel References (Auto-found if empty)")]
    [Tooltip("The HistoryListPanel instance in the scene.")]
    public HistoryListPanel historyListPanel;

    [Tooltip("The HistoryPanel (Detail) instance in the scene.")]
    public HistoryPanel historyDetailPanel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        AutoFindPanels();
        EnsureHistoryDatabase();
    }

    private void AutoFindPanels()
    {
        if (historyListPanel == null)
        {
            historyListPanel = FindObjectOfType<HistoryListPanel>(true);
        }
        if (historyDetailPanel == null)
        {
            historyDetailPanel = FindObjectOfType<HistoryPanel>(true);
        }
    }

    private void EnsureHistoryDatabase()
    {
        if (historyDatabase == null) historyDatabase = new List<HistoryData>();

        if (historyDatabase.Count == 0)
        {
            // Auto-detect all HistoryData ScriptableObjects in Resources folders (DataSejarah, History, MuseumData, or root Resources)
            HistoryData[] resHistory = Resources.LoadAll<HistoryData>("MuseumData/DataSejarah");
            if (resHistory == null || resHistory.Length == 0) resHistory = Resources.LoadAll<HistoryData>("MuseumData/History");
            if (resHistory == null || resHistory.Length == 0) resHistory = Resources.LoadAll<HistoryData>("DataSejarah");
            if (resHistory == null || resHistory.Length == 0) resHistory = Resources.LoadAll<HistoryData>("MuseumData");
            if (resHistory == null || resHistory.Length == 0) resHistory = Resources.LoadAll<HistoryData>("");

            if (resHistory != null && resHistory.Length > 0)
            {
                historyDatabase.AddRange(resHistory);
                Debug.Log($"HistoryManager: Automatically loaded {resHistory.Length} HistoryData assets from Resources.");
            }
        }
    }

    /// <summary>
    /// Open the HistoryListPanel showing all entries in the history database.
    /// </summary>
    public void ShowHistoryList(string title = "Ruang Sejarah", string subtitle = "Sejarah Terengganu")
    {
        AutoFindPanels();
        EnsureHistoryDatabase();

        if (historyListPanel == null)
        {
            Debug.LogWarning("HistoryManager: No HistoryListPanel found in scene!");
            return;
        }

        historyListPanel.ShowList(historyDatabase, title, subtitle);
    }

    /// <summary>
    /// Open the HistoryPanel (Detail) with the specified HistoryData.
    /// </summary>
    public void ShowHistoryDetail(HistoryData data)
    {
        AutoFindPanels();

        if (historyDetailPanel == null)
        {
            Debug.LogWarning("HistoryManager: No HistoryDetailPanel (HistoryPanel) found in scene!");
            return;
        }

        historyDetailPanel.Setup(data);
        historyDetailPanel.OpenPanel();
    }

    /// <summary>
    /// Open the HistoryPanel (Detail) for a history entry by its unique historyId string.
    /// </summary>
    public void ShowHistoryDetail(string historyId)
    {
        if (string.IsNullOrEmpty(historyId)) return;
        EnsureHistoryDatabase();

        HistoryData match = historyDatabase.Find(h => h != null && h.historyId.Equals(historyId, System.StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            ShowHistoryDetail(match);
        }
        else
        {
            Debug.LogWarning($"HistoryManager: No HistoryData found with ID '{historyId}'.");
        }
    }

    /// <summary>
    /// Close both list and detail history panels.
    /// </summary>
    public void CloseAllPanels()
    {
        if (historyListPanel != null) historyListPanel.ClosePanel();
        if (historyDetailPanel != null) historyDetailPanel.ClosePanel();
    }
}
