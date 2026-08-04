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

        // 2. Load Local Entries first (instant response)
        List<LeaderboardEntry> localDisplayEntries = GetLocalDisplayEntries(currentGameId);
        DisplayLeaderboard(localDisplayEntries);

        // 3. Fetch from Firebase Firestore in background
        StopAllCoroutines();
        StartCoroutine(FetchLeaderboardCoroutine());
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

        LocalEntry newEntry = new LocalEntry
        {
            name = string.IsNullOrWhiteSpace(playerName) ? "Anda" : playerName,
            timeSeconds = timeInSeconds
        };

        entries.Add(newEntry);
        // Sort by fastest time ascending (lowest time = 1st place)
        entries.Sort((a, b) => a.timeSeconds.CompareTo(b.timeSeconds));

        if (entries.Count > 10) entries.RemoveRange(10, entries.Count - 10);

        // Save to PlayerPrefs
        LocalEntryListWrapper wrapper = new LocalEntryListWrapper { entries = entries };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();

        Debug.Log($"[Leaderboard] Saved local time for '{cleanGameId}': {playerName} - {FormatTime(timeInSeconds)}");

        // Asynchronously post to Firestore
        if (Instance != null)
        {
            Instance.StartCoroutine(PostScore(cleanGameId, newEntry.name, Mathf.RoundToInt(timeInSeconds)));
        }
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
                    return wrapper.entries;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Leaderboard] Failed to parse local PlayerPrefs JSON: {ex.Message}");
            }
        }

        // Return default sample entries if no local entries exist
        return GetDefaultSampleEntries(cleanGameId);
    }

    private static List<LocalEntry> GetDefaultSampleEntries(string gameId)
    {
        List<LocalEntry> sample = new List<LocalEntry>();
        string gid = (gameId ?? "").ToLower();

        if (gid.Contains("game_1") || gid.Contains("guess") || gid.Contains("bayangan"))
        {
            sample.Add(new LocalEntry { name = "Howard", timeSeconds = 24f });
            sample.Add(new LocalEntry { name = "Nelsen", timeSeconds = 31f });
            sample.Add(new LocalEntry { name = "Celyne", timeSeconds = 38f });
            sample.Add(new LocalEntry { name = "Stephanie", timeSeconds = 45f });
            sample.Add(new LocalEntry { name = "Michael", timeSeconds = 53f });
        }
        else if (gid.Contains("game_2") || gid.Contains("batik"))
        {
            sample.Add(new LocalEntry { name = "Rayhan", timeSeconds = 28f });
            sample.Add(new LocalEntry { name = "Howard", timeSeconds = 35f });
            sample.Add(new LocalEntry { name = "Siti", timeSeconds = 42f });
            sample.Add(new LocalEntry { name = "Ahmad", timeSeconds = 49f });
            sample.Add(new LocalEntry { name = "Farhan", timeSeconds = 58f });
        }
        else if (gid.Contains("game_3") || gid.Contains("timeline") || gid.Contains("sejarah"))
        {
            sample.Add(new LocalEntry { name = "Budi", timeSeconds = 26f });
            sample.Add(new LocalEntry { name = "Howard", timeSeconds = 33f });
            sample.Add(new LocalEntry { name = "Dewi", timeSeconds = 41f });
            sample.Add(new LocalEntry { name = "Rudi", timeSeconds = 47f });
            sample.Add(new LocalEntry { name = "Siti", timeSeconds = 56f });
        }
        else
        {
            sample.Add(new LocalEntry { name = "Pemain #1", timeSeconds = 30f });
            sample.Add(new LocalEntry { name = "Pemain #2", timeSeconds = 40f });
            sample.Add(new LocalEntry { name = "Pemain #3", timeSeconds = 50f });
            sample.Add(new LocalEntry { name = "Pemain #4", timeSeconds = 60f });
            sample.Add(new LocalEntry { name = "Pemain #5", timeSeconds = 70f });
        }

        return sample;
    }

    private static List<LeaderboardEntry> GetLocalDisplayEntries(string gameId)
    {
        List<LocalEntry> local = LoadLocalEntries(gameId);
        List<LeaderboardEntry> display = new List<LeaderboardEntry>();

        foreach (LocalEntry e in local)
        {
            display.Add(new LeaderboardEntry
            {
                name = e.name,
                timeSeconds = e.timeSeconds,
                formattedScore = FormatTime(e.timeSeconds),
                gameID = gameId
            });
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

    private IEnumerator FetchLeaderboardCoroutine()
    {
        string baseUrl = string.IsNullOrEmpty(firestoreUrl) ? DefaultFirestoreUrl : firestoreUrl;
        
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

        if (fetchedEntries.Count > 0)
        {
            // Sort by fastest completion time ascending (lowest score/time = 1st place)
            fetchedEntries.Sort((a, b) => a.timeSeconds.CompareTo(b.timeSeconds));
            DisplayLeaderboard(fetchedEntries);
        }
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

    /// <summary>
    /// Helper to post a player score to Firestore database.
    /// </summary>
    public static IEnumerator PostScore(string gameID, string playerName, int score, string firestoreUrl = DefaultFirestoreUrl)
    {
        if (string.IsNullOrEmpty(gameID)) yield break;

        string safeName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string json = $"{{\"fields\":{{\"name\":{{\"stringValue\":\"{safeName}\"}},\"score\":{{\"integerValue\":\"{score}\"}},\"gameID\":{{\"stringValue\":\"{gameID}\"}}}}}}";

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
