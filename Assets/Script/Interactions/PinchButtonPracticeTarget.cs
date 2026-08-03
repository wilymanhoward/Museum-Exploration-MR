using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating practice button for the tutorial's click step. The player points the hand ray
/// at it and pinches, exactly like pressing any real button in the app - it IS an
/// XRButtonSelection (the app's standard button component), so the hover tint, grow-on-hover,
/// click sound and haptics all match the rest of the UI. The rounded sprite and font are
/// borrowed from the scene's RoomListPanel when available, same as the tutorial panel does.
///
/// Billboards toward the player every frame (yaw only) so its label always stays readable.
/// Built entirely at runtime via Create() - no prefab needed.
/// </summary>
public class PinchButtonPracticeTarget : MonoBehaviour
{
    public event Action Pressed;

    private Camera cachedCamera;

    /// <summary>Creates a ready-to-use practice button at the given world position, facing the player.</summary>
    public static PinchButtonPracticeTarget Create(Vector3 position, string label)
    {
        GameObject root = new GameObject("TutorialClickButton");
        root.transform.position = position;
        PinchButtonPracticeTarget target = root.AddComponent<PinchButtonPracticeTarget>();
        target.Build(label);
        return target;
    }

    private void Build(string labelText)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;

        RectTransform canvasRect = GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200f, 80f); // ~20cm x 8cm in world space
        transform.localScale = Vector3.one * 0.001f;   // project standard: 1px = 1mm

        // Background image; XRButtonSelection recolors it to the app's normal/hover tints.
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        RectTransform bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bg = bgGo.AddComponent<Image>();
        bg.raycastTarget = false;

        // Label: dark bold text on the light button surface, auto-sized so it can never
        // overflow the small button no matter the copy.
        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, false);
        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.08f, 0.12f);
        labelRect.anchorMax = new Vector2(0.92f, 0.88f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.text = labelText;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.15f, 0.17f, 0.22f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 18f;
        label.raycastTarget = false;

        ApplyAppStyle(bg, label);

        // Collider first so XRButtonSelection's Awake auto-registers it (canvas-pixel units:
        // the 0.001 canvas scale turns this into ~20cm x 8cm x 4cm in world space).
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(canvasRect.sizeDelta.x, canvasRect.sizeDelta.y, 40f);

        XRButtonSelection xr = gameObject.AddComponent<XRButtonSelection>();
        xr.buttonImage = bg;
        xr.interactionLayers = ~0; // accept every interactor (hand rays, pokes, controllers)
        xr.onClick.AddListener(() => Pressed?.Invoke());
    }

    /// <summary>
    /// Borrows the app's visual identity for the practice button. First choice: the main
    /// menu's MULAI button (scene object "StartButton") - the app's signature olive rounded
    /// button with white serif text. Fallback: a button sprite + font from the RoomListPanel.
    /// Skipped silently when neither exists (editor test scenes) - the flat tinted quad
    /// still works.
    /// </summary>
    private static void ApplyAppStyle(Image bg, TextMeshProUGUI label)
    {
        GameObject mulai = FindSceneObjectByName("StartButton");
        if (mulai == null) mulai = FindSceneObjectByName("MulaiButton");
        if (mulai != null)
        {
            Image mulaiImg = mulai.GetComponent<Image>();
            if (mulaiImg == null) mulaiImg = mulai.GetComponentInChildren<Image>(true);
            if (mulaiImg != null && mulaiImg.sprite != null)
            {
                bg.sprite = mulaiImg.sprite;
                bg.type = mulaiImg.type;
                bg.color = mulaiImg.color;
                bg.material = mulaiImg.material;
                bg.pixelsPerUnitMultiplier = mulaiImg.pixelsPerUnitMultiplier;

                TextMeshProUGUI mulaiLabel = mulai.GetComponentInChildren<TextMeshProUGUI>(true);
                if (mulaiLabel != null)
                {
                    label.font = mulaiLabel.font;
                    label.fontStyle = mulaiLabel.fontStyle;
                    label.color = mulaiLabel.color;
                }
                return;
            }
        }

        GameObject template = FindSceneObjectByName("RoomListPanel");
        if (template == null) return;

        Image srcImg = null;
        foreach (Image img in template.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite == null) continue;
            bool looksLikeButton = img.name.ToLower().Contains("button")
                || img.GetComponent<Button>() != null
                || img.GetComponent<XRButtonSelection>() != null
                || (img.transform.parent != null && img.transform.parent.name.ToLower().Contains("button"));
            if (looksLikeButton) { srcImg = img; break; }
        }
        if (srcImg == null)
        {
            foreach (Image img in template.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != null) { srcImg = img; break; }
            }
        }
        if (srcImg != null)
        {
            bg.sprite = srcImg.sprite;
            bg.type = srcImg.type;
            bg.pixelsPerUnitMultiplier = srcImg.pixelsPerUnitMultiplier;
        }

        TextMeshProUGUI srcLabel = template.GetComponentInChildren<TextMeshProUGUI>(true);
        if (srcLabel != null) label.font = srcLabel.font;
    }

    private static GameObject FindSceneObjectByName(string name)
    {
        GameObject fallback = null;
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || t.name != name || !t.gameObject.scene.IsValid()) continue;
            if (t.gameObject.activeInHierarchy) return t.gameObject;
            if (fallback == null) fallback = t.gameObject;
        }
        return fallback;
    }

    private void Update()
    {
        // Billboard toward the player (yaw only), same convention as TutorialGestureGizmo.
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        }
        if (cachedCamera == null) return;

        Vector3 toCam = cachedCamera.transform.position - transform.position;
        toCam.y = 0f;
        if (toCam.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
        }
    }
}
