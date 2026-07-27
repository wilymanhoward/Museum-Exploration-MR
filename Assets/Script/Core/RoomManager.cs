using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("Museum Configurations")]
    [Tooltip("List of all rooms configured in the museum.")]
    [HideInInspector]
    public List<RoomData> rooms = new List<RoomData>();

    [Tooltip("The room the player starts in (optional).")]
    public RoomData startingRoom;

    [Header("UI Canvas HUD References")]
    public GameObject roomHudContainer;
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI roomSubtitleText;
    public TextMeshProUGUI roomArtifactCountText;
    public Transform artifactListContainer;
    [Tooltip("Prefab for a line item in the artifact checklist UI.")]
    public GameObject artifactListItemPrefab;
    [Tooltip("Material applied to line item cards in the artifact checklist UI.")]
    public Material rowCardMaterial;
    [Tooltip("Text field to show scan feedback (e.g. Scanned Exhibit: Mona Lisa).")]
    public TextMeshProUGUI scanStatusText;

    [Header("Wayfinding & Prefabs")]
    public WayfindingSystem wayfindingSystem;
    public GameObject findButton;

    // Track active state
    private RoomData currentRoom;
    private ArtifactData selectedChecklistArtifact;
    private Dictionary<string, bool> scannedArtifacts = new Dictionary<string, bool>();
    private Dictionary<string, GameObject> hudListItems = new Dictionary<string, GameObject>();
    private float statusTextTimer = 0f;

    public RoomData CurrentRoom => currentRoom;

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
        // 1. Try to find RoomHUDCanvas / RoomCanvas in the scene (active or inactive)
        if (roomHudContainer == null)
        {
            GameObject[] allGo = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject go in allGo)
            {
                if ((go.name == "RoomHUDCanvas" || go.name == "RoomCanvas") && go.scene.isLoaded)
                {
                    roomHudContainer = go;
                    break;
                }
            }
            if (roomHudContainer != null)
            {
                Debug.Log($"RoomManager: Automatically located '{roomHudContainer.name}' (even if inactive) in the scene.");
            }
        }

        // Apply rounded corner material to roomHudContainer background panel if missing
        if (roomHudContainer != null)
        {
            UnityEngine.UI.Image containerBg = roomHudContainer.GetComponent<UnityEngine.UI.Image>();
            if (containerBg == null) containerBg = roomHudContainer.transform.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
            if (containerBg == null) containerBg = roomHudContainer.GetComponentInChildren<UnityEngine.UI.Image>();
            if (containerBg != null && (containerBg.material == null || containerBg.material.name == "Default UI"))
            {
                foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m != null && (m.name == "Mat_RoomHUD" || m.name == "Mat_OptionsCardBackground"))
                    {
                        containerBg.material = m;
                        break;
                    }
                }
            }
        }

        // 2. Try to find child references on the RoomHUDCanvas / RoomCanvas
        if (roomHudContainer != null)
        {
            Transform titlePanel = roomHudContainer.transform.Find("TitlePanel");
            if (titlePanel != null)
            {
                TextMeshProUGUI[] texts = titlePanel.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0 && roomNameText == null)
                {
                    roomNameText = texts[0];
                    Debug.Log($"RoomManager: Automatically located roomNameText '{roomNameText.name}' in TitlePanel.");
                }
                if (texts.Length > 1 && roomSubtitleText == null)
                {
                    roomSubtitleText = texts[1];
                    Debug.Log($"RoomManager: Automatically located roomSubtitleText '{roomSubtitleText.name}' in TitlePanel.");
                }
            }

            if (roomNameText == null)
            {
                roomNameText = roomHudContainer.transform.Find("RoomTitleText")?.GetComponent<TextMeshProUGUI>();
                if (roomNameText != null) Debug.Log("RoomManager: Automatically located 'RoomTitleText' component.");
            }

            if (artifactListContainer == null)
            {
                artifactListContainer = roomHudContainer.transform.Find("ArtifactList");
                if (artifactListContainer == null)
                {
                    artifactListContainer = roomHudContainer.transform.Find("List");
                }
                if (artifactListContainer != null) Debug.Log($"RoomManager: Automatically located list container '{artifactListContainer.name}'.");
            }

            if (scanStatusText == null)
            {
                scanStatusText = roomHudContainer.transform.Find("ScanStatusText")?.GetComponent<TextMeshProUGUI>();
                if (scanStatusText != null) Debug.Log("RoomManager: Automatically located 'ScanStatusText' component.");
            }

            if (roomArtifactCountText == null)
            {
                roomArtifactCountText = roomHudContainer.transform.Find("ArtifactCountText")?.GetComponent<TextMeshProUGUI>();
                if (roomArtifactCountText == null)
                {
                    roomArtifactCountText = roomHudContainer.transform.Find("CountText")?.GetComponent<TextMeshProUGUI>();
                }
                if (roomArtifactCountText == null)
                {
                    foreach (var text in roomHudContainer.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (text.gameObject.name.ToLower().Contains("count") || text.text.ToLower().Contains("jumlah"))
                        {
                            roomArtifactCountText = text;
                            break;
                        }
                    }
                }
                if (roomArtifactCountText != null) Debug.Log($"RoomManager: Automatically located roomArtifactCountText '{roomArtifactCountText.name}'.");
            }
        }

        // 3. Try to locate WayfindingSystem in the scene
        if (wayfindingSystem == null)
        {
            wayfindingSystem = FindObjectOfType<WayfindingSystem>();
            if (wayfindingSystem != null)
            {
                Debug.Log("RoomManager: Automatically located 'WayfindingSystem' in the scene.");
            }
        }

        // 4. Runtime & Editor auto-healing lookup for missing assets
        if (rowCardMaterial == null)
        {
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m != null && m.name == "Mat_OptionsRowCard")
                {
                    rowCardMaterial = m;
                    break;
                }
            }
        }

        if (artifactListItemPrefab == null)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.name == "ArtifactListItemPrefab")
                {
                    artifactListItemPrefab = go;
                    break;
                }
            }
        }

