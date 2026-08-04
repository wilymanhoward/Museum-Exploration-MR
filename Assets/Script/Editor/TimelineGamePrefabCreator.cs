using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimelineGamePrefabCreator : Editor
{
    [MenuItem("Museum MR/Create Timeline Game Panel UI Template (In Scene)", false, 21)]
    public static void CreateTimelinePanelTemplate()
    {
        // 1. Create root Canvas Panel (Game3Panel)
        GameObject panelObj = new GameObject("Game3Panel");
        Canvas canvas = panelObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        panelObj.AddComponent<CanvasScaler>();
        panelObj.AddComponent<GraphicRaycaster>();

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(460f, 400f);
        panelObj.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // 2. Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(panelObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.35f, 0.38f, 0.31f, 1f); // Sage/olive green

        // 3. Add Game3OrderTimeline Component
        Game3OrderTimeline gameComp = panelObj.AddComponent<Game3OrderTimeline>();

        // 4. Close Button
        GameObject closeObj = new GameObject("CloseButton");
        closeObj.transform.SetParent(panelObj.transform, false);
        RectTransform closeRect = closeObj.AddComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(203f, 168f);
        closeRect.sizeDelta = new Vector2(34f, 34f);
        Image closeImg = closeObj.AddComponent<Image>();
        closeImg.color = new Color(0.22f, 0.235f, 0.16f, 1f);
        Button closeBtn = closeObj.AddComponent<Button>();
        gameComp.closeButton = closeBtn;

        GameObject closeX = new GameObject("X");
        closeX.transform.SetParent(closeObj.transform, false);
        RectTransform xRect = closeX.AddComponent<RectTransform>();
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI xTxt = closeX.AddComponent<TextMeshProUGUI>();
        xTxt.text = "X";
        xTxt.fontSize = 16;
        xTxt.alignment = TextAlignmentOptions.Center;
        xTxt.color = Color.white;

        // 5. HeaderIcon (Optional icon object matching hierarchy)
        GameObject iconObj = new GameObject("HeaderIcon");
        iconObj.transform.SetParent(panelObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchoredPosition = new Vector2(-195f, 168f);
        iconRect.sizeDelta = new Vector2(30f, 30f);

        // 6. TaskText (Header question prompt text)
        GameObject taskObj = new GameObject("TaskText");
        taskObj.transform.SetParent(panelObj.transform, false);
        RectTransform taskRect = taskObj.AddComponent<RectTransform>();
        taskRect.anchoredPosition = new Vector2(-15f, 168f);
        taskRect.sizeDelta = new Vector2(320f, 36f);
        TextMeshProUGUI taskTxt = taskObj.AddComponent<TextMeshProUGUI>();
        taskTxt.text = "Urutkan Babak Utama Sejarah Indonesia";
        taskTxt.fontSize = 16;
        taskTxt.alignment = TextAlignmentOptions.Left;
        taskTxt.color = new Color(0.96f, 0.96f, 0.92f, 1f);
        gameComp.taskText = taskTxt;

        // 7. Separator Line
        GameObject sepObj = new GameObject("Separator");
        sepObj.transform.SetParent(panelObj.transform, false);
        RectTransform sepRect = sepObj.AddComponent<RectTransform>();
        sepRect.anchoredPosition = new Vector2(0f, 142f);
        sepRect.sizeDelta = new Vector2(430f, 2f);
        Image sepImg = sepObj.AddComponent<Image>();
        sepImg.color = new Color(0.78f, 0.76f, 0.35f, 0.4f);

        // 8. NumberSlotPanel (Top 5 target slots)
        GameObject slotsContainer = new GameObject("NumberSlotPanel");
        slotsContainer.transform.SetParent(panelObj.transform, false);
        float[] slotXs = { -168f, -84f, 0f, 84f, 168f };
        for (int i = 0; i < 5; i++)
        {
            GameObject slotObj = new GameObject($"Slot_{i + 1}");
            slotObj.transform.SetParent(slotsContainer.transform, false);
            RectTransform sRect = slotObj.AddComponent<RectTransform>();
            sRect.sizeDelta = new Vector2(74f, 120f);
            sRect.anchoredPosition = new Vector2(slotXs[i], 70f);
            Image sImg = slotObj.AddComponent<Image>();
            sImg.color = new Color(0.424f, 0.447f, 0.373f, 1f);

            GameObject numObj = new GameObject("Number");
            numObj.transform.SetParent(slotObj.transform, false);
            RectTransform numRect = numObj.AddComponent<RectTransform>();
            numRect.anchorMin = Vector2.zero;
            numRect.anchorMax = Vector2.one;
            numRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI numTxt = numObj.AddComponent<TextMeshProUGUI>();
            numTxt.text = (i + 1).ToString();
            numTxt.fontSize = 24;
            numTxt.alignment = TextAlignmentOptions.Center;
            numTxt.color = new Color(0.784f, 0.765f, 0.353f, 0.85f);
        }
        gameComp.numberSlotPanel = slotsContainer.transform;

        // 9. ProcessLayout (Cards Process1 to Process5 matching screenshot hierarchy)
        GameObject processLayoutObj = new GameObject("ProcessLayout");
        processLayoutObj.transform.SetParent(panelObj.transform, false);
        RectTransform layoutRect = processLayoutObj.AddComponent<RectTransform>();
        layoutRect.anchorMin = Vector2.zero;
        layoutRect.anchorMax = Vector2.one;
        layoutRect.sizeDelta = Vector2.zero;
        gameComp.processLayout = processLayoutObj.transform;

        GameObject[] cardObjs = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            GameObject cardObj = new GameObject($"Process{i + 1}");
            cardObj.transform.SetParent(processLayoutObj.transform, false);
            RectTransform cRect = cardObj.AddComponent<RectTransform>();
            cRect.sizeDelta = new Vector2(74f, 120f);
            cRect.anchoredPosition = new Vector2(slotXs[i], -70f);

            Image cImg = cardObj.AddComponent<Image>();
            cImg.color = new Color(0.24f, 0.28f, 0.22f, 0.95f);

            GameObject textObj = new GameObject("Text (TMP)");
            textObj.transform.SetParent(cardObj.transform, false);
            RectTransform tRect = textObj.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = new Vector2(-6f, -6f);

            TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 11;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(0.96f, 0.96f, 0.92f, 1f);
            txt.text = $"Timeline {i + 1}";

            cardObjs[i] = cardObj;
        }
        gameComp.processObjects = cardObjs;

        // 10. CheckButton ("Periksa Jawaban")
        GameObject checkObj = new GameObject("CheckButton");
        checkObj.transform.SetParent(panelObj.transform, false);
        RectTransform checkRect = checkObj.AddComponent<RectTransform>();
        checkRect.anchoredPosition = new Vector2(0f, -165f);
        checkRect.sizeDelta = new Vector2(170f, 38f);
        Image checkImg = checkObj.AddComponent<Image>();
        checkImg.color = new Color(0.784f, 0.765f, 0.353f, 1f);
        Button checkBtn = checkObj.AddComponent<Button>();

        GameObject checkTxtObj = new GameObject("Text");
        checkTxtObj.transform.SetParent(checkObj.transform, false);
        RectTransform ctRect = checkTxtObj.AddComponent<RectTransform>();
        ctRect.anchorMin = Vector2.zero;
        ctRect.anchorMax = Vector2.one;
        ctRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI checkTxt = checkTxtObj.AddComponent<TextMeshProUGUI>();
        checkTxt.text = "Periksa Jawaban  >";
        checkTxt.fontSize = 13;
        checkTxt.alignment = TextAlignmentOptions.Center;
        checkTxt.color = new Color(0.96f, 0.96f, 0.92f, 1f);
        gameComp.checkAnswerButton = checkBtn;

        // 11. Wrong Text Warning
        GameObject wrongObj = new GameObject("Wrong Text");
        wrongObj.transform.SetParent(panelObj.transform, false);
        RectTransform wrongRect = wrongObj.AddComponent<RectTransform>();
        wrongRect.anchoredPosition = new Vector2(0f, -130f);
        wrongRect.sizeDelta = new Vector2(300f, 30f);
        TextMeshProUGUI wrongTxt = wrongObj.AddComponent<TextMeshProUGUI>();
        wrongTxt.text = "Urutan belum tepat! Coba lagi.";
        wrongTxt.fontSize = 14;
        wrongTxt.alignment = TextAlignmentOptions.Center;
        wrongTxt.color = new Color(0.95f, 0.3f, 0.3f, 1f);
        wrongObj.SetActive(false);
        gameComp.wrongText = wrongObj;

        Selection.activeGameObject = panelObj;
        Undo.RegisterCreatedObjectUndo(panelObj, "Create Timeline Game Panel UI Template");
        Debug.Log("Created Timeline Game Panel UI Template (Game3Panel) matching hierarchy screenshot successfully!");
    }
}
