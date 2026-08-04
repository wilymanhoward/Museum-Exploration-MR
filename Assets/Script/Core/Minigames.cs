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
    public GameObject leaderboardPanel;

    [Header("External")]
    [Tooltip("Optional – leave empty to use WristWatch.Instance automatically.")]
    public GameObject wristWatchCanvas;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        AutoFindPanels();
    }

    private void OnEnable()
    {
        AutoFindPanels();
        PositionInFrontOfUser();
        ShowMenuPanel();
        HideWristWatch();
    }

    private void OnDisable()
    {
        ShowWristWatch();
    }

    private void AutoFindPanels()
    {
        if (minigameMenuPanel == null)
            minigameMenuPanel = FindObjectInScene("MiniGameMenuPanel");
        if (gameListPanel == null)
            gameListPanel = FindObjectInScene("GameListPanel");
        if (leaderboardPanel == null)
            leaderboardPanel = FindObjectInScene("LeaderboardPanel");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public routing API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Show MiniGameMenuPanel; hide GameListPanel, LeaderboardPanel, and all game panels.</summary>
    public void ShowMenuPanel()
    {
        EnsureCanvasActive();
        if (minigameMenuPanel != null) minigameMenuPanel.SetActive(true);
        if (gameListPanel != null) gameListPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        HideAllGamePanels();
    }

    /// <summary>Show GameListPanel; hide MiniGameMenuPanel, LeaderboardPanel, and all game panels.</summary>
    public void ShowGameListPanel()
    {
        EnsureCanvasActive();
        if (minigameMenuPanel != null) minigameMenuPanel.SetActive(false);
        if (gameListPanel != null) gameListPanel.SetActive(true);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        HideAllGamePanels();
    }

    /// <summary>Show LeaderboardPanel for selected game; hide MiniGameMenuPanel, GameListPanel, and all game panels.</summary>
    public void ShowLeaderboardPanel(string gameId, string gameName)
    {
        EnsureCanvasActive();
        if (minigameMenuPanel != null) minigameMenuPanel.SetActive(false);
        if (gameListPanel != null) gameListPanel.SetActive(false);
        HideAllGamePanels();

        if (leaderboardPanel == null)
            leaderboardPanel = FindObjectInScene("LeaderboardPanel");

        if (leaderboardPanel != null)
        {
            leaderboardPanel.SetActive(true);
            LeaderboardPanel lbScript = leaderboardPanel.GetComponent<LeaderboardPanel>();
            if (lbScript == null) lbScript = leaderboardPanel.AddComponent<LeaderboardPanel>();
            lbScript.LoadLeaderboard(gameId, gameName);
        }
        else
        {
            Debug.LogWarning("MiniGames: LeaderboardPanel not found in scene!");
        }
    }

    /// <summary>Activate a specific game panel; hide everything else.</summary>
    public void StartGame(GameObject gamePanel)
    {
        if (gamePanel == null)
        {
            Debug.LogWarning("MiniGames.StartGame: gamePanel is null – staying in menu.");
            return;
        }

        EnsureCanvasActive();
        if (minigameMenuPanel != null) minigameMenuPanel.SetActive(false);
        if (gameListPanel != null) gameListPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        HideAllGamePanels();
        gamePanel.SetActive(true);
    }

    /// <summary>
    /// Compatibility shim for GameListMenu.cs (wrist-watch path).
    /// Activates the canvas, navigates to the game by ID, and starts it.
    /// </summary>
    public void StartGame(string gameId, Pose pose)
    {
        EnsureCanvasActive();
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

    private void EnsureCanvasActive()
    {
        if (minigameMenuPanel != null && minigameMenuPanel.transform.parent != null)
        {
            minigameMenuPanel.transform.parent.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Hide this canvas; OnDisable will restore the wrist watch.
    /// Also called by in-game "Close" buttons via BaseGame.
    /// </summary>
    public void CloseCanvas()
    {
        if (minigameMenuPanel != null && minigameMenuPanel.transform.parent != null)
        {
            minigameMenuPanel.transform.parent.gameObject.SetActive(false);
        }
        gameObject.SetActive(false);
    }

    /// <summary>Alias kept for game panels that call CloseActiveGame() directly.</summary>
    public void CloseActiveGame() => ShowMenuPanel();

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hides every mini-game panel under this canvas. Deliberately does NOT rely on
    /// MiniGameMenuPanel.Instance.games: when the canvas is activated for the first time,
    /// MiniGames.OnEnable (parent) can run before MiniGameMenuPanel.Awake (child) has set
    /// Instance, which previously made this a silent no-op and let a panel left active in
    /// the Editor (e.g. a game panel someone forgot to close before saving the scene) stay
    /// visible on top of the menu. Searching for every BaseGame in the hierarchy instead
    /// finds and hides all game panels unconditionally, regardless of Inspector wiring,
    /// registration timing, or stray active state saved in the scene.
    /// </summary>
    private void HideAllGamePanels()
    {
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        foreach (BaseGame game in GetComponentsInChildren<BaseGame>(true))
        {
            if (game != null) game.gameObject.SetActive(false);
        }
    }

    public void PositionInFrontOfUser()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        if (fwd == Vector3.zero) fwd = Vector3.forward;

        // Position MiniGamesCanvas (or transform if on canvas root)
        Transform targetTransform = transform;
        if (minigameMenuPanel != null && minigameMenuPanel.transform.parent != null)
        {
            targetTransform = minigameMenuPanel.transform.parent; // MiniGamesCanvas
        }

        Vector3 targetPos = cam.position + fwd * 0.7f - Vector3.up * 0.1f;
        targetTransform.position = targetPos;
        Vector3 toPlayer = cam.position - targetTransform.position;
        toPlayer.y = 0;
        if (toPlayer.sqrMagnitude > 0.0001f)
            targetTransform.rotation = Quaternion.LookRotation(-toPlayer, Vector3.up);
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

    private GameObject FindObjectInScene(string objName)
    {
        Transform direct = transform.Find(objName);
        if (direct != null) return direct.gameObject;

        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == objName && t.gameObject.scene.IsValid())
                return t.gameObject;
        }
        return null;
    }
}
