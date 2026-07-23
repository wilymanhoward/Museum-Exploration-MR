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
        if (topTitleText != null) topTitleText.text = data.artifactName;
        if (bottomTitleText != null) bottomTitleText.text = $"Artefak:\n\"{data.artifactName}\"";
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

        // Automatically spawn the 3D model inside the ObjectSpawner
        OnSpawnModelClicked();

        Debug.Log($"Setup detail panel next to QR code for: {data.artifactName} at position: {transform.position}");
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
    /// Instantiates the 3D model.
    /// </summary>
    public void OnSpawnModelClicked()
    {
        if (spawnedModel != null || artifactData == null) return;

        if (objectSpawner != null && artifactData.modelPrefab != null)
        {
            RotateArtifact rotator = objectSpawner.GetComponent<RotateArtifact>();
            if (rotator != null)
            {
                spawnedModel = rotator.SpawnModel(artifactData.modelPrefab, artifactData.artifactId);
            }
            else
            {
                spawnedModel = Instantiate(artifactData.modelPrefab, objectSpawner.position, objectSpawner.rotation, objectSpawner);
                
                Camera cam = Camera.main;
                float zOffset = -150f;
                if (cam != null)
                {
                    Vector3 localCamDir = objectSpawner.InverseTransformDirection(cam.transform.position - objectSpawner.position).normalized;
                    zOffset = Mathf.Sign(localCamDir.z) * 150f;
                }
                
                spawnedModel.transform.localPosition = new Vector3(0, 0, zOffset);
                spawnedModel.transform.localRotation = Quaternion.identity;
                spawnedModel.transform.localScale = Vector3.one;
            }

            Debug.Log($"3D Model spawned inside ObjectSpawner for {artifactData.artifactName}.");
        }
        else
        {
            Debug.LogWarning("Cannot spawn model: objectSpawner or modelPrefab is missing.");
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
        if (topTitleText != null) topTitleText.text = data.artifactName;
        if (bottomTitleText != null) bottomTitleText.text = $"Artefak:\n\"{data.artifactName}\"";
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

        // Automatically spawn the 3D model inside the ObjectSpawner
        OnSpawnModelClicked();

        Debug.Log($"Updated detail panel with new artifact data: {data.artifactName}");
    }
}
