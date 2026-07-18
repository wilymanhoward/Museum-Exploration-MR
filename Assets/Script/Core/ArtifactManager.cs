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

    [Tooltip("The spawner component (RotateArtifact) under the canvas where models are instantiated.")]
    public RotateArtifact objectSpawner;

    [Tooltip("The script controller (ArtifactInteraction) on the panel UI.")]
    public ArtifactInteraction artifactInteraction;

    [Header("UI Prefab Fallback (Optional)")]
    [Tooltip("Prefab for the floating detail panel spawned when an artifact QR is scanned (fallback only).")]
    public GameObject artifactPanelPrefab;

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
                if (go.name == "ArtifactUICanvas" && go.scene.isLoaded)
                {
                    artifactUiCanvas = go;
                    break;
                }
            }
            if (artifactUiCanvas != null)
            {
                Debug.Log("ArtifactManager: Automatically located 'ArtifactUICanvas' (even if inactive) in the scene.");
            }
        }

        if (artifactUiCanvas != null)
        {
            if (objectSpawner == null)
            {
                var spawnerTransform = artifactUiCanvas.transform.Find("ObjectSpawner");
                if (spawnerTransform != null) objectSpawner = spawnerTransform.GetComponent<RotateArtifact>();
                
                if (objectSpawner == null)
                {
                    objectSpawner = artifactUiCanvas.GetComponentInChildren<RotateArtifact>(true);
                }
            }

            if (artifactInteraction == null)
            {
                artifactInteraction = artifactUiCanvas.GetComponentInChildren<ArtifactInteraction>(true);
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
    /// Callback from QR Scanner when a QR code payload is detected.
    /// </summary>
    private void HandleQRCodeScanned(string payload, Pose pose)
    {
        // Prevent duplicate scanning if this exact artifact detail panel is already open
        if (activePanelInstance != null)
        {
            ArtifactInteraction existingInteraction = activePanelInstance.GetComponentInChildren<ArtifactInteraction>();
            if (existingInteraction != null && existingInteraction.artifactData != null && existingInteraction.artifactData.artifactId == payload)
            {
                return; // Already showing this exact panel, ignore repeat scan
            }
        }

        // Search through rooms configured in the RoomManager to locate the artifact
        ArtifactData artifactMatch = null;
        if (RoomManager.Instance != null)
        {
            // Check current room first
            if (RoomManager.Instance.CurrentRoom != null)
            {
                artifactMatch = RoomManager.Instance.CurrentRoom.artifacts.Find(a => a.artifactId == payload);
            }

            // Fallback: search all rooms
            if (artifactMatch == null)
            {
                foreach (RoomData room in RoomManager.Instance.rooms)
                {
                    artifactMatch = room.artifacts.Find(a => a.artifactId == payload);
                    if (artifactMatch != null) break;
                }
            }
        }

        if (artifactMatch != null)
        {
            // A different QR was scanned while a panel is open: destroy the current
            // panel and its 3D model before showing the new artifact
            if (activePanelInstance != null)
            {
                CloseActivePanel();
            }

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.SetScanStatus($"Scanned Exhibit: {artifactMatch.artifactName}", new Color(0.1f, 0.75f, 0.2f));
            }
            TriggerArtifactScan(artifactMatch, pose);
        }
    }

    private void HandleQRCodeLost(string payload)
    {
        // When physical QR code is lost, close the panel/canvas
        if (activePanelInstance != null)
        {
            ArtifactInteraction interaction = activePanelInstance.GetComponentInChildren<ArtifactInteraction>();
            if (interaction != null && interaction.artifactData != null && interaction.artifactData.artifactId == payload)
            {
                CloseActivePanel();
            }
        }
    }

    private void TriggerArtifactScan(ArtifactData artifact, Pose pose)
    {
        Debug.Log($"Scanned Artifact QR: {artifact.artifactName}");

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
                ArtifactInteraction existingInteraction = activePanelInstance.GetComponentInChildren<ArtifactInteraction>();
                if (existingInteraction != null)
                {
                    if (existingInteraction.artifactData != null && existingInteraction.artifactData.artifactId == artifact.artifactId)
                    {
                        return;
                    }
                    existingInteraction.Setup(artifact, playerTransform, pose, () => {
                        activePanelInstance = null;
                    });
                    return;
                }
            }

            if (artifactPanelPrefab != null)
            {
                GameObject panelInstance = Instantiate(artifactPanelPrefab, pose.position, pose.rotation);
                Canvas canvas = panelInstance.GetComponent<Canvas>();
                if (canvas != null && canvas.worldCamera == null)
                {
                    canvas.worldCamera = Camera.main;
                }

                ArtifactInteraction interaction = panelInstance.GetComponentInChildren<ArtifactInteraction>();
                if (interaction != null)
                {
                    interaction.Setup(artifact, playerTransform, pose, () => {
                        activePanelInstance = null;
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
            if (activePanelInstance == artifactUiCanvas)
            {
                // Destroy the spawned 3D model and hide the persistent canvas
                if (objectSpawner != null)
                {
                    objectSpawner.ClearModel();
                }
                artifactUiCanvas.SetActive(false);
                if (artifactInteraction != null)
                {
                    artifactInteraction.StartClose();
                }
            }
            else
            {
                // Instantiate/Destroy fallback
                ArtifactInteraction interaction = activePanelInstance.GetComponentInChildren<ArtifactInteraction>();
                if (interaction != null)
                {
                    interaction.StartClose();
                }
            }
            activePanelInstance = null;
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
}
