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

        // Clean up any active game first
        CloseActiveGame();

        // Get player camera for eye-level spawning
        Transform playerTransform = Camera.main != null ? Camera.main.transform : null;
        Vector3 spawnPos = qrPose.position;
        if (playerTransform != null)
        {
            // Spawn at eye level
            spawnPos.y = playerTransform.position.y;
        }

        // Use MuseumManager's artifactPanelPrefab as template if not set
        GameObject prefabToUse = panelTemplatePrefab;
        if (prefabToUse == null && MuseumManager.Instance != null)
        {
            prefabToUse = MuseumManager.Instance.artifactPanelPrefab;
        }

        if (prefabToUse == null)
        {
            Debug.LogError("MiniGameManager: No panel prefab template found to spawn the game.");
            return;
        }

        // Spawn the base glassmorphism panel
        activeGameInstance = Instantiate(prefabToUse, spawnPos, qrPose.rotation);
        
        // Remove standard artifact interaction script so it doesn't try to load regular artifact text
        var artInteraction = activeGameInstance.GetComponent<ArtifactInteraction>();
        if (artInteraction != null)
        {
            Destroy(artInteraction);
        }

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

    public void CloseActiveGame()
    {
        if (activeGameInstance != null)
        {
            Destroy(activeGameInstance);
            activeGameInstance = null;
        }
    }
}
