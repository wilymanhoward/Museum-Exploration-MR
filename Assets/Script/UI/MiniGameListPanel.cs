using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles the GameListPanel inside MiniGamesCanvas.
/// On Enable: clears and re-populates the list from MiniGameMenuPanel's game entries.
/// Selecting a row sets that game in MiniGameMenuPanel and returns to the menu.
/// Attach this script to the GameListPanel GameObject.
/// </summary>
public class MiniGameListPanel : MonoBehaviour
{
    [Header("Content Container")]
    [Tooltip("Scrollable container where game rows are spawned. Auto-found/created if empty.")]
    public Transform contentContainer;

    [Tooltip("Optional prefab for each row. Leave empty to use the built-in styled button.")]
    public GameObject gameRowPrefab;

    // ─────────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────────

    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    // ─────────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Wire the panel's own CloseButton to return to the menu
        Transform closeTf = FindDeepChild(transform, "CloseButton");
        if (closeTf != null) WireButton(closeTf.gameObject, OnClose);
    }

    private void OnEnable()
    {
        PopulateList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Close the list and return to the menu panel.</summary>
    public void OnClose()
    {
        MiniGames.Instance?.ShowMenuPanel();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // List population
    // ─────────────────────────────────────────────────────────────────────────

    private void PopulateList()
    {
        ResolveContentContainer();

        // Clear previous rows
        foreach (var row in spawnedRows)
            if (row != null) Destroy(row);
        spawnedRows.Clear();

        if (MiniGameMenuPanel.Instance == null)
        {
            Debug.LogWarning("MiniGameListPanel: MiniGameMenuPanel.Instance is null – cannot populate list.");
            return;
        }

        if (contentContainer == null)
        {
            Debug.LogWarning("MiniGameListPanel: contentContainer could not be resolved.");
            return;
        }

        var games = MiniGameMenuPanel.Instance.games;
        if (games == null || games.Length == 0) return;

        for (int i = 0; i < games.Length; i++)
        {
            int capturedIndex = i;
            var entry = games[i];

            // Build the row
            GameObject row = gameRowPrefab != null
                ? Instantiate(gameRowPrefab, contentContainer)
                : CreateDefaultRow(i);

            row.SetActive(true);

            // Set the display name
            TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = $"{i + 1}.  {entry.gameName}";

            // Wire click: select this game, return to menu
            WireButton(row, () =>
            {
                MiniGameMenuPanel.Instance?.SelectGame(capturedIndex);
                MiniGames.Instance?.ShowMenuPanel();
                Debug.Log($"MiniGameListPanel: Selected '{games[capturedIndex].gameName}'.");
            });

            spawnedRows.Add(row);
        }
    }

    /// <summary>Find or create the scrollable content container inside this panel.</summary>
    private void ResolveContentContainer()
    {
        if (contentContainer != null) return;

        // Look for common names first
        contentContainer = FindDeepChild(transform, "Content")
                        ?? FindDeepChild(transform, "GameListContent");

        // Try ScrollView path
        if (contentContainer == null)
        {
            Transform svp = transform.Find("ScrollView/Viewport/Content");
            if (svp != null) contentContainer = svp;
        }

        if (contentContainer != null) return;

        // Nothing found – create a simple vertical layout container
        GameObject obj = new GameObject("GameListContent");
        obj.transform.SetParent(transform, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.10f);
        rt.anchorMax = new Vector2(0.95f, 0.85f);
        rt.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vlg = obj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(6, 6, 6, 6);

        ContentSizeFitter csf = obj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        contentContainer = obj.transform;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private GameObject CreateDefaultRow(int index)
    {
        GameObject row = new GameObject($"GameRow_{index}");
        row.transform.SetParent(contentContainer, false);

        RectTransform rt = row.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 52f);

        Image bg = row.AddComponent<Image>();
        bg.color = new Color(0.35f, 0.38f, 0.31f, 0.85f);

        // Label
        GameObject textObj = new GameObject("NameText");
        textObj.transform.SetParent(row.transform, false);

        RectTransform trt = textObj.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.06f, 0f);
        trt.anchorMax = new Vector2(0.94f, 1f);
        trt.sizeDelta = Vector2.zero;

        TMP_Text txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 18;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Left;
        txt.color = new Color(0.90f, 0.93f, 0.63f);
        txt.enableWordWrapping = false;
        txt.overflowMode = TMPro.TextOverflowModes.Ellipsis;

        return row;
    }

    private static void WireButton(GameObject obj, UnityEngine.Events.UnityAction action)
    {
        if (obj == null) return;

        Button btn = obj.GetComponent<Button>() ?? obj.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);

        XRButtonSelection xr = obj.GetComponent<XRButtonSelection>();
        if (xr != null) { xr.onClick.RemoveAllListeners(); xr.onClick.AddListener(action); }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name == childName) return t;
        return null;
    }
}
