using UnityEngine;

public class ArtifactManager : MonoBehaviour
{
    private static ArtifactManager _instance;
    public static ArtifactManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ArtifactManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("ArtifactManager");
                    _instance = go.AddComponent<ArtifactManager>();
                }
            }
            _instance.EnsureInitialized();
            return _instance;
        }
        private set => _instance = value;
    }

    /// <summary>
    /// Checks whether a Pose represents a valid, non-default world-space transform.
    /// In C#, default(Pose) has position == 0 and rotation == (0,0,0,0) with sqrMagnitude == 0.
    /// A valid rotation must have sqrMagnitude close to 1.0f.
    /// </summary>
    public static bool IsValidPose(Pose pose)
    {
        if (pose.position == Vector3.zero) return false;
        float sqrMag = pose.rotation.x * pose.rotation.x +
                       pose.rotation.y * pose.rotation.y +
                       pose.rotation.z * pose.rotation.z +
                       pose.rotation.w * pose.rotation.w;
        return sqrMag > 0.5f && sqrMag < 1.5f;
    }

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
    private bool isInitialized = false;

    public void EnsureInitialized()
    {
        if (isInitialized) return;

        // Fallback for player transform
        if (playerTransform == null)
        {
            playerTransform = WallPlacementHelper.ResolveCameraTransform();
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

            // Hide the canvas template initially
            artifactUiCanvas.SetActive(false);
        }

        isInitialized = true;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureInitialized();
    }

    private void Start()
    {
        EnsureInitialized();

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

        EnsureInitialized();

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
                art.RefreshView(); // re-apply photo/"no images" state so a reused panel isn't stale

                // Bring it directly in front of the player if reopened
                if (IsValidPose(customPose))
                {
                    Transform targetTransform = (panel.name.StartsWith("ArtifactDetailPanelCanvas")) ? panel.transform : art.transform;
                    targetTransform.position = customPose.position;
                    targetTransform.rotation = customPose.rotation;
                }
                else
                {
                    art.PositionInFrontOfUser();
                }

                return panel;
            }
        }

        // Prefer the scene-designed ArtifactDetailPanel (the one in the hierarchy, with the
        // play-audio button etc.). Only fall back to the standalone prefab if it isn't assigned.
        GameObject source = artifactUiCanvas != null ? artifactUiCanvas : artifactPanelPrefab;

        // Resolve player transform
        if (playerTransform == null)
        {
            playerTransform = WallPlacementHelper.ResolveCameraTransform();
        }

        // Calculate spawn position in world space
        Vector3 spawnPos;
        Quaternion spawnRot;
        bool isWallMounted = false;

        if (IsValidPose(customPose))
        {
            spawnPos = customPose.position;
            spawnRot = customPose.rotation;
        }
        else
        {
            Pose placementPose = WallPlacementHelper.CalculatePlacementPose(
                playerTransform,
                activePanelInstances.Count,
                0.60f,
                out isWallMounted,
                preferFloating: true // Spawns directly in front of the player at eye level (~0.90m)
            );
            spawnPos = placementPose.position;
            spawnRot = placementPose.rotation;
        }

        if (source == null)
        {
            Debug.LogError("ArtifactManager: No artifactUiCanvas (scene ArtifactDetailPanel) or artifactPanelPrefab assigned to spawn detail panel!");
            return null;
        }

        GameObject newPanelInstance;
        if (source.GetComponent<Canvas>() != null)
        {
            // Source is already a self-contained canvas (the fallback prefab): clone it directly.
            newPanelInstance = Instantiate(source, spawnPos, spawnRot);
            newPanelInstance.SetActive(true);
            Canvas c = newPanelInstance.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            if (c.worldCamera == null) c.worldCamera = WallPlacementHelper.ResolveCamera(Camera.main);
            if (newPanelInstance.transform.localScale.x <= 0.00001f || newPanelInstance.transform.localScale.x > 0.01f)
            {
                newPanelInstance.transform.localScale = Vector3.one * 0.0011f;
            }
        }
        else
        {
            // Source is a panel that lives under another canvas (the scene ArtifactDetailPanel).
            // Wrap a clone of it in its own world-space canvas so it renders standalone at the QR.
            newPanelInstance = BuildWorldSpacePanel(source, spawnPos, spawnRot);
        }
        newPanelInstance.name = $"ArtifactDetailPanel_{artifact.artifactName}";

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

        ArtifactPanelDragger dragger = newPanelInstance.GetComponentInChildren<ArtifactPanelDragger>(true);
        if (dragger != null)
        {
            dragger.SetSnappedToWall(isWallMounted);
        }

        activePanelInstances.Add(newPanelInstance);
        selectedArtifact = artifact;
        lastSelectedArtifact = artifact;

        Debug.Log($"ArtifactManager: Spawned detail panel for '{artifact.artifactName}' in world space (Wall: {isWallMounted}, Pos: {spawnPos}). Total open panels: {activePanelInstances.Count}");
        return newPanelInstance;
    }

    /// <summary>
    /// Wraps a clone of a UI panel (that normally lives under another canvas) in its own
    /// world-space Canvas so it renders and is clickable on its own at the given pose. Size and
    /// scale are taken from the source's current layout so it looks the same as in the editor.
    /// </summary>
    private GameObject BuildWorldSpacePanel(GameObject source, Vector3 pos, Quaternion rot)
    {
        RectTransform srcRT = source.GetComponent<RectTransform>();
        Vector2 size = srcRT != null ? srcRT.sizeDelta : new Vector2(640f, 480f);
        float worldScale = source.transform.lossyScale.x;
        if (worldScale <= 0.00001f || worldScale > 0.01f) worldScale = 0.0011f;

        GameObject wrapper = new GameObject("ArtifactDetailPanelCanvas");
        Canvas canvas = wrapper.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = WallPlacementHelper.ResolveCamera(Camera.main);
        wrapper.AddComponent<UnityEngine.UI.CanvasScaler>();
        wrapper.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        // XR ray/poke UI raycaster so the panel's buttons are clickable with the hands.
        if (wrapper.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() == null)
            wrapper.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        RectTransform wrt = wrapper.GetComponent<RectTransform>();
        wrt.sizeDelta = size;
        wrapper.transform.position = pos;
        wrapper.transform.rotation = rot;
        wrapper.transform.localScale = Vector3.one * worldScale;

        // Clone the designed panel under the wrapper and stretch it to fill, so its child layout
        // (buttons, play-audio, etc.) reproduces exactly what you see in the hierarchy.
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

    private Pose CalculateDefaultPose()
    {
        Transform referenceTransform = WallPlacementHelper.ResolveCameraTransform(playerTransform);
        return WallPlacementHelper.CalculatePlacementPose(referenceTransform, activePanelInstances.Count, 0.60f, out _, preferFloating: true);
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
                RoomManager.Instance.SetScanStatus($"Artifak Diimbas: {artifactMatch.artifactName}", new Color(0.1f, 0.75f, 0.2f));
            }
            SpawnArtifactDetailPanel(artifactMatch, pose);
        }
    }

    private ArtifactData FindArtifactInProject(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        string cleanId = id.Trim().ToLower();
        ArtifactData match = null;

        bool Matches(ArtifactData a)
        {
            if (a == null) return false;
            if (!string.IsNullOrEmpty(a.artifactId))
            {
                string artId = a.artifactId.Trim().ToLower();
                if (artId == cleanId || cleanId.Contains(artId) || artId.Contains(cleanId)) return true;
            }
            if (!string.IsNullOrEmpty(a.artifactName))
            {
                string artName = a.artifactName.Trim().ToLower();
                if (artName == cleanId || cleanId.Contains(artName) || artName.Contains(cleanId)) return true;
            }
            return false;
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

        // Reset QR scanner state so the QR code can be scanned again in the future
        if (QRCodeScanner.Instance != null)
        {
            QRCodeScanner.Instance.ClearActivePayload();
        }
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

        // Reset QR scanner state so QR codes can be scanned again in the future
        if (QRCodeScanner.Instance != null)
        {
            QRCodeScanner.Instance.ClearActivePayload();
        }
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
            countText.text = $"Jumlah Artifak: {allArtifacts.Count}";
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
        if (btn.targetGraphic != null) btn.targetGraphic.raycastTarget = true;

        XRButtonSelection selection = item.GetComponent<XRButtonSelection>();
        if (selection == null) selection = item.AddComponent<XRButtonSelection>();
        selection.onClick.RemoveAllListeners();
        selection.onClick.AddListener(() => {
            btn.onClick.Invoke();
        });

        if (item.GetComponent<UIButtonAudio>() == null)
        {
            item.AddComponent<UIButtonAudio>();
        }

        UnityEngine.UI.Image thumbImg = item.transform.Find("Thumb")?.GetComponent<UnityEngine.UI.Image>();
        if (thumbImg != null)
        {
            if (artifact.images != null && artifact.images.Length > 0 && artifact.images[0].sprite != null)
            {
                thumbImg.sprite = artifact.images[0].sprite;
                thumbImg.preserveAspect = true;
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
                statusText.text = "Sudah Dikunjungi";
                statusText.color = new Color(0.486f, 1f, 0.541f);
            }
            else
            {
                statusText.text = "Belum Dikunjungi";
                statusText.color = new Color(0.816f, 0.835f, 0.8f);
            }
        }
    }
}
