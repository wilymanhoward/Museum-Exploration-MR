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

    [Tooltip("If true, rotates the panel 180 degrees relative to the QR code to face the room.")]
    public bool invertRotation = true;

    [HideInInspector] public ArtifactData artifactData;
    private GameObject spawnedModel;
    private Action onCloseCallback;
    private int currentImageIndex = 0;

    /// <summary>
    /// Configures the panel with data, references, and callback events.
    /// </summary>
    public void Setup(ArtifactData data, Transform playerTransform, Pose qrPose, Action onClose)
    {
        artifactData = data;
        onCloseCallback = onClose;

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

        Debug.Log($"Setup detail panel next to QR code for: {data.artifactName} at position: {transform.position}");
    }

    /// <summary>
    /// Instantiates the 3D model.
    /// </summary>
    public void OnSpawnModelClicked()
    {
        if (spawnedModel != null || artifactData == null) return;

        if (objectSpawner != null && artifactData.modelPrefab != null)
        {
            spawnedModel = Instantiate(artifactData.modelPrefab, objectSpawner.position, objectSpawner.rotation, objectSpawner);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;
            spawnedModel.transform.localScale = Vector3.one;

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
            foreach (Transform child in objectSpawner)
            {
                Destroy(child.gameObject);
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

        Debug.Log($"Updated detail panel with new artifact data: {data.artifactName}");
    }
}
