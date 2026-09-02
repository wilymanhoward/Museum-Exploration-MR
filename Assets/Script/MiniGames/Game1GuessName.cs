using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Mini-Game 1: "Tebak Bayangan Artefak".
/// Inherits from BaseGame.
/// All UI references, buttons, panels, and silhouette material are assigned via the Inspector or auto-resolved.
/// </summary>
public class Game1GuessName : BaseGame
{
    [Header("Game Flow Settings")]
    public int totalRounds = 5;

    [Header("3D Model Spawner & Material (Assign in Inspector)")]
    [Tooltip("Pivot transform where the 3D model will be spawned.")]
    public Transform modelSpawnerPivot;

    [Tooltip("Black material assigned to renderers to create the silhouette effect.")]
    public Material silhouetteMaterial;

    [Tooltip("Sensitivity for drag-to-rotate interaction.")]
    public float rotationSensitivity = 0.4f;

    [Header("Header & Progress UI (Assign in Inspector)")]
    public TMP_Text progressText;
    public TMP_Text taskText;

    [Header("Guess Panel UI (Assign in Inspector)")]
    public GameObject guessPanel;

    [Tooltip("Container LayoutGroup inside GuessPanel where option buttons are generated.")]
    public Transform guessList;

    [Tooltip("Prefab for option choice buttons to instantiate inside guessList.")]
    public GameObject optionButtonPrefab;

    [Tooltip("'Periksa Jawaban' Check Button.")]
    public Button checkAnswerButton;

    [Header("Answer Panel UI (Assign in Inspector)")]
    public GameObject answerPanel;

    [Tooltip("Displays 'Benar' or 'Salah'.")]
    public TMP_Text resultText;

    [Tooltip("Green check icon shown when correct.")]
    public GameObject correctIcon;

    [Tooltip("Red cross icon shown when wrong.")]
    public GameObject incorrectIcon;

    [Tooltip("Displays the name of the correct artifact.")]
    public TMP_Text answerText;

    [Tooltip("'Lanjut >' Next Button.")]
    public Button lanjutButton;


    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private int currentRound = 0;
    private int score = 0;

    private List<ArtifactData> availableArtifacts = new List<ArtifactData>();
    private List<ArtifactData> usedArtifacts = new List<ArtifactData>();
    private ArtifactData currentTargetArtifact;

    private GameObject spawnedModel;
    private Dictionary<Renderer, Material[]> originalMaterialsMap = new Dictionary<Renderer, Material[]>();

    private string selectedAnswerName = "";
    private List<GameObject> spawnedDynamicButtons = new List<GameObject>();

