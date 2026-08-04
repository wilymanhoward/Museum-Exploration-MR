using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class HistoryPanelBuilder : Editor
{
    [MenuItem("Museum MR/Create History Panel UI Template (In Scene)", false, 15)]
    public static void CreateHistoryPanelTemplate()
    {
        // 1. Create root Canvas Panel
        GameObject panelObj = new GameObject("HistoryPanel");
        Canvas canvas = panelObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        panelObj.AddComponent<CanvasScaler>();
        panelObj.AddComponent<GraphicRaycaster>();

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(560f, 400f);
        panelObj.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(panelObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.24f, 0.25f, 0.24f, 0.95f); // Charcoal sage

        // Add HistoryPanel script
        HistoryPanel historyScript = panelObj.AddComponent<HistoryPanel>();

        // ─────────────────────────────────────────────────────────────────────
        // 2. Top Header (Left)
        // ─────────────────────────────────────────────────────────────────────
        GameObject headerObj = new GameObject("HeaderGroup");
        headerObj.transform.SetParent(panelObj.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchoredPosition = new Vector2(-170f, 160f);
        headerRect.sizeDelta = new Vector2(180f, 50f);

        // Museum Icon placeholder
        GameObject iconObj = new GameObject("MuseumIcon");
        iconObj.transform.SetParent(headerObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchoredPosition = new Vector2(-65f, 0f);
        iconRect.sizeDelta = new Vector2(32f, 32f);
        TextMeshProUGUI iconTxt = iconObj.AddComponent<TextMeshProUGUI>();
        iconTxt.text = "🏛";
        iconTxt.fontSize = 22;
        iconTxt.alignment = TextAlignmentOptions.Center;
        iconTxt.color = new Color(0.92f, 0.90f, 0.58f, 1f);

        // Title: "Artefak" / "Sejarah"
        GameObject titleObj = new GameObject("TopTitleText");
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(25f, 10f);
        titleRect.sizeDelta = new Vector2(140f, 26f);
        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "Artefak";
        titleTxt.fontSize = 20;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = Color.white;
        historyScript.topTitleText = titleTxt;

        // Subtitle: "Gamelan"
        GameObject subObj = new GameObject("CategoryText");
        subObj.transform.SetParent(headerObj.transform, false);
        RectTransform subRect = subObj.AddComponent<RectTransform>();
        subRect.anchoredPosition = new Vector2(25f, -12f);
        subRect.sizeDelta = new Vector2(140f, 20f);
        TextMeshProUGUI subTxt = subObj.AddComponent<TextMeshProUGUI>();
        subTxt.text = "Gamelan";
        subTxt.fontSize = 13;
        subTxt.color = new Color(0.85f, 0.85f, 0.60f, 1f);
        historyScript.categoryText = subTxt;

        // ─────────────────────────────────────────────────────────────────────
        // 3. Top Right Control Buttons (Restart, Play/Pause, Back, Close)
        // ────────────────────────────────────────────────────────────────────────
        GameObject buttonsObj = new GameObject("ControlButtons");
        buttonsObj.transform.SetParent(panelObj.transform, false);
        RectTransform buttonsRect = buttonsObj.AddComponent<RectTransform>();
        buttonsRect.anchoredPosition = new Vector2(170f, 160f);
        buttonsRect.sizeDelta = new Vector2(160f, 40f);

        // Helper to create circular control button
        Button createCircleButton(string name, Vector2 pos, string iconSymbol, out GameObject iconGO)
        {
            GameObject bObj = new GameObject(name);
            bObj.transform.SetParent(buttonsObj.transform, false);
            RectTransform bRect = bObj.AddComponent<RectTransform>();
            bRect.anchoredPosition = pos;
            bRect.sizeDelta = new Vector2(32f, 32f);

            Image bImg = bObj.AddComponent<Image>();
            bImg.color = new Color(0.18f, 0.20f, 0.18f, 0.9f);
            Button bBtn = bObj.AddComponent<Button>();

            GameObject iObj = new GameObject("Icon");
            iObj.transform.SetParent(bObj.transform, false);
            RectTransform iRect = iObj.AddComponent<RectTransform>();
            iRect.anchorMin = Vector2.zero;
            iRect.anchorMax = Vector2.one;
            iRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI iTxt = iObj.AddComponent<TextMeshProUGUI>();
            iTxt.text = iconSymbol;
            iTxt.fontSize = 16;
            iTxt.alignment = TextAlignmentOptions.Center;
            iTxt.color = Color.white;

            iconGO = iObj;
            return bBtn;
        }

        historyScript.restartButton = createCircleButton("RestartButton", new Vector2(-54f, 0f), "↺", out _);
        historyScript.playButton = createCircleButton("PlayButton", new Vector2(-18f, 0f), "▶", out historyScript.playIconObj);
        historyScript.backButton = createCircleButton("BackButton", new Vector2(18f, 0f), "↩", out _);
        historyScript.closeButton = createCircleButton("CloseButton", new Vector2(54f, 0f), "✕", out _);

        // ─────────────────────────────────────────────────────────────────────
        // 4. Left Media Area (Matching 2DViewPanel Hierarchy)
        // ─────────────────────────────────────────────────────────────────────
        GameObject panel2D = new GameObject("2DViewPanel");
        panel2D.transform.SetParent(panelObj.transform, false);
        RectTransform panel2DRect = panel2D.AddComponent<RectTransform>();
        panel2DRect.anchoredPosition = new Vector2(-135f, -20f);
        panel2DRect.sizeDelta = new Vector2(250f, 300f);

        // DisplayImage (Static Photo Image)
        GameObject imgObj = new GameObject("DisplayImage");
        imgObj.transform.SetParent(panel2D.transform, false);
        RectTransform imgRect = imgObj.AddComponent<RectTransform>();
        imgRect.anchoredPosition = new Vector2(0f, 15f);
        imgRect.sizeDelta = new Vector2(230f, 190f);
        Image displayImg = imgObj.AddComponent<Image>();
        displayImg.color = Color.white;
        historyScript.displayImage = displayImg;

        // VideoTriggerArea (GameObject with BoxCollider already attached)
        GameObject trigObj = new GameObject("VideoTriggerArea");
        trigObj.transform.SetParent(panel2D.transform, false);
        RectTransform trigRect = trigObj.AddComponent<RectTransform>();
        trigRect.anchoredPosition = new Vector2(0f, 15f);
        trigRect.sizeDelta = new Vector2(230f, 190f);
        BoxCollider boxCol = trigObj.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(230f, 190f, 10f);
        historyScript.videoTriggerArea = boxCol;

        // Label: "HoldText" ("Hold to Play Clip")
        GameObject holdObj = new GameObject("HoldText");
        holdObj.transform.SetParent(panel2D.transform, false);
        RectTransform holdRect = holdObj.AddComponent<RectTransform>();
        holdRect.anchoredPosition = new Vector2(0f, 130f);
        holdRect.sizeDelta = new Vector2(230f, 24f);
        TextMeshProUGUI holdTxt = holdObj.AddComponent<TextMeshProUGUI>();
        holdTxt.text = "Hold to Play Clip";
        holdTxt.fontSize = 15;
        holdTxt.alignment = TextAlignmentOptions.Center;
        holdTxt.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
        historyScript.holdToPlayText = holdTxt;

        // Event Title text below box ("Pertarungan Megat Panji Alam")
        GameObject eventObj = new GameObject("Text (TMP)");
        eventObj.transform.SetParent(panel2D.transform, false);
        RectTransform eventRect = eventObj.AddComponent<RectTransform>();
        eventRect.anchoredPosition = new Vector2(0f, -115f);
        eventRect.sizeDelta = new Vector2(240f, 50f);
        TextMeshProUGUI eventTxt = eventObj.AddComponent<TextMeshProUGUI>();
        eventTxt.text = "Pertarungan Megat\nPanji Alam";
        eventTxt.fontSize = 18;
        eventTxt.fontStyle = FontStyles.Bold;
        eventTxt.alignment = TextAlignmentOptions.Center;
        eventTxt.color = new Color(0.88f, 0.85f, 0.55f, 1f);
        historyScript.eventTitleText = eventTxt;

        // Video Container Panel (Video) - Starts OFF / Hidden!
        GameObject videoContainer = new GameObject("Video");
        videoContainer.transform.SetParent(panel2D.transform, false);
        RectTransform vcRect = videoContainer.AddComponent<RectTransform>();
        vcRect.anchoredPosition = new Vector2(0f, 15f);
        vcRect.sizeDelta = new Vector2(230f, 190f);
        videoContainer.SetActive(false);
        historyScript.videoPanel = videoContainer;

        // Video Player RawImage
        GameObject rawImgObj = new GameObject("RawImage");
        rawImgObj.transform.SetParent(videoContainer.transform, false);
        RectTransform rawImgRect = rawImgObj.AddComponent<RectTransform>();
        rawImgRect.anchorMin = Vector2.zero;
        rawImgRect.anchorMax = Vector2.one;
        rawImgRect.sizeDelta = Vector2.zero;
        RawImage displayRawImg = rawImgObj.AddComponent<RawImage>();
        displayRawImg.color = Color.white;
        historyScript.displayVideoRawImage = displayRawImg;

        // Video Player Component
        GameObject vpObj = new GameObject("VideoPlayer");
        vpObj.transform.SetParent(videoContainer.transform, false);
        VideoPlayer vp = vpObj.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.renderMode = VideoRenderMode.APIOnly;
        historyScript.videoPlayer = vp;

        // ─────────────────────────────────────────────────────────────────────
        // 5. Right Section: Detail Sejarah Card & Tentang Sejarah Ini Card
        // ─────────────────────────────────────────────────────────────────────
        GameObject rightGroup = new GameObject("RightCardsGroup");
        rightGroup.transform.SetParent(panelObj.transform, false);
        RectTransform rightRect = rightGroup.AddComponent<RectTransform>();
        rightRect.anchoredPosition = new Vector2(135f, -20f);
        rightRect.sizeDelta = new Vector2(240f, 300f);

        // --- Card 1: Detail Sejarah (Top) ---
        GameObject detailCardObj = new GameObject("DetailSejarahCard");
        detailCardObj.transform.SetParent(rightGroup.transform, false);
        RectTransform dcRect = detailCardObj.AddComponent<RectTransform>();
        dcRect.anchoredPosition = new Vector2(0f, 95f);
        dcRect.sizeDelta = new Vector2(230f, 100f);
        Image dcBg = detailCardObj.AddComponent<Image>();
        dcBg.color = new Color(1f, 1f, 1f, 0.08f);

        // Header: "Detail Sejarah"
        GameObject dcHeadObj = new GameObject("Header");
        dcHeadObj.transform.SetParent(detailCardObj.transform, false);
        RectTransform dchRect = dcHeadObj.AddComponent<RectTransform>();
        dchRect.anchoredPosition = new Vector2(0f, 32f);
        dchRect.sizeDelta = new Vector2(210f, 24f);
        TextMeshProUGUI dchTxt = dcHeadObj.AddComponent<TextMeshProUGUI>();
        dchTxt.text = "💡 Detail Sejarah";
        dchTxt.fontSize = 15;
        dchTxt.fontStyle = FontStyles.Bold;
        dchTxt.color = Color.white;

        // Separator
        GameObject dcLine = new GameObject("Line");
        dcLine.transform.SetParent(detailCardObj.transform, false);
        RectTransform dclRect = dcLine.AddComponent<RectTransform>();
        dclRect.anchoredPosition = new Vector2(0f, 18f);
        dclRect.sizeDelta = new Vector2(210f, 1f);
        Image dclImg = dcLine.AddComponent<Image>();
        dclImg.color = new Color(1f, 1f, 1f, 0.25f);

        // Time Period Row
        GameObject tpLabelObj = new GameObject("TimePeriodLabel");
        tpLabelObj.transform.SetParent(detailCardObj.transform, false);
        RectTransform tplRect = tpLabelObj.AddComponent<RectTransform>();
        tplRect.anchoredPosition = new Vector2(-40f, -2f);
        tplRect.sizeDelta = new Vector2(130f, 20f);
        TextMeshProUGUI tplTxt = tpLabelObj.AddComponent<TextMeshProUGUI>();
        tplTxt.text = "📅 Time Period";
        tplTxt.fontSize = 13;
        tplTxt.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);

        GameObject tpValObj = new GameObject("TimePeriodValue");
        tpValObj.transform.SetParent(detailCardObj.transform, false);
        RectTransform tpvRect = tpValObj.AddComponent<RectTransform>();
        tpvRect.anchoredPosition = new Vector2(70f, -2f);
        tpvRect.sizeDelta = new Vector2(70f, 20f);
        TextMeshProUGUI tpvTxt = tpValObj.AddComponent<TextMeshProUGUI>();
        tpvTxt.text = "1879";
        tpvTxt.fontSize = 12;
        tpvTxt.alignment = TextAlignmentOptions.Right;
        tpvTxt.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
        historyScript.timePeriodText = tpvTxt;

        // Location Row
        GameObject locLabelObj = new GameObject("LocationLabel");
        locLabelObj.transform.SetParent(detailCardObj.transform, false);
        RectTransform loclRect = locLabelObj.AddComponent<RectTransform>();
        loclRect.anchoredPosition = new Vector2(-40f, -26f);
        loclRect.sizeDelta = new Vector2(130f, 20f);
        TextMeshProUGUI loclTxt = locLabelObj.AddComponent<TextMeshProUGUI>();
        loclTxt.text = "📍 Location";
        loclTxt.fontSize = 13;
        loclTxt.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);

        GameObject locValObj = new GameObject("LocationValue");
        locValObj.transform.SetParent(detailCardObj.transform, false);
        RectTransform locvRect = locValObj.AddComponent<RectTransform>();
        locvRect.anchoredPosition = new Vector2(70f, -26f);
        locvRect.sizeDelta = new Vector2(70f, 20f);
        TextMeshProUGUI locvTxt = locValObj.AddComponent<TextMeshProUGUI>();
        locvTxt.text = "Trengganu";
        locvTxt.fontSize = 12;
        locvTxt.alignment = TextAlignmentOptions.Right;
        locvTxt.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
        historyScript.locationText = locvTxt;

        // --- Card 2: Tentang Sejarah Ini / Description Card (Bottom) ---
        GameObject aboutCardObj = new GameObject("TentangSejarahCard");
        aboutCardObj.transform.SetParent(rightGroup.transform, false);
        RectTransform acRect = aboutCardObj.AddComponent<RectTransform>();
        acRect.anchoredPosition = new Vector2(0f, -55f);
        acRect.sizeDelta = new Vector2(230f, 180f);
        Image acBg = aboutCardObj.AddComponent<Image>();
        acBg.color = new Color(1f, 1f, 1f, 0.08f);

        // Header: "Tentang Artefak Ini" / "Tentang Sejarah Ini"
        GameObject acHeadObj = new GameObject("Header");
        acHeadObj.transform.SetParent(aboutCardObj.transform, false);
        RectTransform achRect = acHeadObj.AddComponent<RectTransform>();
        achRect.anchoredPosition = new Vector2(0f, 72f);
        achRect.sizeDelta = new Vector2(210f, 24f);
        TextMeshProUGUI achTxt = acHeadObj.AddComponent<TextMeshProUGUI>();
        achTxt.text = "ℹ Tentang Artefak Ini";
        achTxt.fontSize = 15;
        achTxt.fontStyle = FontStyles.Bold;
        achTxt.color = Color.white;

        // Separator Line
        GameObject acLine = new GameObject("Line");
        acLine.transform.SetParent(aboutCardObj.transform, false);
        RectTransform aclRect = acLine.AddComponent<RectTransform>();
        aclRect.anchoredPosition = new Vector2(0f, 56f);
        aclRect.sizeDelta = new Vector2(210f, 1f);
        Image aclImg = acLine.AddComponent<Image>();
        aclImg.color = new Color(1f, 1f, 1f, 0.25f);

        // Description Body Paragraph
        GameObject descBodyObj = new GameObject("DescriptionText");
        descBodyObj.transform.SetParent(aboutCardObj.transform, false);
        RectTransform descBodyRect = descBodyObj.AddComponent<RectTransform>();
        descBodyRect.anchoredPosition = new Vector2(0f, -12f);
        descBodyRect.sizeDelta = new Vector2(210f, 120f);
        TextMeshProUGUI descBodyTxt = descBodyObj.AddComponent<TextMeshProUGUI>();
        descBodyTxt.text = "It is a long established fact that a reader will be distracted by the readable content of a page when looking at its layout. The point of using Lorem Ipsum is that it has a more-or-less normal distribution of letters.";
        descBodyTxt.fontSize = 11;
        descBodyTxt.alignment = TextAlignmentOptions.TopLeft;
        descBodyTxt.enableWordWrapping = true;
        descBodyTxt.overflowMode = TextOverflowModes.Ellipsis;
        descBodyTxt.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);
        historyScript.descriptionText = descBodyTxt;

        // Select and Focus
        Selection.activeGameObject = panelObj;
        Undo.RegisterCreatedObjectUndo(panelObj, "Create History Panel UI Template");
        Debug.Log("Created History Panel UI Template matching 2DViewPanel hierarchy successfully!");
    }
}
