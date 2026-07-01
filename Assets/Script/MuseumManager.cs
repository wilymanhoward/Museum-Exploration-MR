using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MuseumManager : MonoBehaviour
{
    public static MuseumManager Instance { get; private set; }

    [Header("Museum Configurations")]
    [Tooltip("List of all rooms configured in the museum.")]
    public List<RoomData> rooms = new List<RoomData>();

    [Tooltip("The room the player starts in (optional).")]
    public RoomData startingRoom;

    [Header("Player Tracking")]
    [Tooltip("Reference to the player's camera or headset transform.")]
    public Transform playerTransform;

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
    [Tooltip("Prefab for the floating detail panel spawned when an artifact QR is scanned.")]
    public GameObject artifactPanelPrefab;

    // Track active state
    private RoomData currentRoom;
    private Dictionary<string, bool> scannedArtifacts = new Dictionary<string, bool>();
    private GameObject activePanelInstance;
    private Dictionary<string, TextMeshProUGUI> hudListItemTexts = new Dictionary<string, TextMeshProUGUI>();
    private float statusTextTimer = 0f;

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
        QRCodeScanner.OnQRCodeLost += HandleQRCodeLost;

        // Initialize starting room if set
        if (startingRoom != null)
        {
            ChangeRoom(startingRoom);
        }
    }

    private void OnDestroy()
    {
        QRCodeScanner.OnQRCodeScanned -= HandleQRCodeScanned;
        QRCodeScanner.OnQRCodeLost -= HandleQRCodeLost;
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
        if (activePanelInstance != null)
        {
            ArtifactInteraction interaction = activePanelInstance.GetComponent<ArtifactInteraction>();
            if (interaction != null)
            {
                interaction.StartClose();
            }
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
        if (artifactListContainer == null || artifactListItemPrefab == null) return;

        // Clear existing items
        foreach (Transform child in artifactListContainer)
        {
            Destroy(child.gameObject);
        }
        hudListItemTexts.Clear();

        // Instantiate item for each artifact in the room
        foreach (ArtifactData artifact in currentRoom.artifacts)
        {
            if (artifact == null) continue;

            GameObject item = Instantiate(artifactListItemPrefab, artifactListContainer);
            TextMeshProUGUI textComp = item.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                // Check if already scanned in the past
                bool isScanned = scannedArtifacts.ContainsKey(artifact.artifactId) && scannedArtifacts[artifact.artifactId];
                UpdateListItemVisual(textComp, artifact, isScanned);
                hudListItemTexts[artifact.artifactId] = textComp;
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
        // 1. Check if the payload matches a room transition QR
        RoomData roomMatch = rooms.Find(r => r.roomId == payload);
        if (roomMatch != null)
        {
            SetScanStatus($"Scanned Room: {roomMatch.roomName}", new Color(0f, 0.7f, 0.9f));
            ChangeRoom(roomMatch);
            return;
        }

        // 2. Check if the payload matches an artifact QR in the current room
        ArtifactData artifactMatch = null;
        if (currentRoom != null)
        {
            artifactMatch = currentRoom.artifacts.Find(a => a.artifactId == payload);
        }

        // Fallback: search all rooms in case they scan out-of-room artifacts
        if (artifactMatch == null)
        {
            foreach (RoomData room in rooms)
            {
                artifactMatch = room.artifacts.Find(a => a.artifactId == payload);
                if (artifactMatch != null) break;
            }
        }

        if (artifactMatch != null)
        {
            SetScanStatus($"Scanned Exhibit: {artifactMatch.artifactName}", new Color(0.1f, 0.75f, 0.2f));
            TriggerArtifactScan(artifactMatch, pose);
        }
        else
        {
            SetScanStatus($"Scanned Code: {payload}", Color.gray);
        }
    }

    private void TriggerArtifactScan(ArtifactData artifact, Pose pose)
    {
        Debug.Log($"Scanned Artifact QR: {artifact.artifactName}");

        // Update scanned state dictionary
        scannedArtifacts[artifact.artifactId] = true;

        // Update HUD list if text is present
        if (hudListItemTexts.TryGetValue(artifact.artifactId, out TextMeshProUGUI textComp))
        {
            UpdateListItemVisual(textComp, artifact, true);
        }

        // Check if there is an active panel already open
        if (activePanelInstance != null)
        {
            ArtifactInteraction existingInteraction = activePanelInstance.GetComponent<ArtifactInteraction>();
            if (existingInteraction != null)
            {
                // REUSE the existing panel instead of instantiating a new one!
                // This updates the text fields, swaps the 3D model, and anchors the distance check to the new scan position.
                existingInteraction.Setup(artifact, playerTransform, pose.position, () => {
                    activePanelInstance = null;
                });
                return;
            }
        }

        // Spawn new panel if none exists
        if (artifactPanelPrefab != null)
        {
            GameObject panelInstance = Instantiate(artifactPanelPrefab, pose.position, pose.rotation);
            
            // Explicitly set the worldCamera for raycasting stability
            Canvas canvas = panelInstance.GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }

            ArtifactInteraction interaction = panelInstance.GetComponent<ArtifactInteraction>();
            
            if (interaction != null)
            {
                interaction.Setup(artifact, playerTransform, pose.position, () => {
                    activePanelInstance = null;
                });
                
                activePanelInstance = panelInstance;
            }
            else
            {
                Debug.LogWarning("Spawned artifact panel prefab does not have ArtifactInteraction script attached.");
            }
        }
    }

    /// <summary>
    /// Callback from QR Scanner when a QR code payload is lost.
    /// </summary>
    private void HandleQRCodeLost(string payload)
    {
        // When physical QR code is lost, we don't necessarily destroy it immediately 
        // because we want to let the player read it until they physically walk away.
        // The distance-based check inside ArtifactInteraction handles actual dismissal.
        Debug.Log($"Camera lost sight of QR code: {payload}");
    }
}
