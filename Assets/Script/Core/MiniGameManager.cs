using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MiniGameManager : MonoBehaviour
{
    public static MiniGameManager Instance { get; private set; }

    [Header("Game References")]
    [Tooltip("Prefab to use as the base container (uses the same glassmorphism canvas as the detail panel).")]
    public GameObject panelTemplatePrefab;

    private GameObject activeGameInstance;
    public string ActiveGamePayload { get; private set; } = "";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        QRCodeScanner.OnQRCodeScanned += HandleQRCodeScanned;
    }

    private void OnDisable()
    {
        QRCodeScanner.OnQRCodeScanned -= HandleQRCodeScanned;
    }

    private void HandleQRCodeScanned(string payload, Pose qrPose)
    {
        // Check if the payload matches a game ID ("game_1", "game_2", "game_3")
        if (payload == "game_1" || payload == "game_2" || payload == "game_3")
        {
            // Check if this game is already active to prevent duplicate scan loops
            if (ActiveGamePayload == payload)
            {
                Debug.Log($"MiniGameManager: Game '{payload}' is already active. Ignoring repeat scan.");
                return;
            }
            
            StartGame(payload, qrPose);
        }
    }

    /// <summary>
    /// Starts a mini-game based on the scanned QR payload.
    /// </summary>
    public void StartGame(string payload, Pose qrPose)
    {
        Debug.Log($"MiniGameManager: Starting game for payload '{payload}'");
        ActiveGamePayload = payload;

        // Clean up any active game first
        CloseActiveGame(false);

        // Get player camera for eye-level spawning in front of the player
        Transform playerTransform = Camera.main != null ? Camera.main.transform : null;
        Vector3 spawnPos = qrPose.position;
        if (playerTransform != null)
        {
            // Spawn within comfortable arm's reach (not 1.4m like the artifact viewer) so
            // cards get grabbed by the near/poke hand interactor instead of the far ray.
            // The far ray's own aiming (see PinchPointFollow in the XRI Hands sample) freezes
            // its rotation whenever a frame's angular change is too large, which is fine for
            // pointing at a distant menu button but fights precise card-into-slot dragging.
            spawnPos = playerTransform.position + playerTransform.forward * 0.5f;
            spawnPos.y = playerTransform.position.y;
        }

        // Use ArtifactManager's artifactPanelPrefab as template if not set
        GameObject prefabToUse = panelTemplatePrefab;
        if (prefabToUse == null && ArtifactManager.Instance != null)
        {
            prefabToUse = ArtifactManager.Instance.artifactPanelPrefab;
        }

        if (prefabToUse == null)
        {
            Debug.LogError("MiniGameManager: No panel prefab template found to spawn the game.");
            return;
        }

        // Spawn the base glassmorphism panel
        activeGameInstance = Instantiate(prefabToUse, spawnPos, qrPose.rotation);

        // The template may be an inactive scene object (e.g. the hidden ArtifactUICanvas),
        // whose clone would also start inactive and never render or run its game logic.
        activeGameInstance.SetActive(true);

        // The artifact panel prefab carries a ModelSpawnAnchor (RotateArtifact spawner) that
        // is irrelevant to mini-games and would leave a stray grabbable object in the panel.
        Transform leftoverSpawner = activeGameInstance.transform.Find("ModelSpawnAnchor");
        if (leftoverSpawner != null)
        {
            Destroy(leftoverSpawner.gameObject);
        }

        // Assign Main Camera as the World Camera on the Canvas for rendering and interaction
        Canvas canvas = activeGameInstance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.worldCamera = Camera.main;
        }
        
        // Fix zero scale issue (originally handled by pop-in animation in ArtifactPanel)
        var artPanel = activeGameInstance.GetComponent<ArtifactPanel>();
        Vector3 targetScale = new Vector3(0.0022f, 0.0022f, 0.0022f); // fallback portrait scale
        if (artPanel != null)
        {
            // If the prefab already has a defined initial scale, use it (usually 0.0022f)
            // But since Awake might have set localScale to Vector3.zero, we check and fallback
            targetScale = new Vector3(0.0022f, 0.0022f, 0.0022f);
            Destroy(artPanel);
        }
        activeGameInstance.transform.localScale = targetScale;

        // Rotate to face player (vertical billboard)
        if (playerTransform != null)
        {
            Vector3 directionToPlayer = playerTransform.position - activeGameInstance.transform.position;
            directionToPlayer.y = 0;
            if (directionToPlayer != Vector3.zero)
            {
                activeGameInstance.transform.rotation = Quaternion.LookRotation(-directionToPlayer);
            }
        }

        // Add the specific game script depending on payload
        if (payload == "game_1")
        {
            activeGameInstance.AddComponent<Game1GuessName>();
        }
        else if (payload == "game_2")
        {
            activeGameInstance.AddComponent<Game2BatikMatch>();
        }
        else if (payload == "game_3")
        {
            activeGameInstance.AddComponent<Game3BatuAssembly>();
        }
    }

    public void CloseActiveGame(bool resetPayload = true)
    {
        if (resetPayload)
        {
            ActiveGamePayload = "";
        }
        if (activeGameInstance != null)
        {
            Destroy(activeGameInstance);
            activeGameInstance = null;
        }
    }
}
