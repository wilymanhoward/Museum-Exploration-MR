using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Game2BatikMatch : MonoBehaviour
{
    private class BatikStep
    {
        public int correctOrder; // 0, 1, 2, 3
        public string title;
        public string description;

        public BatikStep(int order, string t, string d)
        {
            correctOrder = order;
            title = t;
            description = d;
        }
    }

    private List<BatikStep> steps = new List<BatikStep>();
    private List<BatikStep> currentOrder = new List<BatikStep>();
    private List<GameObject> stepContainers = new List<GameObject>();
    private List<GameObject> spawnedButtons = new List<GameObject>();

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI feedbackText;
    private Material buttonMaterial;

    private void Start()
    {
        titleText = transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        feedbackText = transform.Find("ArtistYearText")?.GetComponent<TextMeshProUGUI>();
        var descText = transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();

        if (titleText != null) titleText.text = "PROSES MEMBUAT BATIK";
        if (feedbackText != null)
        {
            feedbackText.text = "Susun langkah membuat batik mengikut turutan yang betul.";
            feedbackText.fontStyle = FontStyles.Normal;
        }
        if (descText != null) descText.text = "";

        buttonMaterial = FindMaterial("Mat_Button");

        // Define steps
        steps.Add(new BatikStep(0, "1. Canting (Melapis Lilin)", "Melukis corak menggunakan lilin cair panas."));
        steps.Add(new BatikStep(1, "2. Pewarnaan (Mewarna)", "Menyapu warna pada kawasan kain tanpa lilin."));
        steps.Add(new BatikStep(2, "3. Merebus (Melarut Lilin)", "Merebus kain dalam air panas untuk membuang lilin."));
        steps.Add(new BatikStep(3, "4. Membasuh & Kering", "Membasuh kain dan menjemurnya sehingga kering."));

        // Initialize shuffled order
        ResetGame();
    }

    private void ResetGame()
    {
        currentOrder.Clear();
        currentOrder.AddRange(steps);

        // Shuffle until not in correct order
        while (IsCorrectOrder())
        {
            ShuffleList(currentOrder);
        }

        if (feedbackText != null) feedbackText.text = "Gunakan butang ↑ ↓ untuk menyusun mengikut turutan.";

        // Clear previous buttons
        foreach (var btn in spawnedButtons) Destroy(btn);
        spawnedButtons.Clear();

        BuildStepListUI();

        // Create Verify Button
        GameObject verifyBtn = CreateGameButton("Sahkan Susunan", new Vector2(-60f, -80f), VerifySelection);
        spawnedButtons.Add(verifyBtn);

        // Create Tutup Button
        GameObject closeBtn = CreateGameButton("Tutup", new Vector2(100f, -80f), () => MiniGameManager.Instance.CloseActiveGame());
        spawnedButtons.Add(closeBtn);
    }

    private bool IsCorrectOrder()
    {
        for (int i = 0; i < currentOrder.Count; i++)
        {
            if (currentOrder[i].correctOrder != i) return false;
        }
        return true;
    }

    private void BuildStepListUI()
    {
        // Destroy existing visual steps
        foreach (var container in stepContainers) Destroy(container);
        stepContainers.Clear();

        float startY = 30f;
        float spacingY = -42f;

        for (int i = 0; i < currentOrder.Count; i++)
        {
            int index = i;
            BatikStep step = currentOrder[i];

            // Container row
            GameObject row = new GameObject("StepRow");
            row.transform.SetParent(transform, false);
            stepContainers.Add(row);

            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(360f, 36f);
            rowRect.anchoredPosition = new Vector2(-15f, startY + (i * spacingY));

            // Background glass visual
            Image bg = row.AddComponent<Image>();
            bg.material = FindMaterial("Mat_ArtifactDetailPanel"); // Reuse glass panel mat
            bg.color = new Color(0.96f, 0.96f, 0.98f, 0.5f);

            // Title Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
            txt.text = $"<b>{step.title}</b> - {step.description}";
            txt.fontSize = 11;
            txt.color = new Color(0.2f, 0.2f, 0.25f);
            txt.alignment = TextAlignmentOptions.Left;

            RectTransform txtRect = textObj.GetComponent<RectTransform>();
            txtRect.anchorMin = new Vector2(0.05f, 0f);
            txtRect.anchorMax = new Vector2(0.7f, 1f);
            txtRect.sizeDelta = Vector2.zero;

            // Up Button (↑)
            if (i > 0)
            {
                GameObject upBtn = CreateArrowButton("↑", new Vector2(110f, 0), row.transform, () => SwapSteps(index, index - 1));
            }

            // Down Button (↓)
            if (i < currentOrder.Count - 1)
            {
                GameObject downBtn = CreateArrowButton("↓", new Vector2(145f, 0), row.transform, () => SwapSteps(index, index + 1));
            }
        }
    }

    private void SwapSteps(int i, int j)
    {
        BatikStep tmp = currentOrder[i];
        currentOrder[i] = currentOrder[j];
        currentOrder[j] = tmp;

        BuildStepListUI();
    }

    private void VerifySelection()
    {
        if (IsCorrectOrder())
        {
            if (feedbackText != null)
            {
                feedbackText.text = "<color=#00CC88>Luar biasa! Susunan anda betul dan tepat!</color>";
            }

            // Mark Batik Fabric as scanned/interacted
            if (MuseumManager.Instance != null)
            {
                MuseumManager.Instance.MarkArtifactInteracted("artifact_batik");
            }

            // Clear verifying buttons and replace with close
            foreach (var btn in spawnedButtons) Destroy(btn);
            spawnedButtons.Clear();

            GameObject resetBtn = CreateGameButton("Main Lagi", new Vector2(-60f, -80f), ResetGame);
            spawnedButtons.Add(resetBtn);

            GameObject closeBtn = CreateGameButton("Tutup", new Vector2(100f, -80f), () => MiniGameManager.Instance.CloseActiveGame());
            spawnedButtons.Add(closeBtn);
        }
        else
        {
            if (feedbackText != null)
            {
                feedbackText.text = "<color=#FF4444>Susunan salah! Cuba perhatikan turutan langkah semula.</color>";
            }
        }
    }

    private GameObject CreateArrowButton(string label, Vector2 localPos, Transform parent, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject buttonObj = new GameObject("ArrowBtn");
        buttonObj.transform.SetParent(parent, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(30f, 26f);
        rect.anchoredPosition = localPos;

        Image img = buttonObj.AddComponent<Image>();
        if (buttonMaterial != null)
        {
            img.material = buttonMaterial;
        }
        img.color = new Color(0.9f, 0.9f, 0.93f, 0.8f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 12;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.1f, 0.1f, 0.15f);

        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        XRButtonSelection select = buttonObj.AddComponent<XRButtonSelection>();
        select.buttonImage = img;
        select.scaleTarget = buttonObj.transform;

        BoxCollider col = buttonObj.AddComponent<BoxCollider>();
        col.size = new Vector3(30f, 26f, 15f);
        col.isTrigger = true;

        select.onClick.AddListener(onClickAction);
        return buttonObj;
    }

    private GameObject CreateGameButton(string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject buttonObj = new GameObject("GameButton");
        buttonObj.transform.SetParent(transform, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(150f, 36f);
        rect.anchoredPosition = anchoredPosition;

        Image img = buttonObj.AddComponent<Image>();
        if (buttonMaterial != null)
        {
            img.material = buttonMaterial;
        }
        img.color = new Color(0.9f, 0.9f, 0.93f, 0.8f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 12;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.1f, 0.1f, 0.15f);

        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        XRButtonSelection select = buttonObj.AddComponent<XRButtonSelection>();
        select.buttonImage = img;
        select.scaleTarget = buttonObj.transform;

        BoxCollider col = buttonObj.AddComponent<BoxCollider>();
        col.size = new Vector3(150f, 36f, 15f);
        col.isTrigger = true;

        select.onClick.AddListener(onClickAction);
        return buttonObj;
    }

    private Material FindMaterial(string matName)
    {
        Material[] mats = Resources.FindObjectsOfTypeAll<Material>();
        foreach (Material m in mats)
        {
            if (m.name == matName) return m;
        }
        return null;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[r];
            list[r] = tmp;
        }
    }
}
