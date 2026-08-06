#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Editor helper that creates a persistent, editable "TutorialPanel" in the open scene,
/// mirroring the panel TutorialManager would otherwise build at runtime. Once built, the
/// panel lives in the hierarchy so its layout, sizes and buttons can be edited freely;
/// TutorialManager binds to it by child names (Title, Body, ProgressLabel, ProgressBar/Fill,
/// StepCounter, SkipButton) and only drives the texts/progress at runtime.
///
/// Note: the panel's WORLD position is still set at runtime (it is placed in front of the
/// player) - edit the layout inside the canvas, not its scene position.
/// </summary>
public static class TutorialPanelBuilder
{
    [MenuItem("Tools/Museum MR/Build Tutorial Panel In Scene")]
    public static void Build()
    {
        GameObject existing = FindInScene("TutorialPanel");
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            EditorGUIUtility.PingObject(existing);
            EditorUtility.DisplayDialog("Tutorial Panel",
                "'TutorialPanel' already exists in the scene. Selected it in the hierarchy.", "OK");
            return;
        }

        GameObject template = FindInScene("RoomListPanel");

        GameObject panel = new GameObject("TutorialPanel");
        Canvas canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;
        panel.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(560f, 340f);
        panel.transform.localScale = Vector3.one * 0.001f; // project standard: 1px = 1mm
        panel.transform.position = new Vector3(0f, 1.4f, 1.2f); // preview spot only

        // Raycastable so the hand ray (and its cursor dot) registers anywhere over the
        // panel, not only when pointing exactly at a button - text stays non-raycastable
        // and lets the ray "see through" to this layer underneath.
        Image bg = CreateImage(panel.transform, "Background", new Color(0.07f, 0.09f, 0.13f, 0.88f));
        bg.raycastTarget = true;
        bg.rectTransform.anchorMin = Vector2.zero;
        bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.offsetMin = Vector2.zero;
        bg.rectTransform.offsetMax = Vector2.zero;

        Image accent = CreateImage(panel.transform, "AccentStrip", new Color(0.35f, 0.78f, 0.95f, 1f));
        accent.rectTransform.anchorMin = new Vector2(0f, 1f);
        accent.rectTransform.anchorMax = new Vector2(1f, 1f);
        accent.rectTransform.pivot = new Vector2(0.5f, 1f);
        accent.rectTransform.anchoredPosition = Vector2.zero;
        accent.rectTransform.sizeDelta = new Vector2(0f, 8f);

        const float marginL = 0.08f, marginR = 0.92f;

        // Preview copy uses the LONGEST real step texts so the authored layout is edited
        // against realistic content (runtime replaces these per step).
        TextMeshProUGUI title = CreateLabel(panel.transform, "Title", 28f, 20f, FontStyles.Bold,
            new Color(0.92f, 0.96f, 1f, 1f), TextAlignmentOptions.Center);
        title.text = "Langkah 2: Cubit & Tahan untuk Putar";
        Place(title.rectTransform, new Vector2(marginL, 0.76f), new Vector2(marginR, 0.96f));

        TextMeshProUGUI body = CreateLabel(panel.transform, "Body", 19f, 14f, FontStyles.Normal,
            new Color(0.88f, 0.9f, 0.94f, 1f), TextAlignmentOptions.Top);
        body.text = "CUBIT dan TAHAN artifak, kemudian gerakkan tangan anda untuk memutarkannya. Terus putar sehingga bar penuh!\n" +
            "<size=80%><i>PINCH and HOLD the artifact, then move your hand to rotate it. Keep spinning until the bar is full!</i></size>";
        Place(body.rectTransform, new Vector2(marginL, 0.24f), new Vector2(marginR, 0.74f));

        TextMeshProUGUI progress = CreateLabel(panel.transform, "ProgressLabel", 19f, 15f, FontStyles.Bold,
            new Color(0.55f, 0.9f, 1f, 1f), TextAlignmentOptions.Center);
        progress.text = "540° / 540°";
        Place(progress.rectTransform, new Vector2(marginL, 0.13f), new Vector2(marginR, 0.24f));

