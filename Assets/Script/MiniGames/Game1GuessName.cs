using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// "Namakan Artefak" mini-game. Shows a rotating 3D artifact model (rendered as a black
/// silhouette so the player must guess by shape) and a grid of name choices. The player
/// drags to rotate the model, taps a choice to select it, then presses "Periksa Jawaban"
/// to check. Runs for 5 questions, then shows a final score screen.
/// </summary>
public class Game1GuessName : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Drag-to-rotate handler for the model preview area
    // ------------------------------------------------------------------
    private class ModelDragRotator : MonoBehaviour, IDragHandler
    {
        public Transform targetModel;
        public float sensitivity = 0.4f;

        public void OnDrag(PointerEventData eventData)
        {
            if (targetModel == null) return;
            targetModel.Rotate(Vector3.up, -eventData.delta.x * sensitivity, Space.World);
            targetModel.Rotate(Vector3.right, eventData.delta.y * sensitivity, Space.World);
        }
    }

    // ------------------------------------------------------------------
    // Palette (matches the batik game's sage/khaki look for a consistent mini-game style)
    // ------------------------------------------------------------------
    private static readonly Color SageBg = new Color(0.537f, 0.557f, 0.478f, 1f);
    private static readonly Color SageDark = new Color(0.424f, 0.447f, 0.373f, 1f);
    private static readonly Color Khaki = new Color(0.784f, 0.765f, 0.353f, 1f);
    private static readonly Color KhakiHover = new Color(0.86f, 0.84f, 0.44f, 1f);
    private static readonly Color DarkOlive = new Color(0.22f, 0.235f, 0.16f, 1f);
    private static readonly Color DarkOliveHover = new Color(0.33f, 0.35f, 0.25f, 1f);
    private static readonly Color Cream = new Color(0.96f, 0.96f, 0.92f, 1f);
    private static readonly Color ChipDefault = new Color(0.66f, 0.68f, 0.6f, 0.9f);
    private static readonly Color ChipDefaultHover = new Color(0.72f, 0.74f, 0.66f, 0.95f);

    private static readonly Vector2 PanelSize = new Vector2(460f, 400f);
    private const int TotalQuestions = 5;

    [Tooltip("Optional serif TMP Font Asset. Only used if it actually has a material (a valid, generated font asset).")]
    public TMP_FontAsset customFont;

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------
    private int questionIndex = 0;
    private int score = 0;
    private List<ArtifactData> allArtifacts = new List<ArtifactData>();
    private List<ArtifactData> usedArtifacts = new List<ArtifactData>();
    private ArtifactData correctAnswer;
    private string selectedOption;
    private bool answerChecked;

    private GameObject spawnedModel;
    private Transform modelViewAnchor;
    private GameObject questionRoot;
    private GameObject resultsRoot;
    private List<(GameObject obj, Image bg, string name)> optionChips = new List<(GameObject, Image, string)>();

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI feedbackText;
    private GameObject checkButton;
    private TextMeshProUGUI checkButtonLabel;

    private Sprite roundedFillSprite;
    private Sprite roundedBorderSprite;
    private Sprite circleSprite;
    private Sprite rotateIconSprite;
    private List<Texture2D> generatedTextures = new List<Texture2D>();
    private Material silhouetteMaterial;

    private void Start()
    {
        roundedFillSprite = MakeRoundedSprite(128, 128, 24, 0);
        roundedBorderSprite = MakeRoundedSprite(128, 128, 24, 6);
        circleSprite = MakeRoundedSprite(64, 64, 32, 0);
        rotateIconSprite = MakePartialRingSprite(64, 10, 70f);

        if (customFont == null)
        {
            TMP_FontAsset loaded = Resources.Load<TMP_FontAsset>("Fonts & Materials/Georgia SDF");
            if (loaded != null && loaded.material != null) customFont = loaded;
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        silhouetteMaterial = new Material(unlitShader) { color = Color.black };

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null) rootRect.sizeDelta = PanelSize;
        transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        Transform bg = transform.Find("Background");
        Image bgImage = bg != null ? bg.GetComponent<Image>() : null;
        if (bgImage != null)
        {
            bgImage.material = null;
            bgImage.sprite = roundedFillSprite;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = SageBg;
        }

        // Re-use (and restyle) the panel's inherited title text for the "x/5 ..." header
        titleText = transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        if (titleText != null)
        {
            SetupTextRect(titleText.rectTransform, new Vector2(-65f, 168f), new Vector2(300f, 36f));
            SafeSetFont(titleText);
            titleText.fontSize = 15;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.color = Cream;
        }

        // Feedback text (correct/wrong messages), inherited "ArtistYearText" slot
        feedbackText = transform.Find("ArtistYearText")?.GetComponent<TextMeshProUGUI>();
        if (feedbackText != null)
        {
            SetupTextRect(feedbackText.rectTransform, new Vector2(0f, -60f), new Vector2(400f, 26f));
            SafeSetFont(feedbackText);
            feedbackText.fontSize = 12;
            feedbackText.fontStyle = FontStyles.Bold;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.color = Cream;
            feedbackText.text = "";
        }

        Transform descT = transform.Find("DescriptionText");
        if (descT != null) descT.GetComponent<TextMeshProUGUI>()?.gameObject.SetActive(false);

        // The artifact panel prefab's ModelSpawnAnchor gets destroyed by MiniGameManager
        // for every mini-game (it's meant for the artifact viewer, not games), so this
        // game builds its own dedicated model-view anchor instead of relying on it.
        CreateCloseButton();

        CollectArtifacts();
        LoadQuestion();
    }

    private void SetupTextRect(RectTransform rect, Vector2 anchoredPos, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
    }

    private void CollectArtifacts()
    {
        allArtifacts.Clear();
        if (RoomManager.Instance == null) return;

        foreach (var room in RoomManager.Instance.rooms)
        {
            foreach (var art in room.artifacts)
            {
                if (art != null && art.modelPrefab != null && !allArtifacts.Contains(art))
                {
                    allArtifacts.Add(art);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Question flow
    // ------------------------------------------------------------------
    private void LoadQuestion()
    {
        if (questionRoot != null) Destroy(questionRoot);
        if (spawnedModel != null) Destroy(spawnedModel);
        optionChips.Clear();

        if (questionIndex >= TotalQuestions || allArtifacts.Count == 0)
        {
            ShowFinalResults();
            return;
        }

        // Pick a correct answer not already used this session
        List<ArtifactData> pool = new List<ArtifactData>(allArtifacts);
        foreach (var used in usedArtifacts) pool.Remove(used);
        if (pool.Count == 0) pool = new List<ArtifactData>(allArtifacts); // ran out of unique artifacts, allow repeats

        correctAnswer = pool[Random.Range(0, pool.Count)];
        usedArtifacts.Add(correctAnswer);
        selectedOption = null;
        answerChecked = false;

        if (titleText != null) titleText.text = $"{questionIndex + 1}/{TotalQuestions}  Namakan Artefak berikut";
        if (feedbackText != null) feedbackText.text = "";

        questionRoot = new GameObject("Question");
        questionRoot.transform.SetParent(transform, false);
        RectTransform qRect = questionRoot.AddComponent<RectTransform>();
        qRect.anchorMin = new Vector2(0.5f, 0.5f);
        qRect.anchorMax = new Vector2(0.5f, 0.5f);
        qRect.anchoredPosition = Vector2.zero;
        qRect.sizeDelta = Vector2.zero;

        BuildRotateHint();
        BuildModelView();
        BuildDivider();
        BuildOptions();
        BuildCheckButton();
    }

    private void BuildRotateHint()
    {
        GameObject hintObj = new GameObject("RotateHint");
        hintObj.transform.SetParent(questionRoot.transform, false);
        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.sizeDelta = new Vector2(220f, 20f);
        hintRect.anchoredPosition = new Vector2(6f, 130f);

        GameObject iconObj = new GameObject("RotateIcon");
        iconObj.transform.SetParent(hintObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(16f, 16f);
        iconRect.anchoredPosition = new Vector2(-95f, 0f);
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = rotateIconSprite;
        iconImg.color = Cream;

        GameObject textObj = new GameObject("HintText");
        textObj.transform.SetParent(hintObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(200f, 20f);
        textRect.anchoredPosition = new Vector2(10f, 0f);
        TextMeshProUGUI hintText = textObj.AddComponent<TextMeshProUGUI>();
        hintText.text = "Drag to Rotate Model";
        SafeSetFont(hintText);
        hintText.fontSize = 11;
        hintText.fontStyle = FontStyles.Italic;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = new Color(Cream.r, Cream.g, Cream.b, 0.85f);
    }

    private void BuildModelView()
    {
        GameObject viewObj = new GameObject("ModelView");
        viewObj.transform.SetParent(questionRoot.transform, false);
        RectTransform viewRect = viewObj.AddComponent<RectTransform>();
        viewRect.sizeDelta = new Vector2(200f, 150f);
        viewRect.anchoredPosition = new Vector2(0f, 35f);

        // Invisible drag-catcher covering the model area (raycastable but unseen)
        Image dragImg = viewObj.AddComponent<Image>();
        dragImg.color = new Color(0f, 0f, 0f, 0f);

        modelViewAnchor = viewObj.transform;

        if (correctAnswer.modelPrefab != null)
        {
            spawnedModel = Instantiate(correctAnswer.modelPrefab, modelViewAnchor.position, modelViewAnchor.rotation, modelViewAnchor);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.Euler(-10f, 25f, 0f);
            spawnedModel.transform.localScale = correctAnswer.modelPrefab.transform.localScale;

            // Auto-fit to a small, consistent real-world size instead of the artifact's
            // own "true" authored size - artifacts vary a lot in physical scale, so
            // rendering some of them at true size overflowed this small preview window.
            Bounds bounds = CalculateBounds(spawnedModel);
            float maxExtent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxExtent > 0.0000001f)
            {
                const float targetWorldSize = 0.11f; // ~11cm across, comfortable for this preview area
                spawnedModel.transform.localScale *= targetWorldSize / maxExtent;
            }

            // Re-center: the model's pivot may not sit at the middle of its geometry,
            // so recompute bounds after scaling and shift it to sit centered in the view.
            Bounds centeredBounds = CalculateBounds(spawnedModel);
            spawnedModel.transform.position -= (centeredBounds.center - modelViewAnchor.position);

            // Render as a solid black silhouette so the player has to guess by shape alone
            foreach (Renderer r in spawnedModel.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = silhouetteMaterial;
                r.materials = mats;
            }

            var rotator = viewObj.AddComponent<ModelDragRotator>();
            rotator.targetModel = spawnedModel.transform;
        }
    }

    private static Bounds CalculateBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    private void BuildDivider()
    {
        GameObject divObj = new GameObject("DividerLine");
        divObj.transform.SetParent(questionRoot.transform, false);
        RectTransform divRect = divObj.AddComponent<RectTransform>();
        divRect.sizeDelta = new Vector2(380f, 2f);
        divRect.anchoredPosition = new Vector2(0f, -50f);
        Image divImg = divObj.AddComponent<Image>();
        divImg.color = new Color(Khaki.r, Khaki.g, Khaki.b, 0.4f);

        GameObject diaObj = new GameObject("DividerDiamond");
        diaObj.transform.SetParent(questionRoot.transform, false);
        RectTransform diaRect = diaObj.AddComponent<RectTransform>();
        diaRect.sizeDelta = new Vector2(10f, 10f);
        diaRect.anchoredPosition = new Vector2(0f, -50f);
        diaRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image diaImg = diaObj.AddComponent<Image>();
        diaImg.color = Khaki;
    }

    private void BuildOptions()
    {
        // Build the wrong-answer pool and pick 4 distractors + the correct answer
        List<string> options = new List<string> { correctAnswer.artifactName };
        List<ArtifactData> wrongPool = new List<ArtifactData>(allArtifacts);
        wrongPool.Remove(correctAnswer);
        for (int i = 0; i < 4 && wrongPool.Count > 0; i++)
        {
            int r = Random.Range(0, wrongPool.Count);
            options.Add(wrongPool[r].artifactName);
            wrongPool.RemoveAt(r);
        }
        ShuffleList(options);

        // 3-then-2 wrapping grid, matching the mockup layout
        Vector2[] positions =
        {
            new Vector2(-140f, -90f), new Vector2(0f, -90f), new Vector2(140f, -90f),
            new Vector2(-70f, -135f), new Vector2(70f, -135f),
        };

        for (int i = 0; i < options.Count && i < positions.Length; i++)
        {
            string optionName = options[i];
            GameObject chip = CreateOptionChip(optionName, positions[i]);
            chip.transform.SetParent(questionRoot.transform, false);
        }
    }

    private GameObject CreateOptionChip(string label, Vector2 anchoredPos)
    {
        GameObject chipObj = new GameObject($"Chip_{label}");
        RectTransform rect = chipObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(132f, 34f);
        rect.anchoredPosition = anchoredPos;

        Image img = chipObj.AddComponent<Image>();
        img.sprite = roundedFillSprite;
        img.type = Image.Type.Sliced;
        img.color = ChipDefault;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(chipObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        SafeSetFont(txt);
        txt.fontSize = 12;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = DarkOlive;
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 8f;
        txt.fontSizeMax = 12f;

        XRButtonSelection select = chipObj.AddComponent<XRButtonSelection>();
        select.buttonImage = img;
        select.scaleTarget = chipObj.transform;
        select.normalColor = ChipDefault;
        select.hoverColor = ChipDefaultHover;

        BoxCollider col = chipObj.AddComponent<BoxCollider>();
        col.size = new Vector3(132f, 34f, 15f);
        col.isTrigger = true;

        select.onClick.AddListener(() => OnOptionSelected(label));

        optionChips.Add((chipObj, img, label));
        return chipObj;
    }

    private void OnOptionSelected(string name)
    {
        if (answerChecked) return; // locked once checked, until Next is pressed

        selectedOption = name;
        foreach (var (obj, img, chipName) in optionChips)
        {
            bool isSelected = chipName == selectedOption;
            img.color = isSelected ? Khaki : ChipDefault;

            XRButtonSelection select = obj.GetComponent<XRButtonSelection>();
            if (select != null)
            {
                select.normalColor = isSelected ? Khaki : ChipDefault;
                select.hoverColor = isSelected ? KhakiHover : ChipDefaultHover;
            }

            TextMeshProUGUI txt = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.color = DarkOlive;
        }
    }

    private void BuildCheckButton()
    {
        checkButton = CreateStyledButton("Periksa Jawaban  >", new Vector2(135f, -172f), new Vector2(170f, 38f), OnCheckOrNextPressed);
        checkButton.transform.SetParent(questionRoot.transform, false);
        checkButtonLabel = checkButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnCheckOrNextPressed()
    {
        if (!answerChecked)
        {
            CheckAnswer();
        }
        else
        {
            questionIndex++;
            LoadQuestion();
        }
    }

    private void CheckAnswer()
    {
        if (string.IsNullOrEmpty(selectedOption))
        {
            if (feedbackText != null)
            {
                feedbackText.text = "<color=#F0D060>Pilih satu jawapan dahulu!</color>";
            }
            return;
        }

        answerChecked = true;
        bool isCorrect = selectedOption == correctAnswer.artifactName;

        if (isCorrect)
        {
            score++;
            if (feedbackText != null) feedbackText.text = "<color=#B8E986>Betul! Tahniah!</color>";
            if (RoomManager.Instance != null) RoomManager.Instance.MarkArtifactInteracted(correctAnswer.artifactId);
        }
        else
        {
            if (feedbackText != null) feedbackText.text = $"<color=#FF6655>Salah! Jawapan betul: {correctAnswer.artifactName}</color>";
        }

        // Highlight the correct chip green and the (wrong) selected chip red
        foreach (var (obj, img, chipName) in optionChips)
        {
            if (chipName == correctAnswer.artifactName) img.color = new Color(0.55f, 0.75f, 0.35f, 1f);
            else if (chipName == selectedOption) img.color = new Color(0.85f, 0.35f, 0.3f, 1f);

            XRButtonSelection select = obj.GetComponent<XRButtonSelection>();
            if (select != null) select.enabled = false;
        }

        if (checkButtonLabel != null)
        {
            checkButtonLabel.text = questionIndex + 1 >= TotalQuestions ? "Selesai  >" : "Seterusnya  >";
        }
    }

    private void ShowFinalResults()
    {
        if (resultsRoot != null) Destroy(resultsRoot);
        resultsRoot = new GameObject("Results");
        resultsRoot.transform.SetParent(transform, false);
        RectTransform rRect = resultsRoot.AddComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 0.5f);
        rRect.anchorMax = new Vector2(0.5f, 0.5f);
        rRect.anchoredPosition = Vector2.zero;
        rRect.sizeDelta = Vector2.zero;

        if (titleText != null) titleText.text = "Keputusan";
        if (feedbackText != null) feedbackText.text = "";

        GameObject scoreObj = new GameObject("ScoreText");
        scoreObj.transform.SetParent(resultsRoot.transform, false);
        RectTransform scoreRect = scoreObj.AddComponent<RectTransform>();
        scoreRect.sizeDelta = new Vector2(360f, 60f);
        scoreRect.anchoredPosition = new Vector2(0f, 20f);
        TextMeshProUGUI scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
        scoreText.text = $"Skor anda: {score}/{TotalQuestions}";
        SafeSetFont(scoreText);
        scoreText.fontSize = 22;
        scoreText.fontStyle = FontStyles.Bold;
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.color = Cream;

        GameObject replayBtn = CreateStyledButton("Main Lagi", new Vector2(-80f, -60f), new Vector2(130f, 38f), ResetGame);
        replayBtn.transform.SetParent(resultsRoot.transform, false);

        GameObject closeBtn = CreateStyledButton("Tutup", new Vector2(80f, -60f), new Vector2(130f, 38f), () => MiniGameManager.Instance.CloseActiveGame());
        closeBtn.transform.SetParent(resultsRoot.transform, false);
    }

    private void ResetGame()
    {
        if (resultsRoot != null) { Destroy(resultsRoot); resultsRoot = null; }
        questionIndex = 0;
        score = 0;
        usedArtifacts.Clear();
        LoadQuestion();
    }

    // ------------------------------------------------------------------
    // Buttons
    // ------------------------------------------------------------------
    private void CreateCloseButton()
    {
        GameObject closeObj = new GameObject("CloseButton");
        closeObj.transform.SetParent(transform, false);
        RectTransform closeRect = closeObj.AddComponent<RectTransform>();
        closeRect.sizeDelta = new Vector2(40f, 40f);
        closeRect.anchoredPosition = new Vector2(203f, 168f);

        Image circleImage = closeObj.AddComponent<Image>();
        circleImage.sprite = circleSprite;
        circleImage.color = DarkOlive;

        GameObject xObj = new GameObject("X");
        xObj.transform.SetParent(closeObj.transform, false);
        RectTransform xRect = xObj.AddComponent<RectTransform>();
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI xText = xObj.AddComponent<TextMeshProUGUI>();
        xText.text = "X";
        SafeSetFont(xText);
        xText.fontSize = 16;
        xText.fontStyle = FontStyles.Bold;
        xText.alignment = TextAlignmentOptions.Center;
        xText.color = Cream;

        XRButtonSelection select = closeObj.AddComponent<XRButtonSelection>();
        select.buttonImage = circleImage;
        select.scaleTarget = closeObj.transform;
        select.normalColor = DarkOlive;
        select.hoverColor = DarkOliveHover;

        BoxCollider col = closeObj.AddComponent<BoxCollider>();
        col.size = new Vector3(40f, 40f, 15f);
        col.isTrigger = true;

        select.onClick.AddListener(() => MiniGameManager.Instance.CloseActiveGame());
    }

    private GameObject CreateStyledButton(string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject buttonObj = new GameObject($"Button_{label}");
        buttonObj.transform.SetParent(transform, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image img = buttonObj.AddComponent<Image>();
        img.sprite = roundedFillSprite;
        img.type = Image.Type.Sliced;
        img.color = Khaki;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        SafeSetFont(txt);
        txt.fontSize = 12;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = DarkOlive;

        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        XRButtonSelection select = buttonObj.AddComponent<XRButtonSelection>();
        select.buttonImage = img;
        select.scaleTarget = buttonObj.transform;
        select.normalColor = Khaki;
        select.hoverColor = KhakiHover;

        BoxCollider col = buttonObj.AddComponent<BoxCollider>();
        col.size = new Vector3(size.x, size.y, 15f);
        col.isTrigger = true;

        select.onClick.AddListener(onClickAction);
        return buttonObj;
    }

    /// <summary>
    /// Assigns customFont to a text element, swallowing any exception - mirrors the same
    /// defensive helper in Game2BatikMatch, since an untested font asset can silently fail
    /// to render (no exception, just zero glyphs) or, worse, throw during layout.
    /// </summary>
    private void SafeSetFont(TextMeshProUGUI text)
    {
        if (text == null || customFont == null) return;
        try
        {
            text.font = customFont;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Game1GuessName: Failed to apply customFont to '{text.name}', leaving default font. {e.Message}");
        }
    }

    // ------------------------------------------------------------------
    // Procedural sprites
    // ------------------------------------------------------------------
    private Sprite MakeRoundedSprite(int w, int h, int radius, int borderWidth)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        generatedTextures.Add(tex);

        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float hw = w * 0.5f;
        float hh = h * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x - cx) - (hw - radius);
                float dy = Mathf.Abs(y - cy) - (hh - radius);
                float outsideDist = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float dist = outsideDist + Mathf.Min(Mathf.Max(dx, dy), 0f);

                float outerAlpha = Mathf.Clamp01(radius - dist + 0.5f);
                float alpha = outerAlpha;
                if (borderWidth > 0)
                {
                    float innerAlpha = Mathf.Clamp01((radius - borderWidth) - dist + 0.5f);
                    alpha = Mathf.Clamp01(outerAlpha - innerAlpha);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        float b = radius + 2f;
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    /// <summary>
    /// A ring with a gap (like a "refresh"/rotate icon) used next to the drag-to-rotate hint.
    /// </summary>
    private Sprite MakePartialRingSprite(int size, int thickness, float gapDegrees)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        generatedTextures.Add(tex);

        float center = size * 0.5f;
        float outerRad = size * 0.5f;
        float innerRad = outerRad - thickness;
        float gapHalf = gapDegrees * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;

                bool inRing = dist >= innerRad && dist <= outerRad;
                bool inGap = angle > (90f - gapHalf) && angle < (90f + gapHalf);

                if (inRing && !inGap)
                {
                    float alpha = 1f;
                    if (dist > outerRad - 1f) alpha = (outerRad - dist);
                    else if (dist < innerRad + 1f) alpha = (dist - innerRad);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
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

    private void OnDestroy()
    {
        foreach (Texture2D tex in generatedTextures)
        {
            if (tex != null) Destroy(tex);
        }
        generatedTextures.Clear();

        if (silhouetteMaterial != null) Destroy(silhouetteMaterial);
    }
}
