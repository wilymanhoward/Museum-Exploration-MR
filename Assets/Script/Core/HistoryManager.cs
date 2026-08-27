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

    private readonly List<GameObject> activePanelInstances = new List<GameObject>();

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
    /// Supports opening multiple history panels simultaneously to place them side-by-side in world space.
    /// </summary>
    public GameObject ShowHistoryDetail(HistoryData data)
    {
        AutoFindPanels();

        if (data == null) return null;

        if (historyDetailPanel == null)
        {
            Debug.LogWarning("HistoryManager: No HistoryDetailPanel (HistoryPanel) found in scene!");
            return null;
        }

        // Clean up any destroyed panel instances
        activePanelInstances.RemoveAll(p => p == null);

        // Check if an existing panel is already open for this exact history entry
        foreach (GameObject panel in activePanelInstances)
        {
            if (panel == null) continue;
            HistoryPanel hp = panel.GetComponentInChildren<HistoryPanel>(true);
            if (hp != null && hp.activeHistoryData != null && !string.IsNullOrEmpty(hp.activeHistoryData.historyId) && hp.activeHistoryData.historyId == data.historyId)
            {
                panel.SetActive(true);
                hp.gameObject.SetActive(true);
                return panel;
            }
        }

        // If no panel is currently active, use the scene's primary historyDetailPanel
        if (!historyDetailPanel.gameObject.activeInHierarchy && activePanelInstances.Count == 0)
        {
            historyDetailPanel.Setup(data, () => {
                activePanelInstances.Remove(historyDetailPanel.gameObject);
            });
            historyDetailPanel.OpenPanel();
            activePanelInstances.Add(historyDetailPanel.gameObject);
            return historyDetailPanel.gameObject;
        }

        // If another panel is already open in world space, spawn a new clone side-by-side!
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
        if (forward == Vector3.zero) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        int count = activePanelInstances.Count;
        float sideOffset = (count % 2 == 1) ? ((count + 1) / 2) * 0.65f : -(count / 2) * 0.65f;

        Vector3 spawnPos = (cam != null ? cam.position : Vector3.zero) + forward * 0.75f + right * sideOffset - Vector3.up * 0.05f;
        Quaternion spawnRot = Quaternion.LookRotation(-forward, Vector3.up);

        GameObject newInstance = Instantiate(historyDetailPanel.gameObject, spawnPos, spawnRot);
        newInstance.name = $"HistoryDetailPanel_{data.name}";
        newInstance.SetActive(true);

        HistoryPanel newHp = newInstance.GetComponent<HistoryPanel>();
        if (newHp != null)
        {
            newHp.Setup(data, () => {
                activePanelInstances.Remove(newInstance);
                Destroy(newInstance);
            });
        }

        activePanelInstances.Add(newInstance);
        Debug.Log($"HistoryManager: Spawned history panel for '{data.name}' in world space. Total active panels: {activePanelInstances.Count}");
        return newInstance;
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
    /// Close all list and detail history panels.
    /// </summary>
    public void CloseAllPanels()
    {
        if (historyListPanel != null) historyListPanel.ClosePanel();

        foreach (GameObject panel in activePanelInstances)
        {
            if (panel == null) continue;
            HistoryPanel hp = panel.GetComponentInChildren<HistoryPanel>(true);
            if (hp != null) hp.ClosePanel();
            else panel.SetActive(false);
        }
        activePanelInstances.Clear();
    }
}
