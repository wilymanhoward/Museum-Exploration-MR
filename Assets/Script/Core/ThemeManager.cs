using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum UIThemeMode
{
    Dark = 0,
    Light = 1
}

/// <summary>
/// Centralized Theme Manager for the Museum Mixed Reality application.
/// Controls Dark Mode and Light Mode states across all UI panels, materials, text, and buttons.
/// Default state is Dark Mode.
/// Automatically swaps frosted glass sprite textures (2_light/2_dark, 8_light/8_dark, 7_light/7_dark)
/// and updates typography, borders, and button highlights.
/// </summary>
public class ThemeManager : MonoBehaviour
{
    private static ThemeManager instance;
    public static ThemeManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ThemeManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("ThemeManager");
                    instance = go.AddComponent<ThemeManager>();
                }
            }
            return instance;
        }
    }

    [Header("Current Theme State")]
    [Tooltip("Active theme mode. Defaults to Dark Mode.")]
    public UIThemeMode currentTheme = UIThemeMode.Dark;

    public static event Action<UIThemeMode> OnThemeChanged;

    private const string PrefsKey = "Museum_UI_ThemeMode";

    [Header("Dark Mode Colors")]
    public Color darkPanelBg = new Color(0.12f, 0.14f, 0.17f, 0.88f);
    public Color darkCardBg = new Color(0.16f, 0.18f, 0.22f, 0.85f);
    public Color darkHeaderTextColor = new Color(0.98f, 0.98f, 1.0f, 1.0f);
    public Color darkBodyTextColor = new Color(0.85f, 0.88f, 0.94f, 0.95f);
    public Color darkAccentTextColor = new Color(0.60f, 0.82f, 1.0f, 1.0f);
    public Color darkButtonBg = new Color(0.20f, 0.22f, 0.27f, 0.88f);
    public Color darkButtonIconColor = Color.white;
    public Color darkScrollbarTrack = new Color(1f, 1f, 1f, 0.12f);
    public Color darkScrollbarHandle = new Color(0.55f, 0.65f, 0.80f, 0.85f);

    [Header("Light Mode Colors")]
    public Color lightPanelBg = new Color(0.77f, 0.79f, 0.83f, 0.86f);
    public Color lightCardBg = new Color(0.71f, 0.75f, 0.80f, 0.88f);
    public Color lightHeaderTextColor = new Color(0.12f, 0.16f, 0.22f, 1.0f);
    public Color lightBodyTextColor = new Color(0.20f, 0.25f, 0.32f, 0.95f);
    public Color lightAccentTextColor = new Color(0.12f, 0.38f, 0.68f, 1.0f);
    public Color lightButtonBg = new Color(0.68f, 0.72f, 0.78f, 0.90f);
    public Color lightButtonIconColor = new Color(0.12f, 0.16f, 0.22f, 1.0f);
    public Color lightScrollbarTrack = new Color(0f, 0f, 0f, 0.08f);
    public Color lightScrollbarHandle = new Color(0.35f, 0.40f, 0.50f, 0.80f);

    // Dynamic Theme Sprites loaded from Resources/Theme
    private Sprite spritePanelLight;
    private Sprite spritePanelDark;
    private Sprite spriteCardLight;
    private Sprite spriteCardDark;
    private Sprite spriteCircleLight;
    private Sprite spriteCircleDark;
    private Sprite spriteHoverLight;
    private Sprite spriteHoverDark;

    // Cache of runtime materials
    private Material matOptionsCardBg;
    private Material matArtifactDetailPanel;
    private Material matOptionsRowCard;
    private Material matImagesBtn;
    private Material mat3DViewBtn;
    private Material matRoomHUD;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Load saved theme preference (Default is Dark = 0)
        int savedTheme = PlayerPrefs.GetInt(PrefsKey, (int)UIThemeMode.Dark);
        currentTheme = (UIThemeMode)savedTheme;

        CacheThemeAssets();
    }

    private void Start()
    {
        // Apply initial theme
        ApplyTheme(currentTheme);
    }

    private void CacheThemeAssets()
    {
        CacheMaterials();

        // Load theme sprites from Resources/Theme
        spritePanelLight = Resources.Load<Sprite>("Theme/2_light");
        spritePanelDark = Resources.Load<Sprite>("Theme/2_dark");
        spriteCardLight = Resources.Load<Sprite>("Theme/8_light");
        spriteCardDark = Resources.Load<Sprite>("Theme/8_dark");
        spriteCircleLight = Resources.Load<Sprite>("Theme/7_light");
        spriteCircleDark = Resources.Load<Sprite>("Theme/7_dark");
        spriteHoverLight = Resources.Load<Sprite>("Theme/9_light");
        spriteHoverDark = Resources.Load<Sprite>("Theme/9_dark");
    }

    private void CacheMaterials()
    {
        foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (m == null) continue;
            string n = m.name;
            if (n == "Mat_OptionsCardBackground") matOptionsCardBg = m;
            else if (n == "Mat_ArtifactDetailPanel") matArtifactDetailPanel = m;
            else if (n == "Mat_OptionsRowCard") matOptionsRowCard = m;
            else if (n == "Mat_ImagesBtn") matImagesBtn = m;
            else if (n == "Mat_3DViewBtn") mat3DViewBtn = m;
            else if (n == "Mat_RoomHUD") matRoomHUD = m;
        }
    }

    /// <summary>
    /// Toggles between Dark Mode and Light Mode.
    /// </summary>
    public void ToggleTheme()
    {
        UIThemeMode nextTheme = (currentTheme == UIThemeMode.Dark) ? UIThemeMode.Light : UIThemeMode.Dark;
        SetTheme(nextTheme);
    }

    /// <summary>
    /// Sets a specific theme mode and broadcasts changes.
    /// </summary>
    public void SetTheme(UIThemeMode mode)
    {
        currentTheme = mode;
        PlayerPrefs.SetInt(PrefsKey, (int)currentTheme);
        PlayerPrefs.Save();

        Debug.Log($"[ThemeManager] Theme changed to: {currentTheme}");

        ApplyTheme(currentTheme);

        OnThemeChanged?.Invoke(currentTheme);
    }

    /// <summary>
    /// Applies the given theme to all materials and discovered UI panels in the scene.
    /// </summary>
    public void ApplyTheme(UIThemeMode mode)
    {
        ApplyToMaterials(mode);
        ApplyToAllScenePanels(mode);
    }

    private void ApplyToMaterials(UIThemeMode mode)
    {
        if (matOptionsCardBg == null) CacheMaterials();

        bool isLight = (mode == UIThemeMode.Light);

        if (matOptionsCardBg != null)
        {
            if (matOptionsCardBg.HasProperty("_Color"))
                matOptionsCardBg.SetColor("_Color", isLight ? new Color(0.77f, 0.80f, 0.84f, 0.86f) : new Color(0.11f, 0.12f, 0.14f, 0.72f));
            if (matOptionsCardBg.HasProperty("_DotColor"))
                matOptionsCardBg.SetColor("_DotColor", isLight ? new Color(0.55f, 0.60f, 0.68f, 0.35f) : new Color(0.28f, 0.30f, 0.25f, 0.40f));
        }

        if (matRoomHUD != null)
        {
            if (matRoomHUD.HasProperty("_Color"))
                matRoomHUD.SetColor("_Color", isLight ? new Color(0.77f, 0.80f, 0.84f, 0.86f) : new Color(0.11f, 0.12f, 0.14f, 0.72f));
        }

        if (matArtifactDetailPanel != null)
        {
            if (matArtifactDetailPanel.HasProperty("_BorderColor"))
                matArtifactDetailPanel.SetColor("_BorderColor", isLight ? new Color(0.48f, 0.55f, 0.65f, 0.80f) : new Color(0.70f, 0.80f, 0.95f, 0.75f));
        }

        if (matOptionsRowCard != null)
        {
            if (matOptionsRowCard.HasProperty("_Color"))
                matOptionsRowCard.SetColor("_Color", isLight ? new Color(0.71f, 0.75f, 0.80f, 0.86f) : new Color(0.16f, 0.18f, 0.22f, 0.75f));
            if (matOptionsRowCard.HasProperty("_BorderColor"))
                matOptionsRowCard.SetColor("_BorderColor", isLight ? new Color(0.58f, 0.64f, 0.72f, 0.85f) : new Color(0.45f, 0.50f, 0.60f, 0.80f));
        }

        if (matImagesBtn != null)
        {
            if (matImagesBtn.HasProperty("_Color"))
                matImagesBtn.SetColor("_Color", isLight ? new Color(0.62f, 0.68f, 0.76f, 0.90f) : new Color(0.38f, 0.44f, 0.54f, 0.95f));
            if (matImagesBtn.HasProperty("_BorderColor"))
                matImagesBtn.SetColor("_BorderColor", isLight ? new Color(0.45f, 0.55f, 0.68f, 0.90f) : new Color(0.65f, 0.75f, 0.88f, 0.98f));
        }

        if (mat3DViewBtn != null)
        {
            if (mat3DViewBtn.HasProperty("_Color"))
                mat3DViewBtn.SetColor("_Color", isLight ? new Color(0.70f, 0.74f, 0.80f, 0.88f) : new Color(0.22f, 0.25f, 0.30f, 0.90f));
            if (mat3DViewBtn.HasProperty("_BorderColor"))
                mat3DViewBtn.SetColor("_BorderColor", isLight ? new Color(0.58f, 0.64f, 0.72f, 0.85f) : new Color(0.45f, 0.50f, 0.60f, 0.80f));
        }
    }

    /// <summary>
    /// Finds all known UI panels in the scene and applies the theme to their hierarchy.
    /// </summary>
    public void ApplyToAllScenePanels(UIThemeMode mode)
    {
        // 1. History Panels (Original and any clones)
        foreach (HistoryPanel hp in FindObjectsOfType<HistoryPanel>(true))
        {
            if (hp != null) ApplyToHierarchy(hp.gameObject, mode);
        }

        // 2. HistoryListPanel
        if (HistoryListPanel.Instance != null)
        {
            ApplyToHierarchy(HistoryListPanel.Instance.gameObject, mode);
        }

        // 3. Artifact Detail Panels
        foreach (Artifact art in FindObjectsOfType<Artifact>(true))
        {
            if (art != null) ApplyToHierarchy(art.gameObject, mode);
        }

        // 4. WristWatch Options Panel
        if (WristWatch.Instance != null)
        {
            if (WristWatch.Instance.optionsPanelObj != null)
            {
                ApplyToHierarchy(WristWatch.Instance.optionsPanelObj, mode);
            }
            if (WristWatch.Instance.roomListPanel != null)
            {
                ApplyToHierarchy(WristWatch.Instance.roomListPanel, mode);
            }
        }
    }

    /// <summary>
    /// Recursively applies theme styling to a specific UI hierarchy.
    /// Swaps textures between dark and light frosted glass and updates typography.
    /// </summary>
    public void ApplyToHierarchy(GameObject root, UIThemeMode? overrideMode = null)
    {
        if (root == null) return;
        UIThemeMode mode = overrideMode ?? currentTheme;
        bool isLight = (mode == UIThemeMode.Light);

        if (spritePanelLight == null) CacheThemeAssets();

        Sprite activePanel = isLight ? spritePanelLight : spritePanelDark;
        Sprite activeCard = isLight ? spriteCardLight : spriteCardDark;
        Sprite activeCircle = isLight ? spriteCircleLight : spriteCircleDark;
        Sprite activeHover = isLight ? spriteHoverLight : spriteHoverDark;

        // 1. Images (Backgrounds, cards, circular buttons)
        Image[] images = root.GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            if (img == null) continue;
            string n = img.gameObject.name.ToLower();

            // Skip photos / artwork textures / raw video
            if (n.Contains("photo") || n.Contains("thumb") || n.Contains("artifactimage") || n.Contains("displayimage"))
                continue;

            // Root panel backgrounds (e.g. 2.png, 2_dark, 2_light)
            if (n == "background" || n.Contains("panelbg") || n.Contains("windowbg") ||
                (img.sprite != null && (img.sprite.name.StartsWith("2") || img.sprite.name.Contains("Panel"))))
            {
                if (activePanel != null)
                {
                    img.sprite = activePanel;
                    img.color = Color.white;
                }
                else
                {
                    img.color = isLight ? lightPanelBg : darkPanelBg;
                }
            }
            // Sub-cards (e.g. TentangArtefakCard, DetailArtefakCard, list item cards, 8.png)
            else if (n.Contains("card") || n.Contains("item") || n.Contains("frame") ||
                     (img.sprite != null && img.sprite.name.StartsWith("8")))
            {
                if (activeCard != null)
                {
                    img.sprite = activeCard;
                    img.color = Color.white;
                }
                else
                {
                    img.color = isLight ? lightCardBg : darkCardBg;
                }
            }
            // Circular action / control buttons (Close, Theme, Back, Play, Restart, 7.png)
            else if (n.Contains("close") || n.Contains("theme") || n.Contains("back") || n.Contains("play") || n.Contains("replay") || n.Contains("restart") || n.Contains("circle") || n.Contains("action") ||
                     (img.sprite != null && img.sprite.name.StartsWith("7")))
            {
                if (activeCircle != null)
                {
                    img.sprite = activeCircle;
                    img.color = Color.white;
                }
                else
                {
                    img.color = isLight ? lightButtonBg : darkButtonBg;
                }
            }
            // Scrollbar track & handle
            else if (n.Contains("track"))
            {
                img.color = isLight ? lightScrollbarTrack : darkScrollbarTrack;
            }
            else if (n.Contains("handle"))
            {
                img.color = isLight ? lightScrollbarHandle : darkScrollbarHandle;
            }

            // Update button sprite hover swap if applicable
            Button btn = img.GetComponent<Button>();
            if (btn != null && btn.transition == Selectable.Transition.SpriteSwap && activeHover != null)
            {
                SpriteState ss = btn.spriteState;
                ss.highlightedSprite = activeHover;
                btn.spriteState = ss;
            }
        }

        // 2. TextMeshProUGUI (Headers, subtitles, descriptions, button symbols)
        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI tmp in texts)
        {
            if (tmp == null) continue;
            string n = tmp.gameObject.name.ToLower();

            // Headings / Titles
            if (n.Contains("toptitle") || n.Contains("title") || n.Contains("header") || tmp.fontSize >= 17f)
            {
                tmp.color = isLight ? lightHeaderTextColor : darkHeaderTextColor;
            }
            // Theme toggle button icons (☀️ / 🌙)
            else if (n.Contains("theme") || tmp.text == "☀️" || tmp.text == "🌙")
            {
                tmp.text = isLight ? "☀️" : "🌙";
                tmp.color = isLight ? new Color(0.95f, 0.65f, 0.1f, 1f) : new Color(0.7f, 0.85f, 1f, 1f);
            }
            // Button symbols / Icons (e.g. '✕', '◀', '▶', '↺', etc.)
            else if (n.Contains("icon") || n.Contains("symbol") || n.Contains("close") || n.Contains("back"))
            {
                tmp.color = isLight ? lightButtonIconColor : darkButtonIconColor;
            }
            // Secondary / Body descriptions
            else
            {
                tmp.color = isLight ? lightBodyTextColor : darkBodyTextColor;
            }
        }

        // 3. XRButtonSelection hover colors
        XRButtonSelection[] xrButtons = root.GetComponentsInChildren<XRButtonSelection>(true);
        foreach (XRButtonSelection xr in xrButtons)
        {
            if (xr == null) continue;
            xr.normalColor = isLight ? new Color(0.71f, 0.75f, 0.80f, 0.86f) : new Color(0.9f, 0.9f, 0.93f, 0.8f);
            xr.hoverColor = isLight ? new Color(0.60f, 0.68f, 0.80f, 0.92f) : new Color(0.8f, 0.85f, 0.96f, 0.95f);
        }
    }
}