        GameObject barGo = new GameObject("ProgressBar");
        barGo.transform.SetParent(panel.transform, false);
        RectTransform barRect = barGo.AddComponent<RectTransform>();
        Place(barRect, new Vector2(marginL, 0.075f), new Vector2(marginR, 0.115f));
        Image barBg = barGo.AddComponent<Image>();
        barBg.color = new Color(1f, 1f, 1f, 0.12f);
        barBg.raycastTarget = false;

        Image fill = CreateImage(barGo.transform, "Fill", new Color(0.35f, 0.85f, 0.55f, 1f));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0.5f, 1f); // runtime drives the X; 0.5 previews a half-full bar
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI counter = CreateLabel(panel.transform, "StepCounter", 15f, 12f, FontStyles.Normal,
            new Color(0.7f, 0.75f, 0.82f, 1f), TextAlignmentOptions.Center);
        counter.text = "Langkah 1 / 2";
        Place(counter.rectTransform, new Vector2(marginL, 0.0f), new Vector2(marginR, 0.07f));

        // Adopt the app's design language (RoomListPanel card sprite + fonts) at build time,
        // so the authored panel starts out matching the rest of the UI.
        if (template != null && ApplyTemplateStyle(template, bg, title, body, progress, counter))
        {
            accent.gameObject.SetActive(false);
        }

        BuildSkipButton(panel, template);

        // Hand the panel to the TutorialManager so it binds instead of building its own.
        TutorialManager manager = Object.FindObjectOfType<TutorialManager>(true);
        if (manager != null)
        {
            Undo.RecordObject(manager, "Assign Tutorial Panel");
            manager.sceneAuthoredPanel = panel;
            EditorUtility.SetDirty(manager);
        }

        panel.SetActive(false); // TutorialManager activates it when the tutorial starts

        Undo.RegisterCreatedObjectUndo(panel, "Build Tutorial Panel");
        EditorSceneManager.MarkSceneDirty(panel.scene);
        Selection.activeGameObject = panel;
        EditorGUIUtility.PingObject(panel);

        EditorUtility.DisplayDialog("Tutorial Panel",
            "'TutorialPanel' built in the scene hierarchy" +
            (manager != null ? " and assigned to the TutorialManager." : ". (No TutorialManager found to auto-assign - set its 'Scene Authored Panel' field manually.)") +
            "\n\nEdit the layout and sizes freely, but keep the child names (Title, Body, ProgressLabel, ProgressBar/Fill, StepCounter, SkipButton) - the runtime binds by name." +
            "\n\nThe panel's world position is set at runtime in front of the player; only the layout inside the canvas matters.", "OK");
    }

    /// <summary>Copies the RoomListPanel's card sprite and TMP fonts/colors onto the new panel.</summary>
    private static bool ApplyTemplateStyle(GameObject template, Image targetBackground,
        TextMeshProUGUI title, TextMeshProUGUI body, TextMeshProUGUI progress, TextMeshProUGUI counter)
    {
        Image srcBg = template.GetComponent<Image>();
        if (srcBg == null)
        {
            foreach (Image img in template.GetComponentsInChildren<Image>(true))
            {
                string n = img.name.ToLower();
                if (n == "image" || n == "bg" || n.Contains("background") || n.Contains("panel"))
                {
                    srcBg = img;
                    break;
                }
            }
        }
        if (srcBg == null) srcBg = template.GetComponentInChildren<Image>(true);

        if (srcBg != null && srcBg.sprite != null)
        {
            targetBackground.sprite = srcBg.sprite;
            targetBackground.type = srcBg.type;
            targetBackground.color = srcBg.color;
            targetBackground.material = srcBg.material;
            targetBackground.pixelsPerUnitMultiplier = srcBg.pixelsPerUnitMultiplier;
        }

        TextMeshProUGUI srcTitle = null;
        Transform titleT = FindChild(template.transform, "RoomTitleText");
        if (titleT != null) srcTitle = titleT.GetComponent<TextMeshProUGUI>();

        TextMeshProUGUI[] allLabels = template.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (srcTitle == null && allLabels.Length > 0) srcTitle = allLabels[0];

        TextMeshProUGUI srcBody = null;
        foreach (TextMeshProUGUI tmp in allLabels)
        {
            if (tmp != srcTitle) { srcBody = tmp; break; }
        }
        if (srcBody == null) srcBody = srcTitle;

        if (srcTitle != null)
        {
            title.font = srcTitle.font;
            title.color = srcTitle.color;
            progress.font = srcTitle.font;
            progress.color = srcTitle.color;
        }
        if (srcBody != null)
        {
            body.font = srcBody.font;
            body.color = srcBody.color;
            counter.font = srcBody.font;
            Color dim = srcBody.color;
            dim.a *= 0.65f;
            counter.color = dim;
        }

        return (srcBg != null && srcBg.sprite != null) || srcTitle != null;
    }

    /// <summary>
    /// Adds the Skip (close/abort) button in the panel's top-right corner: a clone of the
    /// RoomListPanel's CloseButton - a plain X, matching every other close button in the
    /// app - or a simple house-style X button if no template exists. Deliberately NOT the
    /// round skip-forward icon (that one is reserved for the separate runtime-built Skip
    /// Narration button - see TutorialManager.EnsureSkipNarrationButton), so the two can
    /// never be visually mistaken for each other. Serialized events are cleared either way -
    /// they'd otherwise act on the ORIGINAL panel. TutorialManager wires the actual skip
    /// action at runtime.
    /// </summary>
    private static void BuildSkipButton(GameObject panel, GameObject template)
    {
        Transform closeT = template != null ? FindChild(template.transform, "CloseButton") : null;
        GameObject btn;

        if (closeT != null)
        {
            btn = Object.Instantiate(closeT.gameObject, panel.transform);
            btn.name = "SkipButton";

            Button b = btn.GetComponentInChildren<Button>(true);
            if (b != null) b.onClick = new Button.ButtonClickedEvent();

            XRButtonSelection xr = btn.GetComponentInChildren<XRButtonSelection>(true);
            if (xr != null)
            {
                xr.onClick = new UnityEvent();
                if (btn.GetComponentInChildren<Collider>(true) == null)
                {
                    BoxCollider box = xr.gameObject.AddComponent<BoxCollider>();
                    RectTransform xrRect = xr.GetComponent<RectTransform>();
                    if (xrRect != null)
                    {
                        box.size = new Vector3(Mathf.Max(xrRect.rect.width, 40f), Mathf.Max(xrRect.rect.height, 40f), 10f);
                    }
                    xr.colliders.Add(box);
                }
            }
            btn.SetActive(true);
        }
        else
        {
            btn = new GameObject("SkipButton");
            btn.transform.SetParent(panel.transform, false);
            RectTransform fr = btn.AddComponent<RectTransform>();
            fr.sizeDelta = new Vector2(56f, 56f);

            Image img = btn.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.93f, 0.8f); // XRButtonSelection default normalColor

            BoxCollider box = btn.AddComponent<BoxCollider>();
            box.size = new Vector3(56f, 56f, 10f);

            XRButtonSelection xr = btn.AddComponent<XRButtonSelection>();
            xr.buttonImage = img;

            TextMeshProUGUI x = CreateLabel(btn.transform, "X", 30f, 20f, FontStyles.Bold,
                new Color(0.15f, 0.17f, 0.22f, 1f), TextAlignmentOptions.Center);
            x.text = "X";
            Place(x.rectTransform, Vector2.zero, Vector2.one);
        }

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-46f, -44f);
        }
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, float sizeMax,
        float sizeMin, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.fontSize = sizeMax;
        label.enableAutoSizing = true;
        label.fontSizeMin = sizeMin;
        label.fontSizeMax = sizeMax;
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        return label;
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }
        return null;
    }

    private static GameObject FindInScene(string name)
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
}
#endif
