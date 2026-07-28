using UnityEngine;

public class ArtifactManager : MonoBehaviour
{
    public static ArtifactManager Instance { get; private set; }

    [Header("Player Tracking")]
    [Tooltip("Reference to the player's camera or headset transform.")]
    public Transform playerTransform;

    [Header("Persistent Scene UI Canvas References")]
    [Tooltip("The persistent parent canvas GameObject in the scene (ArtifactUICanvas).")]
    public GameObject artifactUiCanvas;

    [Tooltip("The script controller (Artifact) on the panel UI.")]
    public Artifact artifactInteraction;

    [Header("UI Prefab Fallback (Optional)")]
    [Tooltip("Prefab for the floating detail panel spawned when an artifact QR is scanned (fallback only).")]
    public GameObject artifactPanelPrefab;

    [Header("Selected Artifact Details")]
    [Tooltip("The currently selected artifact.")]
    public ArtifactData selectedArtifact;

    private ArtifactData lastSelectedArtifact;
    private GameObject activePanelInstance;
    private readonly System.Collections.Generic.List<GameObject> activePanelInstances = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.HashSet<string> scannedArtifactIds = new System.Collections.Generic.HashSet<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Fallback for player transform
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }

        // Automatic scene lookup for persistent references (even if inactive)
        if (artifactUiCanvas == null)
        {
            GameObject[] allGo = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject go in allGo)
            {
                if ((go.name == "ArtifactDetailPanel" || go.name == "ArtifactUICanvas" || go.name == "ArtifactPanelPrefab") && go.scene.isLoaded)
                {
                    artifactUiCanvas = go;
                    break;
                }
            }
            if (artifactUiCanvas != null)
            {
                Debug.Log($"ArtifactManager: Automatically located '{artifactUiCanvas.name}' (even if inactive) in the scene.");
            }
        }

        if (artifactUiCanvas != null)
        {
            if (artifactInteraction == null)
            {
                artifactInteraction = artifactUiCanvas.GetComponentInChildren<Artifact>(true);
            }

            UnityEngine.UI.Image canvasBg = artifactUiCanvas.GetComponent<UnityEngine.UI.Image>();
            if (canvasBg == null) canvasBg = artifactUiCanvas.transform.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
            if (canvasBg == null) canvasBg = artifactUiCanvas.GetComponentInChildren<UnityEngine.UI.Image>();
            if (canvasBg != null && (canvasBg.material == null || canvasBg.material.name == "Default UI"))
            {
                foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m != null && (m.name == "Mat_ArtifactDetailPanel" || m.name == "Mat_OptionsCardBackground"))
                    {
                        canvasBg.material = m;
                        break;
                    }
                }
            }

            // Hide the canvas initially
            artifactUiCanvas.SetActive(false);
        }

        // Register for QR Scanner events
        QRCodeScanner.OnQRCodeScanned += HandleQRCodeScanned;
        QRCodeScanner.OnQRCodeLost += HandleQRCodeLost;
    }

    private void OnDestroy()
    {
        QRCodeScanner.OnQRCodeScanned -= HandleQRCodeScanned;
        QRCodeScanner.OnQRCodeLost -= HandleQRCodeLost;
    }

    /// <summary>
    /// Updates the selected artifact, syncs the UI panel to display it, and repositions the panel if a pose is provided.
    /// </summary>
    public void UpdateArtifact(ArtifactData artifact)
    {
        UpdateArtifact(artifact, CalculateDefaultPose());
    }

    /// <summary>
    /// Updates the selected artifact and spawns a new world-space detail panel for it.
    /// </summary>
    public void UpdateArtifact(ArtifactData artifact, Pose pose)
    {
        selectedArtifact = artifact;
        lastSelectedArtifact = artifact;

        if (artifact == null)
        {
            CloseActivePanel();
            return;
        }

        SpawnArtifactDetailPanel(artifact, pose);
    }

    /// <summary>
    /// Spawns a new independent world-space detail panel for the specified artifact.
    /// Multiple artifact detail panels can exist in world space simultaneously.
    /// </summary>
    public GameObject SpawnArtifactDetailPanel(ArtifactData artifact, Pose customPose = default)
    {
        if (artifact == null) return null;

        if (!string.IsNullOrEmpty(artifact.artifactId))
        {
            scannedArtifactIds.Add(artifact.artifactId.Trim().ToLower());
        }

        // Clean up any destroyed panels
        activePanelInstances.RemoveAll(p => p == null);

        // Check if a panel is already open for this exact artifact ID
        foreach (GameObject panel in activePanelInstances)
        {
            if (panel == null) continue;
            Artifact art = panel.GetComponentInChildren<Artifact>(true);
            if (art != null && art.artifactData != null && !string.IsNullOrEmpty(art.artifactData.artifactId) && art.artifactData.artifactId == artifact.artifactId)
            {
                panel.SetActive(true);
                art.gameObject.SetActive(true);
                return panel;
            }
        }

        // Resolve template prefab to instantiate
        GameObject prefabToUse = artifactPanelPrefab;
        if (prefabToUse == null) prefabToUse = artifactUiCanvas;

        // Resolve player transform
        if (playerTransform == null)
        {
            if (Camera.main != null) playerTransform = Camera.main.transform;
            else if (FindObjectOfType<Camera>() != null) playerTransform = FindObjectOfType<Camera>().transform;
        }

        // Calculate spawn position in world space
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (customPose.position != Vector3.zero || customPose.rotation != Quaternion.identity)
        {
            spawnPos = customPose.position;
            spawnRot = customPose.rotation;
        }
        else
        {
            // Position 1.0m in front of player, staggered side-by-side if multiple panels are open
            Vector3 fwd = playerTransform != null ? Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up).normalized : Vector3.forward;
            if (fwd == Vector3.zero) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            // Stagger multiple open panels horizontally: 0m, +0.6m, -0.6m, +1.2m, -1.2m
            int count = activePanelInstances.Count;
            float sideOffset = (count % 2 == 1) ? ((count + 1) / 2) * 0.6f : -(count / 2) * 0.6f;

            Vector3 basePos = playerTransform != null ? playerTransform.position : Vector3.zero;
            spawnPos = basePos + fwd * 1.0f + right * sideOffset;
            spawnRot = Quaternion.LookRotation(-fwd, Vector3.up);
        }

        GameObject newPanelInstance = null;
        if (prefabToUse != null)
        {
            newPanelInstance = Instantiate(prefabToUse, spawnPos, spawnRot);
            newPanelInstance.name = $"ArtifactDetailPanel_{artifact.artifactName}";
            newPanelInstance.SetActive(true);
        }
        else
        {
            Debug.LogError("ArtifactManager: No artifactPanelPrefab or artifactUiCanvas assigned to spawn detail panel!");
            return null;
        }

        Canvas canvas = newPanelInstance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        }

        Artifact interaction = newPanelInstance.GetComponentInChildren<Artifact>(true);
        if (interaction != null)
        {
            interaction.gameObject.SetActive(true);
            interaction.Setup(artifact, playerTransform, new Pose(spawnPos, spawnRot), () => {
                activePanelInstances.Remove(newPanelInstance);
                if (newPanelInstance != artifactUiCanvas)
                {
                    Destroy(newPanelInstance);
                }
                else
                {
                    newPanelInstance.SetActive(false);
                }
            });
        }

        activePanelInstances.Add(newPanelInstance);
        selectedArtifact = artifact;
        lastSelectedArtifact = artifact;

        Debug.Log($"ArtifactManager: Spawned detail panel for '{artifact.artifactName}' in world space. Total open panels: {activePanelInstances.Count}");
        return newPanelInstance;
    }

    private Pose CalculateDefaultPose()
    {
        Transform referenceTransform = playerTransform != null ? playerTransform : (Camera.main != null ? Camera.main.transform : transform);
        Vector3 pos = referenceTransform.position + referenceTransform.forward * 1.5f;
        Quaternion rot = Quaternion.LookRotation(referenceTransform.forward, Vector3.up);
        return new Pose(pos, rot);
    }

    /// <summary>
    /// Callback from QR Scanner when a QR code payload is detected.
    /// </summary>
    private void HandleQRCodeScanned(string payload, Pose pose)
    {
        if (!MainMenu.IsExplorationStarted)
        {
            Debug.Log("ArtifactManager: Exploration has not started yet. Ignoring QR scan.");
            return;
        }

        ArtifactData artifactMatch = FindArtifactInProject(payload);
        if (artifactMatch != null)
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.SetScanStatus($"Scanned Exhibit: {artifactMatch.artifactName}", new Color(0.1f, 0.75f, 0.2f));
            }
            SpawnArtifactDetailPanel(artifactMatch, pose);
        }
    }

    private ArtifactData FindArtifactInProject(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        string cleanId = id.Trim().ToLower();
        ArtifactData match = null;

        // Helper comparison lambda: matches exact ID, case-insensitive ID, or substring payload
        bool Matches(ArtifactData a)
        {
            if (a == null || string.IsNullOrEmpty(a.artifactId)) return false;
            string artId = a.artifactId.Trim().ToLower();
            return artId == cleanId || cleanId.Contains(artId) || artId.Contains(cleanId);
        }

        // 1. Try to find via RoomManager active room or loaded room list
        if (RoomManager.Instance != null)
        {
            if (RoomManager.Instance.CurrentRoom != null && RoomManager.Instance.CurrentRoom.artifacts != null)
            {
                match = RoomManager.Instance.CurrentRoom.artifacts.Find(Matches);
            }

            if (match == null && RoomManager.Instance.rooms != null)
            {
                foreach (RoomData room in RoomManager.Instance.rooms)
                {
                    if (room != null && room.artifacts != null)
                    {
                        match = room.artifacts.Find(Matches);
                        if (match != null) break;
                    }
                }
            }
        }

        // 2. Load directly from Resources folder (works 100% in standalone APK builds on Quest 3!)
        if (match == null)
        {
            ArtifactData[] resourceArtifacts = Resources.LoadAll<ArtifactData>("MuseumData/Artifacts");
            if (resourceArtifacts != null)
            {
                foreach (ArtifactData data in resourceArtifacts)
                {
                    if (Matches(data))
                    {
                        match = data;
                        break;
                    }
                }
            }

            if (match == null)
            {
                foreach (ArtifactData data in Resources.FindObjectsOfTypeAll<ArtifactData>())
                {
                    if (Matches(data))
                    {
                        match = data;
                        break;
                    }
                }
            }
        }

        // 3. Editor Fallback: Find asset directly in project database
#if UNITY_EDITOR
        if (match == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ArtifactData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ArtifactData data = UnityEditor.AssetDatabase.LoadAssetAtPath<ArtifactData>(path);
                if (Matches(data))
                {
                    match = data;
                    break;
                }
            }
        }
