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
        newPanelInstance.SetActive(true);

        HistoryPanel newHp = newPanelInstance.GetComponentInChildren<HistoryPanel>(true);
        if (newHp != null)
        {
            newHp.gameObject.SetActive(true);
            newHp.Setup(data, () => {
                activePanelInstances.Remove(newPanelInstance);
                Destroy(newPanelInstance);
            });
        }

        // Keep the scene template panel hidden so only the world space instance is displayed
        historyDetailPanel.gameObject.SetActive(false);

        activePanelInstances.Add(newPanelInstance);
        Debug.Log($"HistoryManager: Spawned history panel for '{data.name}' in world space. Total active panels: {activePanelInstances.Count}");
        return newPanelInstance;
    }

    /// <summary>
    /// Wraps a clone of the HistoryPanel in its own world-space Canvas with graphic raycasters
    /// and repositioning dragger so multiple panels can float and be interacted with side-by-side.
    /// </summary>
    private GameObject BuildWorldSpacePanel(GameObject source, Vector3 pos, Quaternion rot)
    {
        RectTransform srcRT = source.GetComponent<RectTransform>();
        Vector2 size = srcRT != null ? srcRT.sizeDelta : new Vector2(560f, 400f);
        if (size.x < 100f || size.y < 100f) size = new Vector2(560f, 400f);

        float worldScale = source.transform.lossyScale.x;
        if (worldScale <= 0.00001f) worldScale = 0.001f;

        GameObject wrapper = new GameObject("HistoryDetailPanelCanvas");
        Canvas canvas = wrapper.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        wrapper.AddComponent<UnityEngine.UI.CanvasScaler>();
        wrapper.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // XR ray/poke UI raycaster so the panel's buttons are clickable with VR hands/controllers
        if (wrapper.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() == null)
            wrapper.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        // Pinch-and-hold dragger for repositioning panels freely in world space
        if (wrapper.GetComponent<ArtifactPanelDragger>() == null)
            wrapper.AddComponent<ArtifactPanelDragger>();

        RectTransform wrt = wrapper.GetComponent<RectTransform>();
        wrt.sizeDelta = size;
        wrapper.transform.position = pos;
        wrapper.transform.rotation = rot;
        wrapper.transform.localScale = Vector3.one * worldScale;

        // Clone the designed panel under the wrapper and stretch it to fill
        GameObject panel = Instantiate(source, wrapper.transform);
        panel.SetActive(true);
        RectTransform prt = panel.GetComponent<RectTransform>();
        if (prt != null)
        {
            prt.localScale = Vector3.one;
            prt.localRotation = Quaternion.identity;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = size;
            prt.anchoredPosition = Vector2.zero;
        }

        return wrapper;
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
            Destroy(panel);
        }
        activePanelInstances.Clear();

        if (historyDetailPanel != null)
        {
            historyDetailPanel.gameObject.SetActive(false);
        }
    }
}
