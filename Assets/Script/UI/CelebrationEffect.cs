using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Short "well done" moment for world-space UI panels: a banner that pops in, a shower of
/// confetti and a small chime. Everything is built at runtime (no prefab, sprite or audio
/// assets), it never blocks input, and it removes itself when finished.
///
/// Used for a new 1st place on the leaderboard (LeaderboardPanel) and for scrolling an
/// artifact description to the end (Artifact).
/// </summary>
public class CelebrationEffect : MonoBehaviour
{
    private const float Duration = 3.4f;
    private const float BannerPopSeconds = 0.35f;
    private const float FadeOutSeconds = 0.6f;
    private const int ConfettiCount = 70;

    private static readonly Color[] ConfettiColors =
    {
        new Color(0.98f, 0.82f, 0.36f), // gold
        new Color(0.00f, 0.83f, 1.00f), // app cyan
        new Color(0.40f, 0.85f, 0.40f), // green
        new Color(0.96f, 0.45f, 0.40f), // coral
        new Color(0.85f, 0.89f, 0.62f), // soft lime
        new Color(0.95f, 0.94f, 0.91f), // cream
    };
    private static readonly Color BannerBg = new Color(0.07f, 0.11f, 0.17f, 0.92f);
    private static readonly Color BannerBorder = new Color(0.98f, 0.82f, 0.36f, 0.95f);
    private static readonly Color TitleColor = new Color(0.98f, 0.82f, 0.36f);
    private static readonly Color SubtitleColor = new Color(0.95f, 0.94f, 0.91f);

    private class Piece
    {
        public RectTransform rt;
        public Image img;
        public Vector2 position;
        public Vector2 velocity;
        public float spin;
        public float wobblePhase;
        public float wobbleSpeed;
        public float wobbleAmount;
        public float delay;
    }

    private static Sprite roundedSprite;
    private static AudioClip chime;

    private readonly List<Piece> pieces = new List<Piece>();
    private RectTransform banner;
    private CanvasGroup group;
    private Vector2 area;
    private float elapsed;

    /// <summary>
    /// Plays the celebration over <paramref name="host"/> (a panel's RectTransform).
    /// A celebration already running on the same host is replaced.
    /// </summary>
    public static CelebrationEffect Play(RectTransform host, string title, string subtitle = null)
    {
        if (host == null || !host.gameObject.activeInHierarchy) return null;

        foreach (CelebrationEffect running in host.GetComponentsInChildren<CelebrationEffect>(true))
        {
            if (running.transform.parent == host) Destroy(running.gameObject);
        }

        GameObject root = new GameObject("CelebrationEffect", typeof(RectTransform));
        RectTransform rt = (RectTransform)root.transform;
        rt.SetParent(host, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling(); // drawn after the panel's cards, i.e. on top

        CelebrationEffect fx = root.AddComponent<CelebrationEffect>();
        fx.Build(host, title, subtitle);
        return fx;
    }

    private void Build(RectTransform host, string title, string subtitle)
    {
        area = host.rect.size;
        if (area.x < 50f || area.y < 50f) area = new Vector2(600f, 400f);

        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        BuildConfetti();
        BuildBanner(host, title, subtitle);
        PlayChime();
    }

    private void BuildConfetti()
    {
        for (int i = 0; i < ConfettiCount; i++)
        {
            GameObject go = new GameObject("ConfettiPiece", typeof(RectTransform), typeof(Image));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

            float w = area.y * Random.Range(0.012f, 0.022f);
            rt.sizeDelta = new Vector2(w, w * Random.Range(1.4f, 2.2f));

            Image img = go.GetComponent<Image>();
            img.color = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
            img.raycastTarget = false;

            Piece p = new Piece
            {
                rt = rt,
                img = img,
                position = new Vector2(Random.Range(-0.48f, 0.48f) * area.x, area.y * Random.Range(0.45f, 0.65f)),
                velocity = new Vector2(Random.Range(-0.08f, 0.08f) * area.x, -Random.Range(0.28f, 0.55f) * area.y),
                spin = Random.Range(-420f, 420f),
                wobblePhase = Random.Range(0f, Mathf.PI * 2f),
                wobbleSpeed = Random.Range(4f, 8f),
                wobbleAmount = Random.Range(0.01f, 0.03f) * area.x,
                delay = Random.Range(0f, 0.7f),
            };
            rt.anchoredPosition = p.position;
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            go.SetActive(p.delay <= 0f);
            pieces.Add(p);
        }
    }

    private void BuildBanner(RectTransform host, string title, string subtitle)
    {
        TMP_Text sample = host.GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;

        GameObject bannerGo = new GameObject("CelebrationBanner", typeof(RectTransform), typeof(Image));
        banner = (RectTransform)bannerGo.transform;
        banner.SetParent(transform, false);
        banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0.5f);
        banner.anchoredPosition = new Vector2(0f, area.y * 0.08f);

        Image bg = bannerGo.GetComponent<Image>();
        bg.sprite = GetRoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = BannerBg;
        bg.raycastTarget = false;
        Outline outline = bannerGo.AddComponent<Outline>();
        outline.effectColor = BannerBorder;
        outline.effectDistance = new Vector2(2f, -2f);

        float titleSize = Mathf.Clamp(area.y * 0.075f, 18f, 48f);
        TextMeshProUGUI titleText = CreateText("CelebrationTitle", title, font, titleSize, TitleColor, FontStyles.Bold);
        TextMeshProUGUI subText = string.IsNullOrEmpty(subtitle)
            ? null
            : CreateText("CelebrationSubtitle", subtitle, font, titleSize * 0.48f, SubtitleColor, FontStyles.Normal);

        float maxTextWidth = area.x * 0.78f;
        Vector2 titlePref = titleText.GetPreferredValues(title, maxTextWidth, 1000f);
        Vector2 subPref = subText != null ? subText.GetPreferredValues(subtitle, maxTextWidth, 1000f) : Vector2.zero;

        float padX = titleSize * 0.9f;
        float padY = titleSize * 0.55f;
        float gap = subText != null ? titleSize * 0.2f : 0f;
        float width = Mathf.Min(Mathf.Max(titlePref.x, subPref.x), maxTextWidth) + padX * 2f;
        float height = titlePref.y + gap + subPref.y + padY * 2f;
        banner.sizeDelta = new Vector2(width, height);

        float top = height * 0.5f - padY;
        PlaceText(titleText, width - padX * 2f, titlePref.y, top - titlePref.y * 0.5f);
        if (subText != null)
        {
            PlaceText(subText, width - padX * 2f, subPref.y, top - titlePref.y - gap - subPref.y * 0.5f);
        }

        banner.localScale = Vector3.zero;
    }

