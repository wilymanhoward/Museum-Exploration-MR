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

    /// <summary>
    /// Starts a mini-game based on the scanned QR payload.
    /// </summary>
    public void StartGame(string payload, Pose qrPose)
    {
        Debug.Log($"MiniGameManager: Starting game for payload '{payload}'");
        ActiveGamePayload = payload;

        // Clean up any active game first
        CloseActiveGame(false);

        // Get player camera for eye-level spawning
        Transform playerTransform = Camera.main != null ? Camera.main.transform : null;
        Vector3 spawnPos = qrPose.position;
        if (playerTransform != null)
        {
            // Spawn at eye level
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
        
        // Fix zero scale issue (originally handled by pop-in animation in ArtifactInteraction)
        var artInteraction = activeGameInstance.GetComponent<ArtifactInteraction>();
        Vector3 targetScale = new Vector3(0.0022f, 0.0022f, 0.0022f); // fallback portrait scale
        if (artInteraction != null)
        {
            // If the prefab already has a defined initial scale, use it (usually 0.0022f)
            // But since Awake might have set localScale to Vector3.zero, we check and fallback
            targetScale = new Vector3(0.0022f, 0.0022f, 0.0022f);
            Destroy(artInteraction);
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
