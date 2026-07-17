using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("Museum Configurations")]
    [Tooltip("List of all rooms configured in the museum.")]
    public List<RoomData> rooms = new List<RoomData>();

    [Tooltip("The room the player starts in (optional).")]
    public RoomData startingRoom;

    [Header("UI Canvas HUD References")]
    public GameObject roomHudContainer;
    public TextMeshProUGUI roomNameText;
    public Transform artifactListContainer;
    [Tooltip("Prefab for a line item in the artifact checklist UI.")]
    public GameObject artifactListItemPrefab;
    [Tooltip("Text field to show scan feedback (e.g. Scanned Exhibit: Mona Lisa).")]
    public TextMeshProUGUI scanStatusText;

    [Header("Wayfinding & Prefabs")]
    public WayfindingSystem wayfindingSystem;

    // Track active state
    private RoomData currentRoom;
    private Dictionary<string, bool> scannedArtifacts = new Dictionary<string, bool>();
    private Dictionary<string, TextMeshProUGUI> hudListItemTexts = new Dictionary<string, TextMeshProUGUI>();
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
        // 1. Try to find RoomHUDCanvas in the scene (active or inactive)
        if (roomHudContainer == null)
        {
            GameObject[] allGo = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject go in allGo)
            {
                if (go.name == "RoomHUDCanvas" && go.scene.isLoaded)
                {
                    roomHudContainer = go;
                    break;
                }
            }
            if (roomHudContainer != null)
            {
                Debug.Log("RoomManager: Automatically located 'RoomHUDCanvas' (even if inactive).");
            }
        }

        // 2. Try to find child references on the RoomHUDCanvas
        if (roomHudContainer != null)
        {
            if (roomNameText == null)
            {
                roomNameText = roomHudContainer.transform.Find("RoomTitleText")?.GetComponent<TextMeshProUGUI>();
                if (roomNameText != null) Debug.Log("RoomManager: Automatically located 'RoomTitleText' component.");
            }

            if (artifactListContainer == null)
            {
                artifactListContainer = roomHudContainer.transform.Find("ArtifactList");
                if (artifactListContainer != null) Debug.Log("RoomManager: Automatically located 'ArtifactList' container.");
            }

            if (scanStatusText == null)
            {
                scanStatusText = roomHudContainer.transform.Find("ScanStatusText")?.GetComponent<TextMeshProUGUI>();
                if (scanStatusText != null) Debug.Log("RoomManager: Automatically located 'ScanStatusText' component.");
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

        // 4. Editor auto-healing lookup for missing assets
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
        if (roomHudContainer != null)
        {
            roomHudContainer.SetActive(true);
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

        // Update UI Text
        if (roomNameText != null)
        {
            roomNameText.text = currentRoom.roomName;
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

        // Clear existing items
        int clearedCount = 0;
        foreach (Transform child in artifactListContainer)
        {
            Destroy(child.gameObject);
            clearedCount++;
        }
        Debug.Log($"Cleared {clearedCount} existing checklist items.");
        hudListItemTexts.Clear();

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

            TextMeshProUGUI textComp = item.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                // Check if already scanned in the past
                bool isScanned = scannedArtifacts.ContainsKey(artifact.artifactId) && scannedArtifacts[artifact.artifactId];
                UpdateListItemVisual(textComp, artifact, isScanned);
                hudListItemTexts[artifact.artifactId] = textComp;
                Debug.Log($"Successfully spawned checklist item for artifact '{artifact.artifactName}' (Scanned: {isScanned}) at scale: {item.transform.localScale}");
            }
            else
            {
                Debug.LogError($"Spawned checklist item prefab, but it does NOT contain a TextMeshProUGUI component in children!");
            }
        }
    }

    private void UpdateListItemVisual(TextMeshProUGUI textComp, ArtifactData artifact, bool isScanned)
    {
        if (isScanned)
        {
            // Styled color for scanned artifacts (Minimalist emerald green)
            textComp.text = $"<color=#009977>✓ {artifact.artifactName}</color>";
        }
        else
        {
            // Standard unvisited style (Charcoal gray for light background)
            textComp.text = $"<color=#333333>○ {artifact.artifactName}</color>";
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
        Debug.Log($"RoomManager received QR scan payload: '{payload}'");

        // Check if the payload matches a room transition QR
        RoomData roomMatch = rooms.Find(r => r.roomId == payload);
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
        if (hudListItemTexts.TryGetValue(artifactId, out TextMeshProUGUI textComp))
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
                UpdateListItemVisual(textComp, artifact, true);
                SetScanStatus($"Completed: {artifact.artifactName}", new Color(0f, 0.8f, 0.4f));
            }
        }
    }
}
