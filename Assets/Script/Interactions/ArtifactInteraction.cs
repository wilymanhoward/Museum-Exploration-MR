using System;
using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;

public class ArtifactInteraction : MonoBehaviour
{
    [Header("UI Text Fields")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI artistYearText;
    public TextMeshProUGUI descriptionText;

    [Header("UI Spawner Buttons")]
    [Tooltip("Button in the middle of the panel to spawn the 3D model.")]
    public UnityEngine.UI.Button spawnModelButton;

    [Tooltip("The spawner component (RotateArtifact) under the canvas where models are instantiated.")]
    public RotateArtifact modelSpawnAnchor;

    [Tooltip("Speed at which the spawned 3D model rotates.")]
    public float modelSpinSpeed = 20f;

    [Header("Spatial Positioning")]
    [Tooltip("Offset of the panel relative to the QR code's local space. (X = right/left, Y = up/down, Z = forward/back out of wall)")]
    public Vector3 panelOffset = new Vector3(0.45f, 0.0f, 0.05f);

    [Tooltip("If true, rotates the panel 180 degrees relative to the QR code to face the room.")]
    public bool invertRotation = true;

    [Header("Proximity Dismissal")]
    [Tooltip("If true, the panel will automatically close when the player walks too far away.")]
    public bool enableProximityDismissal = false;

    [Tooltip("If the player walks further than this distance (in meters) from the panel, the timer starts.")]
    public float maxInteractionDistance = 5.0f;

    [Tooltip("Time in seconds the player must remain outside the interaction distance before the panel closes.")]
    public float closeDelay = 3.0f;

    [Tooltip("Speed of scale animations during pop-in and fade-out.")]
    public float transitionSpeed = 6.0f;

    [HideInInspector] public ArtifactData artifactData;
    private Transform player;
    private Action onCloseCallback;
    private GameObject spawnedModel;

    private Vector3 initialScale;
    private Vector3 targetScale;
    private bool isClosing = false;
    private Vector3 scanPosition;

    private float outsideTimer = 0f;
    private bool isPlayerOutside = false;

    private void Awake()
    {
        initialScale = transform.localScale;
        if (initialScale == Vector3.zero)
        {
            initialScale = new Vector3(0.001f, 0.001f, 0.001f); // Fallback to prevent zero scale
        }
        // Start from zero scale for a smooth pop-in animation
        transform.localScale = Vector3.zero;
        targetScale = initialScale;
    }

    private void Start()
    {
        if (modelSpawnAnchor == null)
        {
            // Auto-heal: Try to find RotateArtifact on the sibling ObjectSpawner
            if (transform.parent != null)
            {
                modelSpawnAnchor = transform.parent.GetComponentInChildren<RotateArtifact>(true);
            }
            if (modelSpawnAnchor == null)
            {
                RotateArtifact[] rotators = Resources.FindObjectsOfTypeAll<RotateArtifact>();
                foreach (RotateArtifact r in rotators)
                {
                    if (r.gameObject.scene.isLoaded)
                    {
                        modelSpawnAnchor = r;
                        break;
                    }
                }
            }
            if (modelSpawnAnchor != null)
            {
                Debug.Log("ArtifactInteraction: Automatically resolved missing 'modelSpawnAnchor' (RotateArtifact) reference.");
            }
        }

        if (spawnModelButton == null)
        {
            spawnModelButton = GetComponentInChildren<UnityEngine.UI.Button>();
        }

        if (spawnModelButton != null)
        {
            spawnModelButton.onClick.AddListener(OnSpawnModelClicked);
        }
        else
        {
            Debug.LogWarning($"ArtifactInteraction: No Button found on panel {gameObject.name}. Spawning model programmatically may be required.");
        }
    }

    /// <summary>
    /// Configures the panel with data, references, and callback events.
    /// </summary>
    public void Setup(ArtifactData data, Transform playerTransform, Pose qrPose, Action onClose)
    {
        artifactData = data;
        player = playerTransform;
        scanPosition = qrPose.position;
        onCloseCallback = onClose;

        // Position the parent ArtifactUICanvas flat against the wall relative to the QR code
        Vector3 worldOffset = qrPose.rotation * panelOffset;
        Vector3 targetPos = qrPose.position + worldOffset;

        // Always spawn at eye level with the user (matching the camera's height)
        if (playerTransform != null)
        {
            targetPos.y = playerTransform.position.y;
        }

        // Move the parent canvas (ArtifactUICanvas) so that ObjectSpawner and ArtifactPanel move together
        Transform rootToMove = transform.parent != null ? transform.parent : transform;
        rootToMove.position = targetPos;
        rootToMove.rotation = qrPose.rotation * (invertRotation ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }

        // Populate Text Fields
        if (titleText != null) titleText.text = data.artifactName;
        if (artistYearText != null) artistYearText.text = $"{data.artist}, {data.year}";
        if (descriptionText != null) descriptionText.text = data.description;

        // Reset scale for a fresh pop-in animation
        transform.localScale = Vector3.zero;
        targetScale = initialScale;

        // Clean up previous models inside the ObjectSpawner
        if (modelSpawnAnchor != null)
        {
            modelSpawnAnchor.ClearModel();
        }
        spawnedModel = null;

        // Reset the button interactability
        if (spawnModelButton != null)
        {
            spawnModelButton.interactable = true;
        }

        // Reset proximity timers
        isPlayerOutside = false;
        outsideTimer = 0f;

        // If the panel was in the process of closing, cancel it and pop back up
        if (isClosing)
        {
            isClosing = false;
            targetScale = initialScale;
        }

        Debug.Log($"Setup detail panel next to QR code for: {data.artifactName} at position: {transform.position}");

        // Automatically spawn the 3D model upon scanning the QR code
        OnSpawnModelClicked();
    }

    private void Update()
    {
        if (player == null)
        {
            if (Camera.main != null)
            {
                player = Camera.main.transform;
            }
            else
            {
                Camera cam = FindObjectOfType<Camera>();
                if (cam != null)
                {
                    player = cam.transform;
                }
            }
        }

        if (player == null) return;

        // 1. Rotate the spawned 3D model (only if not currently grabbed/inspected)
        if (spawnedModel != null)
        {
            var grabInteractable = spawnedModel.GetComponent<XRGrabInteractable>();
            if (grabInteractable == null || !grabInteractable.isSelected)
            {
                spawnedModel.transform.Rotate(Vector3.up, modelSpinSpeed * Time.deltaTime, Space.World);
            }
        }

        // 2. Proximity check with timer
        if (enableProximityDismissal)
        {
            float distanceToPanel = Vector3.Distance(player.position, transform.position);
            if (distanceToPanel > maxInteractionDistance)
            {
                if (!isPlayerOutside)
                {
                    isPlayerOutside = true;
                    outsideTimer = 0f;
                    Debug.Log($"Player moved outside interaction distance ({distanceToPanel:F2}m). Auto-close timer started.");
                }
                else
                {
                    outsideTimer += Time.deltaTime;
                    if (outsideTimer >= closeDelay && !isClosing)
                    {
                        Debug.Log($"Player outside for {closeDelay}s. Closing panel.");
                        StartClose();
                    }
                }
            }
            else
            {
                if (isPlayerOutside)
                {
                    isPlayerOutside = false;
                    outsideTimer = 0f;
                    Debug.Log("Player returned inside range. Timer reset.");
                }
            }
        }

        // 3. Smooth scale transition
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * transitionSpeed);

        // 4. Clean up or hide when fully scaled down
        if (isClosing && Vector3.Distance(transform.localScale, Vector3.zero) < 0.01f)
        {
            // If this is the persistent UI panel or has a parent, deactivate instead of destroying
            if (transform.parent != null && (transform.parent.name == "ArtifactUICanvas" || transform.parent.name == "ArtifactPanel"))
            {
                transform.parent.gameObject.SetActive(false);
                isClosing = false;
            }
            else if (gameObject.name == "ArtifactUICanvas" || gameObject.name == "ArtifactPanel")
            {
                gameObject.SetActive(false);
                isClosing = false;
            }
            else
            {
                Destroy(gameObject);
            }
        }

    }

    /// <summary>
    /// Instantiates the 3D model and configures grab interactions.
    /// </summary>
    public void OnSpawnModelClicked()
    {
        if (spawnedModel != null || artifactData == null) return;

        if (modelSpawnAnchor != null && artifactData.modelPrefab != null)
        {
            spawnedModel = modelSpawnAnchor.SpawnModel(artifactData.modelPrefab, artifactData.artifactId);

            // Disable button once spawned
            if (spawnModelButton != null)
            {
                spawnModelButton.interactable = false;
            }

            Debug.Log($"3D Model spawned inside ObjectSpawner for {artifactData.artifactName}.");
        }
        else
        {
            Debug.LogWarning("Cannot spawn model: modelSpawnAnchor or modelPrefab is missing.");
        }
    }

    /// <summary>
    /// Initiates the closing animation (scale-down) and cleans up.
    /// </summary>
    public void StartClose()
    {
        if (isClosing) return;

        isClosing = true;
        targetScale = Vector3.zero;
        onCloseCallback?.Invoke();
    }
}
