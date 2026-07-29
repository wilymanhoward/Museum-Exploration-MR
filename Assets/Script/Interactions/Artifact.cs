using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Artifact : MonoBehaviour
{
    [Header("UI Text Fields")]
    public TextMeshProUGUI topTitleText;
    public TextMeshProUGUI bottomTitleText;
    public TextMeshProUGUI descriptionText;

    [Header("UI Details Fields")]
    public TextMeshProUGUI timePeriodText;
    public TextMeshProUGUI locationText;
    public TextMeshProUGUI dimensionText;
    public TextMeshProUGUI materialText;

    [Header("UI Image Gallery Fields")]
    public UnityEngine.UI.Image displayImage;
    public TextMeshProUGUI noImagesText;

    [Header("Object Spawner Reference")]
    [Tooltip("The Empty Object where the 3D model prefab will be instantiated.")]
    public Transform objectSpawner;

    [Header("Spatial Positioning")]
    [Tooltip("Offset of the panel relative to the QR code's local space. (X = right/left, Y = up/down, Z = forward/back out of wall)")]
    public Vector3 panelOffset = new Vector3(0.45f, 0.0f, 0.05f);

    [Tooltip("If true, rotates the panel 180 degrees relative to the player direction.")]
    public bool invertRotation = false;

    [Header("Exploration Canvas Flow References")]
    [Tooltip("The canvas GameObject to hide. If null, will automatically find the parent Canvas's GameObject.")]
    public GameObject canvasObject;
    public GameObject twoDViewPanel;
    public GameObject threeDViewPanel;

    [Header("View Toggle Buttons")]
    public Button imagesButton;
    public XRButtonSelection imagesButtonXR;
    public Button threeDViewButton;
    public XRButtonSelection threeDViewButtonXR;
    public GameObject noModelTextObj;

    [Header("Close Buttons (Hides entire Canvas)")]
    public Button closeButton;
    public XRButtonSelection closeButtonXR;

    [Header("Back Buttons (Goes back to Room Panel)")]
    public Button backButton;
    public XRButtonSelection backButtonXR;

    [Header("Audio Narration UI")]
    public Button playButton;
    public XRButtonSelection playButtonXR;
    public GameObject playIconObj;
    public GameObject pauseIconObj;
    public Button restartButton;
    public XRButtonSelection restartButtonXR;

    [Header("Instrument Audio UI")]
    public Button playInstrumentButton;
    public XRButtonSelection playInstrumentButtonXR;

    [HideInInspector] public ArtifactData artifactData;
    private GameObject spawnedModel;
    private Action onCloseCallback;
    private int currentImageIndex = 0;
    private Transform trackedPlayer;
    private Room previousRoomPanel;
    private AudioSource audioSource;

    private void Awake()
    {
        // Ensure detail panel is hidden at startup until opened by scan or menu
        gameObject.SetActive(false);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void Start()
    {
        // Hook view toggle buttons
        if (imagesButton != null)
        {
            imagesButton.onClick.AddListener(() => SetViewMode(true));
        }
        if (imagesButtonXR != null)
        {
            imagesButtonXR.onClick.AddListener(() => SetViewMode(true));
        }

        if (threeDViewButton != null)
        {
            threeDViewButton.onClick.AddListener(() => SetViewMode(false));
            threeDViewButton.onClick.AddListener(On3DViewButtonClicked);
        }
        if (threeDViewButtonXR != null)
        {
            threeDViewButtonXR.onClick.AddListener(() => SetViewMode(false));
            threeDViewButtonXR.onClick.AddListener(On3DViewButtonClicked);
        }

        // Hook close button click to hide the canvas
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseCanvas);
        }
        if (closeButtonXR != null)
        {
            closeButtonXR.onClick.AddListener(CloseCanvas);
        }

        // Hook back button click to return to the room panel
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackPressed);
        }
        if (backButtonXR != null)
        {
            backButtonXR.onClick.AddListener(OnBackPressed);
        }

        // Hook audio buttons
        if (playButton != null) playButton.onClick.AddListener(OnPlayPauseClicked);
        if (playButtonXR != null) playButtonXR.onClick.AddListener(OnPlayPauseClicked);

        if (restartButton != null) restartButton.onClick.AddListener(RestartNarration);
        if (restartButtonXR != null) restartButtonXR.onClick.AddListener(RestartNarration);

        if (playInstrumentButton != null) playInstrumentButton.onClick.AddListener(PlayInstrumentAudio);
        if (playInstrumentButtonXR != null) playInstrumentButtonXR.onClick.AddListener(PlayInstrumentAudio);
    }

    private void OnEnable()
    {
        PositionInFrontOfUser();
    }

    public void PositionInFrontOfUser()
    {
        if (trackedPlayer == null && Camera.main != null)
        {
            trackedPlayer = Camera.main.transform;
        }

        if (trackedPlayer != null)
        {
            Vector3 forwardDir = Vector3.ProjectOnPlane(trackedPlayer.forward, Vector3.up).normalized;
            if (forwardDir == Vector3.zero) forwardDir = Vector3.forward;

            // Spawn 1.5 metres in front of the user's camera (matches ExplorationCanvas distance)
            transform.position = trackedPlayer.position + forwardDir * 1.5f;

            Vector3 directionToPlayer = trackedPlayer.position - transform.position;
            directionToPlayer.y = 0;
            if (directionToPlayer != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(-directionToPlayer, Vector3.up);
            }
        }
    }



    /// <summary>
    /// Configures the panel with data, references, and callback events.
    /// </summary>
    public void Setup(ArtifactData data, Transform playerTransform, Pose qrPose, Action onClose)
    {
        artifactData = data;
        onCloseCallback = onClose;
        if (playerTransform != null) trackedPlayer = playerTransform;

        PositionInFrontOfUser();

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }

        // Populate Text Fields
        if (topTitleText != null)
        {
            topTitleText.text = data.artifactName;
            topTitleText.enableAutoSizing = true;
            topTitleText.fontSizeMin = 12f;
            topTitleText.fontSizeMax = 24f;
            topTitleText.overflowMode = TextOverflowModes.Ellipsis;
        }
        if (bottomTitleText != null)
        {
            bottomTitleText.text = $"Artefak:\n\"{data.artifactName}\"";
            bottomTitleText.enableAutoSizing = true;
            bottomTitleText.fontSizeMin = 12f;
            bottomTitleText.fontSizeMax = 20f;
            bottomTitleText.overflowMode = TextOverflowModes.Ellipsis;
        }
        if (descriptionText != null) descriptionText.text = data.description;

        // Populate Details
        if (timePeriodText != null) timePeriodText.text = data.timePeriod;
        if (locationText != null) locationText.text = data.location;
        if (dimensionText != null) dimensionText.text = $"{data.height}cm x {data.width}cm x {data.length}cm";
        if (materialText != null) materialText.text = data.material;

        // Reset image gallery index & show photo
        currentImageIndex = 0;
        UpdateImageUI();

        // Clean up previous models inside the ObjectSpawner (3D model only appears when 3D View button is clicked)
        ClearSpawnedModel();

        // Configure 3D View button visibility
        Refresh3DViewButtonState();

        // Auto-play narration audio on reveal
        if (audioSource != null)
        {
            audioSource.Stop();
            if (data != null && data.narrationClip != null)
            {
                audioSource.clip = data.narrationClip;
                audioSource.Play();
                Debug.Log($"ArtifactPanel: Auto-playing narration for {data.artifactName}");
            }
        }

        // Show instrument button if this is an instrument artifact
        if (playInstrumentButton != null)
        {
            playInstrumentButton.gameObject.SetActive(data != null && data.instrumentClip != null);
        }

        Debug.Log($"Setup detail panel next to QR code for: {data.artifactName} at position: {transform.position}");
    }

    /// <summary>
    /// Ensures 3D View button is active and wired up so clicking/pinching it displays the 3D model.
    /// </summary>
    private void Refresh3DViewButtonState()
    {
        if (threeDViewButton == null)
        {
            Transform btnT = transform.Find("3DViewButton");
            if (btnT == null)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "3DViewButton" || t.name == "3DView" || t.name == "View3D")
                    {
                        btnT = t;
                        break;
                    }
                }
            }
            if (btnT != null)
            {
                threeDViewButton = btnT.GetComponent<Button>();
                if (threeDViewButton == null) threeDViewButton = btnT.gameObject.AddComponent<Button>();
                
                threeDViewButtonXR = btnT.GetComponent<XRButtonSelection>();

                // Hook listeners if resolved dynamically
                threeDViewButton.onClick.AddListener(() => SetViewMode(false));
                threeDViewButton.onClick.AddListener(On3DViewButtonClicked);
                if (threeDViewButtonXR != null)
                {
                    threeDViewButtonXR.onClick.AddListener(() => SetViewMode(false));
                    threeDViewButtonXR.onClick.AddListener(On3DViewButtonClicked);
                }
            }
        }

        if (noModelTextObj == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == "NoModelText" || n == "NoModel" || n == "No3DModelText" || n.Contains("No3D") || n.Contains("NoModel"))
                {
                    noModelTextObj = t.gameObject;
                    break;
                }
            }
        }

        bool hasModel = artifactData != null && artifactData.modelPrefab != null;

        if (threeDViewButton != null)
        {
            // Only show the 3D View button if this artifact actually has a 3D model prefab!
            threeDViewButton.gameObject.SetActive(hasModel);
        }

        if (noModelTextObj != null)
        {
            // Only show the "no 3D model available" text if there is no 3D model prefab!
            noModelTextObj.SetActive(!hasModel);
        }
    }

    /// <summary>
    /// Invoked when player clicks/taps/pinches the 3D View button.
    /// Freshly spawns or re-centers the 3D model right in front of the panel.
    /// </summary>
    public void On3DViewButtonClicked()
    {
        Debug.Log("[ArtifactPanel] 3D View Button Clicked / Pinched!");
        ClearSpawnedModel();
        OnSpawnModelClicked();
    }

    /// <summary>
    /// Keeps the panel at the player's eye level and billboarded to face them while it's open.
    /// </summary>
    private void LateUpdate()
    {
        if (trackedPlayer == null) return;

        Vector3 pos = transform.position;
        pos.y = trackedPlayer.position.y;
        transform.position = pos;

        Vector3 directionToPlayer = trackedPlayer.position - transform.position;
        directionToPlayer.y = 0; // yaw only, keep the panel upright
        if (directionToPlayer != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(-directionToPlayer, Vector3.up);
        }
    }

    /// <summary>
    /// Instantiates the 3D model inside the ObjectSpawner and hides the photo image.
    /// </summary>
    public void OnSpawnModelClicked()
    {
        if (artifactData == null) return;

        // Hide photo image while 3D view is active
        if (displayImage != null)
        {
            displayImage.gameObject.SetActive(false);
        }
        if (noImagesText != null) noImagesText.gameObject.SetActive(false);

        // Clean up previous model if present
        ClearSpawnedModelSilently();

        // Ensure objectSpawner exists
        if (objectSpawner == null)
        {
            Transform spawnerT = transform.Find("ObjectSpawner");
            if (spawnerT != null) objectSpawner = spawnerT;
            else
            {
                GameObject newSpawner = new GameObject("ObjectSpawner");
                newSpawner.transform.SetParent(transform, false);
                objectSpawner = newSpawner.transform;
            }
        }

        RotateArtifact rotator = objectSpawner.GetComponent<RotateArtifact>();
        GameObject prefabToSpawn = artifactData.modelPrefab;

        // Fallback: Load model from Resources if modelPrefab unassigned
        if (prefabToSpawn == null)
        {
            prefabToSpawn = Resources.Load<GameObject>($"Models/{artifactData.artifactId}") ??
                            Resources.Load<GameObject>($"Models/model_{artifactData.artifactId}") ??
                            Resources.Load<GameObject>($"Prefabs/model_{artifactData.artifactId}");
        }

        if (prefabToSpawn != null)
        {
            if (rotator != null)
            {
                spawnedModel = rotator.SpawnModel(prefabToSpawn, artifactData.artifactId);
            }
            else
            {
                spawnedModel = Instantiate(prefabToSpawn, objectSpawner.position, objectSpawner.rotation, objectSpawner);
                spawnedModel.transform.localPosition = new Vector3(0, 0, -0.05f);
            }
        }
        else
        {
            // Create a clean 3D display object as fallback so a 3D model ALWAYS appears on click
            GameObject fallbackObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallbackObj.name = $"3DDisplay_{artifactData.artifactId}";
            fallbackObj.transform.SetParent(objectSpawner, false);
            fallbackObj.transform.localPosition = new Vector3(0, 0, -0.05f);
            fallbackObj.transform.localScale = Vector3.one * 0.25f;

            spawnedModel = fallbackObj;
        }

        if (spawnedModel != null)
        {
            spawnedModel.SetActive(true);
            ApplyTextureToModel(spawnedModel, artifactData);
            FitModelToWorldSize(spawnedModel, 0.35f);
        }

        Debug.Log($"3D Model successfully displayed with texture for {artifactData.artifactName}.");
    }

    /// <summary>
    /// Applies the artifact's photo texture onto the 3D model renderers.
    /// </summary>
    private void ApplyTextureToModel(GameObject model, ArtifactData data)
    {
        if (model == null || data == null) return;

        Texture2D textureToApply = null;
        if (data.images != null && data.images.Length > 0 && data.images[0].sprite != null)
        {
            textureToApply = data.images[0].sprite.texture;
        }

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;

            Material mat = r.material;
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                r.material = mat;
            }

            if (textureToApply != null)
            {
                mat.mainTexture = textureToApply;
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", textureToApply);
                }
                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", textureToApply);
                }
                mat.color = Color.white;
            }
        }
    }

    /// <summary>
    /// Scales the spawned model so its largest world-space bounding-box dimension is
    /// targetSizeMeters. Works regardless of the parent's scale (e.g. a 0.001 world-space
    /// canvas), because it measures true world bounds and multiplies the local scale.
    /// </summary>
    private void FitModelToWorldSize(GameObject model, float targetSizeMeters)
    {
        if (model == null) return;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[ArtifactPanel] Spawned model has no renderers to size/display.");
            return;
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            b.Encapsulate(renderers[i].bounds);
        }

        float maxDimension = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maxDimension > 0.0001f)
        {
            float scaleFactor = targetSizeMeters / maxDimension;
            model.transform.localScale *= scaleFactor;
        }
    }

    /// <summary>
    /// Cleans up and invokes the close callback.
    /// </summary>
    public void StartClose()
    {
        ClearSpawnedModel();
        onCloseCallback?.Invoke();
    }

    private void ClearSpawnedModelSilently()
    {
        if (spawnedModel != null)
        {
            Destroy(spawnedModel);
            spawnedModel = null;
        }

        if (objectSpawner != null)
        {
            RotateArtifact rotator = objectSpawner.GetComponent<RotateArtifact>();
            if (rotator != null)
            {
                rotator.ClearModel();
            }
            else
            {
                foreach (Transform child in objectSpawner)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }

    public void ClearSpawnedModel()
    {
        ClearSpawnedModelSilently();

        // Restore photo image when 3D model is cleared
        if (displayImage != null)
        {
            displayImage.gameObject.SetActive(true);
        }
        if (noImagesText != null) noImagesText.gameObject.SetActive(true);
    }

    #region Image Gallery Functions
    public void ShowNextImage()
    {
        if (artifactData == null || artifactData.images == null || artifactData.images.Length == 0) return;

        currentImageIndex++;
        if (currentImageIndex >= artifactData.images.Length)
        {
            currentImageIndex = 0;
        }
        UpdateImageUI();
    }

    public void ShowPreviousImage()
    {
        if (artifactData == null || artifactData.images == null || artifactData.images.Length == 0) return;

        currentImageIndex--;
        if (currentImageIndex < 0)
        {
            currentImageIndex = artifactData.images.Length - 1;
        }
        UpdateImageUI();
    }

    private void UpdateImageUI()
    {
        if (artifactData == null) return;

        if (artifactData.images != null && artifactData.images.Length > 0)
        {
            if (displayImage != null)
            {
                displayImage.gameObject.SetActive(true);
                displayImage.sprite = artifactData.images[currentImageIndex].sprite;
            }
            if (noImagesText != null)
            {
                noImagesText.gameObject.SetActive(false);
            }
        }
        else
        {
            if (displayImage != null)
            {
                displayImage.gameObject.SetActive(false);
            }
            if (noImagesText != null)
            {
                noImagesText.gameObject.SetActive(true);
                noImagesText.text = "Artefak tidak ada Gambar";
            }
        }
    }
    #endregion

    /// <summary>
    /// Updates the panel fields with new artifact details without modifying its position or rotation.
    /// </summary>
    public void UpdateDetails(ArtifactData data)
    {
        artifactData = data;

        // Populate Text Fields
        if (topTitleText != null)
        {
            topTitleText.text = data.artifactName;
            topTitleText.enableAutoSizing = true;
            topTitleText.fontSizeMin = 12f;
            topTitleText.fontSizeMax = 24f;
            topTitleText.overflowMode = TextOverflowModes.Ellipsis;
        }
        if (bottomTitleText != null)
        {
            bottomTitleText.text = $"Artefak:\n\"{data.artifactName}\"";
            bottomTitleText.enableAutoSizing = true;
            bottomTitleText.fontSizeMin = 12f;
            bottomTitleText.fontSizeMax = 20f;
            bottomTitleText.overflowMode = TextOverflowModes.Ellipsis;
        }
        if (descriptionText != null) descriptionText.text = data.description;

        // Populate Details
        if (timePeriodText != null) timePeriodText.text = data.timePeriod;
        if (locationText != null) locationText.text = data.location;
        if (dimensionText != null) dimensionText.text = $"{data.height}cm x {data.width}cm x {data.length}cm";
        if (materialText != null) materialText.text = data.material;

        // Reset image gallery index
        currentImageIndex = 0;
        UpdateImageUI();

        // Clean up previous models inside the ObjectSpawner
        ClearSpawnedModel();

        // Configure 3D View button visibility (hide if no 3D model)
        Refresh3DViewButtonState();

        // Automatically spawn the 3D model if present
        if (data != null && data.modelPrefab != null)
        {
            OnSpawnModelClicked();
        }

        // Auto-play narration audio on update details
        if (audioSource != null)
        {
            audioSource.Stop();
            if (data != null && data.narrationClip != null)
            {
                audioSource.clip = data.narrationClip;
                audioSource.Play();
                Debug.Log($"ArtifactPanel: Auto-playing narration for {data.artifactName}");
            }
        }

        if (playInstrumentButton != null)
        {
            playInstrumentButton.gameObject.SetActive(data != null && data.instrumentClip != null);
        }

        Debug.Log($"Updated detail panel with new artifact data: {data.artifactName}");
    }

    /// <summary>
    /// Displays the detailed information of the specified artifact for the Exploration Canvas flow.
    /// </summary>
    public void ShowArtifact(ArtifactData data, Room previousPanel)
    {
        previousRoomPanel = previousPanel;
        gameObject.SetActive(true);

        if (previousRoomPanel != null)
        {
            previousRoomPanel.gameObject.SetActive(false);
        }

        // Re-snap position in front of user (matches ExplorationCanvas placement)
        PositionInFrontOfUser();

        UpdateDetails(data);

        // Default to 2D view
        SetViewMode(true);
    }

    private void SetViewMode(bool show2D)
    {
        if (twoDViewPanel != null)
        {
            twoDViewPanel.SetActive(show2D);
        }
        if (threeDViewPanel != null)
        {
            threeDViewPanel.SetActive(!show2D);
        }

        if (twoDViewPanel != null || threeDViewPanel != null)
        {
            if (displayImage != null)
            {
                displayImage.gameObject.SetActive(show2D);
            }
            if (noImagesText != null) noImagesText.gameObject.SetActive(show2D && (artifactData == null || artifactData.images == null || artifactData.images.Length == 0));
        }
    }

    private void OnBackPressed()
    {
        ClearSpawnedModel();

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (previousRoomPanel != null)
        {
            previousRoomPanel.gameObject.SetActive(true);
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides the canvas/parent canvas. This function can be called from UnityEvents.
    /// Since the panel state is not altered, revealing the canvas again will show the last active panel.
    /// </summary>
    public void CloseCanvas()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }
        else
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                parentCanvas.gameObject.SetActive(false);
            }
            else
            {
                if (transform.parent != null)
                {
                    transform.parent.gameObject.SetActive(false);
                }
            }
        }

        if (WristWatch.Instance != null)
        {
            WristWatch.Instance.EnsureWatchButtonVisible();
        }
        else
        {
            WristWatch ww = FindObjectOfType<WristWatch>();
            if (ww != null) ww.EnsureWatchButtonVisible();
        }
    }

    public void PlayNarration()
    {
        if (audioSource != null)
        {
            if (artifactData != null && audioSource.clip != artifactData.narrationClip)
            {
                audioSource.clip = artifactData.narrationClip;
            }
            if (audioSource.clip != null)
            {
                audioSource.Play();
                Debug.Log("ArtifactPanel: Narration playing.");
            }
        }
    }

    public void PauseNarration()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
            Debug.Log("ArtifactPanel: Narration paused.");
        }
    }

    public void RestartNarration()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Stop();
            audioSource.Play();
            Debug.Log("ArtifactPanel: Narration restarted.");
        }
    }

    private void PlayInstrumentAudio()
    {
        if (artifactData != null && artifactData.instrumentClip != null && audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = artifactData.instrumentClip;
            audioSource.Play();
            Debug.Log("ArtifactPanel: Playing instrument audio.");
        }
    }

    private void Update()
    {
        UpdateAudioUI();
    }

    private void UpdateAudioUI()
    {
        bool isPlaying = audioSource != null && audioSource.isPlaying;
        if (playIconObj != null)
        {
            playIconObj.SetActive(!isPlaying);
        }
        if (pauseIconObj != null)
        {
            pauseIconObj.SetActive(isPlaying);
        }
    }

    public void OnPlayPauseClicked()
    {
        if (audioSource == null) return;

        if (audioSource.isPlaying)
        {
            PauseNarration();
        }
        else
        {
            PlayNarration();
        }
    }
}
