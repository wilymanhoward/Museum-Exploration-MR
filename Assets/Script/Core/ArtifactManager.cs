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

    [Tooltip("The script controller (ArtifactPanel) on the panel UI.")]
    public ArtifactPanel artifactInteraction;

    [Header("UI Prefab Fallback (Optional)")]
    [Tooltip("Prefab for the floating detail panel spawned when an artifact QR is scanned (fallback only).")]
    public GameObject artifactPanelPrefab;

    [Header("Selected Artifact Details")]
    [Tooltip("The currently selected artifact.")]
    public ArtifactData selectedArtifact;

    private ArtifactData lastSelectedArtifact;
    private GameObject activePanelInstance;

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
                artifactInteraction = artifactUiCanvas.GetComponentInChildren<ArtifactPanel>(true);
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

    private readonly System.Collections.Generic.HashSet<string> scannedArtifactIds = new System.Collections.Generic.HashSet<string>();

    /// <summary>
    /// Updates the selected artifact, syncs the UI panel to display it, and repositions the panel to the given pose.
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

        if (!string.IsNullOrEmpty(artifact.artifactId))
        {
            scannedArtifactIds.Add(artifact.artifactId.Trim().ToLower());
        }

        // If a panel is already open, update its details and position directly
        if (activePanelInstance != null)
        {
            ArtifactPanel existingInteraction = activePanelInstance.GetComponentInChildren<ArtifactPanel>();
            if (existingInteraction != null)
            {
                existingInteraction.UpdateDetails(artifact);
                return;
            }
        }

        // If no panel is open, trigger a new scan setup
        TriggerArtifactScan(artifact, pose);
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
        // Ignore exhibit QR scans until player taps MULAI on the Main Menu
        if (!MainMenuManager.IsExplorationStarted)
        {
            Debug.Log("ArtifactManager: Exploration has not started yet. Ignoring QR scan.");
            return;
        }

        // Prevent duplicate scanning if this exact artifact detail panel is already open
        if (activePanelInstance != null)
        {
            ArtifactPanel existingInteraction = activePanelInstance.GetComponentInChildren<ArtifactPanel>();
            if (existingInteraction != null && existingInteraction.artifactData != null && existingInteraction.artifactData.artifactId == payload)
            {
                return; // Already showing this exact panel, ignore repeat scan
            }
        }

        // Search for the artifact in the rooms, and use editor fallback if needed
        ArtifactData artifactMatch = FindArtifactInProject(payload);

        if (artifactMatch != null)
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.SetScanStatus($"Scanned Exhibit: {artifactMatch.artifactName}", new Color(0.1f, 0.75f, 0.2f));
            }
            UpdateArtifact(artifactMatch, pose);
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
        if (activePanelInstance != null)
        {
            ArtifactPanel interaction = activePanelInstance.GetComponentInChildren<ArtifactPanel>();
            if (interaction != null && interaction.artifactData != null && interaction.artifactData.artifactId == payload)
            {
                CloseActivePanel();
            }
        }
    }

    private void TriggerArtifactScan(ArtifactData artifact, Pose pose)
    {
        Debug.Log($"Scanned Artifact QR: {artifact.artifactName}");

        // Update selected artifact tracking
        selectedArtifact = artifact;
        lastSelectedArtifact = artifact;

        // Ensure we have a valid player transform reference (in case it was null at Start())
        if (playerTransform == null)
        {
            if (Camera.main != null)
            {
                playerTransform = Camera.main.transform;
            }
            else
            {
                Camera cam = FindObjectOfType<Camera>();
                if (cam != null)
                {
                    playerTransform = cam.transform;
                }
            }
        }

        // If we are using the persistent canvas
        if (artifactUiCanvas != null && artifactInteraction != null)
        {
            // Position the parent ArtifactUICanvas flat against the wall relative to the QR code
            // (Note: the local Y coordinate is adjusted to the player's eye level inside Setup())
            artifactUiCanvas.SetActive(true);
            artifactInteraction.Setup(artifact, playerTransform, pose, () => {
                // Callback if the canvas is closed
                CloseActivePanel();
            });
            activePanelInstance = artifactUiCanvas;
        }
        else
        {
            // Fallback to prefab spawning if the persistent canvas is not in the scene
            if (activePanelInstance != null)
            {
                ArtifactPanel existingInteraction = activePanelInstance.GetComponentInChildren<ArtifactPanel>();
                if (existingInteraction != null)
                {
                    if (existingInteraction.artifactData != null && existingInteraction.artifactData.artifactId == artifact.artifactId)
                    {
                        return;
                    }
                    existingInteraction.Setup(artifact, playerTransform, pose, () => {
                        CloseActivePanel();
                    });
                    return;
                }
            }

            if (artifactPanelPrefab != null)
            {
                Vector3 spawnPos = pose.position;
                if (playerTransform != null)
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up).normalized;
                    if (fwd == Vector3.zero) fwd = playerTransform.forward;
                    spawnPos = playerTransform.position + fwd * 1.0f;
                }

                GameObject panelInstance = Instantiate(artifactPanelPrefab, spawnPos, pose.rotation);
                Canvas canvas = panelInstance.GetComponent<Canvas>();
                if (canvas != null && canvas.worldCamera == null)
                {
                    canvas.worldCamera = Camera.main;
                }

                ArtifactPanel interaction = panelInstance.GetComponentInChildren<ArtifactPanel>();
                if (interaction != null)
                {
                    interaction.Setup(artifact, playerTransform, pose, () => {
                        CloseActivePanel();
                    });
                    activePanelInstance = panelInstance;
                }
            }
        }
    }

    /// <summary>
    /// Closes the active artifact detail panel if one exists.
    /// </summary>
    public void CloseActivePanel()
    {
        if (activePanelInstance != null)
        {
            GameObject tempInstance = activePanelInstance;
            activePanelInstance = null; // Clear first to prevent recursion loops

            if (tempInstance == artifactUiCanvas)
            {
                // Hide persistent canvas and trigger its close animation/cleanup
                artifactUiCanvas.SetActive(false);
                if (artifactInteraction != null)
                {
                    artifactInteraction.StartClose();
                }
            }
            else
            {
                // Instantiate/Destroy fallback - trigger cleanup and destroy the prefab instance
                ArtifactPanel interaction = tempInstance.GetComponentInChildren<ArtifactPanel>();
                if (interaction != null)
                {
                    interaction.StartClose();
                }
                Destroy(tempInstance);
            }
        }

        // Clear selected artifact tracking
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
