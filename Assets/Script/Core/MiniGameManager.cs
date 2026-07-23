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
        // Ignore all mini-game scans until player taps MULAI on the Main Menu
        if (!MainMenuManager.IsExplorationStarted)
        {
            Debug.Log("MiniGameManager: Exploration has not started yet. Ignoring QR scan.");
            return;
        }

        if (string.IsNullOrEmpty(payload)) return;

        string cleanPayload = payload.Trim().ToLower();
        string gameId = null;

        if (cleanPayload.Contains("game_1") || cleanPayload.Contains("game1") || cleanPayload == "1" || cleanPayload.Contains("guess"))
        {
            gameId = "game_1";
        }
        else if (cleanPayload.Contains("game_2") || cleanPayload.Contains("game2") || cleanPayload == "2" || cleanPayload.Contains("batik"))
        {
            gameId = "game_2";
        }
        else if (cleanPayload.Contains("game_3") || cleanPayload.Contains("game3") || cleanPayload == "3" || cleanPayload.Contains("batu") || cleanPayload.Contains("assemble"))
        {
            gameId = "game_3";
        }

        if (gameId != null)
        {
            if (ActiveGamePayload == gameId)
            {
                Debug.Log($"MiniGameManager: Game '{gameId}' is already active. Ignoring repeat scan.");
                return;
            }

            Debug.Log($"MiniGameManager matched raw QR scan '{payload}' to game '{gameId}'.");
            StartGame(gameId, qrPose);
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

        // Clean up all template children (e.g. artifact detail panel elements) so game has a clean container canvas
        System.Collections.Generic.List<GameObject> childrenToDestroy = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in activeGameInstance.transform)
        {
            childrenToDestroy.Add(child.gameObject);
        }
        foreach (GameObject child in childrenToDestroy)
        {
            DestroyImmediate(child);
        }

        // Add a clean Background image for the mini-game
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(activeGameInstance.transform, false);
        UnityEngine.UI.Image bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.537f, 0.557f, 0.478f, 1f); // Sage background (#898E7A)
        foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (m != null && (m.name == "Mat_OptionsCardBackground" || m.name == "Mat_RoomHUD" || m.name == "Mat_ArtifactDetailPanel"))
            {
                bgImg.material = m;
                break;
            }
        }
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

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

        // Rotate to face player view (right-side up, unmirrored)
        if (playerTransform != null)
        {
            Vector3 playerForward = playerTransform.forward;
            playerForward.y = 0;
            if (playerForward != Vector3.zero)
            {
                activeGameInstance.transform.rotation = Quaternion.LookRotation(playerForward, Vector3.up);
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
