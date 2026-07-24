using System;
using UnityEngine;
using TMPro;

public class ArtifactPanel : MonoBehaviour
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
    public TextMeshProUGUI imageIndexText; // displays image title

    [Header("Object Spawner Reference")]
    [Tooltip("The Empty Object where the 3D model prefab will be instantiated.")]
    public Transform objectSpawner;

    [Header("Spatial Positioning")]
    [Tooltip("Offset of the panel relative to the QR code's local space. (X = right/left, Y = up/down, Z = forward/back out of wall)")]
    public Vector3 panelOffset = new Vector3(0.45f, 0.0f, 0.05f);

    [Tooltip("If true, rotates the panel 180 degrees relative to the player direction.")]
    public bool invertRotation = false;

    [HideInInspector] public ArtifactData artifactData;
    private GameObject spawnedModel;
    private Action onCloseCallback;
    private int currentImageIndex = 0;
    private Transform trackedPlayer;

    private void Awake()
    {
        // Ensure detail panel is hidden at startup until opened by scan or menu
        gameObject.SetActive(false);
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

            // Spawn exactly 1.0 meter in front of the user's camera
            transform.position = trackedPlayer.position + forwardDir * 1.0f;

            Vector3 directionToPlayer = trackedPlayer.position - transform.position;
            directionToPlayer.y = 0;
            if (directionToPlayer != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(-directionToPlayer, Vector3.up);
            }
        }
    }

    [Header("3D View Button Reference")]
    [Tooltip("The 3D View button on the panel. Auto-located if unassigned.")]
    public GameObject view3DButton;

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

        Debug.Log($"Setup detail panel next to QR code for: {data.artifactName} at position: {transform.position}");
    }

    /// <summary>
    /// Ensures 3D View button is active and wired up so clicking it always displays a 3D model.
    /// </summary>
    private void Refresh3DViewButtonState()
    {
        if (view3DButton == null)
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
            if (btnT != null) view3DButton = btnT.gameObject;
        }

        if (view3DButton != null)
        {
            view3DButton.SetActive(true);

            UnityEngine.UI.Button btn = view3DButton.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = view3DButton.GetComponentInChildren<UnityEngine.UI.Button>();
            if (btn == null) btn = view3DButton.AddComponent<UnityEngine.UI.Button>();

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(On3DViewButtonClicked);

            XRButtonSelection selection = view3DButton.GetComponent<XRButtonSelection>();
            if (selection != null)
            {
                selection.onClick.RemoveAllListeners();
                selection.onClick.AddListener(On3DViewButtonClicked);
            }
        }
    }

    /// <summary>
    /// Invoked when player clicks/taps the 3D View button.
    /// Freshly spawns or re-centers the 3D model right in front of the panel.
    /// </summary>
    public void On3DViewButtonClicked()
    {
        if (artifactData == null) return;

        // Freshly spawn or re-center the 3D model right on the panel
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
    /// Instantiates the 3D model inside the ObjectSpawner.
    /// </summary>
    public void OnSpawnModelClicked()
    {
        if (spawnedModel != null || artifactData == null) return;

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

        if (artifactData.modelPrefab != null)
        {
            if (rotator != null)
            {
                spawnedModel = rotator.SpawnModel(artifactData.modelPrefab, artifactData.artifactId);
            }
            else
            {
                spawnedModel = Instantiate(artifactData.modelPrefab, objectSpawner.position, objectSpawner.rotation, objectSpawner);
                spawnedModel.transform.localPosition = new Vector3(0, 0, -0.05f);
            }

            if (spawnedModel != null)
            {
                spawnedModel.SetActive(true);
            }

            Debug.Log($"3D Model spawned inside ObjectSpawner for {artifactData.artifactName}.");
        }
        else
        {
            Debug.LogWarning("Cannot spawn model: objectSpawner or modelPrefab is missing.");
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

    public void ClearSpawnedModel()
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
            if (imageIndexText != null)
            {
                imageIndexText.text = artifactData.images[currentImageIndex].title;
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
            if (imageIndexText != null)
            {
                imageIndexText.text = "No Image";
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

        Debug.Log($"Updated detail panel with new artifact data: {data.artifactName}");
    }
}
