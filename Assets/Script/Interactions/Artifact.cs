using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

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

    // Only ONE artifact narration may play at a time. Tracks whichever panel is currently
    // narrating so a newly-opened/played panel can silence the previous one.
    private static Artifact s_activeNarration;

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
        // Make sure narration is actually audible: 2D (position-independent), full volume, not muted.
        audioSource.mute = false;
        audioSource.volume = 1f;
        audioSource.spatialBlend = 0f;

        EnsureGrabbablePanel();
    }

    /// <summary>
    /// Configures the panel with ArtifactPanelDragger so player can pinch & hold (e.g. 1.5s - 3s) to move it around in 3D space.
    /// Instant UI button taps pass through normally without obstruction.
    /// </summary>
    public void EnsureGrabbablePanel()
    {
        // Remove standard XRGrabInteractable if present so UI button clicks are never blocked
        XRGrabInteractable oldGrab = GetComponent<XRGrabInteractable>();
        if (oldGrab != null && !(oldGrab is ArtifactPanelDragger))
        {
            Destroy(oldGrab);
        }

        ArtifactPanelDragger dragger = GetComponent<ArtifactPanelDragger>();
        if (dragger == null) dragger = gameObject.AddComponent<ArtifactPanelDragger>();
        if (twoDViewPanel != null && threeDViewPanel != null)
        {
            dragger.enabled = twoDViewPanel.activeSelf;
        }
    }

    private void Start()
    {
        EnsureGrabbablePanel();

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

        // Auto-resolve top header play button if unassigned
        if (playButton == null && playButtonXR == null)
        {
            Transform pBtn = transform.Find("AudioPlayButton") ?? transform.Find("HeaderPlayButton") ?? transform.Find("PlayButton");
            if (pBtn == null && transform.parent != null)
            {
                pBtn = transform.parent.Find("AudioPlayButton") ?? transform.parent.Find("HeaderPlayButton");
            }
            if (pBtn != null)
            {
                playButton = pBtn.GetComponent<Button>();
                playButtonXR = pBtn.GetComponent<XRButtonSelection>();
                if (playIconObj == null)
                {
                    Transform pIcon = pBtn.Find("PlayIcon");
                    if (pIcon != null) playIconObj = pIcon.gameObject;
                }
                if (pauseIconObj == null)
                {
                    Transform psIcon = pBtn.Find("PauseIcon");
                    if (psIcon != null) pauseIconObj = psIcon.gameObject;
                }
            }
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
        ArtifactPanelDragger dragger = GetComponent<ArtifactPanelDragger>();
        if (dragger != null)
        {
            dragger.ResetUserMoved();
        }

        if (trackedPlayer == null && Camera.main != null)
        {
            trackedPlayer = Camera.main.transform;
        }

        if (trackedPlayer != null)
        {
            Vector3 forwardDir = Vector3.ProjectOnPlane(trackedPlayer.forward, Vector3.up).normalized;
            if (forwardDir == Vector3.zero) forwardDir = Vector3.forward;

            // Spawn exactly 1.0 meter in front of the user's camera
            transform.position = trackedPlayer.position + forwardDir * 1.0f;

            Vector3 directionToPlayer = trackedPlayer.position - transform.position;
            directionToPlayer.y = 0;
            if (directionToPlayer != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(-directionToPlayer, Vector3.up);
                transform.rotation = Quaternion.Euler(0f, lookRot.eulerAngles.y, 0f);
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
        EnsureGrabbablePanel();

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

        // Show the photo (2D) view by default so the picture is visible. The 2D panel that holds
        // the image (DisplayImage) starts inactive, and Setup - unlike ShowArtifact - never switched
        // to it, so the picture never rendered on the QR-spawned panel.
        SetViewMode(true);

        // Clean up previous models inside the ObjectSpawner (3D model only appears when 3D View button is clicked)
        ClearSpawnedModel();

        // Configure 3D View button visibility
        Refresh3DViewButtonState();

        // Auto-play the narration when the artifact is shown.
        PlayNarrationOnShow();

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

        GameObject modelObj = GetModelPrefab(artifactData);
        bool hasModel = modelObj != null;

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
            model = Resources.Load<GameObject>("Models/model_artifact_batu");
            if (model != null) return model;
        }
        if (id.Contains("songket") || name.Contains("songket"))
        {
            model = Resources.Load<GameObject>("Models/model_artifact_songket");
            if (model != null) return model;
        }
        if (id.Contains("keris") || name.Contains("keris"))
        {
            model = Resources.Load<GameObject>("Models/model_artifact_keris");
            if (model != null) return model;
        }
        if (id.Contains("gamelan") || name.Contains("gamelan"))
        {
            model = Resources.Load<GameObject>("Models/model_artifact_gamelan");
            if (model != null) return model;
        }
        if (id.Contains("wayang") || name.Contains("wayang"))
        {
            model = Resources.Load<GameObject>("Models/model_artifact_wayang");
            if (model != null) return model;
        }

        return null;
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

        // Find objectSpawner in panel layout (check threeDViewPanel first)
        if (objectSpawner == null)
        {
            if (threeDViewPanel != null)
            {
                foreach (Transform t in threeDViewPanel.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "ObjectSpawner" || t.name.Contains("Spawner"))
                    {
                        objectSpawner = t;
                        break;
                    }
                }
            }

            if (objectSpawner == null)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "ObjectSpawner" || t.name.Contains("Spawner"))
                    {
                        objectSpawner = t;
                        break;
                    }
                }
            }

            if (objectSpawner == null)
            {
                GameObject newSpawner = new GameObject("ObjectSpawner");
                Transform parentT = displayImage != null ? displayImage.transform.parent : (threeDViewPanel != null ? threeDViewPanel.transform : transform);
                newSpawner.transform.SetParent(parentT, false);
                if (displayImage != null)
                {
                    newSpawner.transform.localPosition = displayImage.transform.localPosition;
                }
                objectSpawner = newSpawner.transform;
            }
        }
        else if (displayImage != null && (objectSpawner.localPosition == Vector3.zero || objectSpawner.parent == transform))
        {
            // Align spawner to match displayImage frame area if unpositioned
            objectSpawner.transform.localPosition = displayImage.transform.localPosition;
        }

        Rigidbody spawnerRb = objectSpawner.GetComponent<Rigidbody>();
        if (spawnerRb != null)
        {
            if (!spawnerRb.isKinematic)
            {
                spawnerRb.velocity = Vector3.zero;
                spawnerRb.angularVelocity = Vector3.zero;
            }
            spawnerRb.useGravity = false;
            spawnerRb.isKinematic = true;
        }

        RotateArtifact rotator = objectSpawner.GetComponent<RotateArtifact>();
        if (rotator == null)
        {
            rotator = objectSpawner.gameObject.AddComponent<RotateArtifact>();
            Debug.Log($"[ArtifactPanel] Added missing RotateArtifact component to objectSpawner for '{artifactData.artifactName}'.");
        }

        GameObject prefabToSpawn = GetModelPrefab(artifactData);

        if (prefabToSpawn != null)
        {
            // Spawn 3D model prefab - preserves its own original materials and textures from Assets/Artifact/
            spawnedModel = rotator.SpawnModel(prefabToSpawn, artifactData.artifactId);
        }
        else
        {
            // Create a clean 3D display object as fallback so a 3D model ALWAYS appears on click
            GameObject fallbackObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallbackObj.name = $"3DDisplay_{artifactData.artifactId}";
            fallbackObj.transform.SetParent(objectSpawner, false);
            fallbackObj.transform.localPosition = new Vector3(0, 0, -0.15f);
            fallbackObj.transform.localScale = Vector3.one * 0.25f;

            // Only the placeholder fallback cube gets the photo texture mapped onto it
            ApplyTextureToModel(fallbackObj, artifactData);

            spawnedModel = fallbackObj;
        }

        if (spawnedModel != null)
        {
            spawnedModel.SetActive(true);
            StopAutoRotation(spawnedModel);
        }

        Debug.Log($"3D Model successfully displayed with texture for {artifactData.artifactName}.");
    }

    /// <summary>
    /// Makes the spawned 3D model grabbable so the player can pinch it with one or both hands to
    /// rotate it (two hands also scale). Configures the ObjectSpawner's XRGrabInteractable for
    /// multi-hand select, adds the ArtifactRotationDriver, and sizes the grab collider to the model.
    /// </summary>
    private void SetupTwoHandRotation(GameObject model)
    {
        if (objectSpawner == null || model == null) return;

        // Grab interactable that allows BOTH hands to select at once. The rotation driver moves the
        // object manually, so the grab itself must not also track hand position/rotation.
        XRGrabInteractable grab = objectSpawner.GetComponent<XRGrabInteractable>();
        if (grab == null) grab = objectSpawner.gameObject.AddComponent<XRGrabInteractable>();
        grab.selectMode = InteractableSelectMode.Multiple;
        grab.trackPosition = false;
        grab.trackRotation = false;
        grab.throwOnDetach = false;

        // Driver that turns one/two-hand grabs into rotation (+ two-hand scale).
        if (objectSpawner.GetComponent<ArtifactRotationDriver>() == null)
        {
            objectSpawner.gameObject.AddComponent<ArtifactRotationDriver>();
        }

        // Physics off so it doesn't tumble; the grab/rotation moves it via the transform.
        Rigidbody rb = objectSpawner.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        // Size the grab collider to the model's world bounds so the hands can actually grab it.
        SphereCollider col = objectSpawner.GetComponent<SphereCollider>();
        if (col == null) col = objectSpawner.gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float worldRadius = b.extents.magnitude + 0.05f;
            float uniformScale = Mathf.Max(objectSpawner.lossyScale.x, 0.0001f);
            col.radius = worldRadius / uniformScale;
            col.center = objectSpawner.InverseTransformPoint(b.center);
        }
    }

    /// <summary>
    /// Applies the artifact's photo texture onto the 3D model renderers.
    /// </summary>
    private void ApplyTextureToModel(GameObject model, ArtifactData data)
    {
        if (model == null || data == null) return;

        Texture2D textureToApply = null;

        // 1. Check for dedicated 3D model texture in Resources
        if (!string.IsNullOrEmpty(data.artifactId))
        {
            textureToApply = Resources.Load<Texture2D>($"Textures/{data.artifactId}") ??
                             Resources.Load<Texture2D>($"Models/{data.artifactId}_tex") ??
                             Resources.Load<Texture2D>($"Textures/tex_{data.artifactId}");
        }

        // 2. Fallback to artifact photo image sprite texture
        if (textureToApply == null && data.images != null && data.images.Length > 0 && data.images[0].sprite != null)
        {
            textureToApply = data.images[0].sprite.texture;
        }

        if (textureToApply == null) return;

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

    /// <summary>
    /// Stops a spawned model from spinning on its own: disables Animators and any behaviour that
    /// looks like a rotator/spinner. The artifact should sit still (the player rotates it by hand).
    /// </summary>
    private void StopAutoRotation(GameObject model)
    {
        if (model == null) return;

        foreach (Animator anim in model.GetComponentsInChildren<Animator>(true))
        {
            if (anim != null) anim.enabled = false;
        }

        foreach (MonoBehaviour mb in model.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name.ToLower();
            if (typeName.Contains("rotat") || typeName.Contains("spin") || typeName.Contains("turntable"))
            {
                mb.enabled = false;
            }
        }

        // If the model itself carries a Rigidbody, stop it from being driven by physics (which
        // would tumble it under the moving UI parent).
        foreach (Rigidbody rb in model.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null) continue;
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.useGravity = false;
            rb.isKinematic = true;
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
        if (audioSource != null)
        {
            audioSource.Stop();
        }
        Action cb = onCloseCallback;
        onCloseCallback = null;
        cb?.Invoke();
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

        // Restore the photo view when the 3D model is cleared. Use UpdateImageUI so it shows the
        // photo XOR the "no images" text - never both. (Previously this force-activated BOTH the
        // image and noImagesText, so "No Images Available" showed on top of a valid photo.)
        UpdateImageUI();
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

        // Treat a null sprite as "no image" too, so we never show a blank image box next to text.
        bool hasImage = artifactData.images != null
                        && artifactData.images.Length > 0
                        && currentImageIndex >= 0 && currentImageIndex < artifactData.images.Length
                        && artifactData.images[currentImageIndex].sprite != null;

        if (hasImage)
        {
            if (displayImage != null)
            {
                displayImage.gameObject.SetActive(true);
                displayImage.sprite = artifactData.images[currentImageIndex].sprite;
                displayImage.color = Color.white;
            }
            if (noImagesText != null)
            {
                noImagesText.gameObject.SetActive(false);
            }

            // Belt-and-suspenders: hide ANY "no images" text in the panel, not just the wired one.
            // On a cloned panel the serialized noImagesText can differ from the visible placeholder,
            // which left "No Images Available" showing on top of a valid photo.
            foreach (TextMeshProUGUI tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp == null || tmp == noImagesText) continue;
                string t = (tmp.text ?? string.Empty).ToLower();
                if (t.Contains("no image") || t.Contains("tidak ada gambar"))
                {
                    tmp.gameObject.SetActive(false);
                }
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
                noImagesText.text = "Artefak ini tiada gambar";
            }
        }
    }

    /// <summary>
    /// Re-applies the 2D image view (photo XOR "no images" text). Used when a panel is reused for
    /// the same artifact without re-running Setup, so it never keeps a stale image+text state.
    /// </summary>
    public void RefreshView()
    {
        SetViewMode(true);
        // A reused panel is re-shown without running Setup, so replay the narration here too.
        PlayNarrationOnShow();
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

        // Auto-play the narration when the artifact is shown.
        PlayNarrationOnShow();

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

        // Disable panel repositioning (dragger) when in 3D View Mode,
        // and enable panel repositioning when in 2D Image Mode.
        ArtifactPanelDragger dragger = GetComponent<ArtifactPanelDragger>();
        if (dragger != null)
        {
            dragger.ResetUserMoved();
            dragger.enabled = show2D;
        }

        if (show2D)
        {
            // Let the data decide: show the photo OR the "no images" text, never both.
            UpdateImageUI();
        }
        else
        {
            if (displayImage != null) displayImage.gameObject.SetActive(false);
            if (noImagesText != null) noImagesText.gameObject.SetActive(false);
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
    /// Hides/destroys the canvas/parent canvas. This function can be called from UnityEvents.
    /// </summary>
    public void CloseCanvas()
    {
        StartClose();

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
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }

        if (WristWatch.Instance != null)
        {
            WristWatch.Instance.EnsureWatchButtonVisible();
        }
    }

    public AudioClip GetOrCreateNarrationClip(ArtifactData data)
    {
        if (data != null && data.narrationClip != null) return data.narrationClip;
        if (data != null && data.instrumentClip != null) return data.instrumentClip;

        if (data != null)
        {
            AudioClip loaded = Resources.Load<AudioClip>($"Audio/{data.artifactId}") ?? Resources.Load<AudioClip>($"Audio/{data.artifactName}");
            if (loaded != null) return loaded;
        }

        // Generate clean procedural narration chime fallback so EVERY artifact 100% has audio
        string clipName = data != null ? $"Narration_{data.artifactId}" : "Narration_Fallback";
        int sampleRate = 44100;
        int samples = sampleRate * 2;
        AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);

        float[] dataSamples = new float[samples];
        float freq = 440f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 2.2f);
            dataSamples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.25f;
        }
        clip.SetData(dataSamples, 0);
        return clip;
    }

    /// <summary>
    /// Makes this panel the sole active narrator: stops whichever other panel was narrating so
    /// only one narration is ever audible at a time (the most recently played one).
    /// </summary>
    private void BecomeActiveNarrator()
    {
        if (s_activeNarration != null && s_activeNarration != this)
        {
            s_activeNarration.StopNarration();
        }
        s_activeNarration = this;
    }

    /// <summary>Stops this panel's narration (used when another panel takes over, or on close).</summary>
    public void StopNarration()
    {
        if (audioSource != null) audioSource.Stop();
        if (s_activeNarration == this) s_activeNarration = null;
        UpdateAudioUI();
    }

    /// <summary>
    /// Called whenever the panel is (re)shown. Auto-plays the real narration clip if one is
    /// assigned; otherwise preloads the fallback so the Play button still does something without
    /// ringing a chime on reveal. Plays on the NEXT frame because the cloned panel is
    /// deactivated/reactivated during spawn, and Play() on the same frame can be dropped.
    /// </summary>
    public void PlayNarrationOnShow()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        if (audioSource == null) return;

        audioSource.mute = false;
        audioSource.volume = 1f;
        audioSource.spatialBlend = 0f;
        audioSource.Stop();

        if (artifactData != null && artifactData.narrationClip != null)
        {
            audioSource.clip = artifactData.narrationClip;
            if (isActiveAndEnabled)
            {
                BecomeActiveNarrator(); // silence any other narrating panel immediately
                StopCoroutine(nameof(PlayClipNextFrame));
                StartCoroutine(nameof(PlayClipNextFrame));
            }
            Debug.Log($"ArtifactPanel: Auto-playing narration '{artifactData.narrationClip.name}' for {artifactData.artifactName}.");
        }
        else if (artifactData != null)
        {
            // No real narration assigned: just preload the fallback for the Play button.
            audioSource.clip = GetOrCreateNarrationClip(artifactData);
            Debug.LogWarning($"ArtifactPanel: No narrationClip assigned on '{artifactData.artifactName}' - Play button will use the fallback tone.");
        }
    }

    private System.Collections.IEnumerator PlayClipNextFrame()
    {
        yield return null; // let the panel finish (re)activating this frame
        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
        {
            BecomeActiveNarrator();
            audioSource.Play();
        }
        UpdateAudioUI();
    }

    public void PlayNarration()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        if (audioSource != null)
        {
            if (audioSource.clip == null && artifactData != null)
            {
                audioSource.clip = GetOrCreateNarrationClip(artifactData);
            }
            if (audioSource.clip != null)
            {
                BecomeActiveNarrator();
                audioSource.Play();
                Debug.Log($"ArtifactPanel: Narration playing for {artifactData?.artifactName}.");
            }
        }
        UpdateAudioUI();
    }

    public void PauseNarration()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
            Debug.Log("ArtifactPanel: Narration paused.");
        }
        UpdateAudioUI();
    }

    public void RestartNarration()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        if (audioSource != null)
        {
            if (audioSource.clip == null && artifactData != null)
            {
                audioSource.clip = GetOrCreateNarrationClip(artifactData);
            }
            if (audioSource.clip != null)
            {
                BecomeActiveNarrator();
                audioSource.Stop();
                audioSource.Play();
                Debug.Log("ArtifactPanel: Narration restarted.");
            }
        }
        UpdateAudioUI();
    }

    private void OnDisable()
    {
        // Releasing the narrator slot when this panel is hidden/closed/destroyed keeps the
        // "only one at a time" tracker from pointing at a gone panel. (An inactive GameObject's
        // AudioSource stops on its own.)
        if (s_activeNarration == this) s_activeNarration = null;
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
        UpdateAudioUI();
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
        if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        if (audioSource == null) return;

        if (audioSource.clip == null && artifactData != null)
        {
            audioSource.clip = GetOrCreateNarrationClip(artifactData);
        }

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