#if UNITY_EDITOR
        if (rooms == null || rooms.Count == 0)
        {
            rooms = new List<RoomData>();
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:RoomData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                RoomData r = UnityEditor.AssetDatabase.LoadAssetAtPath<RoomData>(path);
                if (r != null && !rooms.Contains(r))
                {
                    rooms.Add(r);
                }
            }
            Debug.Log($"RoomManager: Automatically loaded {rooms.Count} RoomData assets in Editor.");
        }

        if (startingRoom == null && rooms.Count > 0)
        {
            startingRoom = rooms.Find(r => r.roomId == "room_textile");
            if (startingRoom == null) startingRoom = rooms[0];
            Debug.Log($"RoomManager: Automatically assigned starting room to '{startingRoom.roomName}' in Editor.");
        }

        if (artifactListItemPrefab == null)
        {
            artifactListItemPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ArtifactListItemPrefab.prefab");
            if (artifactListItemPrefab != null)
            {
                Debug.Log("RoomManager: Automatically loaded 'ArtifactListItemPrefab' in Editor.");
            }
        }
#endif

        // 5. Validate and print helpful warnings if references are still missing
        ValidateReferences();

        // Hide exploration HUD at start (it will be enabled via MainMenuManager)
        if (roomHudContainer != null)
        {
            roomHudContainer.SetActive(false);
        }

        if (scanStatusText != null)
        {
            scanStatusText.text = "";
        }

        // Register for QR Scanner events
        QRCodeScanner.OnQRCodeScanned += HandleQRCodeScanned;

        // Wire up the Find Button ("Temukan" button) if found
        if (findButton != null)
        {
            UnityEngine.UI.Button findBtnComponent = findButton.GetComponent<UnityEngine.UI.Button>();
            if (findBtnComponent == null) findBtnComponent = findButton.GetComponentInChildren<UnityEngine.UI.Button>();
            if (findBtnComponent != null)
            {
                findBtnComponent.onClick.RemoveAllListeners();
                findBtnComponent.onClick.AddListener(OnFindButtonClicked);
            }
        }

        // Initialize starting room if set
        if (startingRoom != null)
        {
            ChangeRoom(startingRoom);
        }
    }

    private void ValidateReferences()
    {
        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogWarning("RoomManager: The 'rooms' list is empty. Please run the menu option 'Tools > Museum MR > Setup Custom Museum Rooms' to populate the rooms database.");
        }
        if (startingRoom == null)
        {
            Debug.LogWarning("RoomManager: 'startingRoom' is not assigned. No room will load automatically on start.");
        }
        if (roomHudContainer == null)
        {
            Debug.LogWarning("RoomManager: 'roomHudContainer' (RoomHUDCanvas) is not assigned or found in the scene.");
        }
        if (roomNameText == null)
        {
            Debug.LogWarning("RoomManager: 'roomNameText' (RoomTitleText component) is not assigned or found.");
        }
        if (artifactListContainer == null)
        {
            Debug.LogWarning("RoomManager: 'artifactListContainer' (ArtifactList transform) is not assigned or found.");
        }
        if (artifactListItemPrefab == null)
        {
            Debug.LogWarning("RoomManager: 'artifactListItemPrefab' is not assigned. Checklist items will fail to spawn.");
        }
    }

    private void OnDestroy()
    {
        QRCodeScanner.OnQRCodeScanned -= HandleQRCodeScanned;
    }

    private void Update()
    {
        // Clear scan status text after timer expires
        if (statusTextTimer > 0f)
        {
            statusTextTimer -= Time.deltaTime;
            if (statusTextTimer <= 0f && scanStatusText != null)
            {
                scanStatusText.text = "";
            }
        }
    }

    /// <summary>
    /// Updates the scan status feedback message on the HUD UI.
    /// </summary>
    public void SetScanStatus(string message, Color color)
    {
        if (scanStatusText != null)
        {
            scanStatusText.text = message;
            scanStatusText.color = color;
            statusTextTimer = 4f; // Display feedback for 4 seconds
        }
        Debug.Log($"Scan Status HUD Update: {message}");
    }

    /// <summary>
    /// Starts the exploration mode (called by MainMenuManager)
    /// </summary>
    public void StartExploration()
    {
        // Keep roomHudContainer hidden at launch; it only appears when player taps the Ruang button in the Options panel
        if (roomHudContainer != null)
        {
            roomHudContainer.SetActive(false);
        }

        if (currentRoom == null && rooms.Count > 0)
        {
            ChangeRoom(rooms[0]);
        }
        else if (currentRoom != null)
        {
            // Update wayfinding for current room
            UpdateWayfinding();
        }
    }

    /// <summary>
    /// Changes the player's active room, updating wayfinding and UI HUD list.
    /// </summary>
    public void ChangeRoom(RoomData newRoom)
    {
        if (newRoom == null || currentRoom == newRoom) return;

        currentRoom = newRoom;
        Debug.Log($"Entered Room: {currentRoom.roomName}");

        // Close active artifact panel when changing rooms
        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.CloseActivePanel();
        }

        // Do NOT force the room canvas visible here. Its content (room name, checklist) is
        // rebuilt below regardless, but visibility is owned solely by the 'Ruang' button in the
        // Options panel (WristWatchMenu.OnClickRuang). Auto-showing it on room change/startup is
        // what previously made the artifact list pop up the moment exploration began. If the
        // panel is already open it stays open; if closed it stays closed until the player opens it.

        // Update UI Text
        if (roomNameText != null)
        {
            roomNameText.text = currentRoom.roomName;
        }

        // Update Subtitle Text
        if (roomSubtitleText != null)
        {
            roomSubtitleText.text = currentRoom.roomSubtitle;
        }

        // Update Artifact Count Text
        if (roomArtifactCountText != null)
        {
            int count = (currentRoom.artifacts != null) ? currentRoom.artifacts.Count : 0;
            roomArtifactCountText.text = $"Jumlah Artefak: {count}";
        }

        // Repopulate HUD checklist
        RebuildArtifactChecklist();

        // Update Floor Navigation Lines
        UpdateWayfinding();
    }

    private void RebuildArtifactChecklist()
    {
        Debug.Log($"RebuildArtifactChecklist called. currentRoom: {(currentRoom != null ? currentRoom.roomName : "null")}");

        // Auto-resolve artifactListContainer if null
        if (artifactListContainer == null)
        {
            GameObject canvasObj = roomHudContainer != null ? roomHudContainer : GameObject.Find("RoomHUDCanvas") ?? GameObject.Find("ExplorationCanvas");
            if (canvasObj != null)
            {
                Transform found = canvasObj.transform.Find("RoomPanel/ArtifactList")
                               ?? canvasObj.transform.Find("ArtifactList")
                               ?? canvasObj.transform.Find("List");
                if (found == null)
                {
                    foreach (Transform t in canvasObj.GetComponentsInChildren<Transform>(true))
                    {
                        if ((t.name == "ArtifactList" || t.name == "List" || t.name == "Content") && t.name != "RoomList")
                        {
                            found = t;
                            break;
                        }
                    }
                }
                if (found != null) artifactListContainer = found;
            }
        }

        if (artifactListContainer == null)
        {
            Debug.LogError("RebuildArtifactChecklist aborted: artifactListContainer is null!");
            return;
        }

        // Hide findButton initially, show scanStatusText
        if (findButton != null)
        {
            findButton.SetActive(false);
        }
        if (scanStatusText != null)
        {
            scanStatusText.gameObject.SetActive(true);
        }
        selectedChecklistArtifact = null;

        // Clear existing items
        int clearedCount = 0;
        foreach (Transform child in artifactListContainer)
        {
            Destroy(child.gameObject);
            clearedCount++;
        }
        Debug.Log($"Cleared {clearedCount} existing checklist items.");
        hudListItems.Clear();

        if (currentRoom == null)
        {
            Debug.LogWarning("currentRoom is null, cannot build checklist!");
            return;
        }

        if (currentRoom.artifacts == null || currentRoom.artifacts.Count == 0)
        {
            Debug.LogWarning($"No artifacts found in current room '{currentRoom.roomName}'!");
            return;
        }

        Debug.Log($"Spawning checklist for {currentRoom.artifacts.Count} artifacts...");
        // Instantiate item for each artifact in the room
        for (int i = 0; i < currentRoom.artifacts.Count; i++)
        {
            ArtifactData artifact = currentRoom.artifacts[i];
            if (artifact == null)
            {
                Debug.LogWarning("Found null ArtifactData inside currentRoom.artifacts!");
                continue;
            }

            GameObject item = null;
            if (artifactListItemPrefab != null)
            {
                item = Instantiate(artifactListItemPrefab, artifactListContainer);
            }
            else
            {
                item = new GameObject($"ArtifactCard_{i + 1}");
                item.transform.SetParent(artifactListContainer, false);
            }

            item.SetActive(true);
            item.transform.localScale = Vector3.one;
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;

            // Check if already scanned in the past
            bool isScanned = (scannedArtifacts != null && artifact.artifactId != null && scannedArtifacts.ContainsKey(artifact.artifactId) && scannedArtifacts[artifact.artifactId]);
            UpdateListItemVisual(item, artifact, isScanned);

            if (artifact.artifactId != null)
            {
                hudListItems[artifact.artifactId] = item;
            }

            // Hook up the button click event
            UnityEngine.UI.Button btn = item.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = item.AddComponent<UnityEngine.UI.Button>();
            
            ArtifactData currentArtifact = artifact;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => {
                SelectArtifactFromList(currentArtifact);
            });

            XRButtonSelection selection = item.GetComponent<XRButtonSelection>();
            if (selection == null) selection = item.AddComponent<XRButtonSelection>();
            selection.onClick.RemoveAllListeners();
            selection.onClick.AddListener(() => {
                btn.onClick.Invoke();
            });

            Debug.Log($"Successfully spawned checklist item for artifact '{artifact.artifactName}' (Scanned: {isScanned})");
        }
    }

    private void UpdateListItemVisual(GameObject item, ArtifactData artifact, bool isScanned)
    {
        if (item == null || artifact == null) return;

        TMPro.TMP_FontAsset fontAsset = GetDefaultTMPFont();
        Color paleYellow = new Color(0.90f, 0.93f, 0.63f, 1.0f); // #E5EE9C
        Color visitedGreen = new Color(0.55f, 0.89f, 0.63f, 1.0f); // #8BE4A0
        Color unvisitedGray = new Color(0.88f, 0.88f, 0.88f, 1.0f); // #E0E0E0

        // 1. Card Background Image
        UnityEngine.UI.Image bgImage = item.GetComponent<UnityEngine.UI.Image>();
        if (bgImage == null) bgImage = item.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.25f, 0.28f, 0.22f, 0.75f);

        if (rowCardMaterial == null)
        {
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m != null && m.name == "Mat_OptionsRowCard")
                {
                    rowCardMaterial = m;
                    break;
                }
            }
        }
        if (rowCardMaterial != null) bgImage.material = rowCardMaterial;

        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x > 0 ? itemRect.sizeDelta.x : 300f, 65f);
        }

        // 2. Thumbnail Image (Thumb)
        Transform thumbT = item.transform.Find("Thumb");
        UnityEngine.UI.Image thumbImg = null;
        if (thumbT == null)
        {
            GameObject thumbObj = new GameObject("Thumb");
            thumbObj.transform.SetParent(item.transform, false);
            thumbT = thumbObj.transform;
            thumbImg = thumbObj.AddComponent<UnityEngine.UI.Image>();
            RectTransform tRect = thumbObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.03f, 0.10f);
            tRect.anchorMax = new Vector2(0.20f, 0.90f);
            tRect.sizeDelta = Vector2.zero;
        }
        else
        {
            thumbImg = thumbT.GetComponent<UnityEngine.UI.Image>();
        }

        if (thumbImg != null)
        {
            Sprite artSprite = (artifact.images != null && artifact.images.Length > 0) 
                ? artifact.images[0].sprite 
                : null;

            if (artSprite == null && !string.IsNullOrEmpty(artifact.artifactId))
            {
                foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s != null && s.name.ToLower().Contains(artifact.artifactId.ToLower()))
                    {
                        artSprite = s;
                        break;
                    }
                }
            }

            if (artSprite != null)
            {
                thumbImg.sprite = artSprite;
                thumbImg.color = Color.white;
                thumbImg.gameObject.SetActive(true);
            }
            else
            {
                thumbImg.gameObject.SetActive(false);
            }
        }

        // 3. Resolve & Update Text Components (NumText, NameText, StatusText)
        TextMeshProUGUI numTextComp = null;
        TextMeshProUGUI nameTextComp = null;
        TextMeshProUGUI statusTextComp = null;

        // Deactivate legacy UI text
        foreach (UnityEngine.UI.Text legacyTxt in item.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            legacyTxt.gameObject.SetActive(false);
        }

        // Search existing TMP texts on prefab to avoid duplicate/overlapping text objects like template "MONA LISA"
        TextMeshProUGUI[] existingTMPs = item.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI tmp in existingTMPs)
        {
            string n = tmp.name.ToLower();
            if (n == "numtext" || n == "num" || n == "index")
            {
                numTextComp = tmp;
            }
            else if (n == "nametext" || n == "title" || n == "name" || n == "text" || n.Contains("mona"))
            {
                if (nameTextComp == null)
                {
                    nameTextComp = tmp;
                }
                else
                {
                    tmp.gameObject.SetActive(false);
                }
            }
            else if (n == "statustext" || n == "status")
            {
                statusTextComp = tmp;
            }
            else
            {
                // Disable any unhandled template text to prevent overlap
                tmp.gameObject.SetActive(false);
            }
        }

        // Build NumText if missing
        if (numTextComp == null)
        {
            GameObject numObj = new GameObject("NumText");
            numObj.transform.SetParent(item.transform, false);
            numTextComp = numObj.AddComponent<TextMeshProUGUI>();
            RectTransform nRect = numObj.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0.22f, 0.48f);
            nRect.anchorMax = new Vector2(0.30f, 0.92f);
            nRect.sizeDelta = Vector2.zero;
        }
        numTextComp.gameObject.SetActive(true);
        if (fontAsset != null) numTextComp.font = fontAsset;
        int index = (currentRoom != null && currentRoom.artifacts != null) ? currentRoom.artifacts.IndexOf(artifact) : 0;
        numTextComp.text = (index >= 0 ? index + 1 : 1).ToString("00");
        numTextComp.fontSize = 18;
        numTextComp.fontStyle = FontStyles.Bold;
        numTextComp.color = paleYellow;
        numTextComp.alignment = TextAlignmentOptions.Left;

        // Build NameText if missing
        if (nameTextComp == null)
        {
            GameObject nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(item.transform, false);
            nameTextComp = nameObj.AddComponent<TextMeshProUGUI>();
            RectTransform nRect = nameObj.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0.31f, 0.48f);
            nRect.anchorMax = new Vector2(0.96f, 0.92f);
            nRect.sizeDelta = Vector2.zero;
        }
        nameTextComp.gameObject.SetActive(true);
        if (fontAsset != null) nameTextComp.font = fontAsset;
        nameTextComp.text = artifact.artifactName;
        nameTextComp.enableAutoSizing = true;
        nameTextComp.fontSizeMin = 10f;
        nameTextComp.fontSizeMax = 20f;
        nameTextComp.fontStyle = FontStyles.Bold;
        nameTextComp.color = paleYellow;
        nameTextComp.alignment = TextAlignmentOptions.Left;
        nameTextComp.overflowMode = TextOverflowModes.Ellipsis;

        // Build StatusText if missing
        if (statusTextComp == null)
        {
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(item.transform, false);
            statusTextComp = statusObj.AddComponent<TextMeshProUGUI>();
            RectTransform sRect = statusObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.31f, 0.10f);
            sRect.anchorMax = new Vector2(0.96f, 0.48f);
            sRect.sizeDelta = Vector2.zero;
        }
        statusTextComp.gameObject.SetActive(true);
        if (fontAsset != null) statusTextComp.font = fontAsset;
        statusTextComp.text = isScanned ? "Sudah Dikunjungi" : "Belum Dikunjungi";
        statusTextComp.fontSize = 15;
        statusTextComp.fontStyle = FontStyles.Normal;
        statusTextComp.color = isScanned ? visitedGreen : unvisitedGray;
        statusTextComp.alignment = TextAlignmentOptions.Left;
    }

    public void SelectArtifactFromList(ArtifactData artifact)
    {
        selectedChecklistArtifact = artifact;
        Debug.Log($"Selected artifact from checklist: {artifact.artifactName}");

        // Hide the scanStatusText and show the findButton
        if (scanStatusText != null)
        {
            scanStatusText.gameObject.SetActive(false);
        }
        if (findButton != null)
        {
            findButton.SetActive(true);
        }
    }

    private void OnFindButtonClicked()
    {
        if (selectedChecklistArtifact == null)
        {
            Debug.LogWarning("No checklist artifact selected to find!");
            return;
        }

        Debug.Log($"Find Button pressed! Showing waypoint to: {selectedChecklistArtifact.artifactName}");
        
        // Show scan status text again to provide search feedback
        if (scanStatusText != null)
        {
            scanStatusText.gameObject.SetActive(true);
            SetScanStatus($"Mencari: {selectedChecklistArtifact.artifactName}", new Color(0.1f, 0.75f, 0.2f));
        }

        // Trigger waypoint rendering (simulated)
        if (wayfindingSystem != null)
        {
            Transform camTransform = Camera.main != null ? Camera.main.transform : transform;
            Vector3 startPos = camTransform.position;
            Vector3 endPos = startPos + camTransform.forward * 2.0f;
            wayfindingSystem.SetPath(new Vector3[] { startPos, endPos });
        }
    }

    private void UpdateWayfinding()
    {
        if (wayfindingSystem != null && currentRoom != null)
        {
            wayfindingSystem.SetPath(currentRoom.waypoints);
        }
    }

    /// <summary>
    /// Callback from QR Scanner when a QR code payload is detected.
    /// </summary>
    private void HandleQRCodeScanned(string payload, Pose pose)
    {
        // Ignore room transition QR scans until player taps MULAI on the Main Menu
        if (!MainMenu.IsExplorationStarted)
        {
            Debug.Log("RoomManager: Exploration has not started yet. Ignoring QR scan.");
            return;
        }

        Debug.Log($"RoomManager received QR scan payload: '{payload}'");

        string cleanPayload = payload.Trim().ToLower();
        bool MatchesRoom(RoomData r)
        {
            if (r == null || string.IsNullOrEmpty(r.roomId)) return false;
            string rId = r.roomId.Trim().ToLower();
            return rId == cleanPayload || cleanPayload.Contains(rId) || rId.Contains(cleanPayload);
        }

        // Search for the room in our loaded list, Resources folder, memory fallback, or editor fallback
        RoomData roomMatch = (rooms != null) ? rooms.Find(MatchesRoom) : null;

        if (roomMatch == null)
        {
            RoomData[] resourceRooms = Resources.LoadAll<RoomData>("MuseumData/Rooms");
            if (resourceRooms != null)
            {
                foreach (RoomData r in resourceRooms)
                {
                    if (MatchesRoom(r))
                    {
                        roomMatch = r;
                        if (rooms != null && !rooms.Contains(r)) rooms.Add(r);
                        break;
                    }
                }
            }

            if (roomMatch == null)
            {
                foreach (RoomData r in Resources.FindObjectsOfTypeAll<RoomData>())
                {
                    if (MatchesRoom(r))
                    {
                        roomMatch = r;
                        if (rooms != null && !rooms.Contains(r)) rooms.Add(r);
                        break;
                    }
                }
            }
        }

#if UNITY_EDITOR
        if (roomMatch == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:RoomData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                RoomData r = UnityEditor.AssetDatabase.LoadAssetAtPath<RoomData>(path);
                if (MatchesRoom(r))
                {
                    roomMatch = r;
                    if (rooms != null && !rooms.Contains(r)) rooms.Add(r);
                    break;
                }
            }
        }
