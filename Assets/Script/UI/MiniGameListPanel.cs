using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles the GameListPanel inside MiniGamesCanvas.
/// Automatically tidies the header, divider, and background framing to eliminate dead void.
/// Generates rich museum-grade game cards with numbered golden badges, titles, subtitles,
/// and interactive "Main ▶" action pills with XR haptic and audio feedback.
/// Attach this script to the GameListPanel GameObject.
/// </summary>
public class MiniGameListPanel : MonoBehaviour
{
    [Header("Content Container")]
    [Tooltip("Scrollable container where game rows are spawned. Auto-found/created if empty.")]
    public Transform contentContainer;

    [Tooltip("Optional prefab for each row. Sliced sprite '8.png' is used for card styling.")]
    public GameObject gameRowPrefab;

    // ─────────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────────

    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private TMP_FontAsset cachedFont;
    private Sprite buttonSprite;

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
        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.ApplyToHierarchy(gameObject);
        }
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
    // List population & Layout
    // ─────────────────────────────────────────────────────────────────────────

    private void PopulateList()
    {
        ResolveContentContainer();

        // Clear previous rows
        foreach (var row in spawnedRows)
            if (row != null) Destroy(row);
        spawnedRows.Clear();

        // Cache font from existing title if not already cached
        Transform titleTf = FindDeepChild(transform, "RoomTitleText");
        if (titleTf != null)
        {
            TMP_Text tComp = titleTf.GetComponent<TMP_Text>();
            if (tComp != null && tComp.font != null) cachedFont = tComp.font;
        }

        // Cache button sprite from gameRowPrefab or existing images
        if (buttonSprite == null && gameRowPrefab != null)
        {
            Image pImg = gameRowPrefab.GetComponent<Image>();
            if (pImg != null && pImg.sprite != null) buttonSprite = pImg.sprite;
        }

        var games = MiniGameMenuPanel.Instance != null ? MiniGameMenuPanel.Instance.games : null;
        int gameCount = games != null ? games.Length : 0;

        // Auto-tidy panel layout, framing, and header alignment
        TidyPanelLayout(gameCount);

        if (games == null || games.Length == 0)
        {
            Debug.LogWarning("MiniGameListPanel: MiniGameMenuPanel games list is empty or null.");
            return;
        }

        if (contentContainer == null)
        {
            Debug.LogWarning("MiniGameListPanel: contentContainer could not be resolved.");
            return;
        }

        // Build rich game cards
        for (int i = 0; i < games.Length; i++)
        {
            int capturedIndex = i;
            var entry = games[i];

            GameObject card = CreateGameCard(capturedIndex, entry);
            card.SetActive(true);

            // Wire click: select this game, return to menu panel
            WireButton(card, () =>
            {
                MiniGameMenuPanel.Instance?.SelectGame(capturedIndex);
                MiniGames.Instance?.ShowMenuPanel();
                Debug.Log($"MiniGameListPanel: Selected '{games[capturedIndex].gameName}'.");
            });

            spawnedRows.Add(card);
        }

        // Add subtle footer hint banner
        GameObject footer = CreateFooterHint();
        if (footer != null) spawnedRows.Add(footer);
    }

    /// <summary>
    /// Align header items, fix oversized close button collider glitch, ensure a subtle gold divider,
    /// and resize the background image to hug the content with zero dead void.
    /// </summary>
    private void TidyPanelLayout(int gameCount)
    {
        // 1. Align Header Title ("Senarai Permainan")
        Transform titleTf = FindDeepChild(transform, "RoomTitleText");
        if (titleTf != null)
        {
            RectTransform titleRt = titleTf.GetComponent<RectTransform>();
            if (titleRt != null)
            {
                titleRt.anchorMin = titleRt.anchorMax = titleRt.pivot = new Vector2(0.5f, 0.5f);
                titleRt.anchoredPosition = new Vector2(-10.3f, -142f);
                titleRt.sizeDelta = new Vector2(250f, 32f);
                titleRt.localScale = Vector3.one;
            }

            TMP_Text titleTmp = titleTf.GetComponent<TMP_Text>();
            if (titleTmp != null)
            {
                titleTmp.text = "Senarai Permainan";
                titleTmp.fontSize = 21f;
                titleTmp.fontStyle = FontStyles.Bold;
                titleTmp.alignment = TextAlignmentOptions.Center;
                titleTmp.color = new Color(0.98f, 0.96f, 0.91f, 1f); // Warm ivory
                titleTmp.characterSpacing = 1.5f;
            }
        }

        // 2. Align Header Icon (Museum Building)
        Transform iconTf = FindDeepChild(transform, "HeaderIcon");
        if (iconTf != null)
        {
            RectTransform iconRt = iconTf.GetComponent<RectTransform>();
            if (iconRt != null)
            {
                iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(-155f, -142f);
                iconRt.sizeDelta = new Vector2(34f, 34f);
                iconRt.localScale = Vector3.one;
            }

            Image iconImg = iconTf.GetComponent<Image>();
            if (iconImg != null)
            {
                iconImg.color = new Color(0.95f, 0.84f, 0.50f, 1f); // Warm gold tint
            }
        }

        // 3. Fix & Align Close Button (Fixing 1137.67px sizeDelta bug)
        Transform closeTf = FindDeepChild(transform, "CloseButton");
        if (closeTf != null)
        {
            RectTransform closeRt = closeTf.GetComponent<RectTransform>();
            if (closeRt != null)
            {
                closeRt.anchorMin = closeRt.anchorMax = closeRt.pivot = new Vector2(0.5f, 0.5f);
                closeRt.anchoredPosition = new Vector2(145f, -142f);
                closeRt.sizeDelta = new Vector2(30f, 30f);
                closeRt.localScale = Vector3.one;
            }

            // Center child 'x' icon if any
            if (closeTf.childCount > 0)
            {
                RectTransform childRt = closeTf.GetChild(0).GetComponent<RectTransform>();
                if (childRt != null)
                {
                    childRt.anchorMin = childRt.anchorMax = childRt.pivot = new Vector2(0.5f, 0.5f);
                    childRt.anchoredPosition = Vector2.zero;
                    childRt.sizeDelta = new Vector2(16f, 16f);
                }
            }

            if (closeTf.GetComponent<UIButtonAudio>() == null)
                closeTf.gameObject.AddComponent<UIButtonAudio>();
        }

        // 4. Ensure subtle gold divider line right under the header
        Transform divTf = transform.Find("HeaderDivider");
        GameObject divObj;
        if (divTf == null)
        {
            divObj = new GameObject("HeaderDivider");
            divObj.transform.SetParent(transform, false);
        }
        else
        {
            divObj = divTf.gameObject;
        }
        divObj.transform.SetSiblingIndex(4); // Between header and contentContainer

        RectTransform divRt = divObj.GetComponent<RectTransform>() ?? divObj.AddComponent<RectTransform>();
        divRt.anchorMin = divRt.anchorMax = divRt.pivot = new Vector2(0.5f, 0.5f);
        divRt.anchoredPosition = new Vector2(-10.3f, -164f);
        divRt.sizeDelta = new Vector2(334f, 1.5f);

        Image divImg = divObj.GetComponent<Image>() ?? divObj.AddComponent<Image>();
        divImg.color = new Color(0.85f, 0.72f, 0.40f, 0.45f); // Gold translucent accent
        divImg.raycastTarget = false;

        // 5. Adjust Content Container
        if (contentContainer != null)
        {
            RectTransform cRt = contentContainer.GetComponent<RectTransform>();
            if (cRt != null)
            {
                cRt.anchorMin = cRt.anchorMax = cRt.pivot = new Vector2(0.5f, 0.5f);
                cRt.anchoredPosition = new Vector2(-10.3f, -250f);
                cRt.sizeDelta = new Vector2(334f, 160f);
            }

            VerticalLayoutGroup vlg = contentContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = contentContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 2, 2);
        }

        // 6. Resize background card Image to eliminate the massive dead void
        Transform bgTf = transform.Find("Image");
        if (bgTf != null)
        {
            RectTransform bgRt = bgTf.GetComponent<RectTransform>();
            if (bgRt != null)
            {
                float s = bgRt.localScale.x > 0 ? bgRt.localScale.x : 0.34166664f;
                int count = Mathf.Max(gameCount, 2);
                float contentH = (count * 62f) + ((count - 1) * 8f) + 32f;
                float panelHeight = Mathf.Max(250f, 95f + contentH);

                bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
                bgRt.anchoredPosition = new Vector2(-10.3f, -118f - (panelHeight * 0.5f));
                bgRt.sizeDelta = new Vector2(368f / s, panelHeight / s);
            }
        }
    }

    /// <summary>Find or create the scrollable content container inside this panel.</summary>
    private void ResolveContentContainer()
    {
        if (contentContainer != null) return;

        // Look for common names first
        contentContainer = FindDeepChild(transform, "GameList")
                        ?? FindDeepChild(transform, "Content")
                        ?? FindDeepChild(transform, "GameListContent");

        // Try ScrollView path
        if (contentContainer == null)
        {
            Transform svp = transform.Find("ScrollView/Viewport/Content");
            if (svp != null) contentContainer = svp;
        }

        if (contentContainer != null) return;

        // Nothing found – create container
        GameObject obj = new GameObject("GameList");
        obj.transform.SetParent(transform, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-10.3f, -250f);
        rt.sizeDelta = new Vector2(334f, 160f);

        VerticalLayoutGroup vlg = obj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 2, 2);

        contentContainer = obj.transform;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI Card Construction
    // ─────────────────────────────────────────────────────────────────────────

    private GameObject CreateGameCard(int index, MiniGameMenuPanel.GameEntry entry)
    {
        GameObject row = new GameObject($"GameRow_{index}");
        row.transform.SetParent(contentContainer, false);

        RectTransform rt = row.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(334f, 62f);

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 62f;
        le.flexibleWidth = 1f;

        // Background Image
        Image bg = row.AddComponent<Image>();
        if (buttonSprite != null)
        {
            bg.sprite = buttonSprite;
            bg.type = Image.Type.Sliced;
        }
        bg.color = new Color(0.12f, 0.14f, 0.18f, 0.88f); // Deep obsidian slate

        // Subtle museum gold card outline
        Outline outline = row.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.72f, 0.40f, 0.35f);
        outline.effectDistance = new Vector2(1f, -1f);

        // Interactive Button with rich color states
        Button btn = row.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.12f, 0.14f, 0.18f, 0.88f);
        cb.highlightedColor = new Color(0.20f, 0.24f, 0.32f, 0.98f);
        cb.pressedColor = new Color(0.09f, 0.11f, 0.14f, 1f);
        cb.selectedColor = new Color(0.18f, 0.22f, 0.30f, 0.95f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
        btn.colors = cb;

        // XR Hover Scale & Color feedback
        XRButtonSelection xr = row.AddComponent<XRButtonSelection>();
        xr.buttonImage = bg;
        xr.normalColor = cb.normalColor;
        xr.hoverColor = cb.highlightedColor;
        xr.hoverScaleMultiplier = 1.025f;
        xr.transitionSpeed = 10f;

        // Audio & Haptics
        row.AddComponent<UIButtonAudio>();

        // ── 1. Left Number Badge ("01", "02") ──
        GameObject badgeObj = new GameObject("NumberBadge");
        badgeObj.transform.SetParent(row.transform, false);

        RectTransform badgeRt = badgeObj.AddComponent<RectTransform>();
        badgeRt.anchorMin = badgeRt.anchorMax = badgeRt.pivot = new Vector2(0f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(12f, 0f);
        badgeRt.sizeDelta = new Vector2(36f, 36f);

        Image badgeImg = badgeObj.AddComponent<Image>();
        if (buttonSprite != null) { badgeImg.sprite = buttonSprite; badgeImg.type = Image.Type.Sliced; }
        badgeImg.color = new Color(0.85f, 0.72f, 0.38f, 0.22f); // Golden amber translucent badge

        Outline badgeOutline = badgeObj.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(0.85f, 0.72f, 0.38f, 0.45f);
        badgeOutline.effectDistance = new Vector2(1f, -1f);

        GameObject badgeTextObj = new GameObject("BadgeNum");
        badgeTextObj.transform.SetParent(badgeObj.transform, false);

        RectTransform badgeTextRt = badgeTextObj.AddComponent<RectTransform>();
        badgeTextRt.anchorMin = Vector2.zero;
        badgeTextRt.anchorMax = Vector2.one;
        badgeTextRt.sizeDelta = Vector2.zero;

        TMP_Text badgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
        if (cachedFont != null) badgeText.font = cachedFont;
        badgeText.text = (index + 1).ToString("D2");
        badgeText.fontSize = 15f;
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.color = new Color(0.96f, 0.86f, 0.55f, 1f); // Warm gold
        badgeText.raycastTarget = false;

        // ── 2. Center Text Info (Title & Subtitle) ──
        GameObject infoObj = new GameObject("InfoBlock");
        infoObj.transform.SetParent(row.transform, false);

        RectTransform infoRt = infoObj.AddComponent<RectTransform>();
        infoRt.anchorMin = Vector2.zero;
        infoRt.anchorMax = Vector2.one;
        infoRt.offsetMin = new Vector2(56f, 5f);
        infoRt.offsetMax = new Vector2(-74f, -5f);

        // Title
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(infoObj.transform, false);

        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.46f);
        titleRt.anchorMax = Vector2.one;
        titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;

        TMP_Text titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        if (cachedFont != null) titleTxt.font = cachedFont;
        titleTxt.text = entry.gameName;
        titleTxt.fontSize = 14.5f;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
        titleTxt.color = new Color(0.98f, 0.97f, 0.93f, 1f);
        titleTxt.overflowMode = TextOverflowModes.Ellipsis;
        titleTxt.raycastTarget = false;

        // Subtitle
        GameObject subObj = new GameObject("SubtitleText");
        subObj.transform.SetParent(infoObj.transform, false);

        RectTransform subRt = subObj.AddComponent<RectTransform>();
        subRt.anchorMin = Vector2.zero;
        subRt.anchorMax = new Vector2(1f, 0.48f);
        subRt.offsetMin = subRt.offsetMax = Vector2.zero;

        TMP_Text subTxt = subObj.AddComponent<TextMeshProUGUI>();
        if (cachedFont != null) subTxt.font = cachedFont;
        subTxt.text = GetGameSubtitle(entry.gameID, entry.gameName);
        subTxt.fontSize = 10.5f;
        subTxt.fontStyle = FontStyles.Normal;
        subTxt.alignment = TextAlignmentOptions.MidlineLeft;
        subTxt.color = new Color(0.78f, 0.75f, 0.68f, 0.90f);
        subTxt.overflowMode = TextOverflowModes.Ellipsis;
        subTxt.raycastTarget = false;

        // ── 3. Right Action Pill ("Main ▶") ──
        GameObject pillObj = new GameObject("ActionPill");
        pillObj.transform.SetParent(row.transform, false);

        RectTransform pillRt = pillObj.AddComponent<RectTransform>();
        pillRt.anchorMin = pillRt.anchorMax = pillRt.pivot = new Vector2(1f, 0.5f);
        pillRt.anchoredPosition = new Vector2(-10f, 0f);
        pillRt.sizeDelta = new Vector2(58f, 30f);

        Image pillImg = pillObj.AddComponent<Image>();
        if (buttonSprite != null) { pillImg.sprite = buttonSprite; pillImg.type = Image.Type.Sliced; }
        pillImg.color = new Color(0.85f, 0.70f, 0.35f, 0.95f); // Rich gold
        pillImg.raycastTarget = false;

        GameObject pillTextObj = new GameObject("PillLabel");
        pillTextObj.transform.SetParent(pillObj.transform, false);

        RectTransform pillTextRt = pillTextObj.AddComponent<RectTransform>();
        pillTextRt.anchorMin = Vector2.zero;
        pillTextRt.anchorMax = Vector2.one;
        pillTextRt.sizeDelta = Vector2.zero;

        TMP_Text pillTxt = pillTextObj.AddComponent<TextMeshProUGUI>();
        if (cachedFont != null) pillTxt.font = cachedFont;
        pillTxt.text = "Main ▶";
        pillTxt.fontSize = 11.5f;
        pillTxt.fontStyle = FontStyles.Bold;
        pillTxt.alignment = TextAlignmentOptions.Center;
        pillTxt.color = new Color(0.10f, 0.12f, 0.15f, 1f); // Charcoal text on gold
        pillTxt.raycastTarget = false;

        return row;
    }

    private GameObject CreateFooterHint()
    {
        if (contentContainer == null) return null;

        GameObject footer = new GameObject("FooterHint");
        footer.transform.SetParent(contentContainer, false);

        RectTransform rt = footer.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(334f, 22f);

        LayoutElement le = footer.AddComponent<LayoutElement>();
        le.preferredHeight = 22f;
        le.flexibleWidth = 1f;

        TMP_Text txt = footer.AddComponent<TextMeshProUGUI>();
        if (cachedFont != null) txt.font = cachedFont;
        txt.text = "★  Kumpul markah untuk menduduki Papan Pendahulu!";
        txt.fontSize = 10.5f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.85f, 0.75f, 0.50f, 0.65f); // Soft golden glow
        txt.raycastTarget = false;

        return footer;
    }

    private static string GetGameSubtitle(string gameId, string gameName)
    {
        string id = (gameId ?? "").ToLower();
        string name = (gameName ?? "").ToLower();

        if (id.Contains("1") || name.Contains("tebak") || name.Contains("teka") || name.Contains("bayang"))
            return "Uji ketajaman mata & kenali artifak 3D";
        if (id.Contains("2") || name.Contains("susun") || name.Contains("langkah") || name.Contains("kisah"))
            return "Susun peristiwa bersejarah Terengganu";

        return "Sentuh untuk mula bermain";
    }

    private static void WireButton(GameObject obj, UnityEngine.Events.UnityAction action)
    {
        if (obj == null) return;

        Button btn = obj.GetComponent<Button>() ?? obj.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);

        XRButtonSelection xr = obj.GetComponent<XRButtonSelection>();
        if (xr != null)
        {
            xr.onClick.RemoveAllListeners();
            xr.onClick.AddListener(action);
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name == childName) return t;
        return null;
    }
}