#endif

        return match;
    }

    private void HandleQRCodeLost(string payload)
    {
        // When physical QR code is lost, close the panel/canvas
        foreach (GameObject panel in activePanelInstances)
        {
            if (panel != null)
            {
                Artifact interaction = panel.GetComponentInChildren<Artifact>();
                if (interaction != null && interaction.artifactData != null && interaction.artifactData.artifactId == payload)
                {
                    activePanelInstances.Remove(panel);
                    interaction.StartClose();
                    if (panel != artifactUiCanvas) Destroy(panel);
                    else panel.SetActive(false);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Closes the active artifact detail panel if one exists.
    /// </summary>
    public void CloseActivePanel()
    {
        activePanelInstances.RemoveAll(p => p == null);
        if (activePanelInstances.Count > 0)
        {
            GameObject last = activePanelInstances[activePanelInstances.Count - 1];
            activePanelInstances.RemoveAt(activePanelInstances.Count - 1);
            if (last != null)
            {
                Artifact interaction = last.GetComponentInChildren<Artifact>();
                if (interaction != null) interaction.StartClose();
                if (last != artifactUiCanvas) Destroy(last);
                else last.SetActive(false);
            }
        }
        selectedArtifact = null;
        lastSelectedArtifact = null;
    }

    /// <summary>
    /// Closes all active artifact detail panels in the scene.
    /// </summary>
    public void CloseAllPanels()
    {
        foreach (GameObject panel in activePanelInstances)
        {
            if (panel != null)
            {
                Artifact interaction = panel.GetComponentInChildren<Artifact>();
                if (interaction != null) interaction.StartClose();
                if (panel != artifactUiCanvas) Destroy(panel);
                else panel.SetActive(false);
            }
        }
        activePanelInstances.Clear();
        selectedArtifact = null;
        lastSelectedArtifact = null;
    }

    /// <summary>
    /// Relays artifact interaction state changes to RoomManager.
    /// </summary>
    public void MarkArtifactInteracted(string artifactId)
    {
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.MarkArtifactInteracted(artifactId);
        }
    }

    public bool IsArtifactScanned(ArtifactData data)
    {
        if (data == null || string.IsNullOrEmpty(data.artifactId)) return false;
        return scannedArtifactIds.Contains(data.artifactId.Trim().ToLower());
    }

    public System.Collections.Generic.List<ArtifactData> GetAllMuseumArtifacts()
    {
        var allList = new System.Collections.Generic.List<ArtifactData>();
        var seenIds = new System.Collections.Generic.HashSet<string>();

        void AddIfUnique(ArtifactData art)
        {
            if (art == null || string.IsNullOrEmpty(art.artifactId)) return;
            string key = art.artifactId.Trim().ToLower();
            if (!seenIds.Contains(key))
            {
                seenIds.Add(key);
                allList.Add(art);
            }
        }

        if (RoomManager.Instance != null && RoomManager.Instance.rooms != null)
        {
            foreach (var room in RoomManager.Instance.rooms)
            {
                if (room != null && room.artifacts != null)
                {
                    foreach (var art in room.artifacts) AddIfUnique(art);
                }
            }
        }

        ArtifactData[] resourceArtifacts = Resources.LoadAll<ArtifactData>("MuseumData");
        if (resourceArtifacts != null)
        {
            foreach (var art in resourceArtifacts) AddIfUnique(art);
        }

        foreach (ArtifactData art in Resources.FindObjectsOfTypeAll<ArtifactData>())
        {
            AddIfUnique(art);
        }

        return allList;
    }

    public void PopulateArtifactHUDList()
    {
        GameObject hudCanvas = GameObject.Find("ArtifactHUDCanvas");
        if (hudCanvas == null)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "ArtifactHUDCanvas" && go.scene.isLoaded)
                {
                    hudCanvas = go;
                    break;
                }
            }
        }

        if (hudCanvas == null) return;

        Transform listContainer = hudCanvas.transform.Find("ArtifactList");
        if (listContainer == null)
        {
            listContainer = hudCanvas.transform.Find("Content/ArtifactList") ?? hudCanvas.GetComponentInChildren<UnityEngine.UI.VerticalLayoutGroup>()?.transform;
        }

        if (listContainer == null) return;

        var countText = hudCanvas.transform.Find("ArtifactCountText")?.GetComponent<TMPro.TextMeshProUGUI>();
        if (countText == null) countText = hudCanvas.GetComponentInChildren<TMPro.TextMeshProUGUI>();

        var allArtifacts = GetAllMuseumArtifacts();
        if (countText != null)
        {
            countText.text = $"Jumlah Artefak: {allArtifacts.Count}";
        }

        GameObject itemPrefab = RoomManager.Instance != null ? RoomManager.Instance.artifactListItemPrefab : null;
        if (itemPrefab == null)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "ArtifactListItemPrefab") { itemPrefab = go; break; }
            }
        }

        foreach (Transform child in listContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < allArtifacts.Count; i++)
        {
            ArtifactData art = allArtifacts[i];
            int index = i + 1;

            GameObject item = itemPrefab != null ? Instantiate(itemPrefab, listContainer) : new GameObject($"ArtifactItem_{index}");
            if (itemPrefab == null) item.transform.SetParent(listContainer, false);

            item.name = $"ArtifactItem_{index}";
            item.SetActive(true);

            ConfigureArtifactHUDItem(item, art, index);
        }
    }

    private void ConfigureArtifactHUDItem(GameObject item, ArtifactData artifact, int index)
    {
        if (item == null || artifact == null) return;

        UnityEngine.UI.Image bgImg = item.GetComponent<UnityEngine.UI.Image>();
        if (bgImg == null) bgImg = item.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.25f, 0.28f, 0.22f, 0.75f);

        if (RoomManager.Instance != null && RoomManager.Instance.rowCardMaterial != null)
        {
            bgImg.material = RoomManager.Instance.rowCardMaterial;
        }

        UnityEngine.UI.Button btn = item.GetComponent<UnityEngine.UI.Button>();
        if (btn == null) btn = item.AddComponent<UnityEngine.UI.Button>();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => {
            Debug.Log($"Artifact HUD Item clicked: {artifact.artifactName}");

            GameObject artifactHud = GameObject.Find("ArtifactHUDCanvas");
            if (artifactHud != null) artifactHud.SetActive(false);

            GameObject optionsPanel = GameObject.Find("OptionsPanelCanvas");
            if (optionsPanel != null) optionsPanel.SetActive(false);

            UpdateArtifact(artifact);
        });

        XRButtonSelection selection = item.GetComponent<XRButtonSelection>();
        if (selection == null) selection = item.AddComponent<XRButtonSelection>();
        selection.onClick.RemoveAllListeners();
        selection.onClick.AddListener(() => {
            btn.onClick.Invoke();
        });

        UnityEngine.UI.Image thumbImg = item.transform.Find("Thumb")?.GetComponent<UnityEngine.UI.Image>();
        if (thumbImg != null)
        {
            if (artifact.images != null && artifact.images.Length > 0 && artifact.images[0].sprite != null)
            {
                thumbImg.sprite = artifact.images[0].sprite;
                thumbImg.color = Color.white;
                thumbImg.gameObject.SetActive(true);
            }
            else
            {
                thumbImg.gameObject.SetActive(false);
            }
        }

        var numText = item.transform.Find("NumText")?.GetComponent<TMPro.TextMeshProUGUI>();
        if (numText != null)
        {
            numText.text = index.ToString("D2");
        }

        var nameText = item.transform.Find("NameText")?.GetComponent<TMPro.TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = artifact.artifactName;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 10f;
            nameText.fontSizeMax = 20f;
            nameText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        }

        var statusText = item.transform.Find("StatusText")?.GetComponent<TMPro.TextMeshProUGUI>();
        if (statusText != null)
        {
            bool isScanned = IsArtifactScanned(artifact);
            if (isScanned)
            {
                statusText.text = "Sudah Dikunjung";
                statusText.color = new Color(0.486f, 1f, 0.541f);
            }
            else
            {
                statusText.text = "Belum Dikunjung";
                statusText.color = new Color(0.816f, 0.835f, 0.8f);
            }
        }
    }
}
