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

    [HideInInspector] public List<GameObject> activeHistoryPanels = new List<GameObject>();

    /// <summary>
    /// Open the HistoryPanel (Detail) with the specified HistoryData.
    /// Spawns an independent world-space panel instance so the user can place multiple historical panels around their room.
    /// </summary>
    public GameObject ShowHistoryDetail(HistoryData data)
    {
        if (data == null) return null;
        AutoFindPanels();

        if (historyDetailPanel == null)
        {
            Debug.LogWarning("HistoryManager: No HistoryDetailPanel (HistoryPanel) found in scene!");
            return null;
        }

        // Clean up destroyed entries from tracking list
        activeHistoryPanels.RemoveAll(go => go == null);

        // Check if there is already an active panel displaying this exact HistoryData
        foreach (GameObject panelGo in activeHistoryPanels)
        {
            if (panelGo != null && panelGo.activeInHierarchy)
            {
                HistoryPanel hp = panelGo.GetComponentInChildren<HistoryPanel>(true);
                if (hp != null && hp.activeHistoryData == data)
                {
                    panelGo.SetActive(true);
                    return panelGo;
                }
            }
        }

        // Ensure the scene-template under ExplorationCanvas stays inactive
        if (historyDetailPanel.gameObject.activeSelf && historyDetailPanel.transform.parent != null)
        {
            historyDetailPanel.gameObject.SetActive(false);
        }

        // Calculate spawn position in world space
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
        if (forward == Vector3.zero) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        int count = activeHistoryPanels.Count;
        float sideOffset = (count % 2 == 1) ? ((count + 1) / 2) * 0.65f : -(count / 2) * 0.65f;

        Vector3 basePos = cam != null ? cam.position : Vector3.zero;
        Vector3 spawnPos = basePos + forward * 0.85f + right * sideOffset;
        Quaternion spawnRot = Quaternion.LookRotation(-forward, Vector3.up);

        GameObject newWrapper = BuildWorldSpaceHistoryPanel(historyDetailPanel.gameObject, spawnPos, spawnRot);
        newWrapper.name = $"HistoryDetailPanel_{data.name}";

        HistoryPanel cloneHp = newWrapper.GetComponentInChildren<HistoryPanel>(true);
        if (cloneHp != null)
        {
            cloneHp.gameObject.SetActive(true);
            cloneHp.Setup(data, () =>
            {
                activeHistoryPanels.Remove(newWrapper);
                if (newWrapper != null) Destroy(newWrapper);
            });
        }

        activeHistoryPanels.Add(newWrapper);
        Debug.Log($"HistoryManager: Spawned standalone history panel for '{data.name}'. Total open history panels: {activeHistoryPanels.Count}");
        return newWrapper;
    }

    private GameObject BuildWorldSpaceHistoryPanel(GameObject source, Vector3 pos, Quaternion rot)
    {
        RectTransform srcRT = source.GetComponent<RectTransform>();
        Vector2 size = srcRT != null ? srcRT.sizeDelta : new Vector2(640f, 480f);
        float worldScale = source.transform.lossyScale.x;
        if (worldScale <= 0.00001f) worldScale = 0.0011f;

        GameObject wrapper = new GameObject("HistoryDetailPanelCanvas");
        Canvas canvas = wrapper.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        wrapper.AddComponent<UnityEngine.UI.CanvasScaler>();
        wrapper.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        if (wrapper.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() == null)
            wrapper.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

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
    /// Close both list and detail history panels.
    /// </summary>
    public void CloseAllPanels()
    {
        if (historyListPanel != null) historyListPanel.ClosePanel();
        if (historyDetailPanel != null) historyDetailPanel.ClosePanel();

        foreach (GameObject panelGo in activeHistoryPanels)
        {
            if (panelGo != null && panelGo != historyDetailPanel.gameObject)
            {
                Destroy(panelGo);
            }
        }
        activeHistoryPanels.Clear();
    }
}
