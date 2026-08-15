using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base class for all mini-game panels (Game1Panel, Game2Panel, Game3Panel, …).
/// Provides game timer tracking, leaderboard presentation on completion, and a Close button that returns the player to MiniGameMenuPanel.
/// </summary>
public class BaseGame : MonoBehaviour
{
    [Header("Close Button (auto-found by name if left empty)")]
    [Tooltip("Button that returns the player to the mini-game menu.")]
    public Button closeButton;

    [Header("Timer UI (auto-created or auto-found by name)")]
    [Tooltip("TMP_Text displaying the live elapsed time during gameplay.")]
    public TMPro.TMP_Text timerText;

    protected float gameStartTime = 0f;
    protected bool isTimerRunning = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        ResolveCloseButton();
        WireCloseButton();
        EnsureTimerUI();
    }

    protected virtual void OnEnable()
    {
        OnGameStart();
    }

    protected virtual void OnDisable()
    {
        OnGameEnd();
    }

    protected virtual void Update()
    {
        if (isTimerRunning)
        {
            UpdateTimerDisplay();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Virtual game lifecycle – override in subclasses
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when this game panel is activated (OnEnable).
    /// Override to reset and initialise your game state.
    /// </summary>
    public virtual void OnGameStart()
    {
        gameStartTime = Time.time;
        isTimerRunning = true;
        EnsureTimerUI();
        UpdateTimerDisplay();
        Debug.Log($"{GetType().Name}: OnGameStart at t={gameStartTime}");
    }

    /// <summary>
    /// Called when this game panel is deactivated (OnDisable).
    /// Override to clean up timers, spawned objects, etc.
    /// </summary>
    public virtual void OnGameEnd()
    {
        isTimerRunning = false;
        Debug.Log($"{GetType().Name}: OnGameEnd");
    }

    public float GetElapsedTimeSeconds()
    {
        return Mathf.Max(0f, Time.time - gameStartTime);
    }

    protected void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            float elapsed = GetElapsedTimeSeconds();
            int mins = Mathf.FloorToInt(elapsed / 60f);
            int secs = Mathf.FloorToInt(elapsed % 60f);
            timerText.text = $"<color=#FFD54F>⏱</color> {mins:D2}:{secs:D2}";
        }
    }

    /// <summary>
    /// Call when a mini-game is completed. Saves player completion time and opens the LeaderboardPanel for this game.
    /// </summary>
    public void FinishGameAndShowLeaderboard(string gameId, string gameName)
    {
        float totalTime = GetElapsedTimeSeconds();
        Debug.Log($"{GetType().Name}: Completed '{gameName}' ({gameId}) in {totalTime:F2} seconds!");

        // 1. Save Player Completion Time
        string playerName = PlayerPrefs.GetString("PlayerName", "Pengunjung");
        if (string.IsNullOrWhiteSpace(playerName)) playerName = "Pengunjung";
        LeaderboardPanel.SavePlayerTime(gameId, playerName, totalTime);

        // 2. Hide game panel & open Leaderboard panel
        gameObject.SetActive(false);

        if (MiniGames.Instance != null)
        {
            MiniGames.Instance.ShowLeaderboardPanel(gameId, gameName);
        }
        else
        {
            LeaderboardPanel lb = FindObjectOfType<LeaderboardPanel>(true);
            if (lb != null)
            {
                lb.gameObject.SetActive(true);
                lb.LoadLeaderboard(gameId, gameName);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Close / Back
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deactivates this game panel and returns to the mini-game menu.
    /// </summary>
    public virtual void OnClose()
    {
        Debug.Log($"{GetType().Name}: Closed – returning to menu.");
        gameObject.SetActive(false);           // triggers OnDisable → OnGameEnd
        MiniGames.Instance?.ShowMenuPanel();   // re-activates MiniGameMenuPanel
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ResolveCloseButton()
    {
        if (closeButton != null) return;

        // Search by common names
        string[] candidateNames = { "CloseButton", "ButtonClose", "BackButton", "ButtonBack", "CloseBtn" };
        foreach (string n in candidateNames)
        {
            Transform tf = FindDeepChild(transform, n);
            if (tf != null)
            {
                closeButton = tf.GetComponent<Button>();
                if (closeButton != null) return;
            }
        }
    }

    private void WireCloseButton()
    {
        if (closeButton == null) return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(OnClose);

        // Also wire XRButtonSelection so hand-ray / poke works
        XRButtonSelection xr = closeButton.GetComponent<XRButtonSelection>();
        if (xr != null)
        {
            xr.onClick.RemoveAllListeners();
            xr.onClick.AddListener(OnClose);
        }
    }

    private void EnsureTimerUI()
    {
        if (timerText != null) return;

        // 1. Try finding existing timer text in hierarchy
        string[] candidates = { "TimerText", "Timer", "TimeText", "TimeDisplay", "Waktu", "TimerBadge" };
        foreach (string n in candidates)
        {
            Transform found = FindDeepChild(transform, n);
            if (found != null)
            {
                timerText = found.GetComponent<TMPro.TMP_Text>();
                if (timerText != null) return;
            }
        }

        // 2. Dynamically create a sleek TimerBadge at top of panel
        GameObject badgeObj = new GameObject("TimerBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgeObj.transform.SetParent(transform, false);

        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 1f);
        badgeRect.anchorMax = new Vector2(0.5f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0f, -12f);
        badgeRect.sizeDelta = new Vector2(105f, 30f);

        Image bgImage = badgeObj.GetComponent<Image>();
        bgImage.color = new Color(0.06f, 0.09f, 0.14f, 0.85f);
        bgImage.raycastTarget = false;

        // Create TextMeshProUGUI inside badge
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        textObj.transform.SetParent(badgeObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        TMPro.TextMeshProUGUI tmp = textObj.GetComponent<TMPro.TextMeshProUGUI>();
        tmp.fontSize = 15;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        // Borrow font asset from an existing TMP in this panel
        TMPro.TMP_Text sampleText = GetComponentInChildren<TMPro.TMP_Text>(true);
        if (sampleText != null && sampleText.font != null)
        {
            tmp.font = sampleText.font;
        }

        timerText = tmp;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name == childName) return t;
        return null;
    }
}
