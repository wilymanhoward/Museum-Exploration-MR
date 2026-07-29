using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the MiniGames canvas menu:
/// - OnEnable: shows only the MinigameMenuPanel, hides game panels.
/// - Next / Previous buttons cycle through the games array (wraps around).
/// - Close button hides MiniGamesCanvas and re-activates the WristCanvas.
/// - Start button activates the currently selected game's panel.
/// </summary>
public class MiniGames : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Data
    // -------------------------------------------------------------------------

    [System.Serializable]
    public struct GameEntry
    {
        public string gameID;
        public string gameName;
    }

    [Header("Game List")]
    [Tooltip("Fill in each game's ID and display name. The order here is the order shown in the menu.")]
    public GameEntry[] games = new GameEntry[]
    {
        new GameEntry { gameID = "game_1", gameName = "Tebak Bayangan Artefak" },
        new GameEntry { gameID = "game_2", gameName = "Urutkan Proses Pembuatan Batik" },
        new GameEntry { gameID = "game_3", gameName = "Kuis Artefak" }
    };

    // -------------------------------------------------------------------------
    // UI References
    // -------------------------------------------------------------------------

    [Header("Menu Panel")]
    [Tooltip("The panel that contains the title, prev/next buttons, close and start.")]
    public GameObject minigameMenuPanel;

    [Header("Title")]
    [Tooltip("The TMP text in the centre that shows the current game name.")]
    public TMP_Text gameTitle;

    [Header("Navigation Buttons")]
    public Button previousButton;
    public Button nextButton;

    [Header("Action Buttons")]
    public Button closeButton;
    public Button startButton;

    [Header("Game Panels (assign in Inspector)")]
    [Tooltip("Panel that is activated for Game 1.")]
    public GameObject game1Panel;
    [Tooltip("Panel that is activated for Game 2.")]
    public GameObject game2Panel;
    [Tooltip("Panel that is activated for Game 3.")]
    public GameObject game3Panel;

    [Header("Canvases")]
    [Tooltip("The whole MiniGamesCanvas – this GameObject's root canvas. Closed by the Close button.")]
    public GameObject miniGamesCanvas;
    [Tooltip("The WristCanvas that is re-shown when the MiniGames canvas is closed.")]
    public GameObject wristCanvas;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private int currentIndex = 0;

    // -------------------------------------------------------------------------
    // Unity Messages
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Wire buttons in code so nothing is forgotten in the Inspector
        if (previousButton != null) previousButton.onClick.AddListener(OnPrevious);
        if (nextButton     != null) nextButton.onClick.AddListener(OnNext);
        if (closeButton    != null) closeButton.onClick.AddListener(OnClose);
        if (startButton    != null) startButton.onClick.AddListener(OnStart);
    }

    private void OnEnable()
    {
        // Snap the canvas in front of the player before showing anything
        PositionInFrontOfUser();

        // Show only the menu panel; hide all game panels
        SetMenuPanelVisible(true);
        HideAllGamePanels();

        // Reset to first game
        currentIndex = 0;
        RefreshTitle();

        Debug.Log("MiniGames: Menu opened.");
    }

    /// <summary>
    /// Snaps this GameObject 1 metre in front of the player camera, facing them.
    /// Mirrors the same pattern used by Artifact.cs.
    /// </summary>
    public void PositionInFrontOfUser()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;

        // Project camera forward onto the horizontal plane so the panel stays upright
        Vector3 forwardDir = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        if (forwardDir == Vector3.zero) forwardDir = Vector3.forward;

        transform.position = cam.position + forwardDir * 1.5f;

        // Rotate to face the player (panel's forward points toward camera)
        Vector3 toPlayer = cam.position - transform.position;
        toPlayer.y = 0;
        if (toPlayer != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(-toPlayer, Vector3.up);
    }

    // -------------------------------------------------------------------------
    // Button Handlers
    // -------------------------------------------------------------------------

    /// <summary>Cycle backward through the games array, wrapping around.</summary>
    public void OnPrevious()
    {
        if (games == null || games.Length == 0) return;

        currentIndex--;
        if (currentIndex < 0)
            currentIndex = games.Length - 1;   // wrap to last

        RefreshTitle();
        Debug.Log($"MiniGames: Navigated to index {currentIndex} – {games[currentIndex].gameName}");
    }

    /// <summary>Cycle forward through the games array, wrapping around.</summary>
    public void OnNext()
    {
        if (games == null || games.Length == 0) return;

        currentIndex++;
        if (currentIndex >= games.Length)
            currentIndex = 0;   // wrap to first

        RefreshTitle();
        Debug.Log($"MiniGames: Navigated to index {currentIndex} – {games[currentIndex].gameName}");
    }

    /// <summary>Hide the MiniGamesCanvas and reveal the WristCanvas.</summary>
    public void OnClose()
    {
        Debug.Log("MiniGames: Close button pressed.");

        if (miniGamesCanvas != null)
            miniGamesCanvas.SetActive(false);

        if (wristCanvas != null)
            wristCanvas.SetActive(true);
    }

    /// <summary>Activate the game panel that corresponds to the currently displayed game.</summary>
    public void OnStart()
    {
        if (games == null || games.Length == 0) return;

        string id = games[currentIndex].gameID;

        // Resolve the target panel first – if it isn't assigned, do nothing at all.
        GameObject targetPanel = id switch
        {
            "game_1" => game1Panel,
            "game_2" => game2Panel,
            "game_3" => game3Panel,
            _        => null
        };

        if (targetPanel == null) return;

        Debug.Log($"MiniGames: Starting game '{id}' – {games[currentIndex].gameName}");

        // Only now hide the menu and show the game panel
        SetMenuPanelVisible(false);
        HideAllGamePanels();
        targetPanel.SetActive(true);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void RefreshTitle()
    {
        if (gameTitle == null || games == null || games.Length == 0) return;
        gameTitle.text = games[currentIndex].gameName;
    }

    private void SetMenuPanelVisible(bool visible)
    {
        if (minigameMenuPanel != null)
            minigameMenuPanel.SetActive(visible);
    }

    private void HideAllGamePanels()
    {
        if (game1Panel != null) game1Panel.SetActive(false);
        if (game2Panel != null) game2Panel.SetActive(false);
        if (game3Panel != null) game3Panel.SetActive(false);
    }

    private void ActivatePanel(GameObject panel)
    {
        if (panel == null) return;
        panel.SetActive(true);
    }
}
