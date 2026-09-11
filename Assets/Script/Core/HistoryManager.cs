using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton coordinator for History Panels in the scene.
/// Manages both HistoryListPanel (showing list of historical entries) and HistoryPanel (Detail Panel).
/// Automatically auto-detects and loads all HistoryData ScriptableObjects placed in Resources folders.
/// </summary>
public class HistoryManager : MonoBehaviour
{
    private static HistoryManager instance;
    public static HistoryManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<HistoryManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("HistoryManager");
                    instance = go.AddComponent<HistoryManager>();
                }
            }
            return instance;
        }
    }

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
        if (instance == null) instance = this;
        else if (instance != this) { Destroy(gameObject); return; }

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

    public readonly List<GameObject> activePanelInstances = new List<GameObject>();

    /// <summary>
    /// Ensures only one history panel plays audio narration at a time to prevent overlapping audio.
    /// </summary>
    public static void StopAllHistoryAudioExcept(HistoryPanel current)
    {
        if (Instance == null || Instance.activePanelInstances == null) return;
        foreach (var p in Instance.activePanelInstances)
        {
            if (p == null) continue;
            var hp = p.GetComponentInChildren<HistoryPanel>(true);
            if (hp != null && hp != current)
            {
                hp.StopAudio();
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

        // Calculate spawn position in world space
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
        if (forward == Vector3.zero) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // Stagger multiple open panels horizontally: 0m, +0.65m, -0.65m, +1.3m, -1.3m
        int count = activePanelInstances.Count;
        float sideOffset = (count == 0) ? 0f : ((count % 2 == 1) ? ((count + 1) / 2) * 0.65f : -(count / 2) * 0.65f);

        Vector3 spawnPos = (cam != null ? cam.position : Vector3.zero) + forward * 0.85f + right * sideOffset - Vector3.up * 0.05f;
        Quaternion spawnRot = Quaternion.LookRotation(forward, Vector3.up);

        GameObject newPanelInstance = BuildWorldSpacePanel(historyDetailPanel.gameObject, spawnPos, spawnRot);
        newPanelInstance.name = $"HistoryDetailPanel_{data.name}";

        HistoryPanel newHp = newPanelInstance.GetComponentInChildren<HistoryPanel>(true);
        if (newHp != null)
        {
            newHp.Setup(data, () => {
                activePanelInstances.Remove(newPanelInstance);
                if (newPanelInstance != null && (historyDetailPanel == null || newPanelInstance != historyDetailPanel.gameObject))
                {
                    Destroy(newPanelInstance);
                }
            });
        }

        newPanelInstance.SetActive(true);

        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.ApplyToHierarchy(newPanelInstance);
        }

        // Keep the scene template panel hidden so only the world space instance is displayed
        historyDetailPanel.gameObject.SetActive(false);

        activePanelInstances.Add(newPanelInstance);
        Debug.Log($"HistoryManager: Spawned history panel for '{data.name}' in world space. Total active panels: {activePanelInstances.Count}");
        return newPanelInstance;
    }

    /// <summary>
    /// Clones the designed HistoryPanel directly into a standalone world-space Canvas
    /// with graphic raycasters and an ArtifactPanelDragger so multiple panels can float,
    /// be moved freely, and snap to walls side-by-side.
    /// </summary>
    private GameObject BuildWorldSpacePanel(GameObject source, Vector3 pos, Quaternion rot)
    {
        // Clone the source detail panel directly as an independent root world-space panel
        GameObject clone = Instantiate(source);
        clone.transform.SetParent(null, false);
        clone.transform.position = pos;
        clone.transform.rotation = rot;
        clone.transform.localScale = Vector3.one * 0.0011f;

        // Ensure it has its own world-space Canvas
        Canvas canvas = clone.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = clone.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 0;
        if (Camera.main != null) canvas.worldCamera = Camera.main;

        // Ensure UI and XR raycasters
        if (clone.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            clone.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        if (clone.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() == null)
            clone.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        // Ensure pinch-and-hold dragger and wall-snapping
        if (clone.GetComponent<ArtifactPanelDragger>() == null)
            clone.AddComponent<ArtifactPanelDragger>();

        return clone;
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
            if (historyDetailPanel != null && panel == historyDetailPanel.gameObject)
            {
                panel.SetActive(false);
            }
            else
            {
                Destroy(panel);
            }
        }
        activePanelInstances.Clear();

        if (historyDetailPanel != null)
        {
            historyDetailPanel.gameObject.SetActive(false);
        }
    }
}
