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
        ArtifactData match = null;

        // 1. Try to find via RoomManager
        if (RoomManager.Instance != null)
        {
            if (RoomManager.Instance.CurrentRoom != null)
            {
                match = RoomManager.Instance.CurrentRoom.artifacts.Find(a => a.artifactId == id);
            }

            if (match == null)
            {
                foreach (RoomData room in RoomManager.Instance.rooms)
                {
                    match = room.artifacts.Find(a => a.artifactId == id);
                    if (match != null) break;
                }
            }
        }

        // 2. Editor Fallback: Find asset directly in project database
#if UNITY_EDITOR
        if (match == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ArtifactData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ArtifactData data = UnityEditor.AssetDatabase.LoadAssetAtPath<ArtifactData>(path);
                if (data != null && data.artifactId == id)
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
}