#endif

        if (roomMatch != null)
        {
            if (currentRoom != null && currentRoom.roomId == payload)
            {
                Debug.Log($"Already in room '{roomMatch.roomName}'. Ignoring repeat scan.");
                return;
            }
            SetScanStatus($"Scanned Room: {roomMatch.roomName}", new Color(0f, 0.7f, 0.9f));
            ChangeRoom(roomMatch);
        }
        else
        {
            Debug.Log($"RoomManager: Scanned QR code '{payload}' is not a room ID. Ignoring room change.");
        }
    }

    /// <summary>
    /// Marks an artifact as completed (interacted) and updates the HUD checklist visual.
    /// </summary>
    public void MarkArtifactInteracted(string artifactId)
    {
        if (string.IsNullOrEmpty(artifactId)) return;

        // Mark as completed in progress tracking
        scannedArtifacts[artifactId] = true;

        // Update HUD list if the item is in the current room list
        if (hudListItems.TryGetValue(artifactId, out GameObject item))
        {
            ArtifactData artifact = currentRoom.artifacts.Find(a => a.artifactId == artifactId);
            if (artifact == null)
            {
                // Fallback: search all rooms
                foreach (RoomData room in rooms)
                {
                    artifact = room.artifacts.Find(a => a.artifactId == artifactId);
                    if (artifact != null) break;
                }
            }

            if (artifact != null)
            {
                UpdateListItemVisual(item, artifact, true);
                SetScanStatus($"Completed: {artifact.artifactName}", new Color(0f, 0.8f, 0.4f));
            }
        }
    }

    public void EnsureAllFiveRoomsLoaded()
    {
        if (rooms == null) rooms = new System.Collections.Generic.List<RoomData>();

        RoomData[] resourceRooms = Resources.LoadAll<RoomData>("MuseumData/Rooms");
        if (resourceRooms != null && resourceRooms.Length > 0)
        {
            foreach (RoomData r in resourceRooms)
            {
                if (r != null && !rooms.Contains(r))
                {
                    rooms.Add(r);
                }
            }
        }

        if (rooms.Count < 5)
        {
            foreach (RoomData r in Resources.FindObjectsOfTypeAll<RoomData>())
            {
                if (r != null && !rooms.Contains(r) && !string.IsNullOrEmpty(r.roomName))
                {
                    rooms.Add(r);
                }
            }
        }

        foreach (RoomData r in rooms)
        {
            if (r == null) continue;
            if (r.artifacts == null || r.artifacts.Count == 0)
            {
                if (r.artifacts == null) r.artifacts = new System.Collections.Generic.List<ArtifactData>();
                ArtifactData[] allArts = Resources.LoadAll<ArtifactData>("MuseumData");
                if (allArts == null || allArts.Length == 0)
                {
                    allArts = Resources.FindObjectsOfTypeAll<ArtifactData>();
                }
                if (allArts != null)
                {
                    foreach (ArtifactData art in allArts)
                    {
                        if (art != null && !r.artifacts.Contains(art))
                        {
                            r.artifacts.Add(art);
                        }
                    }
                }
            }
        }

        rooms.Sort((a, b) => {
            int GetOrder(RoomData r) {
                if (r == null) return 99;
                string n = (r.roomName ?? "").ToLower();
                if (n.Contains("tekstil")) return 1;
                if (n.Contains("seni")) return 2;
                if (n.Contains("kraf")) return 3;
                if (n.Contains("sejarah") || n.Contains("5")) return 4;
                if (n.Contains("mandalika")) return 5;
                return 99;
            }
            return GetOrder(a).CompareTo(GetOrder(b));
        });
    }

    private TMPro.TMP_FontAsset GetDefaultTMPFont()
    {
        if (roomNameText != null && roomNameText.font != null)
        {
            return roomNameText.font;
        }
        foreach (var font in Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>())
        {
            if (font != null && font.name != null && !font.name.Contains("Sprite"))
            {
                return font;
            }
        }
        return null;
    }

    public void PopulateRoomListUI()
    {
        EnsureAllFiveRoomsLoaded();

        GameObject canvasObj = roomHudContainer != null ? roomHudContainer : GameObject.Find("RoomHUDCanvas") ?? GameObject.Find("ExplorationCanvas");
        if (canvasObj == null) return;

        RoomList rList = canvasObj.GetComponentInChildren<RoomList>(true);
        if (rList != null)
        {
            rList.PopulateRoomsList();
            return;
        }

        Transform roomListPanel = canvasObj.name == "RoomListPanel" ? canvasObj.transform : canvasObj.transform.Find("RoomListPanel") ?? canvasObj.transform;
        Transform roomListContainer = roomListPanel.Find("RoomList") ?? roomListPanel.Find("Content/RoomList") ?? roomListPanel.GetComponentInChildren<UnityEngine.UI.GridLayoutGroup>()?.transform ?? roomListPanel.GetComponentInChildren<UnityEngine.UI.VerticalLayoutGroup>()?.transform;

        if (roomListContainer == null) return;

        foreach (Transform child in roomListContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomData room = rooms[i];
            int index = i + 1;

            GameObject cardObj = new GameObject($"RoomCard_{index}");
            cardObj.transform.SetParent(roomListContainer, false);
            cardObj.SetActive(true);

            ConfigureRoomCardItem(cardObj, room, index);
        }
    }

    public void OpenRoomDetailPanel()
    {
        GameObject canvasObj = roomHudContainer != null ? roomHudContainer : GameObject.Find("RoomHUDCanvas") ?? GameObject.Find("ExplorationCanvas");
        if (canvasObj == null) return;

        // 1. Hide RoomListPanel
        Transform roomListPanel = canvasObj.transform.Find("RoomListPanel");
        if (roomListPanel == null)
        {
            foreach (Transform t in canvasObj.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "RoomListPanel")
                {
                    roomListPanel = t;
                    break;
                }
            }
        }
        if (roomListPanel != null)
        {
            roomListPanel.gameObject.SetActive(false);
        }

        // 2. Show RoomPanel / Artifact Detail list
        Transform roomPanel = canvasObj.transform.Find("RoomPanel");
        if (roomPanel == null)
        {
            foreach (Transform t in canvasObj.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "RoomPanel" || t.name == "ArtifactDetailPanel")
                {
                    roomPanel = t;
                    break;
                }
            }
        }

        if (roomPanel != null)
        {
            roomPanel.gameObject.SetActive(true);
        }
        else
        {
            // If RoomPanel is not a distinct child, enable all other siblings in canvasObj
            foreach (Transform child in canvasObj.transform)
            {
                if (child.name == "RoomListPanel")
                {
                    child.gameObject.SetActive(false);
                }
                else
                {
                    child.gameObject.SetActive(true);
                }
            }
        }
    }

    private void ConfigureRoomCardItem(GameObject card, RoomData room, int index)
    {
        if (card == null || room == null) return;

        bool isActiveRoom = (currentRoom == room);

        UnityEngine.UI.Image bgImg = card.GetComponent<UnityEngine.UI.Image>();
        if (bgImg == null) bgImg = card.AddComponent<UnityEngine.UI.Image>();

        if (rowCardMaterial != null) bgImg.material = rowCardMaterial;

        if (isActiveRoom)
        {
            bgImg.color = new Color(0.71f, 0.76f, 0.41f, 0.95f);
        }
        else
        {
            bgImg.color = new Color(0.35f, 0.38f, 0.33f, 0.85f);
        }

        RectTransform rt = card.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(170f, 65f);
        }

        UnityEngine.UI.Button btn = card.GetComponent<UnityEngine.UI.Button>();
        if (btn == null) btn = card.AddComponent<UnityEngine.UI.Button>();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => {
            Debug.Log($"Room Card clicked: {room.roomName}");
            ChangeRoom(room);
            OpenRoomDetailPanel();
        });

        XRButtonSelection selection = card.GetComponent<XRButtonSelection>();
        if (selection == null) selection = card.AddComponent<XRButtonSelection>();
        selection.onClick.RemoveAllListeners();
        selection.onClick.AddListener(() => {
            btn.onClick.Invoke();
        });

        GameObject numObj = new GameObject("NumText");
        numObj.transform.SetParent(card.transform, false);
        var numText = numObj.AddComponent<TMPro.TextMeshProUGUI>();
        RectTransform nRect = numObj.GetComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0.06f, 0.20f);
        nRect.anchorMax = new Vector2(0.28f, 0.80f);
        nRect.sizeDelta = Vector2.zero;

        numText.text = index.ToString("D2");
        numText.fontSize = 16;
        numText.fontStyle = TMPro.FontStyles.Bold;
        numText.color = isActiveRoom ? Color.white : new Color(0.85f, 0.89f, 0.58f);
        numText.alignment = TMPro.TextAlignmentOptions.Center;

        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(card.transform, false);
        var nameText = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.28f, 0.10f);
        nameRect.anchorMax = new Vector2(0.96f, 0.90f);
        nameRect.sizeDelta = Vector2.zero;

        nameText.text = room.roomName;
        nameText.fontSize = 18;
        nameText.fontStyle = TMPro.FontStyles.Bold;
        nameText.color = Color.white;
        nameText.alignment = TMPro.TextAlignmentOptions.Left;
        nameText.verticalAlignment = TMPro.VerticalAlignmentOptions.Middle;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 12;
        nameText.fontSizeMax = 20;
    }
}
