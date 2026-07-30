using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// Controls the LeaderboardPanel UI.
/// Fetches rankings for the selected game from Firebase Firestore REST API,
/// updates the top 3 podium entries and rank 4-6 (or more) row items,
/// and sets the subtitle to the active game name.
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
    public static LeaderboardPanel Instance { get; private set; }

    private const string DefaultFirestoreUrl = "https://firestore.googleapis.com/v1/projects/museum-mixed-reality-app/databases/(default)/documents";

    [System.Serializable]
    public class LeaderboardSlotUI
    {
        [Tooltip("Text displaying player name.")]
        public TMP_Text nameText;
        [Tooltip("Text displaying player score.")]
        public TMP_Text scoreText;
    }

    [System.Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public int score;
        public string gameID;
    }

    [Header("Firebase Config")]
    [Tooltip("Firestore REST API Documents Endpoint.")]
    public string firestoreUrl = DefaultFirestoreUrl;

    [Header("UI References (Assign in Inspector or auto-found by name)")]
    [Tooltip("Subtitle text under the 'Leaderboard' title, showing active game name.")]
    public TMP_Text subtitleText;

    [Tooltip("Button that returns to MiniGameMenuPanel.")]
    public Button closeButton;

    [Tooltip("Button at the bottom of panel (e.g. 'Return To Menu').")]
    public Button returnToMenuButton;

    [Header("Podium Rankings (1st, 2nd, 3rd place)")]
    [Tooltip("1st Place Slot (Center pedestal).")]
    public LeaderboardSlotUI firstPlaceSlot;

    [Tooltip("2nd Place Slot (Left pedestal).")]
    public LeaderboardSlotUI secondPlaceSlot;

    [Tooltip("3rd Place Slot (Right pedestal).")]
    public LeaderboardSlotUI thirdPlaceSlot;

    [Header("Row Rankings (Rank 4, 5, 6, etc.)")]
    [Tooltip("Rank 4, 5, 6... list row slots.")]
    public LeaderboardSlotUI[] rowSlots;

    // Active game ID and name
    private string currentGameId = "game_1";
    private string currentGameName = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);

        AutoWireUI();
    }

    private void OnEnable()
    {
        AutoWireUI();
    }

    /// <summary>
    /// Opens and loads the leaderboard for the specified game.
    /// </summary>
    public void LoadLeaderboard(string gameId, string gameTitleName)
    {
        currentGameId = string.IsNullOrEmpty(gameId) ? "game_1" : gameId;
        currentGameName = gameTitleName;

        // 1. Update Subtitle
        if (subtitleText != null)
        {
            subtitleText.text = string.IsNullOrEmpty(currentGameName) ? currentGameId : currentGameName;
        }

        // 2. Clear / Reset UI to '-' placeholders
        ResetUIPlaceholders();

        // 3. Fetch from Firebase Firestore
        StopAllCoroutines();
        StartCoroutine(FetchLeaderboardCoroutine());
    }

    /// <summary>
    /// Resets all podium and row text slots to '-' when loading or if no player exists.
    /// </summary>
    public void ResetUIPlaceholders()
    {
        SetSlotUI(firstPlaceSlot, "-", "-");
        SetSlotUI(secondPlaceSlot, "-", "-");
        SetSlotUI(thirdPlaceSlot, "-", "-");

        if (rowSlots != null)
        {
            for (int i = 0; i < rowSlots.Length; i++)
            {
                SetSlotUI(rowSlots[i], "-", "-");
            }
        }
    }

    private IEnumerator FetchLeaderboardCoroutine()
    {
        string baseUrl = string.IsNullOrEmpty(firestoreUrl) ? DefaultFirestoreUrl : firestoreUrl;
        
        // Try fetching game-specific collection first (leaderboard_{gameID})
        string primaryUrl = $"{baseUrl}/leaderboard_{currentGameId}";
        string fallbackUrl = $"{baseUrl}/leaderboard";

        List<LeaderboardEntry> fetchedEntries = new List<LeaderboardEntry>();

        using (UnityWebRequest req = UnityWebRequest.Get(primaryUrl))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                fetchedEntries = ParseFirestoreResponse(json, currentGameId);
            }
        }

        // If game-specific collection was empty or returned 0 entries, try the shared 'leaderboard' collection
        if (fetchedEntries.Count == 0)
        {
            using (UnityWebRequest reqFallback = UnityWebRequest.Get(fallbackUrl))
            {
                yield return reqFallback.SendWebRequest();

                if (reqFallback.result == UnityWebRequest.Result.Success)
                {
                    string json = reqFallback.downloadHandler.text;
                    fetchedEntries = ParseFirestoreResponse(json, currentGameId);
                }
            }
        }

        // Sort entries by score descending (highest score = 1st place)
        fetchedEntries.Sort((a, b) => b.score.CompareTo(a.score));

        // Display scores on UI
        DisplayLeaderboard(fetchedEntries);
    }

    private void DisplayLeaderboard(List<LeaderboardEntry> entries)
    {
        // 1st Place
        if (entries.Count > 0 && entries[0] != null)
            SetSlotUI(firstPlaceSlot, entries[0].name, entries[0].score.ToString());
        else
            SetSlotUI(firstPlaceSlot, "-", "-");

        // 2nd Place
        if (entries.Count > 1 && entries[1] != null)
            SetSlotUI(secondPlaceSlot, entries[1].name, entries[1].score.ToString());
        else
            SetSlotUI(secondPlaceSlot, "-", "-");

        // 3rd Place
        if (entries.Count > 2 && entries[2] != null)
            SetSlotUI(thirdPlaceSlot, entries[2].name, entries[2].score.ToString());
        else
            SetSlotUI(thirdPlaceSlot, "-", "-");

        // Ranks 4, 5, 6, etc.
        if (rowSlots != null)
        {
            for (int i = 0; i < rowSlots.Length; i++)
            {
                int entryIndex = i + 3; // 4th place is index 3
                if (entryIndex < entries.Count && entries[entryIndex] != null)
                {
                    SetSlotUI(rowSlots[i], entries[entryIndex].name, entries[entryIndex].score.ToString());
                }
                else
                {
                    SetSlotUI(rowSlots[i], "-", "-");
                }
            }
        }
    }

    private void SetSlotUI(LeaderboardSlotUI slot, string playerName, string scoreStr)
    {
        if (slot == null) return;
        if (slot.nameText != null) slot.nameText.text = playerName;
        if (slot.scoreText != null) slot.scoreText.text = scoreStr;
    }

    /// <summary>
    /// Robust Firestore REST JSON parser. Extracts name, score, and optional gameID fields.
    /// </summary>
    public static List<LeaderboardEntry> ParseFirestoreResponse(string json, string targetGameId)
    {
        List<LeaderboardEntry> results = new List<LeaderboardEntry>();
        if (string.IsNullOrEmpty(json) || json == "null" || json == "{}" || json == "[]") return results;

        // Split JSON by "fields" or document objects
        string[] docs = json.Split(new string[] { "\"fields\":" }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < docs.Length; i++)
        {
            string doc = docs[i];

            // 1. Extract Name
            string name = "";
            int nameIdx = doc.IndexOf("\"name\":");
            if (nameIdx != -1)
            {
                int strIdx = doc.IndexOf("\"stringValue\":", nameIdx);
                if (strIdx != -1)
                {
                    int start = doc.IndexOf("\"", strIdx + 14) + 1;
                    int end = doc.IndexOf("\"", start);
                    if (start > 0 && end > start)
                    {
                        name = doc.Substring(start, end - start);
                    }
                }
            }

            // 2. Extract Score (supports integerValue, doubleValue, stringValue)
            int score = 0;
            int scoreIdx = doc.IndexOf("\"score\":");
            if (scoreIdx == -1) scoreIdx = doc.IndexOf("\"time\":"); // Fallback check for time field

            if (scoreIdx != -1)
            {
                int valIdx = doc.IndexOf("\"integerValue\":", scoreIdx);
                int prefixLen = 15;
                if (valIdx == -1)
                {
                    valIdx = doc.IndexOf("\"doubleValue\":", scoreIdx);
                    prefixLen = 14;
                }
                if (valIdx == -1)
                {
                    valIdx = doc.IndexOf("\"stringValue\":", scoreIdx);
                    prefixLen = 14;
                }

                if (valIdx != -1)
                {
                    int start = valIdx + prefixLen;
                    while (start < doc.Length && (doc[start] == ' ' || doc[start] == '"' || doc[start] == ':')) start++;

                    int end = start;
                    while (end < doc.Length && (char.IsDigit(doc[end]) || doc[end] == '.' || doc[end] == '-')) end++;

                    if (end > start)
                    {
                        string scoreStr = doc.Substring(start, end - start);
                        if (float.TryParse(scoreStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedVal))
                        {
                            score = Mathf.RoundToInt(parsedVal);
                        }
                    }
                }
            }

            // 3. Extract Game ID (optional filter if entries come from a shared 'leaderboard' collection)
            string entryGameId = "";
            int gameIdIdx = doc.IndexOf("\"gameID\":");
            if (gameIdIdx == -1) gameIdIdx = doc.IndexOf("\"game_id\":");
            if (gameIdIdx != -1)
            {
                int strIdx = doc.IndexOf("\"stringValue\":", gameIdIdx);
                if (strIdx != -1)
                {
                    int start = doc.IndexOf("\"", strIdx + 14) + 1;
                    int end = doc.IndexOf("\"", start);
                    if (start > 0 && end > start)
                    {
                        entryGameId = doc.Substring(start, end - start);
                    }
                }
            }

            // Filter if gameID is specified in the doc and doesn't match target
            if (!string.IsNullOrEmpty(entryGameId) && !string.IsNullOrEmpty(targetGameId) && entryGameId != targetGameId)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(name))
            {
                results.Add(new LeaderboardEntry { name = name, score = score, gameID = entryGameId });
            }
        }

        return results;
    }

    /// <summary>
    /// Helper to post a player score to Firestore database.
    /// </summary>
    public static IEnumerator PostScore(string gameID, string playerName, int score, string firestoreUrl = DefaultFirestoreUrl)
    {
        if (string.IsNullOrEmpty(gameID)) yield break;

        string safeName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string json = $"{{\"fields\":{{\"name\":{{\"stringValue\":\"{safeName}\"}},\"score\":{{\"integerValue\":\"{score}\"}},\"gameID\":{{\"stringValue\":\"{gameID}\"}}}}}}";

        // Post to game specific collection
        string postUrl = $"{firestoreUrl}/leaderboard_{gameID}";

        using (UnityWebRequest req = new UnityWebRequest(postUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();
        }
    }

    public void OnClose()
    {
        gameObject.SetActive(false);
        if (MiniGames.Instance != null)
        {
            MiniGames.Instance.ShowMenuPanel();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auto-Wiring
    // ─────────────────────────────────────────────────────────────────────────

    private void AutoWireUI()
    {
        // Subtitle
        if (subtitleText == null)
        {
            subtitleText = FindChildTMP("SubtitleText", "Subtitle", "Text (TMP)", "GameTitle");
        }

        // Close Buttons
        if (closeButton == null) closeButton = FindChildButton("CloseButton", "ButtonClose", "CloseBtn");
        if (returnToMenuButton == null) returnToMenuButton = FindChildButton("Return To Menu", "ReturnToMenu", "ButtonReturn", "Mulai");

        WireButton(closeButton, OnClose);
        WireButton(returnToMenuButton, OnClose);

        // Auto-find podium slots if unassigned
        if (firstPlaceSlot == null || firstPlaceSlot.nameText == null)
        {
            firstPlaceSlot = FindSlotUI("1", "First", "Howard", "HowardText");
        }
        if (secondPlaceSlot == null || secondPlaceSlot.nameText == null)
        {
            secondPlaceSlot = FindSlotUI("2", "Second", "Nelsen", "NelsenText");
        }
        if (thirdPlaceSlot == null || thirdPlaceSlot.nameText == null)
        {
            thirdPlaceSlot = FindSlotUI("3", "Third", "Celyne", "CelyneText");
        }

        // Auto-find rows 4, 5, 6 if unassigned
        if (rowSlots == null || rowSlots.Length == 0)
        {
            List<LeaderboardSlotUI> foundRows = new List<LeaderboardSlotUI>();

            // Try to find Row4/Stephanie, Row5/Michael, Row6/Rayhan or 4, 5, 6
            LeaderboardSlotUI r4 = FindSlotUI("4", "Stephanie", "Row4", "Item4");
            LeaderboardSlotUI r5 = FindSlotUI("5", "Michael", "Row5", "Item5");
            LeaderboardSlotUI r6 = FindSlotUI("6", "Rayhan", "Row6", "Item6");

            if (r4 != null) foundRows.Add(r4);
            if (r5 != null) foundRows.Add(r5);
            if (r6 != null) foundRows.Add(r6);

            if (foundRows.Count > 0)
            {
                rowSlots = foundRows.ToArray();
            }
        }
    }

    private LeaderboardSlotUI FindSlotUI(params string[] searchKeywords)
    {
        foreach (string kw in searchKeywords)
        {
            Transform found = FindDeepChild(transform, kw);
            if (found != null)
            {
                LeaderboardSlotUI slot = new LeaderboardSlotUI();
                TMP_Text[] texts = found.GetComponentsInChildren<TMP_Text>(true);

                if (texts.Length >= 2)
                {
                    // If 2 texts: [0] = name, [1] = score
                    slot.nameText = texts[0];
                    slot.scoreText = texts[1];
                }
                else if (texts.Length == 1)
                {
                    slot.nameText = texts[0];
                }

                return slot;
            }
        }
        return null;
    }

    private static void WireButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);

        XRButtonSelection xr = btn.GetComponent<XRButtonSelection>();
        if (xr != null)
        {
            xr.onClick.RemoveAllListeners();
            xr.onClick.AddListener(action);
        }
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
        return null;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name.Equals(childName, StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }
}
