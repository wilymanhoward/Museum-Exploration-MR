using System;
using UnityEngine;
using TMPro;

public class ArtifactInteraction : MonoBehaviour
{
    [Header("UI Text Fields")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI artistYearText;
    public TextMeshProUGUI descriptionText;

    [Header("Model Spawn Configurations")]
    [Tooltip("The transform anchor where the 3D model will be spawned.")]
    public Transform modelSpawnAnchor;

    [Tooltip("Speed at which the spawned 3D model rotates.")]
    public float modelSpinSpeed = 20f;

    [Header("Proximity Dismissal")]
    [Tooltip("If the player walks further than this distance (in meters), the panel will close.")]
    public float maxInteractionDistance = 2.0f;

    [Tooltip("Speed of scale animations during pop-in and fade-out.")]
    public float transitionSpeed = 6.0f;

    [Header("Follow Settings")]
    [Tooltip("Distance in meters the panel floats in front of the player.")]
    public float followDistance = 1.3f;

    [Tooltip("Smoothing speed for the panel following the player.")]
    public float followSpeed = 4.0f;

    [Tooltip("Distance in meters to offset the panel to the side of the player's view (positive for right, negative for left).")]
    public float sideOffset = 0.55f;

    private ArtifactData artifactData;
    private Transform player;
    private Action onCloseCallback;
    private GameObject spawnedModel;

    private Vector3 initialScale;
    private Vector3 targetScale;
    private bool isClosing = false;
    private Vector3 scanPosition;

    private void Awake()
    {
        initialScale = transform.localScale;
        // Start from zero scale for a smooth pop-in animation
        transform.localScale = Vector3.zero;
        targetScale = initialScale;
    }

    /// <summary>
    /// Configures the panel with data, references, and callback events.
    /// </summary>
    public void Setup(ArtifactData data, Transform playerTransform, Vector3 scanPos, Action onClose)
    {
        artifactData = data;
        player = playerTransform;
        scanPosition = scanPos;
        onCloseCallback = onClose;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }

        // Populate Text Fields
        if (titleText != null) titleText.text = data.artifactName;
        if (artistYearText != null) artistYearText.text = $"{data.artist}, {data.year}";
        if (descriptionText != null) descriptionText.text = data.description;

        // Clean up previous model if we are reusing this panel instance
        if (spawnedModel != null)
        {
            Destroy(spawnedModel);
        }

        // Spawn 3D Model Prefab
        if (modelSpawnAnchor != null && data.modelPrefab != null)
        {
            spawnedModel = Instantiate(data.modelPrefab, modelSpawnAnchor.position, modelSpawnAnchor.rotation, modelSpawnAnchor);
            
            // Normalize local scale to fit in the spawn region
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;
        }

        // If the panel was in the process of closing, cancel it and pop back up
        if (isClosing)
        {
            isClosing = false;
            targetScale = initialScale;
        }

        Debug.Log($"Setup detail panel for: {data.artifactName}");
    }

    private void Update()
    {
        if (player == null) return;

        // 1. Rotate the spawned 3D model
        if (spawnedModel != null)
        {
            spawnedModel.transform.Rotate(Vector3.up, modelSpinSpeed * Time.deltaTime, Space.World);
        }

        // 2. Smoothly follow the player's position (floating to the side of their view)
        if (!isClosing)
        {
            Vector3 targetPos = player.position + (player.forward * followDistance) + (player.right * sideOffset);
            // Position it slightly below direct eye level for a comfortable reading angle
            targetPos.y = player.position.y - 0.1f;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
        }

        // 3. Billboard panel to face the player (only rotating around the vertical Y-axis)
        Vector3 directionToPlayer = player.position - transform.position;
        directionToPlayer.y = 0; // Lock vertical tilt
        if (directionToPlayer != Vector3.zero)
        {
            // Panel canvas faces the local -Z direction.
            // To make the front of the text face the player, the canvas's local +Z forward vector must point AWAY from the player.
            transform.rotation = Quaternion.LookRotation(-directionToPlayer);
        }

        // 4. Proximity check - check distance between player and the physical QR code scan position
        float distanceToExhibit = Vector3.Distance(player.position, scanPosition);
        if (distanceToExhibit > maxInteractionDistance && !isClosing)
        {
            StartClose();
        }

        // 5. Smooth scale transition
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * transitionSpeed);

        // 6. Clean up when fully scaled down
        if (isClosing && Vector3.Distance(transform.localScale, Vector3.zero) < 0.01f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Initiates the closing animation (scale-down) and cleans up.
    /// </summary>
    public void StartClose()
    {
        if (isClosing) return;

        Debug.Log($"Player walked away ({Vector3.Distance(player.position, scanPosition):F2}m from exhibit). Closing panel: {artifactData.artifactName}");
        isClosing = true;
        targetScale = Vector3.zero;
        onCloseCallback?.Invoke();
    }
}