    private TextMeshProUGUI CreateText(string name, string content, TMP_FontAsset font, float size, Color color, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(banner, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    private static void PlaceText(TMP_Text t, float width, float height, float centerY)
    {
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, centerY);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        elapsed += dt;

        // Banner: overshoot pop-in
        if (banner != null)
        {
            float t = Mathf.Clamp01(elapsed / BannerPopSeconds);
            float s = EaseOutBack(t);
            banner.localScale = new Vector3(s, s, 1f);
        }

        // Confetti: staggered start, gravity, sideways wobble, spin
        float gravity = area.y * 0.35f;
        foreach (Piece p in pieces)
        {
            if (p.rt == null) continue;
            if (p.delay > 0f)
            {
                p.delay -= dt;
                if (p.delay > 0f) continue;
                p.rt.gameObject.SetActive(true);
            }
            p.velocity.y -= gravity * dt;
            p.position += p.velocity * dt;
            float wobble = Mathf.Sin(elapsed * p.wobbleSpeed + p.wobblePhase) * p.wobbleAmount;
            p.rt.anchoredPosition = new Vector2(p.position.x + wobble, p.position.y);
            p.rt.Rotate(0f, 0f, p.spin * dt);
        }

        // Fade everything out at the end
        float fadeStart = Duration - FadeOutSeconds;
        if (elapsed > fadeStart && group != null)
        {
            group.alpha = 1f - Mathf.Clamp01((elapsed - fadeStart) / FadeOutSeconds);
        }

        if (elapsed >= Duration) Destroy(gameObject);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void PlayChime()
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0.35f;
        src.clip = GetChime();
        src.Play();
    }

    /// <summary>Rising C-E-G-C arpeggio with a soft bell-like decay, generated once.</summary>
    private static AudioClip GetChime()
    {
        if (chime != null) return chime;

        const int sampleRate = 44100;
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
        const float step = 0.11f;
        const float lastNoteLength = 0.7f;
        int total = Mathf.CeilToInt((step * (notes.Length - 1) + lastNoteLength) * sampleRate);
        float[] data = new float[total];

        for (int n = 0; n < notes.Length; n++)
        {
            int start = Mathf.RoundToInt(n * step * sampleRate);
            float length = n == notes.Length - 1 ? lastNoteLength : step * 2.2f;
            int count = Mathf.Min(Mathf.RoundToInt(length * sampleRate), total - start);
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)sampleRate;
                float attack = Mathf.Clamp01(time / 0.008f);
                float envelope = attack * Mathf.Exp(-time * 6f);
                float tone = Mathf.Sin(2f * Mathf.PI * notes[n] * time)
                           + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[n] * time);
                data[start + i] += 0.28f * envelope * tone;
            }
        }

        chime = AudioClip.Create("CelebrationChime", total, 1, sampleRate, false);
        chime.SetData(data, 0);
        return chime;
    }

    /// <summary>White rounded-rectangle sprite with 9-slice borders, generated once.</summary>
    private static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;

        const int size = 64;
        const float radius = 22f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius - dist + 0.5f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        roundedSprite.name = "CelebrationRounded_Procedural";
        return roundedSprite;
    }
}
