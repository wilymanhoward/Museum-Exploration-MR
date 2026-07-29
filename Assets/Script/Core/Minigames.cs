using UnityEngine;

/// <summary>
/// Canvas-level coordinator for the MiniGamesCanvas.
/// Attach to the root MiniGamesCanvas GameObject.
/// Responsible for: positioning the canvas, routing between panels, wrist-watch show/hide.
/// </summary>
public class MiniGames : MonoBehaviour
{
    public static MiniGames Instance { get; private set; }

    [Header("Panels (auto-found by name if left empty)")]
    public GameObject minigameMenuPanel;
    public GameObject gameListPanel;

    [Header("External")]
    [Tooltip("Optional – leave empty to use WristWatch.Instance automatically.")]
    public GameObject wristWatchCanvas;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Auto-find child panels by name if not assigned
        if (minigameMenuPanel == null)
            minigameMenuPanel = FindDirectChild("MiniGameMenuPanel");
        if (gameListPanel == null)
            gameListPanel = FindDirectChild("GameListPanel");
    }

    private void OnEnable()
    {
        PositionInFrontOfUser();
        ShowMenuPanel();
        HideWristWatch();
    }

    private void OnDisable()
    {
        ShowWristWatch();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public routing API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Show MiniGameMenuPanel; hide GameListPanel and all game panels.</summary>
    public void ShowMenuPanel()
    {
        if (minigameMenuPanel != null) minigameMenuPanel.SetActive(true);
        if (gameListPanel != null) gameListPanel.SetActive(false);
        HideAllGamePanels();
    }

    /// <summary>Show GameListPanel; hide MiniGameMenuPanel and all game panels.</summary>
    public void ShowGameListPanel()
    {
        if (minigameMenuPanel != null) minigameMenuPanel.SetActive(false);
        if (gameListPanel != null) gameListPanel.SetActive(true);
        HideAllGamePanels();
    }

    /// <summary>Activate a specific game panel; hide everything else.</summary>
    public void StartGame(GameObject gamePanel)
    {
        if (gamePanel == null)
        {
            Debug.LogWarning("MiniGames.StartGame: gamePanel is null – staying in menu.");
            return;
        }

        if (minigameMenuPanel != null) minigameMenuPanel.SetActive(false);
        if (gameListPanel != null) gameListPanel.SetActive(false);
        HideAllGamePanels();
        gamePanel.SetActive(true);
    }

    /// <summary>
    /// Compatibility shim for GameListMenu.cs (wrist-watch path).
    /// Activates the canvas, navigates to the game by ID, and starts it.
    /// </summary>
    public void StartGame(string gameId, Pose pose)
    {
        if (!gameObject.activeSelf)
        {
            transform.position = pose.position;
            transform.rotation = pose.rotation;
            gameObject.SetActive(true); // triggers OnEnable → ShowMenuPanel + HideWristWatch
        }

        if (MiniGameMenuPanel.Instance != null)
        {
            MiniGameMenuPanel.Instance.SelectGameById(gameId);
            MiniGameMenuPanel.Instance.StartSelected();
        }
    }

    /// <summary>
    /// Hide this canvas; OnDisable will restore the wrist watch.
    /// Also called by in-game "Close" buttons via BaseGame.
    /// </summary>
    public void CloseCanvas()
    {
        gameObject.SetActive(false);
    }

    /// <summary>Alias kept for game panels that call CloseActiveGame() directly.</summary>
    public void CloseActiveGame() => ShowMenuPanel();

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void HideAllGamePanels()
    {
        if (MiniGameMenuPanel.Instance == null) return;
        foreach (var entry in MiniGameMenuPanel.Instance.games)
            if (entry.gamePanel != null) entry.gamePanel.SetActive(false);
    }

    public void PositionInFrontOfUser()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        if (fwd == Vector3.zero) fwd = Vector3.forward;
        transform.position = cam.position + fwd * 1.5f;
        Vector3 toPlayer = cam.position - transform.position;
        toPlayer.y = 0;
        if (toPlayer.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(-toPlayer, Vector3.up);
    }

    private void HideWristWatch()
    {
        if (WristWatch.Instance != null && WristWatch.Instance.wristWatchButtonObj != null)
            WristWatch.Instance.wristWatchButtonObj.SetActive(false);
        if (wristWatchCanvas != null)
            wristWatchCanvas.SetActive(false);
    }

    private void ShowWristWatch()
    {
        if (WristWatch.Instance != null)
            WristWatch.Instance.EnsureWatchButtonVisible();
        else if (wristWatchCanvas != null)
            wristWatchCanvas.SetActive(true);
    }

    private GameObject FindDirectChild(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.gameObject : null;
    }
}
