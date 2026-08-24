using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// Controls the LeaderboardPanel UI.
/// Supports game-specific local high scores (PlayerPrefs) and Firebase Firestore REST API rankings.
/// Compares player completion times (fastest time = 1st place).
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
        [Tooltip("Text displaying player score/time.")]
        public TMP_Text scoreText;
    }

    [System.Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public float timeSeconds;
        public string formattedScore;
        public string gameID;
    }

    [System.Serializable]
    public class LocalEntry
    {
        public string name;
        public float timeSeconds;
    }

    [System.Serializable]
    private class LocalEntryListWrapper
    {
        public List<LocalEntry> entries = new List<LocalEntry>();
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

    private static string pendingGameId = null;
    private static string pendingPlayerName = null;
    private static int pendingScoreSeconds = 0;

    /// <summary>
    /// Opens and loads the leaderboard for the specified game.
    /// </summary>
    public void LoadLeaderboard(string gameId, string gameTitleName)
    {
        currentGameId = string.IsNullOrEmpty(gameId) ? "game_1" : gameId;
        currentGameName = gameTitleName;

        AutoWireUI();

        // 1. Update Subtitle
        if (subtitleText != null)
        {
            subtitleText.text = string.IsNullOrEmpty(currentGameName) ? currentGameId : currentGameName;
        }

        // 2. Clear UI placeholders to '-' immediately
        ResetUIPlaceholders();

        // 3. Immediately display local entries so player sees their score instantly!
        List<LeaderboardEntry> localDisplayEntries = GetLocalDisplayEntries(currentGameId);
        if (localDisplayEntries.Count > 0)
        {
            DisplayLeaderboard(localDisplayEntries);
        }

        // 4. Start Post & Fetch coroutine (StopAllCoroutines runs BEFORE starting the post, so it is never aborted!)
        StopAllCoroutines();
        StartCoroutine(PostAndFetchLeaderboardCoroutine());
    }

    /// <summary>
    /// Saves a player's completion time for a mini-game and updates the leaderboard.
    /// </summary>
    public static void SavePlayerTime(string gameId, string playerName, float timeInSeconds)
    {
        if (string.IsNullOrEmpty(gameId) || timeInSeconds <= 0f) return;

        string cleanGameId = gameId.Trim().ToLower();
        string key = $"LocalLeaderboard_{cleanGameId}";

        List<LocalEntry> entries = LoadLocalEntries(cleanGameId);

        // Resolve real player name (prefer user-entered name, never "Anda")
        string customName = PlayerPrefs.GetString("PlayerName", "").Trim();
        string resolvedName = playerName;
        if (string.IsNullOrWhiteSpace(resolvedName) || resolvedName.Equals("Anda", StringComparison.OrdinalIgnoreCase) || resolvedName.Equals("Pengunjung", StringComparison.OrdinalIgnoreCase))
        {
            resolvedName = !string.IsNullOrWhiteSpace(customName) ? customName : "Howard";
        }

        LocalEntry newEntry = new LocalEntry
        {
            name = resolvedName,
            timeSeconds = timeInSeconds
        };

        // Remove any old "Anda" placeholder entries
        entries.RemoveAll(e => e == null || string.IsNullOrWhiteSpace(e.name) || e.name.Trim().Equals("Anda", StringComparison.OrdinalIgnoreCase));

        entries.Add(newEntry);
        // Sort by fastest time ascending (lowest time = 1st place)
        entries.Sort((a, b) => a.timeSeconds.CompareTo(b.timeSeconds));

        if (entries.Count > 10) entries.RemoveRange(10, entries.Count - 10);

        // Save to PlayerPrefs
        LocalEntryListWrapper wrapper = new LocalEntryListWrapper { entries = entries };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();

        // Stage this score to be posted to Firestore when the leaderboard panel opens
        pendingGameId = cleanGameId;
        pendingPlayerName = resolvedName;
        pendingScoreSeconds = Mathf.RoundToInt(timeInSeconds);

        Debug.Log($"[Leaderboard] Saved local time for '{cleanGameId}': {resolvedName} - {FormatTime(timeInSeconds)} (Staged for Firestore sync)");
    }

    private static List<LocalEntry> LoadLocalEntries(string gameId)
    {
        string cleanGameId = (gameId ?? "").Trim().ToLower();
        string key = $"LocalLeaderboard_{cleanGameId}";
        string json = PlayerPrefs.GetString(key, "");

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                LocalEntryListWrapper wrapper = JsonUtility.FromJson<LocalEntryListWrapper>(json);
                if (wrapper != null && wrapper.entries != null && wrapper.entries.Count > 0)
                {
                    // Filter out any legacy "Anda" entries
                    wrapper.entries.RemoveAll(e => e == null || string.IsNullOrWhiteSpace(e.name) || e.name.Trim().Equals("Anda", StringComparison.OrdinalIgnoreCase));
                    return wrapper.entries;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Leaderboard] Failed to parse local PlayerPrefs JSON: {ex.Message}");
            }
        }

        // Return empty list if no local entries exist
        return new List<LocalEntry>();
    }

    private static List<LeaderboardEntry> GetLocalDisplayEntries(string gameId)
    {
        List<LocalEntry> local = LoadLocalEntries(gameId);
        List<LeaderboardEntry> display = new List<LeaderboardEntry>();

        foreach (LocalEntry e in local)
        {
            if (e != null && !string.IsNullOrWhiteSpace(e.name) && !e.name.Trim().Equals("Anda", StringComparison.OrdinalIgnoreCase))
            {
                display.Add(new LeaderboardEntry
                {
                    name = e.name,
                    timeSeconds = e.timeSeconds,
                    formattedScore = FormatTime(e.timeSeconds),
                    gameID = gameId
                });
            }
        }

        return display;
    }

    public static string FormatTime(float totalSeconds)
    {
        if (totalSeconds <= 0f) return "-";
        int mins = Mathf.FloorToInt(totalSeconds / 60f);
        int secs = Mathf.FloorToInt(totalSeconds % 60f);
        if (mins > 0)
        {
            return $"{mins:D2}:{secs:D2}m";
        }
        return $"{secs:D2}s";
    }

    /// <summary>
    /// Resets all podium and row text slots to '-' when loading.
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

    public static string GetFirestoreCollectionName(string gameId)
    {
        string clean = (gameId ?? "game_1").Trim().ToLower();
        if (clean == "game_1" || clean.Contains("1") || clean.Contains("guess") || clean.Contains("bayangan") || clean.Contains("tebak"))
            return "leaderboard_game_1";
        if (clean == "game_2" || clean.Contains("2") || clean.Contains("batik") || clean.Contains("proses") || clean.Contains("susun"))
            return "leaderboard_game_2";
        if (clean == "game_3" || clean.Contains("3") || clean.Contains("timeline") || clean.Contains("sejarah") || clean.Contains("kuis") || clean.Contains("artefak"))
            return "leaderboard_game_3";

        return $"leaderboard_{clean}";
    }

    private IEnumerator PostAndFetchLeaderboardCoroutine()
    {
        string baseUrl = string.IsNullOrEmpty(firestoreUrl) ? DefaultFirestoreUrl : firestoreUrl;
        string collectionName = GetFirestoreCollectionName(currentGameId);
        string collectionUrl = $"{baseUrl}/{collectionName}";

        // 1. If there is a pending score to post for this game, post it now!
        if (!string.IsNullOrEmpty(pendingGameId) && pendingGameId == currentGameId && pendingScoreSeconds > 0)
        {
            string pName = pendingPlayerName;
            int pScore = pendingScoreSeconds;
            pendingGameId = null;
            pendingPlayerName = null;
            pendingScoreSeconds = 0;

            yield return PostScore(currentGameId, pName, pScore, baseUrl);
        }

        // 2. Fetch latest records from Firestore
        List<LeaderboardEntry> fetchedEntries = new List<LeaderboardEntry>();

        using (UnityWebRequest req = UnityWebRequest.Get(collectionUrl))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                fetchedEntries = ParseFirestoreResponse(json, currentGameId);
            }
            else
            {
                Debug.Log($"[Leaderboard] Firestore GET ({collectionName}) status: {req.result} ({req.responseCode})");
            }
        }

        // 3. Filter out any "Anda" documents from Firestore results
        fetchedEntries.RemoveAll(fe => fe == null || string.IsNullOrWhiteSpace(fe.name) || fe.name.Trim().Equals("Anda", StringComparison.OrdinalIgnoreCase));

        // 4. Merge fetched Firestore entries with local entries
        List<LeaderboardEntry> finalEntries = new List<LeaderboardEntry>();
        HashSet<string> seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (LeaderboardEntry fe in fetchedEntries)
        {
            if (fe != null && !string.IsNullOrWhiteSpace(fe.name) && fe.timeSeconds > 0f)
            {
                string key = $"{fe.name.Trim().ToLower()}_{Mathf.RoundToInt(fe.timeSeconds)}";
                if (!seenKeys.Contains(key))
                {
                    seenKeys.Add(key);
                    finalEntries.Add(fe);
                }
            }
        }

        List<LeaderboardEntry> localDisplay = GetLocalDisplayEntries(currentGameId);
        foreach (LeaderboardEntry le in localDisplay)
        {
            if (le != null && !string.IsNullOrWhiteSpace(le.name) && le.timeSeconds > 0f)
            {
                string key = $"{le.name.Trim().ToLower()}_{Mathf.RoundToInt(le.timeSeconds)}";
                if (!seenKeys.Contains(key))
                {
                    seenKeys.Add(key);
                    finalEntries.Add(le);
                }
            }
        }

        // Sort by fastest completion time ascending (lowest score/time = 1st place)
        finalEntries.Sort((a, b) => a.timeSeconds.CompareTo(b.timeSeconds));
        DisplayLeaderboard(finalEntries);
    }

    private void DisplayLeaderboard(List<LeaderboardEntry> entries)
    {
        // 1st Place
        if (entries.Count > 0 && entries[0] != null)
            SetSlotUI(firstPlaceSlot, entries[0].name, GetFormattedOrScore(entries[0]));
        else
            SetSlotUI(firstPlaceSlot, "-", "-");

        // 2nd Place
        if (entries.Count > 1 && entries[1] != null)
            SetSlotUI(secondPlaceSlot, entries[1].name, GetFormattedOrScore(entries[1]));
        else
            SetSlotUI(secondPlaceSlot, "-", "-");

        // 3rd Place
        if (entries.Count > 2 && entries[2] != null)
            SetSlotUI(thirdPlaceSlot, entries[2].name, GetFormattedOrScore(entries[2]));
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
                    SetSlotUI(rowSlots[i], entries[entryIndex].name, GetFormattedOrScore(entries[entryIndex]));
                }
                else
                {
                    SetSlotUI(rowSlots[i], "-", "-");
                }
            }
        }
    }

    private string GetFormattedOrScore(LeaderboardEntry entry)
    {
        if (entry == null) return "-";
        if (!string.IsNullOrEmpty(entry.formattedScore)) return entry.formattedScore;
        return FormatTime(entry.timeSeconds);
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

            // 2. Extract Score (time in seconds)
            float timeSec = 0f;
            int scoreIdx = doc.IndexOf("\"score\":");
            if (scoreIdx == -1) scoreIdx = doc.IndexOf("\"time\":");

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
                            timeSec = parsedVal;
                        }
                    }
                }
            }

            // 3. Extract Game ID
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

            if (!string.IsNullOrEmpty(entryGameId) && !string.IsNullOrEmpty(targetGameId) && entryGameId != targetGameId)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(name) && timeSec > 0f)
            {
                results.Add(new LeaderboardEntry 
                { 
                    name = name, 
                    timeSeconds = timeSec,
                    formattedScore = FormatTime(timeSec),
                    gameID = entryGameId 
                });
            }
        }

        return results;
    }

    public static IEnumerator PostScore(string gameID, string playerName, int score, string firestoreUrl = DefaultFirestoreUrl)
    {
        if (string.IsNullOrEmpty(gameID) || score <= 0) yield break;

        string customName = PlayerPrefs.GetString("PlayerName", "").Trim();
        string resolvedName = playerName;
        if (string.IsNullOrWhiteSpace(resolvedName) || resolvedName.Equals("Anda", StringComparison.OrdinalIgnoreCase) || resolvedName.Equals("Pengunjung", StringComparison.OrdinalIgnoreCase))
        {
            resolvedName = !string.IsNullOrWhiteSpace(customName) ? customName : "Howard";
        }

        string collectionName = GetFirestoreCollectionName(gameID);
        string safeName = resolvedName.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string json = $"{{\"fields\":{{\"name\":{{\"stringValue\":\"{safeName}\"}},\"score\":{{\"integerValue\":\"{score}\"}},\"gameID\":{{\"stringValue\":\"{gameID}\"}}}}}}";

        string postUrl = $"{firestoreUrl}/{collectionName}";

        using (UnityWebRequest req = new UnityWebRequest(postUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 5;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Leaderboard] Failed to post score to {collectionName}: {req.error}");
            }
            else
            {
                Debug.Log($"[Leaderboard] Successfully posted score to {collectionName} for {playerName}: {score}s");
            }
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

        // Auto-find podium slots
        if (firstPlaceSlot == null || firstPlaceSlot.nameText == null || firstPlaceSlot.scoreText == null)
        {
            firstPlaceSlot = FindSlotUI("Rank1", "1", "First", "Howard", "HowardText");
        }
        if (secondPlaceSlot == null || secondPlaceSlot.nameText == null || secondPlaceSlot.scoreText == null || (firstPlaceSlot != null && secondPlaceSlot.nameText == firstPlaceSlot.nameText))
        {
            secondPlaceSlot = FindSlotUI("Rank2", "2", "Second", "Nelsen", "NelsenText");
        }
        if (thirdPlaceSlot == null || thirdPlaceSlot.nameText == null || thirdPlaceSlot.scoreText == null || (secondPlaceSlot != null && thirdPlaceSlot.nameText == secondPlaceSlot.nameText) || (firstPlaceSlot != null && thirdPlaceSlot.nameText == firstPlaceSlot.nameText))
        {
            thirdPlaceSlot = FindSlotUI("Rank3", "3", "Third", "Celyne", "CelyneText");
        }

        // Auto-find rows 4, 5, 6
        if (rowSlots == null || rowSlots.Length == 0)
        {
            List<LeaderboardSlotUI> foundRows = new List<LeaderboardSlotUI>();

            LeaderboardSlotUI r4 = FindSlotUI("Rank4", "4", "Stephanie", "Row4", "Item4");
            LeaderboardSlotUI r5 = FindSlotUI("Rank5", "5", "Michael", "Row5", "Item5");
            LeaderboardSlotUI r6 = FindSlotUI("Rank6", "6", "Rayhan", "Row6", "Item6");

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

                // Find WinnerName / Name text
                Transform nameChild = FindDeepChild(found, "WinnerName") ?? FindDeepChild(found, "Name") ?? FindDeepChild(found, "PlayerName") ?? FindDeepChild(found, "Player");
                if (nameChild != null) slot.nameText = nameChild.GetComponent<TMP_Text>();

                // Find Score / Time text
                Transform scoreChild = FindDeepChild(found, "Score") ?? FindDeepChild(found, "Time") ?? FindDeepChild(found, "ScoreText") ?? FindDeepChild(found, "TimeText");
                if (scoreChild != null) slot.scoreText = scoreChild.GetComponent<TMP_Text>();

                // Fallback to GetComponentsInChildren if named children not found
                if (slot.nameText == null || slot.scoreText == null)
                {
                    TMP_Text[] texts = found.GetComponentsInChildren<TMP_Text>(true);
                    if (texts.Length >= 2)
                    {
                        if (slot.nameText == null) slot.nameText = texts[0];
                        if (slot.scoreText == null) slot.scoreText = texts[1];
                    }
                    else if (texts.Length == 1 && slot.nameText == null)
                    {
                        slot.nameText = texts[0];
                    }
                }

                if (slot.nameText != null || slot.scoreText != null)
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