    // ─────────────────────────────────────────────────────────────────────────
    // BaseGame Overrides & Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        EnsureModelSpawnerPivot();
        WireStaticButtons();
    }

    /// <summary>
    /// Overridden from BaseGame. Triggered automatically on OnEnable.
    /// Restarts the 5-round game session.
    /// </summary>
    public override void OnGameStart()
    {
        base.OnGameStart();

        currentRound = 0;
        score = 0;
        usedArtifacts.Clear();

        EnsureModelSpawnerPivot();
        LoadArtifactsWith3DModels();
        StartRound(currentRound);
    }

    public override void OnGameEnd()
    {
        base.OnGameEnd();
        ClearSpawnedModel();
        ClearSpawnedDynamicButtons();
    }

    private void ClearSpawnedDynamicButtons()
    {
        foreach (GameObject obj in spawnedDynamicButtons)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedDynamicButtons.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Round Management
    // ─────────────────────────────────────────────────────────────────────────

    private void StartRound(int roundIndex)
    {
        selectedAnswerName = "";

        // Reset panel states
        if (guessPanel != null) guessPanel.SetActive(true);
        if (answerPanel != null) answerPanel.SetActive(false);

        if (checkAnswerButton != null)
        {
            checkAnswerButton.interactable = false;
        }

        // Update progress text
        if (progressText != null)
        {
            progressText.text = $"{roundIndex + 1}/{totalRounds} Namakan Artifak berikut";
        }

        if (taskText != null)
        {
            taskText.text = "Seret untuk Putar Model";
        }

        // 1. Pick a random target artifact with a 3D model
        currentTargetArtifact = PickRandomArtifact();

        // 2. Spawn the 3D model with black silhouette material
        SpawnSilhouetteModel(currentTargetArtifact);

        // 3. Populate choice buttons randomly
        PopulateOptionButtons();
    }

    private void OnOptionSelected(string optionName, Button clickedButton)
    {
        selectedAnswerName = optionName;

        // Highlight selection state across spawned buttons
        foreach (GameObject btnObj in spawnedDynamicButtons)
        {
            if (btnObj == null) continue;
            Button btn = btnObj.GetComponent<Button>();
            Image img = btnObj.GetComponent<Image>();
            if (img != null && btn != null)
            {
                img.color = (btn == clickedButton)
                    ? new Color(0.784f, 0.765f, 0.353f, 1f) // Selected (Khaki)
                    : new Color(0.45f, 0.47f, 0.40f, 0.85f); // Default
            }
        }

        // Enable Check Answer Button
        if (checkAnswerButton != null)
        {
            checkAnswerButton.interactable = true;
        }
    }

    private void OnCheckAnswerPressed()
    {
        if (string.IsNullOrEmpty(selectedAnswerName)) return;

        bool isCorrect = (currentTargetArtifact != null && selectedAnswerName == currentTargetArtifact.artifactName);
        if (isCorrect) score++;

        // 1. Reveal true 3D model textures
        RevealModelTextures();

        // 2. Transition panels
        if (guessPanel != null) guessPanel.SetActive(false);
        if (answerPanel != null) answerPanel.SetActive(true);

        // 3. Show Result Info
        if (resultText != null)
        {
            resultText.text = isCorrect ? "Benar" : "Salah";
            resultText.color = isCorrect ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.9f, 0.35f, 0.35f);
        }

        if (correctIcon != null) correctIcon.SetActive(isCorrect);
        if (incorrectIcon != null) incorrectIcon.SetActive(!isCorrect);

        if (answerText != null)
        {
            answerText.text = currentTargetArtifact != null ? currentTargetArtifact.artifactName : "";
        }
    }

    private void OnLanjutPressed()
    {
        currentRound++;
        if (currentRound < totalRounds)
        {
            StartRound(currentRound);
        }
        else
        {
            // 5 rounds finished -> Show Game 1 Leaderboard with player completion time!
            Debug.Log($"Game 1 Complete! Final score: {score}/{totalRounds}");
            FinishGameAndShowLeaderboard("game_1", "Teka Bayang Artifak");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3D Model Resolution & Spawning Logic
    // ─────────────────────────────────────────────────────────────────────────

    public GameObject GetModelPrefab(ArtifactData data)
    {
        if (data == null) return null;
        if (data.modelPrefab != null) return data.modelPrefab;

        string id = data.artifactId != null ? data.artifactId.ToLower() : "";
        string name = data.artifactName != null ? data.artifactName.ToLower() : "";

        // 1. Direct ID / Name lookup in Resources/Models
        GameObject model = Resources.Load<GameObject>($"Models/{data.artifactId}") ??
                           Resources.Load<GameObject>($"Models/model_{data.artifactId}") ??
                           Resources.Load<GameObject>($"Models/model_artifact_{data.artifactId}");
        if (model != null) return model;

        // 2. Keyword fallback matching for Batu, Songket, Keris, Gamelan, Wayang
        if (id.Contains("batu") || name.Contains("batu"))
        {
            model = Resources.Load<GameObject>("Models/model_artifact_batu") ??
                    Resources.Load<GameObject>("Models/Batu") ??
                    Resources.Load<GameObject>("Prefabs/model_artifact_batu");
            if (model != null) return model;
        }
        if (id.Contains("songket") || name.Contains("songket"))
        {
            model = Resources.Load<GameObject>("Models/model_artifact_songket") ??
                    Resources.Load<GameObject>("Models/Songket") ??
                    Resources.Load<GameObject>("Prefabs/model_artifact_songket");
            if (model != null) return model;
        }
        if (id.Contains("keris") || name.Contains("keris"))
        {
            model = Resources.Load<GameObject>("Models/model_artifact_keris") ??
                    Resources.Load<GameObject>("Models/Keris") ??
                    Resources.Load<GameObject>("Prefabs/model_artifact_keris");
            if (model != null) return model;
        }
        if (id.Contains("gamelan") || name.Contains("gamelan"))
        {
            model = Resources.Load<GameObject>("Models/model_artifact_gamelan") ??
                    Resources.Load<GameObject>("Models/Gamelen") ??
                    Resources.Load<GameObject>("Prefabs/model_artifact_gamelan");
            if (model != null) return model;
        }
        if (id.Contains("wayang") || name.Contains("wayang"))
        {
            model = Resources.Load<GameObject>("Models/model_artifact_wayang") ??
                    Resources.Load<GameObject>("Models/Wayang") ??
                    Resources.Load<GameObject>("Prefabs/model_artifact_wayang");
            if (model != null) return model;
        }
        if (id.Contains("batik") || name.Contains("batik") || id.Contains("canting") || name.Contains("canting"))
        {
            model = Resources.Load<GameObject>("Models/model_artifact_batik") ??
                    Resources.Load<GameObject>("Prefabs/model_artifact_batik");
            if (model != null) return model;
        }

        return null;
    }

    private void EnsureModelSpawnerPivot()
    {
        if (modelSpawnerPivot != null) return;

        // Search for ObjectSpawner under this game panel
        Transform spawner = transform.Find("ObjectSpawner") 
                         ?? transform.Find("3DViewPanel/ObjectSpawner") 
                         ?? transform.Find("ModelSpawnerPivot")
                         ?? transform.Find("Spawner");

        if (spawner == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "ObjectSpawner" || t.name.Contains("Spawner") || t.name.Contains("Pivot"))
                {
                    spawner = t;
                    break;
                }
            }
        }

        if (spawner == null)
        {
            // Create ObjectSpawner dynamically in center of panel
            GameObject newSpawner = new GameObject("ObjectSpawner");
            newSpawner.transform.SetParent(transform, false);
            newSpawner.transform.localPosition = new Vector3(0f, 0f, 0f);
            spawner = newSpawner.transform;
        }

        modelSpawnerPivot = spawner;
    }

    private void LoadArtifactsWith3DModels()
    {
        availableArtifacts.Clear();

        // 1. Auto-detect all ArtifactData ScriptableObjects from Resources
        ArtifactData[] resArtifacts = Resources.LoadAll<ArtifactData>("");
        foreach (ArtifactData art in resArtifacts)
        {
            if (art != null && GetModelPrefab(art) != null && !availableArtifacts.Contains(art))
            {
                availableArtifacts.Add(art);
            }
        }

        // 2. Also check RoomManager for any additional room artifacts with 3D models
        if (RoomManager.Instance != null && RoomManager.Instance.rooms != null)
        {
            foreach (RoomData room in RoomManager.Instance.rooms)
            {
                if (room != null && room.artifacts != null)
                {
                    foreach (ArtifactData art in room.artifacts)
                    {
                        if (art != null && GetModelPrefab(art) != null && !availableArtifacts.Contains(art))
                        {
                            availableArtifacts.Add(art);
                        }
                    }
                }
            }
        }

        Debug.Log($"Game1GuessName: Loaded {availableArtifacts.Count} 3D artifacts from Resources & RoomManager.");
    }

    private ArtifactData PickRandomArtifact()
    {
        if (availableArtifacts.Count == 0) return null;

        List<ArtifactData> pool = new List<ArtifactData>();
        foreach (ArtifactData art in availableArtifacts)
        {
            if (!usedArtifacts.Contains(art)) pool.Add(art);
        }

        if (pool.Count == 0) pool = new List<ArtifactData>(availableArtifacts);

        ArtifactData chosen = pool[Random.Range(0, pool.Count)];
        usedArtifacts.Add(chosen);
        return chosen;
    }

    private void SpawnSilhouetteModel(ArtifactData data)
    {
        ClearSpawnedModel();
        originalMaterialsMap.Clear();

        if (data == null) return;

        EnsureModelSpawnerPivot();
        if (modelSpawnerPivot == null) return;

        GameObject prefabToSpawn = GetModelPrefab(data);
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"Game1GuessName: Could not resolve 3D model prefab for artifact '{data.artifactName}' ({data.artifactId}).");
            return;
        }

        RotateArtifact rotator = modelSpawnerPivot.GetComponent<RotateArtifact>();
        if (rotator == null)
        {
            rotator = modelSpawnerPivot.gameObject.AddComponent<RotateArtifact>();
        }

        spawnedModel = rotator.SpawnModel(prefabToSpawn, data.artifactId, 0.16f);
        if (spawnedModel == null) return;

        // Auto-create silhouetteMaterial if unassigned in Inspector
        if (silhouetteMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
            if (shader != null)
            {
                silhouetteMaterial = new Material(shader);
                silhouetteMaterial.color = Color.black;
            }
        }

        // Store original materials and apply black silhouette material
        Renderer[] renderers = spawnedModel.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;
            originalMaterialsMap[rend] = rend.sharedMaterials;

            Material[] silMats = new Material[rend.sharedMaterials.Length];
            for (int i = 0; i < silMats.Length; i++)
            {
                Material origMat = rend.sharedMaterials[i];
                if (origMat != null)
                {
                    // Create an instance copy of the original material so texture alpha maps & cutout shaders are preserved!
                    Material silMat = new Material(origMat);
                    silMat.name = origMat.name + "_Silhouette";

                    // Tint the base RGB color to pure black while retaining original alpha channel & cutout properties
                    if (silMat.HasProperty("_BaseColor"))
                    {
                        Color c = silMat.GetColor("_BaseColor");
                        silMat.SetColor("_BaseColor", new Color(0f, 0f, 0f, c.a));
                    }
                    if (silMat.HasProperty("_Color"))
                    {
                        Color c = silMat.GetColor("_Color");
                        silMat.SetColor("_Color", new Color(0f, 0f, 0f, c.a));
                    }
                    if (silMat.HasProperty("_SpecColor"))
                    {
                        silMat.SetColor("_SpecColor", Color.black);
                    }
                    if (silMat.HasProperty("_EmissionColor"))
                    {
                        silMat.SetColor("_EmissionColor", Color.black);
                    }

                    silMat.DisableKeyword("_EMISSION");
                    silMats[i] = silMat;
                }
                else if (silhouetteMaterial != null)
                {
                    silMats[i] = silhouetteMaterial;
                }
            }
            rend.materials = silMats;
        }
    }

    private void RevealModelTextures()
    {
        if (spawnedModel == null) return;

        foreach (var pair in originalMaterialsMap)
        {
            if (pair.Key != null && pair.Value != null)
            {
                pair.Key.materials = pair.Value;
            }
        }
    }

    private void ClearSpawnedModel()
    {
        if (modelSpawnerPivot != null)
        {
            RotateArtifact rotator = modelSpawnerPivot.GetComponent<RotateArtifact>();
            if (rotator != null)
            {
                rotator.ClearModel();
            }
        }
        if (spawnedModel != null)
        {
            Destroy(spawnedModel);
            spawnedModel = null;
        }
        originalMaterialsMap.Clear();
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Option Buttons Wiring
    // ─────────────────────────────────────────────────────────────────────────

    private void PopulateOptionButtons()
    {
        // 1. Resolve guessList container if not explicitly set
        if (guessList == null && guessPanel != null)
        {
            Transform found = guessPanel.transform.Find("GuessList") 
                           ?? guessPanel.transform.Find("OptionContainer")
                           ?? guessPanel.transform.Find("Content");
            if (found != null) guessList = found;
        }

        // Clear previously spawned dynamic buttons under guessList
        foreach (GameObject obj in spawnedDynamicButtons)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedDynamicButtons.Clear();

        // Target number of choices (5 choices)
        int numChoices = 5;

        // Build list of choice names (1 correct answer + random wrong answers)
        List<string> options = new List<string>();
        string correctName = currentTargetArtifact != null ? currentTargetArtifact.artifactName : "";
        if (!string.IsNullOrEmpty(correctName)) options.Add(correctName);

        List<string> wrongPool = new List<string>();
        foreach (ArtifactData art in availableArtifacts)
        {
            if (art != null && !string.IsNullOrEmpty(art.artifactName) && art.artifactName != correctName && !wrongPool.Contains(art.artifactName))
            {
                wrongPool.Add(art.artifactName);
            }
        }

        // Search Resources for any other ArtifactData with 3D models if needed
        if (wrongPool.Count < numChoices - 1)
        {
            ArtifactData[] resArtifacts = Resources.LoadAll<ArtifactData>("");
            foreach (ArtifactData art in resArtifacts)
            {
                if (art != null && GetModelPrefab(art) != null && !string.IsNullOrEmpty(art.artifactName) && art.artifactName != correctName && !wrongPool.Contains(art.artifactName))
                {
                    wrongPool.Add(art.artifactName);
                }
            }
        }

        ShuffleList(wrongPool);
        for (int i = 0; i < numChoices - 1 && i < wrongPool.Count; i++)
        {
            options.Add(wrongPool[i]);
        }

        // Randomize option locations
        ShuffleList(options);

        // Instantiate dynamic buttons into guessList IF optionButtonPrefab & guessList are available
        if (guessList != null && optionButtonPrefab != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                string optName = options[i];
                GameObject btnObj = Instantiate(optionButtonPrefab, guessList, false);
                btnObj.name = $"Option_{i + 1}_{optName}";
                btnObj.SetActive(true);

                TMP_Text label = btnObj.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = optName;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 8f;
                    label.fontSizeMax = 18f;
                    label.enableWordWrapping = true;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.alignment = TextAlignmentOptions.Center;
                }

                Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();

                Button capturedBtn = btn;
                string capturedName = optName;

                WireVRButton(btnObj, () => OnOptionSelected(capturedName, capturedBtn));

                spawnedDynamicButtons.Add(btnObj);
            }
        }
    }

    private void WireStaticButtons()
    {
        if (checkAnswerButton != null)
        {
            WireVRButton(checkAnswerButton.gameObject, OnCheckAnswerPressed);
        }

        if (lanjutButton != null)
        {
            WireVRButton(lanjutButton.gameObject, OnLanjutPressed);
        }
    }

    private static void WireVRButton(GameObject obj, UnityEngine.Events.UnityAction action)
    {
        if (obj == null) return;

        Button btn = obj.GetComponent<Button>() ?? obj.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);

        XRButtonSelection xr = obj.GetComponent<XRButtonSelection>();
        if (xr != null)
        {
            xr.onClick.RemoveAllListeners();
            xr.onClick.AddListener(action);
        }

        // Ensure BoxCollider exists for VR Raycast and Direct Poke interaction in 3D world space
        BoxCollider col = obj.GetComponent<BoxCollider>();
        if (col == null)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt != null)
            {
                col = obj.AddComponent<BoxCollider>();
                float w = rt.rect.width > 0 ? rt.rect.width : 120f;
                float h = rt.rect.height > 0 ? rt.rect.height : 40f;
                col.size = new Vector3(w, h, 10f);
                col.isTrigger = true;
            }
        }
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randIndex = Random.Range(i, list.Count);
            list[i] = list[randIndex];
            list[randIndex] = temp;
        }
    }
}
