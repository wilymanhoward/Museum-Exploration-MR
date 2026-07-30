using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles the MiniGameMenuPanel UI: game title display, previous/next cycling,
/// start, list (expand), and close buttons.
/// Attach this script to the MiniGameMenuPanel GameObject.
/// </summary>
public class MiniGameMenuPanel : MonoBehaviour
{
    public static MiniGameMenuPanel Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Data
    // ─────────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public struct GameEntry
    {
        [Tooltip("Unique identifier, e.g. 'game_1'. Used by the wrist-path GameListMenu.")]
        public string gameID;
        [Tooltip("Display name shown in the title and game list.")]
        public string gameName;
        [Tooltip("The panel GameObject activated when this game is started. " +
                 "Leave empty to stay in the menu (no-op start).")]
        public GameObject gamePanel;
    }

    [Header("Games")]
    public GameEntry[] games = new GameEntry[]
    {
        new GameEntry { gameID = "game_1", gameName = "Tebak Bayangan Artefak" },
        new GameEntry { gameID = "game_2", gameName = "Urutkan Proses Pembuatan Batik" },
        new GameEntry { gameID = "game_3", gameName = "Kuis Artefak" }
    };

    // ─────────────────────────────────────────────────────────────────────────
    // UI References  (assign in Inspector or auto-wired by name)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("UI References (auto-wired by name if left empty)")]
    [Tooltip("TMP text that shows the current game name.")]
    public TMP_Text gameTitle;

    [Tooltip("Cycles to the previous game.")]
    public Button previousButton;

    [Tooltip("Cycles to the next game.")]
    public Button nextButton;

    [Tooltip("Starts the currently selected game.")]
    public Button startButton;

    [Tooltip("Opens the LeaderboardPanel for the current game.")]
    public Button leaderboardButton;

    [Tooltip("Opens the GameListPanel.")]
    public Button listButton;

