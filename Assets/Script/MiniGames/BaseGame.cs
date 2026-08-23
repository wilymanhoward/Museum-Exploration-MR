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
            timerText.text = $"{mins:D2}:{secs:D2}";
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

    private static Sprite cachedStopwatchSprite;
    private static Sprite cachedCapsuleSprite;

    private static Sprite GetOrCreateStopwatchSprite()
    {
        if (cachedStopwatchSprite != null) return cachedStopwatchSprite;

#if UNITY_EDITOR
        Sprite loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/128/PictoIcon_Stopwatch_1.Png");
        if (loaded != null)
        {
            cachedStopwatchSprite = loaded;
            return cachedStopwatchSprite;
        }
#endif

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(32f, 28f);
        float outerRadius = 22f;
        float innerRadius = 17f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float dist = Vector2.Distance(new Vector2(px, py), center);

                float alpha = 0f;

                // Main circular body ring
                if (dist <= outerRadius + 0.5f && dist >= innerRadius - 0.5f)
                {
                    float ringAlpha = 1f;
                    if (dist > outerRadius - 0.5f) ringAlpha = Mathf.Clamp01(outerRadius + 0.5f - dist);
                    else if (dist < innerRadius + 0.5f) ringAlpha = Mathf.Clamp01(dist - (innerRadius - 0.5f));
                    alpha = Mathf.Max(alpha, ringAlpha);
                }

                // Top button stem & cap
                if (px >= 30f && px <= 34f && py >= 49f && py <= 56f) alpha = Mathf.Max(alpha, 1f);
                if (px >= 26f && px <= 38f && py >= 56f && py <= 60f) alpha = Mathf.Max(alpha, 1f);

                // Top-right side pusher
                Vector2 sideBtnCenter = center + new Vector2(15f, 15f);
                if (Vector2.Distance(new Vector2(px, py), sideBtnCenter) <= 4.5f) alpha = Mathf.Max(alpha, 1f);

                // Center hub
                if (dist <= 3.5f) alpha = Mathf.Max(alpha, 1f);

                // Stopwatch hand pointing at ~2 o'clock
                Vector2 handDir = new Vector2(px, py) - center;
                float handDist = handDir.magnitude;
                if (handDist <= 13f && handDist >= 2f)
                {
                    float angle = Mathf.Atan2(handDir.y, handDir.x) * Mathf.Rad2Deg;
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, 60f)) < 12f) alpha = Mathf.Max(alpha, 1f);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedStopwatchSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedStopwatchSprite;
    }

    private static Sprite GetOrCreateCapsuleSprite()
    {
        if (cachedCapsuleSprite != null) return cachedCapsuleSprite;

        int size = 64;
        float cornerRadius = 24f;
        float halfSize = size / 2f;
        float innerHalf = halfSize - cornerRadius;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs((x + 0.5f) - halfSize);
                float py = Mathf.Abs((y + 0.5f) - halfSize);

                float dx = Mathf.Max(px - innerHalf, 0f);
                float dy = Mathf.Max(py - innerHalf, 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(cornerRadius - dist + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        Vector4 border = new Vector4(24, 24, 24, 24);
        cachedCapsuleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        return cachedCapsuleSprite;
    }

    private void EnsureTimerUI()
    {
        if (timerText != null) return;

        // 1. Try finding existing timer text in hierarchy (if already wired or built)
        string[] candidates = { "TimerText", "Timer", "TimeText", "TimeDisplay", "Waktu" };
        foreach (string n in candidates)
        {
            Transform found = FindDeepChild(transform, n);
            if (found != null && found.name != "TimerBadge")
            {
                timerText = found.GetComponent<TMPro.TMP_Text>();
                if (timerText != null) return;
            }
        }

        // Check if TimerBadge was already built
        Transform existingBadge = FindDeepChild(transform, "TimerBadge");
        if (existingBadge != null)
        {
            timerText = existingBadge.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (timerText != null) return;
        }

        // 2. Dynamically create a sleek TimerBadge at top of panel with dedicated Stopwatch Icon
        GameObject badgeObj = new GameObject("TimerBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgeObj.transform.SetParent(transform, false);

        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 1f);
        badgeRect.anchorMax = new Vector2(0.5f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0f, -12f);
        badgeRect.sizeDelta = new Vector2(115f, 32f);

        Image bgImage = badgeObj.GetComponent<Image>();
        bgImage.sprite = GetOrCreateCapsuleSprite();
        bgImage.type = Image.Type.Sliced;
        bgImage.color = new Color(0.05f, 0.08f, 0.14f, 0.90f);
        bgImage.raycastTarget = false;

        // Create Stopwatch Icon Image
        GameObject iconObj = new GameObject("TimerIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObj.transform.SetParent(badgeObj.transform, false);

        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(12f, 0f);
        iconRect.sizeDelta = new Vector2(18f, 18f);

        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = GetOrCreateStopwatchSprite();
        iconImg.color = new Color(1f, 0.835f, 0.31f, 1f); // Warm Amber Gold (#FFD54F)
        iconImg.raycastTarget = false;

        // Create TextMeshProUGUI inside badge
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        textObj.transform.SetParent(badgeObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(36f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        TMPro.TextMeshProUGUI tmp = textObj.GetComponent<TMPro.TextMeshProUGUI>();
        tmp.fontSize = 15;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
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
