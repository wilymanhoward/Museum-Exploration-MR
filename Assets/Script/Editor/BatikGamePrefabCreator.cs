using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BatikGamePrefabCreator : Editor
{
    [MenuItem("Museum MR/Create Batik Game Panel UI Template (In Scene)", false, 20)]
    public static void CreateBatikPanelTemplate()
    {
        // 1. Create root Canvas Panel
        GameObject panelObj = new GameObject("BatikGamePanel_ManualTemplate");
        Canvas canvas = panelObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        panelObj.AddComponent<CanvasScaler>();
        panelObj.AddComponent<GraphicRaycaster>();
        panelObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(460f, 400f);
        panelObj.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(panelObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.537f, 0.557f, 0.478f, 1f); // Sage green

        // 2. Add Game2BatikMatch Component
        Game2BatikMatch matchGame = panelObj.AddComponent<Game2BatikMatch>();

        // 3. Header Title Text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(-65f, 168f);
        titleRect.sizeDelta = new Vector2(300f, 36f);
        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "Susunkan proses pembuatan batik";
        titleTxt.fontSize = 18;
        titleTxt.color = new Color(0.96f, 0.96f, 0.92f, 1f);
        matchGame.customTitleText = titleTxt;

        // 4. Feedback Text
        GameObject feedObj = new GameObject("ArtistYearText");
        feedObj.transform.SetParent(panelObj.transform, false);
        RectTransform feedRect = feedObj.AddComponent<RectTransform>();
        feedRect.anchoredPosition = new Vector2(-50f, -172f);
        feedRect.sizeDelta = new Vector2(250f, 34f);
        TextMeshProUGUI feedTxt = feedObj.AddComponent<TextMeshProUGUI>();
        feedTxt.fontSize = 11;
        feedTxt.color = new Color(0.96f, 0.96f, 0.92f, 1f);
        matchGame.customFeedbackText = feedTxt;

        // Description Text (Leaderboard)
        GameObject descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(panelObj.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchoredPosition = Vector2.zero;
        descRect.sizeDelta = new Vector2(420f, 300f);
        descObj.AddComponent<TextMeshProUGUI>();

        // 5. Close Button
        GameObject closeObj = new GameObject("CloseButton");
        closeObj.transform.SetParent(panelObj.transform, false);
        RectTransform closeRect = closeObj.AddComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(203f, 168f);
        closeRect.sizeDelta = new Vector2(34f, 34f);
        Image closeImg = closeObj.AddComponent<Image>();
        closeImg.color = new Color(0.22f, 0.235f, 0.16f, 1f);
        Button closeBtn = closeObj.AddComponent<Button>();
        matchGame.customCloseButton = closeBtn;

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

        // 6. Manual Slots Container
        GameObject slotsContainer = new GameObject("CustomSlotsContainer");
        slotsContainer.transform.SetParent(panelObj.transform, false);
        float[] slotXs = { -168f, -84f, 0f, 84f, 168f };
        for (int i = 0; i < 5; i++)
        {
            GameObject slotObj = new GameObject($"Slot_{i + 1}");
            slotObj.transform.SetParent(slotsContainer.transform, false);
            RectTransform sRect = slotObj.AddComponent<RectTransform>();
            sRect.sizeDelta = new Vector2(74f, 120f);
            sRect.anchoredPosition = new Vector2(slotXs[i], 85f);
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
        matchGame.customSlotsContainer = slotsContainer.transform;

        // 7. Check Answer Button
        GameObject checkObj = new GameObject("CheckAnswerButton");
        checkObj.transform.SetParent(panelObj.transform, false);
        RectTransform checkRect = checkObj.AddComponent<RectTransform>();
        checkRect.anchoredPosition = new Vector2(135f, -172f);
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
        checkTxt.fontSize = 12;
        checkTxt.alignment = TextAlignmentOptions.Center;
        checkTxt.color = new Color(0.96f, 0.96f, 0.92f, 1f);
        matchGame.customCheckAnswerButton = checkBtn;

        Selection.activeGameObject = panelObj;
        Undo.RegisterCreatedObjectUndo(panelObj, "Create Batik UI Panel Template");
        Debug.Log("Created Batik Panel Manual UI Template in scene. You can now adjust any slot, text, or button in the Inspector!");
    }
}