    [Tooltip("Closes the MiniGamesCanvas and restores the wrist watch.")]
    public Button closeButton;

    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    private int currentIndex = 0;
    public int CurrentIndex => currentIndex;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        AutoFindButtons();
        WireButtons();
    }

    private void OnEnable()
    {
        if (games != null && games.Length > 0)
        {
            currentIndex = Mathf.Clamp(currentIndex, 0, games.Length - 1);
        }
        RefreshTitle();
        EnsureLeaderboardButtonVisible();
    }

    private void EnsureLeaderboardButtonVisible()
    {
        if (leaderboardButton == null)
            leaderboardButton = FindChildButton("LeaderboardButton", "ButtonLeaderboard", "Leaderboard");

        if (leaderboardButton != null && !leaderboardButton.gameObject.activeSelf)
        {
            leaderboardButton.gameObject.SetActive(true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Cycle to the previous game, wrapping from first → last.</summary>
    public void OnPrevious()
    {
        if (games == null || games.Length == 0) return;
        currentIndex = (currentIndex - 1 + games.Length) % games.Length;
        RefreshTitle();
        Debug.Log($"MiniGameMenuPanel: ← {games[currentIndex].gameName}");
    }

    /// <summary>Cycle to the next game, wrapping from last → first.</summary>
    public void OnNext()
    {
        if (games == null || games.Length == 0) return;
        currentIndex = (currentIndex + 1) % games.Length;
        RefreshTitle();
        Debug.Log($"MiniGameMenuPanel: → {games[currentIndex].gameName}");
    }

    /// <summary>
    /// Start the currently selected game.
    /// If no panel is assigned for the selection, stays in the menu.
    /// </summary>
    public void StartSelected()
    {
        if (games == null || games.Length == 0) return;
        MiniGames.Instance?.StartGame(games[currentIndex].gamePanel);
    }

    /// <summary>Tell MiniGames to show the LeaderboardPanel for the active game selection.</summary>
    public void ShowLeaderboard()
    {
        if (games == null || games.Length == 0) return;
        string gameId = games[currentIndex].gameID;
        string gameName = games[currentIndex].gameName;
        MiniGames.Instance?.ShowLeaderboardPanel(gameId, gameName);
    }

    /// <summary>Tell MiniGames to show the GameListPanel.</summary>
    public void ShowList()
    {
        MiniGames.Instance?.ShowGameListPanel();
    }

    /// <summary>Tell MiniGames to hide the entire canvas and restore the wrist watch.</summary>
    public void Close()
    {
        MiniGames.Instance?.CloseCanvas();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API (used by MiniGameListPanel and the wrist-path StartGame shim)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Select a game by its index in the games array and refresh the title.</summary>
    public void SelectGame(int index)
    {
        if (games == null || index < 0 || index >= games.Length) return;
        currentIndex = index;
        RefreshTitle();
    }

    /// <summary>Select a game by its gameID string and refresh the title.</summary>
    public void SelectGameById(string gameId)
    {
        if (games == null || string.IsNullOrEmpty(gameId)) return;
        for (int i = 0; i < games.Length; i++)
        {
            if (games[i].gameID == gameId)
            {
                currentIndex = i;
                RefreshTitle();
                return;
            }
        }
        Debug.LogWarning($"MiniGameMenuPanel: No game found with ID '{gameId}'.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshTitle()
    {
        if (gameTitle == null || games == null || games.Length == 0) return;
        gameTitle.text = games[currentIndex].gameName;
        gameTitle.enableAutoSizing = true;
        gameTitle.fontSizeMin = 10f;
        gameTitle.fontSizeMax = 24f;
        gameTitle.enableWordWrapping = true;
        gameTitle.overflowMode = TextOverflowModes.Ellipsis;
        gameTitle.alignment = TextAlignmentOptions.Center;
    }

    private void AutoFindButtons()
    {
        // Try to find buttons by common child names if not assigned in Inspector
        if (gameTitle         == null) gameTitle         = FindChildTMP("GameTitle", "TitleText", "GameTitleText");
        if (previousButton    == null) previousButton    = FindChildButton("PreviousButton", "PrevButton", "ButtonPrev", "LeftArrow");
        if (nextButton        == null) nextButton        = FindChildButton("NextButton",     "ButtonNext",  "RightArrow");
        if (startButton       == null) startButton       = FindChildButton("StartButton",    "ButtonStart", "Mulai", "MulaiButton");
        if (leaderboardButton == null) leaderboardButton = FindChildButton("LeaderboardButton", "ButtonLeaderboard", "Leaderboard");
        if (listButton        == null) listButton        = FindChildButton("ListButton",     "ExpandButton","ButtonList","ButtonExpand");
        if (closeButton       == null) closeButton       = FindChildButton("CloseButton",    "ButtonClose", "CloseBtn");
    }

    private void WireButtons()
    {
        WireButton(previousButton?.gameObject,    OnPrevious);
        WireButton(nextButton?.gameObject,        OnNext);
        WireButton(startButton?.gameObject,       StartSelected);
        WireButton(leaderboardButton?.gameObject, ShowLeaderboard);
        WireButton(listButton?.gameObject,        ShowList);
        WireButton(closeButton?.gameObject,       Close);
    }

    private static void WireButton(GameObject obj, UnityEngine.Events.UnityAction action)
    {
        if (obj == null) return;
        Button btn = obj.GetComponent<Button>();
        if (btn != null) { btn.onClick.RemoveAllListeners(); btn.onClick.AddListener(action); }
        XRButtonSelection xr = obj.GetComponent<XRButtonSelection>();
        if (xr != null) { xr.onClick.RemoveAllListeners(); xr.onClick.AddListener(action); }
    }

    private Button FindChildButton(params string[] names)
    {
        foreach (string n in names)
        {
            Transform t = FindDeepChild(transform, n);
            if (t != null)
            {
                Button b = t.GetComponent<Button>() ?? t.GetComponentInChildren<Button>(true);
                if (b != null) return b;
            }
        }
        return null;
    }

    private TMP_Text FindChildTMP(params string[] names)
    {
        foreach (string n in names)
        {
            Transform t = FindDeepChild(transform, n);
            if (t != null)
            {
                TMP_Text txt = t.GetComponent<TMP_Text>();
                if (txt != null) return txt;
            }
        }
        return GetComponentInChildren<TMP_Text>(true);
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name == childName) return t;
        return null;
    }
}
