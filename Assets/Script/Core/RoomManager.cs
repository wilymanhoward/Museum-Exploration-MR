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

        // 4. Editor auto-healing lookup for missing assets (always run to ensure complete list)
#if UNITY_EDITOR
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
        if (artifactListContainer == null)
        {
            Debug.LogError("RebuildArtifactChecklist aborted: artifactListContainer is null!");
            return;
        }
        if (artifactListItemPrefab == null)
        {
            Debug.LogError("RebuildArtifactChecklist aborted: artifactListItemPrefab is null!");
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
        foreach (ArtifactData artifact in currentRoom.artifacts)
        {
            if (artifact == null)
            {
                Debug.LogWarning("Found null ArtifactData inside currentRoom.artifacts!");
                continue;
            }

            GameObject item = Instantiate(artifactListItemPrefab, artifactListContainer);
            item.transform.localScale = Vector3.one;
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;

            // Check if already scanned in the past
            bool isScanned = scannedArtifacts.ContainsKey(artifact.artifactId) && scannedArtifacts[artifact.artifactId];
            UpdateListItemVisual(item, artifact, isScanned);
            hudListItems[artifact.artifactId] = item;

            // Hook up the button click event
            UnityEngine.UI.Button btn = item.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = item.GetComponentInChildren<UnityEngine.UI.Button>();
            if (btn != null)
            {
                ArtifactData currentArtifact = artifact;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    SelectArtifactFromList(currentArtifact);
                });
            }

            Debug.Log($"Successfully spawned checklist item for artifact '{artifact.artifactName}' (Scanned: {isScanned})");
        }
    }

    private void UpdateListItemVisual(GameObject item, ArtifactData artifact, bool isScanned)
    {
        // 1. Ensure item background image and styling match screenshot card layout
        UnityEngine.UI.Image bgImage = item.GetComponent<UnityEngine.UI.Image>();
        if (bgImage == null)
        {
            bgImage = item.AddComponent<UnityEngine.UI.Image>();
        }

        // Apply rounded card material if assigned or keep prefab material
        if (rowCardMaterial != null && (bgImage.material == null || bgImage.material.name == "Default UI"))
        {
            bgImage.material = rowCardMaterial;
        }

        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x > 0 ? itemRect.sizeDelta.x : 300f, 65f);
        }

        // Hide legacy single text if present
        Transform legacyTextT = item.transform.Find("Text");
        if (legacyTextT != null && legacyTextT.name == "Text")
        {
            legacyTextT.gameObject.SetActive(false);
        }

        Color paleYellow = new Color(0.90f, 0.93f, 0.63f, 1.0f); // #E5EE9C
        Color visitedGreen = new Color(0.55f, 0.89f, 0.63f, 1.0f); // #8BE4A0
        Color unvisitedGray = new Color(0.88f, 0.88f, 0.88f, 1.0f); // #E0E0E0

        // 2. Ensure Thumbnail Image (Thumb)
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
            Sprite artSprite = (artifact != null && artifact.images != null && artifact.images.Length > 0 && artifact.images[0].sprite != null) 
                ? artifact.images[0].sprite 
                : null;

            if (artSprite != null)
            {
                thumbImg.sprite = artSprite;
                thumbImg.gameObject.SetActive(true);
            }
        }

        // 3. Ensure Number Tag (NumText e.g. "01")
        Transform numT = item.transform.Find("NumText");
        TextMeshProUGUI numTextComp = null;
        if (numT == null)
        {
            GameObject numObj = new GameObject("NumText");
            numObj.transform.SetParent(item.transform, false);
            numT = numObj.transform;
            numTextComp = numObj.AddComponent<TextMeshProUGUI>();
            RectTransform nRect = numObj.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0.22f, 0.48f);
            nRect.anchorMax = new Vector2(0.30f, 0.92f);
            nRect.sizeDelta = Vector2.zero;
        }
        else
        {
            numTextComp = numT.GetComponent<TextMeshProUGUI>();
        }

        if (numTextComp != null)
        {
            int index = (currentRoom != null && currentRoom.artifacts != null && artifact != null) ? currentRoom.artifacts.IndexOf(artifact) : 0;
            numTextComp.text = (index >= 0 ? index + 1 : 1).ToString("00");
            numTextComp.fontSize = 18;
            numTextComp.fontStyle = FontStyles.Bold;
            numTextComp.color = paleYellow;
            numTextComp.alignment = TextAlignmentOptions.Left;
        }

        // 4. Ensure Artifact Title (NameText e.g. "Mona Lisa")
        Transform nameT = item.transform.Find("NameText");
        TextMeshProUGUI nameTextComp = null;
        if (nameT == null)
        {
            GameObject nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(item.transform, false);
            nameT = nameObj.transform;
            nameTextComp = nameObj.AddComponent<TextMeshProUGUI>();
            RectTransform nRect = nameObj.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0.31f, 0.48f);
            nRect.anchorMax = new Vector2(0.96f, 0.92f);
            nRect.sizeDelta = Vector2.zero;
        }
        else
        {
            nameTextComp = nameT.GetComponent<TextMeshProUGUI>();
        }

        if (nameTextComp != null && artifact != null)
        {
            nameTextComp.text = artifact.artifactName;
            nameTextComp.fontSize = 22;
            nameTextComp.fontStyle = FontStyles.Bold;
            nameTextComp.color = paleYellow;
            nameTextComp.alignment = TextAlignmentOptions.Left;
        }

        // 5. Ensure Visited Status (StatusText e.g. "Sudah Dikunjungi" / "Belum Dikunjungi")
        Transform statusT = item.transform.Find("StatusText");
        TextMeshProUGUI statusTextComp = null;
        if (statusT == null)
        {
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(item.transform, false);
            statusT = statusObj.transform;
            statusTextComp = statusObj.AddComponent<TextMeshProUGUI>();
            RectTransform sRect = statusObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.31f, 0.10f);
            sRect.anchorMax = new Vector2(0.96f, 0.48f);
            sRect.sizeDelta = Vector2.zero;
        }
        else
        {
            statusTextComp = statusT.GetComponent<TextMeshProUGUI>();
        }

        if (statusTextComp != null)
        {
            statusTextComp.text = isScanned ? "Sudah Dikunjungi" : "Belum Dikunjungi";
            statusTextComp.fontSize = 15;
            statusTextComp.fontStyle = FontStyles.Normal;
            statusTextComp.color = isScanned ? visitedGreen : unvisitedGray;
            statusTextComp.alignment = TextAlignmentOptions.Left;
        }
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
        if (!MainMenuManager.IsExplorationStarted)
        {
            Debug.Log("RoomManager: Exploration has not started yet. Ignoring QR scan.");
            return;
        }

        Debug.Log($"RoomManager received QR scan payload: '{payload}'");

        // Search for the room in our loaded list, or use editor fallback
        RoomData roomMatch = rooms.Find(r => r.roomId == payload);

#if UNITY_EDITOR
        if (roomMatch == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:RoomData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                RoomData r = UnityEditor.AssetDatabase.LoadAssetAtPath<RoomData>(path);
                if (r != null && r.roomId == payload)
                {
                    roomMatch = r;
                    if (!rooms.Contains(r)) rooms.Add(r);
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
}
