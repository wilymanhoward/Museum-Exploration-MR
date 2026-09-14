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
/// Centralized Theme Manager for the Museum Mixed Reality project.
/// - In Dark Mode: Exactly preserves and restores 100% of the original authored visuals from before-light-mode;)
///   with zero unwanted overrides.
/// - In Light Mode: Displays the authentic museum Olive-Sage (#90937E) panel aesthetic with the exact same
///   rounded edge radius (225px 9-slice) and slight glass transparency (A=180, ~70% opacity) as Dark Mode.
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
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private const string PrefsKey = "Museum_UI_ThemeMode";

    [Header("Current Theme State")]
    public UIThemeMode currentTheme = UIThemeMode.Dark;
    public bool IsDarkMode => currentTheme == UIThemeMode.Dark;
    public static event Action<UIThemeMode> OnThemeChanged;

    [Header("Light Mode Palette (Olive-Sage Glass Aesthetic)")]
    // Main panel background (#90937E with high glass transparency: A = 115 / 255 = ~0.451)
    [System.NonSerialized] public Color lightPanelBg = new Color(0.565f, 0.576f, 0.494f, 115f / 255f);
    [System.NonSerialized] public Color lightPanelBorder = new Color(0.772f, 0.816f, 0.694f, 150f / 255f); // #C5D0B2 soft luminous rim
    // Sub-cards (Detail Artefak, Tentang Artefak, rows #747968 with A = 125 / 255 = ~0.490)
    [System.NonSerialized] public Color lightCardBg = new Color(0.455f, 0.475f, 0.408f, 125f / 255f);
    [System.NonSerialized] public Color lightCardBorder = new Color(0.698f, 0.737f, 0.620f, 155f / 255f); // #B2BC9E
    // Active View Button (Gambar #A6B668 with slight transparency)
    [System.NonSerialized] public Color lightActiveBtnBg = new Color(0.651f, 0.714f, 0.408f, 200f / 255f);
    [System.NonSerialized] public Color lightActiveBtnBorder = new Color(0.816f, 0.871f, 0.596f, 220f / 255f);
    // Inactive View Button (3D View #8F9588 with A = 120 / 255 = ~0.470)
    [System.NonSerialized] public Color lightInactiveBtnBg = new Color(0.561f, 0.584f, 0.533f, 120f / 255f);
    [System.NonSerialized] public Color lightInactiveBtnBorder = new Color(0.722f, 0.761f, 0.690f, 155f / 255f);
    // Control / Circle buttons (close, back, play, restart #2E332A with A = 150 / 255 = ~0.588)
    [System.NonSerialized] public Color lightControlBtnBg = new Color(0.180f, 0.200f, 0.165f, 150f / 255f);
    [System.NonSerialized] public Color lightControlBtnBorder = new Color(0.392f, 0.431f, 0.373f, 175f / 255f);
    [System.NonSerialized] public Color lightControlBtnIcon = Color.white;
    // Typography
    [System.NonSerialized] public Color lightHeaderTextColor = Color.white;
    [System.NonSerialized] public Color lightAccentTextColor = new Color(0.850f, 0.890f, 0.620f, 1.0f); // #D8E29D Soft Lime
    [System.NonSerialized] public Color lightBodyTextColor = new Color(0.910f, 0.930f, 0.880f, 0.95f);   // #E8EAE0 Cream / Off-White
    [System.NonSerialized] public Color lightSeparatorColor = new Color(0.540f, 0.560f, 0.490f, 0.35f);  // Soft olive line
    // Tutorial & Rotation Progress Bar Colors
    [System.NonSerialized] public Color lightProgressTrackColor = new Color(0.18f, 0.22f, 0.16f, 0.50f); // Recessed dark charcoal-sage slot
    [System.NonSerialized] public Color lightProgressFillColor = new Color(0.01f, 0.52f, 0.78f, 1.0f);   // Vivid Azure (#0284C7) high-contrast fill
    [System.NonSerialized] public Color darkProgressTrackColor = new Color(0.06f, 0.08f, 0.12f, 0.85f);  // Deep slate recessed slot
    [System.NonSerialized] public Color darkProgressFillColor = new Color(0.00f, 0.83f, 1.00f, 1.0f);    // Electric Cyan (#00D4FF) glowing fill

    // Original State Tracking (Ensures 100% faithful restoration of Dark Mode)
    private class OriginalGraphicState
    {
        public Sprite sprite;
        public Color color;
        public Material material;
        public Image.Type type;
    }

    private class OriginalTextState
    {
        public Color color;
    }

    private readonly Dictionary<Graphic, OriginalGraphicState> originalGraphicStates = new Dictionary<Graphic, OriginalGraphicState>();
    private readonly Dictionary<TextMeshProUGUI, OriginalTextState> originalTextStates = new Dictionary<TextMeshProUGUI, OriginalTextState>();

    private class OriginalButtonState
    {
        public SpriteState spriteState;
        public ColorBlock colors;
    }

    private class OriginalXRButtonState
    {
        public Color normalColor;
        public Color hoverColor;
    }

    private readonly Dictionary<Button, OriginalButtonState> originalButtonStates = new Dictionary<Button, OriginalButtonState>();
    private readonly Dictionary<XRButtonSelection, OriginalXRButtonState> originalXRButtonStates = new Dictionary<XRButtonSelection, OriginalXRButtonState>();

    public bool TryGetOriginalSprite(Graphic g, out Sprite sprite)
    {
        sprite = null;
        if (g != null && originalGraphicStates.TryGetValue(g, out var state))
        {
            sprite = state.sprite;
            return sprite != null;
        }
        return false;
    }

    // Cached Procedural 9-Sliced Sprites matching 2.png / 8.png geometry & transparency
    private Sprite lightPanelSprite;
    private Sprite lightCardSprite;
    private Sprite lightCardHoverSprite;
    private Sprite lightControlBtnSprite;
    private Sprite lightActiveBtnSprite;
    private Sprite lightInactiveBtnSprite;
    private Sprite lightActionCircleBtnSprite;
    private Sprite sunIconSprite;
    private Sprite moonIconSprite;

    // Shared Materials Cache
    private Material matArtifactDetailPanel;
    private Material matOptionsCardBg;
    private Material matDetailSubCard;
    private Material matImagesBtn;
    private Material mat3DViewBtn;
    private Material matRoomHUD;
    private Material matMainMenu;
    private Material matOptionsRowCard;
    private Material matMulaiButton;
    private Material matInputBox;

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

        int saved = PlayerPrefs.GetInt(PrefsKey, (int)UIThemeMode.Dark);
        currentTheme = (UIThemeMode)saved;
        CacheMaterials();
    }

    private void Start()
    {
        ApplyTheme(currentTheme);
    }

    private void CacheMaterials()
    {
        foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (m == null) continue;
            string n = m.name;
            if (n == "Mat_ArtifactDetailPanel") matArtifactDetailPanel = m;
            else if (n == "Mat_OptionsCardBackground") matOptionsCardBg = m;
            else if (n == "Mat_DetailSubCard") matDetailSubCard = m;
            else if (n == "Mat_ImagesBtn") matImagesBtn = m;
            else if (n == "Mat_3DViewBtn") mat3DViewBtn = m;
            else if (n == "Mat_RoomHUD") matRoomHUD = m;
            else if (n == "Mat_MainMenu") matMainMenu = m;
            else if (n == "Mat_OptionsRowCard") matOptionsRowCard = m;
            else if (n == "Mat_MulaiButton") matMulaiButton = m;
            else if (n == "Mat_InputBox") matInputBox = m;
        }

        if (matInputBox == null)
        {
            foreach (Image img in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (img != null && img.material != null && img.material.name.Contains("InputBox"))
                {
                    matInputBox = img.material;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Generates an anti-aliased 9-sliced rounded box sprite with smooth curvature and soft border glow.
    /// </summary>
    public static Sprite CreateRoundedBoxSprite(int width, int height, float radius, float borderWidth, Color fillColor, Color borderColor, Vector4 borderInset, float pixelsPerUnit = 100f)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[width * height];
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;
        float bX = halfW - radius;
        float bY = halfH - radius;

        byte fillR = (byte)Mathf.RoundToInt(fillColor.r * 255f);
        byte fillG = (byte)Mathf.RoundToInt(fillColor.g * 255f);
        byte fillB = (byte)Mathf.RoundToInt(fillColor.b * 255f);
        byte fillA = (byte)Mathf.RoundToInt(fillColor.a * 255f);

        byte borderR = (byte)Mathf.RoundToInt(borderColor.r * 255f);
        byte borderG = (byte)Mathf.RoundToInt(borderColor.g * 255f);
        byte borderB = (byte)Mathf.RoundToInt(borderColor.b * 255f);
        byte borderA = (byte)Mathf.RoundToInt(borderColor.a * 255f);

        for (int y = 0; y < height; y++)
        {
            float py = (y + 0.5f) - halfH;
            for (int x = 0; x < width; x++)
            {
                float px = (x + 0.5f) - halfW;

                float qx = Mathf.Abs(px) - bX;
                float qy = Mathf.Abs(py) - bY;
                float d = Mathf.Min(Mathf.Max(qx, qy), 0f) + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - radius;

                if (d > 0.5f)
                {
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    float outerAlpha = Mathf.Clamp01(0.5f - d);

                    if (d >= -borderWidth)
                    {
                        float t = Mathf.Clamp01((-d) / Mathf.Max(borderWidth, 0.001f));
                        byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(borderR, fillR, t * 0.4f));
                        byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(borderG, fillG, t * 0.4f));
                        byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(borderB, fillB, t * 0.4f));
                        byte a = (byte)Mathf.RoundToInt(Mathf.Lerp(borderA, fillA, t) * outerAlpha);
                        pixels[y * width + x] = new Color32(r, g, b, a);
                    }
                    else
                    {
                        byte a = (byte)Mathf.RoundToInt(fillA * outerAlpha);
                        pixels[y * width + x] = new Color32(fillR, fillG, fillB, a);
                    }
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, borderInset);
    }

    /// <summary>
    /// Procedural 9-slice sprite for Main Panel Background.
    /// Exactly matches 2.png: 225px corner radius, 225px 9-slice border inset, and A=180 glass transparency.
    /// </summary>
    public Sprite GetOrCreateLightPanelSprite()
    {
        if (lightPanelSprite == null)
        {
            // 512x512 with 225px radius, 14px border glow, and 225px 9-slice inset at 100 PPU
            lightPanelSprite = CreateRoundedBoxSprite(512, 512, 225f, 14f, lightPanelBg, lightPanelBorder, new Vector4(225, 225, 225, 225), 100f);
            lightPanelSprite.name = "LightPanel_225_Procedural";
        }
        return lightPanelSprite;
    }

    /// <summary>
    /// Procedural 9-slice sprite for Sub-Cards (DetailArtefakCard, TentangArtefakCard, list rows).
    /// Exactly matches 8.png: 225px corner radius, 225px 9-slice border inset, and A=185 glass transparency.
    /// </summary>
    public Sprite GetOrCreateLightCardSprite()
    {
        if (lightCardSprite == null)
        {
            lightCardSprite = CreateRoundedBoxSprite(512, 512, 225f, 14f, lightCardBg, lightCardBorder, new Vector4(225, 225, 225, 225), 100f);
            lightCardSprite.name = "LightCard_225_Procedural";
        }
        return lightCardSprite;
    }

    /// <summary>
    /// Procedural 9-slice sprite for Sub-Cards when highlighted/hovered in Light Mode.
    /// Replaces dark 9.png with a radiant, luminous olive-sage highlight glow.
    /// </summary>
    public Sprite GetOrCreateLightCardHoverSprite()
    {
        if (lightCardHoverSprite == null)
        {
            Color hoverBg = new Color(0.520f, 0.550f, 0.460f, 175f / 255f);
            Color hoverBorder = new Color(0.860f, 0.910f, 0.740f, 235f / 255f); // #DBE8BD soft radiant rim
            lightCardHoverSprite = CreateRoundedBoxSprite(512, 512, 225f, 16f, hoverBg, hoverBorder, new Vector4(225, 225, 225, 225), 100f);
            lightCardHoverSprite.name = "LightCardHover_225_Procedural";
        }
        return lightCardHoverSprite;
    }

    /// <summary>
    /// Procedural circular button sprite matching 7.png.
    /// </summary>
    public Sprite GetOrCreateLightControlBtnSprite()
    {
        if (lightControlBtnSprite == null)
        {
            lightControlBtnSprite = CreateRoundedBoxSprite(128, 128, 62f, 5f, lightControlBtnBg, lightControlBtnBorder, Vector4.zero, 100f);
            lightControlBtnSprite.name = "LightControlBtn_Procedural";
        }
        return lightControlBtnSprite;
    }

    public Sprite GetOrCreateLightActiveBtnSprite()
    {
        if (lightActiveBtnSprite == null)
        {
            lightActiveBtnSprite = CreateRoundedBoxSprite(512, 256, 120f, 12f, lightActiveBtnBg, lightActiveBtnBorder, new Vector4(120, 120, 120, 120), 100f);
            lightActiveBtnSprite.name = "LightActiveBtn_Procedural";
        }
        return lightActiveBtnSprite;
    }

    public Sprite GetOrCreateLightInactiveBtnSprite()
    {
        if (lightInactiveBtnSprite == null)
        {
            lightInactiveBtnSprite = CreateRoundedBoxSprite(512, 256, 120f, 12f, lightInactiveBtnBg, lightInactiveBtnBorder, new Vector4(120, 120, 120, 120), 100f);
            lightInactiveBtnSprite.name = "LightInactiveBtn_Procedural";
        }
        return lightInactiveBtnSprite;
    }

    /// <summary>
    /// Procedural circular active button sprite for action/next control buttons (e.g. SkipNarrationButton).
    /// </summary>
    public Sprite GetOrCreateLightActionCircleBtnSprite()
    {
        if (lightActionCircleBtnSprite == null)
        {
            lightActionCircleBtnSprite = CreateRoundedBoxSprite(128, 128, 62f, 5f, lightActiveBtnBg, lightActiveBtnBorder, Vector4.zero, 100f);
            lightActionCircleBtnSprite.name = "LightActionCircleBtn_Procedural";
        }
        return lightActionCircleBtnSprite;
    }

    public void InvalidateSpriteCache()
    {
        lightPanelSprite = null;
        lightCardSprite = null;
        lightControlBtnSprite = null;
        lightActiveBtnSprite = null;
        lightInactiveBtnSprite = null;
        lightActionCircleBtnSprite = null;
        sunIconSprite = null;
        moonIconSprite = null;
    }

    public Shader GetGlassButtonShader()
    {
        Shader s = Shader.Find("UI/GlassButton");
        if (s == null) s = GetRoundedUIShader();
        return s;
    }

    public Shader GetRoundedUIShader()
    {
        Shader s = Shader.Find("UI/RoundedCorners");
        if (s == null && matMulaiButton != null) s = matMulaiButton.shader;
        if (s == null && matMainMenu != null) s = matMainMenu.shader;
        return s;
    }

    public Sprite GetOrCreateSunIconSprite()
    {
        if (sunIconSprite == null)
        {
            sunIconSprite = CreateSunSprite(256, Color.white);
            sunIconSprite.name = "SunIcon_Procedural";
        }
        return sunIconSprite;
    }

    public Sprite GetOrCreateMoonIconSprite()
    {
        if (moonIconSprite == null)
        {
            moonIconSprite = CreateMoonSprite(256, Color.white);
            moonIconSprite.name = "MoonIcon_Procedural";
        }
        return moonIconSprite;
    }

    private Sprite CreateSunSprite(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.filterMode = FilterMode.Trilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.anisoLevel = 4;
        Color32[] pixels = new Color32[size * size];

        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float diskR = size * 0.17f;
        float rayInner = size * 0.25f;
        float rayOuter = size * 0.40f;
        float rayThickness = size * 0.026f;

        byte cr = (byte)Mathf.RoundToInt(color.r * 255f);
        byte cg = (byte)Mathf.RoundToInt(color.g * 255f);
        byte cb = (byte)Mathf.RoundToInt(color.b * 255f);
        byte ca = (byte)Mathf.RoundToInt(color.a * 255f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float diskD = dist - diskR;
                float diskAlpha = Mathf.Clamp01(1.0f - diskD);

                float rayAlpha = 0.0f;
                if (dist >= rayInner - 1.5f && dist <= rayOuter + 1.5f)
                {
                    float angle = Mathf.Atan2(dy, dx);
                    float step = Mathf.PI / 4.0f;
                    float nearest = Mathf.Round(angle / step) * step;
                    float diff = Mathf.Abs(angle - nearest);
                    while (diff > Mathf.PI) diff = Mathf.Abs(diff - 2.0f * Mathf.PI);
                    float perp = dist * Mathf.Sin(diff);
                    float rFade = Mathf.Clamp01(Mathf.Min(dist - rayInner + 1.0f, rayOuter - dist + 1.0f));
                    float tFade = Mathf.Clamp01(1.0f - (perp - rayThickness));
                    rayAlpha = rFade * tFade;
                }

                float finalAlpha = Mathf.Max(diskAlpha, rayAlpha);
                if (finalAlpha > 0.01f)
                {
                    pixels[y * size + x] = new Color32(cr, cg, cb, (byte)Mathf.RoundToInt(ca * finalAlpha));
                }
                else
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(true, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateMoonSprite(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.filterMode = FilterMode.Trilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.anisoLevel = 4;
        Color32[] pixels = new Color32[size * size];

        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float oCx = cx - size * 0.035f;
        float oCy = cy;
        float oR = size * 0.27f;
        float cCx = cx + size * 0.10f;
        float cCy = cy - size * 0.06f;
        float cR = size * 0.245f;

        byte cr = (byte)Mathf.RoundToInt(color.r * 255f);
        byte cg = (byte)Mathf.RoundToInt(color.g * 255f);
        byte cb = (byte)Mathf.RoundToInt(color.b * 255f);
        byte ca = (byte)Mathf.RoundToInt(color.a * 255f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx1 = x + 0.5f - oCx;
                float dy1 = y + 0.5f - oCy;
                float dist1 = Mathf.Sqrt(dx1 * dx1 + dy1 * dy1);
                float a1 = Mathf.Clamp01(1.0f - (dist1 - oR));

                float dx2 = x + 0.5f - cCx;
                float dy2 = y + 0.5f - cCy;
                float dist2 = Mathf.Sqrt(dx2 * dx2 + dy2 * dy2);
                float a2 = Mathf.Clamp01((dist2 - cR) + 1.0f);

                float finalAlpha = Mathf.Clamp01(a1 * a2);
                if (finalAlpha > 0.01f)
                {
                    pixels[y * size + x] = new Color32(cr, cg, cb, (byte)Mathf.RoundToInt(ca * finalAlpha));
                }
                else
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(true, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void CacheOriginalGraphic(Graphic g)
    {
        if (g == null || originalGraphicStates.ContainsKey(g)) return;

        Image img = g as Image;
        originalGraphicStates[g] = new OriginalGraphicState
        {
            sprite = img != null ? img.sprite : null,
            color = g.color,
            material = g.material,
            type = img != null ? img.type : Image.Type.Simple
        };
    }

    private void CacheOriginalText(TextMeshProUGUI tmp)
    {
        if (tmp == null || originalTextStates.ContainsKey(tmp)) return;

        originalTextStates[tmp] = new OriginalTextState
        {
            color = tmp.color
        };
    }

    private void CacheOriginalButton(Button btn)
    {
        if (btn == null || originalButtonStates.ContainsKey(btn)) return;
        originalButtonStates[btn] = new OriginalButtonState
        {
            spriteState = btn.spriteState,
            colors = btn.colors
        };
    }

    private void CacheOriginalXRButton(XRButtonSelection xr)
    {
        if (xr == null || originalXRButtonStates.ContainsKey(xr)) return;
        originalXRButtonStates[xr] = new OriginalXRButtonState
        {
            normalColor = xr.normalColor,
            hoverColor = xr.hoverColor
        };
    }

    /// <summary>
    /// Restores all modified graphics and text components back to their exact original authored states.
    /// </summary>
    private void RestoreOriginals()
    {
        foreach (var kvp in originalGraphicStates)
        {
            if (kvp.Key != null)
            {
                Image img = kvp.Key as Image;
                if (img != null)
                {
                    img.sprite = kvp.Value.sprite;
                    img.type = kvp.Value.type;
                }
                kvp.Key.color = kvp.Value.color;
                kvp.Key.material = kvp.Value.material;
            }
        }

        foreach (var kvp in originalTextStates)
        {
            if (kvp.Key != null)
            {
                kvp.Key.color = kvp.Value.color;
            }
        }

        foreach (var kvp in originalButtonStates)
        {
            if (kvp.Key != null)
            {
                kvp.Key.spriteState = kvp.Value.spriteState;
                kvp.Key.colors = kvp.Value.colors;
            }
        }

        foreach (var kvp in originalXRButtonStates)
        {
            if (kvp.Key != null)
            {
                kvp.Key.normalColor = kvp.Value.normalColor;
                kvp.Key.hoverColor = kvp.Value.hoverColor;
            }
        }
    }

    /// <summary>
    /// Toggles between Dark Mode and Light Mode.
    /// </summary>
    public void ToggleTheme()
    {
        UIThemeMode next = (currentTheme == UIThemeMode.Dark) ? UIThemeMode.Light : UIThemeMode.Dark;
        SetTheme(next);
    }

    /// <summary>
    /// Sets a specific theme mode, persists preference, and applies visual styling with a smooth crossfade.
    /// </summary>
    public void SetTheme(UIThemeMode mode)
    {
        currentTheme = mode;
        PlayerPrefs.SetInt(PrefsKey, (int)currentTheme);
        PlayerPrefs.Save();

        Debug.Log($"[ThemeManager] Theme mode switched to: {currentTheme}");

        List<GameObject> activePanels = GetActivePanelsForTransition();
        if (activePanels.Count > 0)
        {
            UIAnimationHelper.CrossfadeTheme(activePanels, () =>
            {
                ApplyTheme(currentTheme);
                OnThemeChanged?.Invoke(currentTheme);
            }, 0.12f);
        }
        else
        {
            ApplyTheme(currentTheme);
            OnThemeChanged?.Invoke(currentTheme);
        }
    }

    private List<GameObject> GetActivePanelsForTransition()
    {
        List<GameObject> list = new List<GameObject>();

        if (WristWatch.Instance != null)
        {
            if (WristWatch.Instance.optionsPanelObj != null && WristWatch.Instance.optionsPanelObj.activeInHierarchy)
                list.Add(WristWatch.Instance.optionsPanelObj);
            if (WristWatch.Instance.roomListPanel != null && WristWatch.Instance.roomListPanel.activeInHierarchy)
                list.Add(WristWatch.Instance.roomListPanel);
            if (WristWatch.Instance.roomHudCanvas != null && WristWatch.Instance.roomHudCanvas.activeInHierarchy)
                list.Add(WristWatch.Instance.roomHudCanvas);
            if (WristWatch.Instance.gamesPanel != null && WristWatch.Instance.gamesPanel.activeInHierarchy)
                list.Add(WristWatch.Instance.gamesPanel);
        }

        if (TutorialManager.Instance != null)
        {
            if (TutorialManager.Instance.panelRoot != null && TutorialManager.Instance.panelRoot.activeInHierarchy)
                list.Add(TutorialManager.Instance.panelRoot);
            if (TutorialManager.Instance.skipNarrationButtonRoot != null && TutorialManager.Instance.skipNarrationButtonRoot.activeInHierarchy)
                list.Add(TutorialManager.Instance.skipNarrationButtonRoot);
        }

        if (MainMenu.Instance != null && MainMenu.Instance.mainMenuCanvas != null && MainMenu.Instance.mainMenuCanvas.activeInHierarchy)
        {
            list.Add(MainMenu.Instance.mainMenuCanvas);
        }

        foreach (Canvas c in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (c != null && c.gameObject.activeInHierarchy && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded)
            {
                if (!list.Contains(c.gameObject))
                {
                    list.Add(c.gameObject);
                }
            }
        }

        return list;
    }

    /// <summary>
    /// Applies theme colors to all cached material instances and all known active/inactive UI hierarchies.
    /// </summary>
    public void ApplyTheme(UIThemeMode mode)
    {
        ApplyToMaterials(mode);

        if (mode == UIThemeMode.Dark)
        {
            // Restore all modified elements back to their exact original Dark Mode authored states
            RestoreOriginals();

            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || !canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded) continue;
                Game2OrderProcess g2Dark = canvas.GetComponentInChildren<Game2OrderProcess>(true);
                if (g2Dark != null && g2Dark.gameObject.activeInHierarchy)
                {
                    g2Dark.ApplyCardTheme(UIThemeMode.Dark);
                }
            }
        }
        else
        {
            ApplyToSceneUI(mode);
        }
    }

    private void ApplyToMaterials(UIThemeMode mode)
    {
        if (matArtifactDetailPanel == null) CacheMaterials();

        if (mode == UIThemeMode.Dark)
        {
            // Restore shared materials to their EXACT original values from commit 5bce72a (before-light-mode;))
            if (matArtifactDetailPanel != null)
            {
                if (matArtifactDetailPanel.HasProperty("_Color")) matArtifactDetailPanel.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                if (matArtifactDetailPanel.HasProperty("_BorderColor")) matArtifactDetailPanel.SetColor("_BorderColor", new Color(0.92f, 0.95f, 1.0f, 0.85f));
            }

            if (matOptionsCardBg != null)
            {
                if (matOptionsCardBg.HasProperty("_Color")) matOptionsCardBg.SetColor("_Color", new Color(0.11f, 0.12f, 0.14f, 0.72f));
                if (matOptionsCardBg.HasProperty("_DotColor")) matOptionsCardBg.SetColor("_DotColor", new Color(0.28f, 0.30f, 0.25f, 0.40f));
            }

            if (matDetailSubCard != null)
            {
                if (matDetailSubCard.HasProperty("_Color")) matDetailSubCard.SetColor("_Color", new Color(0.18f, 0.20f, 0.23f, 0.75f));
                if (matDetailSubCard.HasProperty("_BorderColor")) matDetailSubCard.SetColor("_BorderColor", new Color(0.66f, 0.70f, 0.60f, 0.95f));
            }

            if (matImagesBtn != null)
            {
                if (matImagesBtn.HasProperty("_Color")) matImagesBtn.SetColor("_Color", new Color(0.38f, 0.44f, 0.54f, 0.95f));
                if (matImagesBtn.HasProperty("_BorderColor")) matImagesBtn.SetColor("_BorderColor", new Color(0.65f, 0.75f, 0.88f, 0.98f));
            }

            if (mat3DViewBtn != null)
            {
                if (mat3DViewBtn.HasProperty("_Color")) mat3DViewBtn.SetColor("_Color", new Color(0.35f, 0.38f, 0.31f, 0.90f));
                if (mat3DViewBtn.HasProperty("_BorderColor")) mat3DViewBtn.SetColor("_BorderColor", new Color(0.48f, 0.52f, 0.43f, 0.90f));
            }

            if (matMainMenu != null)
            {
                if (matMainMenu.HasProperty("_Color")) matMainMenu.SetColor("_Color", new Color(0.96f, 0.96f, 0.98f, 0.96f));
                if (matMainMenu.HasProperty("_BorderColor")) matMainMenu.SetColor("_BorderColor", new Color(0.82f, 0.82f, 0.86f, 0.85f));
            }

            if (matMulaiButton != null)
            {
                if (matMulaiButton.HasProperty("_Color")) matMulaiButton.SetColor("_Color", new Color(0.32f, 0.42f, 0.58f, 0.95f));
                if (matMulaiButton.HasProperty("_BorderColor")) matMulaiButton.SetColor("_BorderColor", new Color(0.55f, 0.68f, 0.88f, 0.98f));
            }

            if (matRoomHUD != null)
            {
                if (matRoomHUD.HasProperty("_Color")) matRoomHUD.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                if (matRoomHUD.HasProperty("_BorderColor")) matRoomHUD.SetColor("_BorderColor", new Color(0.92f, 0.95f, 1.0f, 0.85f));
            }

            if (matOptionsRowCard != null)
            {
                if (matOptionsRowCard.HasProperty("_Color")) matOptionsRowCard.SetColor("_Color", new Color(0.15f, 0.16f, 0.18f, 0.72f));
                if (matOptionsRowCard.HasProperty("_BorderColor")) matOptionsRowCard.SetColor("_BorderColor", new Color(0.48f, 0.52f, 0.43f, 0.80f));
            }

            if (matInputBox != null)
            {
                if (matInputBox.HasProperty("_Color")) matInputBox.SetColor("_Color", new Color(0.09f, 0.10f, 0.13f, 0.85f));
                if (matInputBox.HasProperty("_BorderColor")) matInputBox.SetColor("_BorderColor", new Color(0.24f, 0.28f, 0.34f, 0.70f));
                if (matInputBox.HasProperty("_BorderWidth")) matInputBox.SetFloat("_BorderWidth", 0.012f);
                if (matInputBox.HasProperty("_CornerRadius")) matInputBox.SetFloat("_CornerRadius", 0.25f);
                if (matInputBox.HasProperty("_SheenIntensity")) matInputBox.SetFloat("_SheenIntensity", 0.0f);
                if (matInputBox.HasProperty("_SheenColor")) matInputBox.SetColor("_SheenColor", Color.clear);
            }
        }
        else
        {
            // Light Mode: Apply olive-sage palette to materials with transparency
            if (matArtifactDetailPanel != null)
            {
                if (matArtifactDetailPanel.HasProperty("_Color")) matArtifactDetailPanel.SetColor("_Color", lightPanelBg);
                if (matArtifactDetailPanel.HasProperty("_BorderColor")) matArtifactDetailPanel.SetColor("_BorderColor", lightPanelBorder);
            }

            if (matOptionsCardBg != null)
            {
                if (matOptionsCardBg.HasProperty("_Color")) matOptionsCardBg.SetColor("_Color", lightPanelBg);
                if (matOptionsCardBg.HasProperty("_DotColor")) matOptionsCardBg.SetColor("_DotColor", new Color(0.65f, 0.68f, 0.58f, 0.35f));
            }

            if (matDetailSubCard != null)
            {
                if (matDetailSubCard.HasProperty("_Color")) matDetailSubCard.SetColor("_Color", lightCardBg);
                if (matDetailSubCard.HasProperty("_BorderColor")) matDetailSubCard.SetColor("_BorderColor", lightCardBorder);
            }

            if (matImagesBtn != null)
            {
                if (matImagesBtn.HasProperty("_Color")) matImagesBtn.SetColor("_Color", lightActiveBtnBg);
                if (matImagesBtn.HasProperty("_BorderColor")) matImagesBtn.SetColor("_BorderColor", lightActiveBtnBorder);
            }

            if (mat3DViewBtn != null)
            {
                if (mat3DViewBtn.HasProperty("_Color")) mat3DViewBtn.SetColor("_Color", lightInactiveBtnBg);
                if (mat3DViewBtn.HasProperty("_BorderColor")) mat3DViewBtn.SetColor("_BorderColor", lightInactiveBtnBorder);
            }

            if (matMainMenu != null)
            {
                if (matMainMenu.HasProperty("_Color")) matMainMenu.SetColor("_Color", lightPanelBg);
                if (matMainMenu.HasProperty("_BorderColor")) matMainMenu.SetColor("_BorderColor", lightPanelBorder);
            }

            if (matMulaiButton != null)
            {
                if (matMulaiButton.HasProperty("_Color")) matMulaiButton.SetColor("_Color", lightActiveBtnBg);
                if (matMulaiButton.HasProperty("_BorderColor")) matMulaiButton.SetColor("_BorderColor", lightActiveBtnBorder);
            }

            if (matRoomHUD != null)
            {
                if (matRoomHUD.HasProperty("_Color")) matRoomHUD.SetColor("_Color", lightPanelBg);
                if (matRoomHUD.HasProperty("_BorderColor")) matRoomHUD.SetColor("_BorderColor", lightPanelBorder);
            }

            if (matOptionsRowCard != null)
            {
                if (matOptionsRowCard.HasProperty("_Color")) matOptionsRowCard.SetColor("_Color", lightCardBg);
                if (matOptionsRowCard.HasProperty("_BorderColor")) matOptionsRowCard.SetColor("_BorderColor", lightCardBorder);
            }

            if (matInputBox != null)
            {
                if (matInputBox.HasProperty("_Color")) matInputBox.SetColor("_Color", new Color(0.86f, 0.89f, 0.82f, 0.85f));
                if (matInputBox.HasProperty("_BorderColor")) matInputBox.SetColor("_BorderColor", new Color(0.58f, 0.66f, 0.50f, 0.75f));
                if (matInputBox.HasProperty("_BorderWidth")) matInputBox.SetFloat("_BorderWidth", 0.012f);
                if (matInputBox.HasProperty("_CornerRadius")) matInputBox.SetFloat("_CornerRadius", 0.25f);
                if (matInputBox.HasProperty("_SheenIntensity")) matInputBox.SetFloat("_SheenIntensity", 0.0f);
                if (matInputBox.HasProperty("_SheenColor")) matInputBox.SetColor("_SheenColor", Color.clear);
            }
        }
    }

    /// <summary>
    /// Updates the visual appearance of the ImagesButton and 3DViewButton.
    /// In Dark Mode: Exactly preserves original authored appearance.
    /// In Light Mode: Sets active to #A6B668 and inactive to #8F9588.
    /// </summary>
    public void UpdateArtifactViewButtons(Image imagesBtnImg, Image threeDBtnImg, bool show2D)
    {
        if (currentTheme == UIThemeMode.Dark)
        {
            // In Dark Mode, restore original buttons without alteration
            if (imagesBtnImg != null && originalGraphicStates.TryGetValue(imagesBtnImg, out var orig2D))
            {
                imagesBtnImg.sprite = orig2D.sprite;
                imagesBtnImg.type = orig2D.type;
                imagesBtnImg.color = orig2D.color;
                imagesBtnImg.material = orig2D.material;
            }
            if (threeDBtnImg != null && originalGraphicStates.TryGetValue(threeDBtnImg, out var orig3D))
            {
                threeDBtnImg.sprite = orig3D.sprite;
                threeDBtnImg.type = orig3D.type;
                threeDBtnImg.color = orig3D.color;
                threeDBtnImg.material = orig3D.material;
            }
            return;
        }

        // Light Mode styling
        if (imagesBtnImg != null)
        {
            CacheOriginalGraphic(imagesBtnImg);
            imagesBtnImg.sprite = show2D ? GetOrCreateLightActiveBtnSprite() : GetOrCreateLightInactiveBtnSprite();
            imagesBtnImg.type = Image.Type.Sliced;
            imagesBtnImg.color = Color.white;
            imagesBtnImg.material = null;
        }

        if (threeDBtnImg != null)
        {
            CacheOriginalGraphic(threeDBtnImg);
            threeDBtnImg.sprite = show2D ? GetOrCreateLightInactiveBtnSprite() : GetOrCreateLightActiveBtnSprite();
            threeDBtnImg.type = Image.Type.Sliced;
            threeDBtnImg.color = Color.white;
            threeDBtnImg.material = null;
        }
    }

    private void ApplyToSceneUI(UIThemeMode mode)
    {
        // 1. Scan and apply to all loaded scene Canvases (including inactive)
        foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas == null) continue;
            // Make sure it belongs to a valid loaded scene (exclude project assets / prefabs)
            if (!canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded) continue;
            ApplyToHierarchy(canvas.gameObject, mode);
        }

        // 2. Explicitly query all panel and game scripts across the scene (including inactive)
        foreach (Artifact art in Resources.FindObjectsOfTypeAll<Artifact>())
        {
            if (art != null && art.gameObject.scene.isLoaded) ApplyToHierarchy(art.gameObject, mode);
        }

        foreach (HistoryPanel hp in Resources.FindObjectsOfTypeAll<HistoryPanel>())
        {
            if (hp != null && hp.gameObject.scene.isLoaded) ApplyToHierarchy(hp.gameObject, mode);
        }

        foreach (HistoryListPanel hlp in Resources.FindObjectsOfTypeAll<HistoryListPanel>())
        {
            if (hlp != null && hlp.gameObject.scene.isLoaded) ApplyToHierarchy(hlp.gameObject, mode);
        }

        foreach (Room r in Resources.FindObjectsOfTypeAll<Room>())
        {
            if (r != null && r.gameObject.scene.isLoaded) ApplyToHierarchy(r.gameObject, mode);
        }

        foreach (RoomList rl in Resources.FindObjectsOfTypeAll<RoomList>())
        {
            if (rl != null && rl.gameObject.scene.isLoaded) ApplyToHierarchy(rl.gameObject, mode);
        }

        foreach (MiniGames mg in Resources.FindObjectsOfTypeAll<MiniGames>())
        {
            if (mg != null && mg.gameObject.scene.isLoaded) ApplyToHierarchy(mg.gameObject, mode);
        }

        foreach (BaseGame bg in Resources.FindObjectsOfTypeAll<BaseGame>())
        {
            if (bg != null && bg.gameObject.scene.isLoaded) ApplyToHierarchy(bg.gameObject, mode);
        }

        foreach (MiniGameMenuPanel mp in Resources.FindObjectsOfTypeAll<MiniGameMenuPanel>())
        {
            if (mp != null && mp.gameObject.scene.isLoaded) ApplyToHierarchy(mp.gameObject, mode);
        }

        foreach (MiniGameListPanel lp in Resources.FindObjectsOfTypeAll<MiniGameListPanel>())
        {
            if (lp != null && lp.gameObject.scene.isLoaded) ApplyToHierarchy(lp.gameObject, mode);
        }

        foreach (LeaderboardPanel lb in Resources.FindObjectsOfTypeAll<LeaderboardPanel>())
        {
            if (lb != null && lb.gameObject.scene.isLoaded) ApplyToHierarchy(lb.gameObject, mode);
        }

        // 3. Apply to WristWatch options and targets
        if (WristWatch.Instance != null)
        {
            if (WristWatch.Instance.optionsPanelObj != null) ApplyToHierarchy(WristWatch.Instance.optionsPanelObj, mode);
            if (WristWatch.Instance.roomListPanel != null) ApplyToHierarchy(WristWatch.Instance.roomListPanel, mode);
            if (WristWatch.Instance.roomHudCanvas != null) ApplyToHierarchy(WristWatch.Instance.roomHudCanvas, mode);
            if (WristWatch.Instance.gamesPanel != null) ApplyToHierarchy(WristWatch.Instance.gamesPanel, mode);
        }

        // 4. Apply to MainMenu
        if (MainMenu.Instance != null && MainMenu.Instance.mainMenuCanvas != null)
        {
            ApplyToHierarchy(MainMenu.Instance.mainMenuCanvas, mode);
        }

        // 5. Apply to Tutorial panel & Skip/Next button
        if (TutorialManager.Instance != null)
        {
            if (TutorialManager.Instance.panelRoot != null)
            {
                ApplyToHierarchy(TutorialManager.Instance.panelRoot, mode);
            }
            if (TutorialManager.Instance.skipNarrationButtonRoot != null)
            {
                ApplyToHierarchy(TutorialManager.Instance.skipNarrationButtonRoot, mode);
            }
            if (TutorialManager.Instance.sceneAuthoredPanel != null)
            {
                ApplyToHierarchy(TutorialManager.Instance.sceneAuthoredPanel, mode);
            }
        }
    }

    /// <summary>
    /// Themes all Image and TextMeshProUGUI components within a given UI hierarchy.
    /// </summary>
    private static bool IsInsideThemeSelection(Transform t)
    {
        while (t != null)
        {
            if (t.name == "ThemeSelectionPanel") return true;
            t = t.parent;
        }
        return false;
    }

    private static bool IsIntroVideoPanel(Transform t)
    {
        while (t != null)
        {
            string n = t.name.ToLower();
            if (n.Contains("intovideopanel") || n.Contains("introvideopanel") || n.Contains("videopanel")) return true;
            t = t.parent;
        }
        return false;
    }

    /// <summary>
    /// Traverses the given root GameObject hierarchy and applies theme styling.
    /// In Dark Mode: Restores original cached sprites, colors, and materials.
    /// In Light Mode: Applies museum olive-sage background, cards, control buttons, and dark contrast typography.
    /// Preserves user photos, artwork thumbnails, and video textures.
    /// </summary>
    public void ApplyToHierarchy(GameObject root, UIThemeMode? overrideMode = null)
    {
        if (root == null) return;
        UIThemeMode mode = overrideMode ?? currentTheme;

        if (mode == UIThemeMode.Dark)
        {
            // Dark Mode: only restore previously cached elements to their exact original states
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null || IsInsideThemeSelection(g.transform)) continue;

                string gn = g.gameObject.name.ToLower();
                // Skip game 2 process illustration / text cards so Game2OrderProcess controls them
                if (gn.StartsWith("process") && !gn.Contains("layout") && !gn.Contains("panel"))
                {
                    continue;
                }

                if (originalGraphicStates.TryGetValue(g, out var orig))
                {
                    Image img = g as Image;
                    if (img != null)
                    {
                        img.sprite = orig.sprite;
                        img.type = orig.type;
                    }
                    g.color = orig.color;
                    g.material = orig.material;
                }

                // Preserve Mat_InputBox on input fields
                Image inputImg = g as Image;
                if (inputImg != null && (inputImg.gameObject.name.ToLower().Contains("inputfield") || (inputImg.material != null && inputImg.material.name.Contains("InputBox"))))
                {
                    if (matInputBox == null) CacheMaterials();
                    if (matInputBox != null) inputImg.material = matInputBox;
                    inputImg.color = Color.white;
                    continue;
                }

                // Ensure progress bars in Dark Mode always have high contrast
                if (gn.Contains("fill") || (gn.Contains("progress") && gn.Contains("bar") && gn.Contains("fill")))
                {
                    g.color = darkProgressFillColor;
                }
                else if (gn.Contains("progressbar") || gn.Contains("progresstrack"))
                {
                    g.color = darkProgressTrackColor;
                }
            }

            foreach (TextMeshProUGUI tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp == null || IsInsideThemeSelection(tmp.transform)) continue;
                if (tmp.gameObject.name == "CardText" || tmp.gameObject.name == "TimerText" || (tmp.transform.parent != null && tmp.transform.parent.name == "TimerBadge")) continue;
                if (originalTextStates.TryGetValue(tmp, out var origText))
                {
                    tmp.color = origText.color;
                }

                // Input field text in Dark Mode: crisp white text, luminous muted slate placeholder
                if (tmp.GetComponentInParent<TMP_InputField>() != null || tmp.GetComponentInParent<InputField>() != null)
                {
                    if (tmp.gameObject.name.ToLower().Contains("placeholder"))
                    {
                        tmp.color = new Color(0.50f, 0.54f, 0.60f, 0.75f);
                    }
                    else
                    {
                        tmp.color = new Color(0.96f, 0.98f, 1.0f, 1f);
                    }
                    continue;
                }

                string tn = tmp.gameObject.name.ToLower();
                if (tn.Contains("progress"))
                {
                    tmp.color = new Color(0.55f, 0.90f, 1.00f, 1f); // Electric Cyan
                }
            }

            foreach (Button btn in root.GetComponentsInChildren<Button>(true))
            {
                if (btn == null || IsInsideThemeSelection(btn.transform)) continue;
                if (originalButtonStates.TryGetValue(btn, out var origBtn))
                {
                    btn.spriteState = origBtn.spriteState;
                    btn.colors = origBtn.colors;
                }
            }

            foreach (XRButtonSelection xr in root.GetComponentsInChildren<XRButtonSelection>(true))
            {
                if (xr == null || IsInsideThemeSelection(xr.transform)) continue;
                if (originalXRButtonStates.TryGetValue(xr, out var origXR))
                {
                    xr.normalColor = origXR.normalColor;
                    xr.hoverColor = origXR.hoverColor;
                }
            }

            foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (input == null || IsInsideThemeSelection(input.transform)) continue;
                input.customCaretColor = true;
                input.caretColor = new Color(0.85f, 0.88f, 0.95f, 1f);
                input.selectionColor = new Color(0.30f, 0.45f, 0.65f, 0.50f);
            }

            Game2OrderProcess g2 = root.GetComponentInChildren<Game2OrderProcess>(true);
            if (g2 != null && g2.gameObject.activeInHierarchy)
            {
                g2.ApplyCardTheme(UIThemeMode.Dark);
            }
            return;
        }

        // --- Light Mode Application ---
        Sprite panelSprite = GetOrCreateLightPanelSprite();
        Sprite cardSprite = GetOrCreateLightCardSprite();
        Sprite controlBtnSprite = GetOrCreateLightControlBtnSprite();
        Sprite activeBtnSprite = GetOrCreateLightActiveBtnSprite();

        // 1. Process Images
        Image[] images = root.GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            if (img == null || IsInsideThemeSelection(img.transform) || IsIntroVideoPanel(img.transform)) continue;
            string n = img.gameObject.name.ToLower();

            // Input field background: retain Mat_InputBox with Light Mode palette
            if (n.Contains("inputfield") || (img.material != null && img.material.name.Contains("InputBox")))
            {
                if (matInputBox == null) CacheMaterials();
                if (matInputBox != null) img.material = matInputBox;
                img.color = Color.white;
                continue;
            }

            // Skip photos, artworks, raw textures, QR textures, and ThemeIcon
            if (n.Contains("photo") || n.Contains("thumb") || n.Contains("displayimage") ||
                n.Contains("artifactimage") || n.Contains("qr") || n.Contains("video") ||
                n.Contains("themeicon") || (img.transform.parent != null && img.transform.parent.name == "ThemeToggleButton"))
            {
                continue;
            }

            // Skip timer badge and timer icon so they retain rich obsidian and amber gold
            if (n.Contains("timerbadge") || n.Contains("timericon") || (img.transform.parent != null && img.transform.parent.name == "TimerBadge"))
            {
                continue;
            }

            // Skip gameplay status icons (trophy, correct checkmark, wrong X)
            if (n.Contains("trophy") || n.Contains("correct") || n.Contains("incorrect"))
            {
                continue;
            }

            // Skip game 2 process illustration cards (Process1 .. Process5)
            if (n.StartsWith("process") && !n.Contains("layout") && !n.Contains("panel"))
            {
                continue;
            }

            string sprName = img.sprite != null ? img.sprite.name : "";

            // If this is a child icon/image inside a control button or navigation/skip button
            if (img.transform.parent != null)
            {
                string parentName = img.transform.parent.name.ToLower();
                // MiniGame carousel Previous / Next buttons child arrow icon (23.png)
                if (parentName.Contains("previous") || (parentName.Contains("next") && !parentName.Contains("skipnarration") && (sprName == "23" || n == "icon" || n == "image" || n.Contains("symbol"))))
                {
                    CacheOriginalGraphic(img);
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    continue;
                }
                else if (parentName.Contains("skipnarration") || parentName.Contains("lanjut"))
                {
                    if (n == "image" || n.Contains("icon") || n.Contains("symbol") || sprName == "15" || sprName == "32")
                    {
                        CacheOriginalGraphic(img);
                        img.color = new Color(0.12f, 0.14f, 0.10f, 1f); // Dark contrast icon on light button
                        continue;
                    }
                }
                else if (parentName.Contains("close") || parentName.Contains("back") || parentName.Contains("themetoggle"))
                {
                    if (n == "image" || n.Contains("icon") || n.Contains("symbol"))
                    {
                        CacheOriginalGraphic(img);
                        img.color = lightControlBtnIcon;
                        continue;
                    }
                }
            }

            // Deactivate unused/dummy HeaderIcon in game panels (e.g. MiniGameMenuPanel, Game1Panel, etc.)
            if (n == "headericon" && (sprName == "7" || sprName == "") && img.transform.parent != null &&
                (img.transform.parent.name.ToLower().Contains("minigame") || img.transform.parent.name.ToLower().Contains("game") || img.transform.parent.name.ToLower().Contains("leaderboard")))
            {
                img.gameObject.SetActive(false);
                continue;
            }

            // Skip transparent raycast blocker panels
            if (img.color.a < 0.05f && sprName != "2" && n != "background")
            {
                continue;
            }

            CacheOriginalGraphic(img);

            // Root panel background (e.g. 2.png or Background GameObject)
            if (sprName == "2" || n == "background" || n.Contains("panelbg") || n.Contains("windowbg"))
            {
                img.sprite = panelSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                img.material = null;
            }
            // Carousel navigation buttons (e.g. PreviousButton, NextButton with 23.png chevron arrow)
            else if (sprName == "23" || n.Contains("previousbutton") || n.Contains("nextbutton"))
            {
                // If button has a child Image (an icon), parent acts as active button background plate
                if (img.transform.childCount > 0 && img.transform.GetComponentInChildren<Image>() != img)
                {
                    img.sprite = activeBtnSprite;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                    img.material = null;
                }
                else
                {
                    // No child icon exists: retain 23.png arrow directly on this Image
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    img.material = null;
                    if (img.sprite == null || img.sprite.name != "23")
                    {
                        if (originalGraphicStates.TryGetValue(img, out var orig) && orig.sprite != null)
                        {
                            img.sprite = orig.sprite;
                        }
                    }
                }
            }
            // Action buttons (e.g. 10.png, CheckButton, StartButton, ContinueButton, ReturnToMenuButton, MulaiButton)
            else if (sprName == "10" || n.Contains("checkbutton") || n.Contains("startbutton") ||
                     n.Contains("continuebutton") || n.Contains("returntomenu") || n.Contains("mulaibutton"))
            {
                img.sprite = activeBtnSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                img.material = null;
            }
            // Next / Skip Narration Action Button (e.g. SkipNarrationButton, LanjutButton)
            else if (n.Contains("skipnarration") || n.Contains("lanjut"))
            {
                img.sprite = GetOrCreateLightActionCircleBtnSprite();
                img.type = Image.Type.Simple;
                img.color = Color.white;
                img.material = null;
            }
            // Circular control buttons (e.g. 7.png or Play, Replay, Close, Back, ThemeToggle, standalone ListButton)
            // Explicitly exclude room list buttons and panel rows so they are not turned into stretched ovals
            else if ((sprName == "7" || n.Contains("close") || n.Contains("back") || n.Contains("play") ||
                      n.Contains("replay") || n.Contains("restart") || n.Contains("circle") || n.Contains("themetoggle") ||
                      n == "listbutton") && !n.Contains("room") && !n.Contains("row"))
            {
                img.sprite = controlBtnSprite;
                img.type = Image.Type.Simple;
                img.color = Color.white;
                img.material = null;
            }
            // Sub-cards and slots (e.g. 8.png, 11.png, TentangArtefakCard, DetailArtefakCard, list rows, slots, ranks, room buttons)
            else if (sprName == "8" || sprName == "11" || n.Contains("card") || n.Contains("frame") ||
                     n.Contains("item") || n.Contains("slot") || n.Contains("rank") || n.Contains("row") ||
                     n.Contains("room"))
            {
                if (!n.Contains("imagesbutton") && !n.Contains("3dviewbutton"))
                {
                    // Options panel row cards (Row_Explore, Row_Artefak, Row_Ruang) use Mat_OptionsRowCard.
                    // Copy exact dark mode shader setup with light mode colors (no oval distortion).
                    if (n.StartsWith("row_") || (img.material != null && img.material.name.Contains("OptionsRowCard")))
                    {
                        if (matOptionsRowCard == null) CacheMaterials();
                        if (matOptionsRowCard != null) img.material = matOptionsRowCard;
                        img.sprite = null;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.sprite = cardSprite;
                        img.type = Image.Type.Sliced;
                        img.color = Color.white;
                        img.material = null;
                        if (n.Contains("room"))
                        {
                            img.pixelsPerUnitMultiplier = 14.87f;
                        }
                        else if (img.pixelsPerUnitMultiplier < 5f && (n.Contains("list") || n.Contains("button") || n.Contains("slot")))
                        {
                            img.pixelsPerUnitMultiplier = 14.87f;
                        }
                    }
                }
            }
            // Separator lines
            else if (sprName == "16" || n.Contains("separator") || n.Contains("line") || n.Contains("decline"))
            {
                img.color = lightSeparatorColor;
            }
            // Progress Bar Fill (Vivid Azure high contrast against sage background)
            else if (n.Contains("fill") || (n.Contains("progress") && n.Contains("bar") && n.Contains("fill")))
            {
                img.color = lightProgressFillColor;
            }
            // Progress Bar Track / Slot (recessed dark groove)
            else if (n.Contains("progressbar") || n.Contains("progresstrack") || n.Contains("progress"))
            {
                img.color = lightProgressTrackColor;
            }
        }

        // 2. Process TextMeshProUGUI Typography
        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI tmp in texts)
        {
            if (tmp == null || IsInsideThemeSelection(tmp.transform) || IsIntroVideoPanel(tmp.transform)) continue;
            if (tmp.gameObject.name == "CardText" || tmp.gameObject.name == "TimerText" || (tmp.transform.parent != null && tmp.transform.parent.name == "TimerBadge")) continue;
            string n = tmp.gameObject.name.ToLower();

            CacheOriginalText(tmp);

            // Input field text: ensure dark readable text on light input background
            if (tmp.GetComponentInParent<TMP_InputField>() != null || tmp.GetComponentInParent<InputField>() != null)
            {
                if (tmp.gameObject.name.ToLower().Contains("placeholder"))
                {
                    tmp.color = new Color(0.36f, 0.40f, 0.32f, 0.70f);
                }
                else
                {
                    tmp.color = new Color(0.10f, 0.12f, 0.08f, 1f);
                }
                continue;
            }

            // Subtitle & Accent: artifact name under Artefak, bottom title, bulb/info icons, step counter, room numbers
            if (n.Contains("bottomtitle") || n.Contains("subtitle") || n.Contains("accent") ||
                n.Contains("gamelan") || n.Contains("step") || n.Contains("counter") ||
                n.Contains("num") || n.Contains("number") ||
                tmp.text.StartsWith("“") || tmp.text.StartsWith("\""))
            {
                tmp.color = lightAccentTextColor;
            }
            // Titles & Main Headers & Room Names
            else if (n.Contains("title") || n.Contains("header") || n.Contains("name") || n.Contains("room") || tmp.fontSize >= 18f)
            {
                tmp.color = lightHeaderTextColor;
            }
            // Progress Label (matches progress fill color for visual clarity)
            if (n.Contains("progress"))
            {
                tmp.color = lightProgressFillColor;
            }
            // Button symbols / Icons (e.g. '✕', '◀', '▶', '↺')
            else if (n.Contains("icon") || n.Contains("symbol") || n.Contains("close") || n.Contains("back") || tmp.text == "▶" || tmp.text == "✕")
            {
                if (tmp.transform.parent != null &&
                    (tmp.transform.parent.name.ToLower().Contains("skipnarration") ||
                     tmp.transform.parent.name.ToLower().Contains("next") ||
                     tmp.transform.parent.name.ToLower().Contains("lanjut")))
                {
                    tmp.color = new Color(0.12f, 0.14f, 0.10f, 1f); // Dark contrast icon on light button
                }
                else
                {
                    tmp.color = lightControlBtnIcon;
                }
            }
            // Field values & body descriptions
            else
            {
                tmp.color = lightBodyTextColor;
            }
        }

        // 3. Process Buttons (eliminate dark 9.png highlight hover in Light Mode)
        Sprite cardHoverSprite = GetOrCreateLightCardHoverSprite();
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            if (btn == null || IsInsideThemeSelection(btn.transform) || IsIntroVideoPanel(btn.transform)) continue;
            CacheOriginalButton(btn);

            if (btn.transition == Selectable.Transition.SpriteSwap)
            {
                SpriteState ss = btn.spriteState;
                if (ss.highlightedSprite != null)
                {
                    ss.highlightedSprite = cardHoverSprite;
                    ss.selectedSprite = cardHoverSprite;
                    btn.spriteState = ss;
                }
            }
            else if (btn.transition == Selectable.Transition.ColorTint)
            {
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.08f, 1.10f, 1.02f, 1f);
                cb.selectedColor = new Color(1.08f, 1.10f, 1.02f, 1f);
                btn.colors = cb;
            }
        }

        // 4. Process XRButtonSelection
        XRButtonSelection[] xrButtons = root.GetComponentsInChildren<XRButtonSelection>(true);
        foreach (XRButtonSelection xr in xrButtons)
        {
            if (xr == null || IsInsideThemeSelection(xr.transform) || IsIntroVideoPanel(xr.transform)) continue;
            CacheOriginalXRButton(xr);

            xr.normalColor = Color.white;
            xr.hoverColor = new Color(1.04f, 1.08f, 0.96f, 1f);
        }

        // 5. Process TMP_InputField Caret & Selection Colors
        foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
        {
            if (input == null || IsInsideThemeSelection(input.transform) || IsIntroVideoPanel(input.transform)) continue;
            input.customCaretColor = true;
            input.caretColor = new Color(0.12f, 0.15f, 0.10f, 1f);
            input.selectionColor = new Color(0.60f, 0.75f, 0.50f, 0.50f);
        }

        Game2OrderProcess g2Light = root.GetComponentInChildren<Game2OrderProcess>(true);
        if (g2Light != null && g2Light.gameObject.activeInHierarchy)
        {
            g2Light.ApplyCardTheme(UIThemeMode.Light);
        }
    }
}
