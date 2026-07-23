using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.EventSystems;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class RotateArtifact : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Tooltip("Degrees of rotation per meter of hand movement. Higher = more responsive.")]
    public float rotationSensitivity = 350f;

    [Tooltip("Padding added around the spawned model's bounds when sizing the grab collider, in meters.")]
    public float grabColliderPadding = 0.05f;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private SphereCollider grabCollider;
    private Camera mainCamera;

    // Track active spawned model and its ID
    private GameObject spawnedModel;
    private string activeArtifactId;

    // Original local pose of the spawner, restored on every new spawn since
    // two-hand grabbing translates/scales this transform directly
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;

    // Track single and double hand selection states across frames
    private int activeInteractorsCount = 0;
    private Vector3 lastSingleInteractorPos;
    private Vector3 lastMidpoint;
    private float lastDistance;
    private Vector3 lastDir;

    private void Awake()
    {
        // Programmatically add a transparent Image if missing so this RectTransform receives UI raycasts
        UnityEngine.UI.Image uiImage = GetComponent<UnityEngine.UI.Image>();
        if (uiImage == null)
        {
            uiImage = gameObject.AddComponent<UnityEngine.UI.Image>();
            uiImage.color = new Color(0, 0, 0, 0); // Completely transparent
        }
        uiImage.raycastTarget = true;

        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        grabCollider = GetComponent<SphereCollider>();

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;

        // Configure Rigidbody automatically for static/kinematic XRI dragging
        rb.useGravity = false;
        rb.isKinematic = true;

        // Configure XRGrabInteractable automatically for rotation dragging
        grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grabInteractable.trackPosition = false;   // Stay anchored, only rotate
        grabInteractable.trackRotation = false;   // Rotation is calculated manually by this script
        grabInteractable.selectMode = InteractableSelectMode.Multiple; // Support two-handed grab
        grabInteractable.useDynamicAttach = true;
        grabInteractable.matchAttachPosition = true;
        grabInteractable.matchAttachRotation = true;

        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    public GameObject SpawnModel(GameObject prefab, string artifactId)
    {
        // 1. Clear previous models
        ClearModel();

        // 2. Restore the spawner's original pose (a previous two-hand grab may have
        // translated/scaled this transform away from its home under the canvas)
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;

        if (prefab == null) return null;

        activeArtifactId = artifactId;

        // 3. Instantiate under the spawner
        spawnedModel = Instantiate(prefab, transform.position, transform.rotation, transform);
        
        // Resolve camera to project model forward (towards the player)
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
        }
        
        float zOffset = -150f;
        if (mainCamera != null)
        {
            Vector3 localCameraDirection = transform.InverseTransformDirection(mainCamera.transform.position - transform.position).normalized;
            zOffset = Mathf.Sign(localCameraDirection.z) * 150f;
        }
        
        spawnedModel.transform.localPosition = new Vector3(0, 0, zOffset);
        spawnedModel.transform.localRotation = Quaternion.identity;

        // Compensate for parent canvas scale so the model is rendered at its true physical size (1:1 with prefab scale)
        Vector3 worldScale = transform.lossyScale;
        // If the canvas is currently scaling up from zero (pop-in animation), fallback to the target scale (0.0022f) for compensation
        if (worldScale.x < 0.0001f)
        {
            worldScale = new Vector3(0.0022f, 0.0022f, 0.0022f);
        }
        
        Vector3 prefabScale = prefab.transform.localScale;
        spawnedModel.transform.localScale = new Vector3(
            worldScale.x != 0 ? prefabScale.x / worldScale.x : prefabScale.x,
            worldScale.y != 0 ? prefabScale.y / worldScale.y : prefabScale.y,
            worldScale.z != 0 ? prefabScale.z / worldScale.z : prefabScale.z
        );

        // The spawner sits inside a scaled-down UI canvas, so its own lossyScale is tiny
        // (e.g. ~0.002). The SphereCollider's radius is in that same tiny local space, so
        // without resizing it here the grab hitbox ends up only millimeters wide in world
        // space regardless of how large the visually-compensated model looks.
        ResizeGrabCollider(spawnedModel);

        return spawnedModel;
    }

    /// <summary>
    /// Grows/shrinks and re-centers the grab SphereCollider to match the true world-space
    /// bounds of the spawned model, so it can actually be pinch-grabbed by hand.
    /// </summary>
    private void ResizeGrabCollider(GameObject model)
    {
        if (grabCollider == null || model == null) return;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[RotateArtifact] No renderers found on spawned model!");
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float worldRadius = bounds.extents.magnitude + grabColliderPadding;
        float uniformScale = Mathf.Max(transform.lossyScale.x, 0.0001f);

        grabCollider.radius = worldRadius / uniformScale;
        grabCollider.center = transform.InverseTransformPoint(bounds.center);

        Debug.Log($"[RotateArtifact] Resized grab collider: radius = {grabCollider.radius} (world radius = {worldRadius}), center = {grabCollider.center}");
    }

    /// <summary>
    /// Clears any currently spawned model and resets state.
    /// </summary>
    public void ClearModel()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        spawnedModel = null;
        activeArtifactId = null;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        Debug.Log($"[RotateArtifact] OnGrabbed: Grabbed by {args.interactorObject.transform.name}. Interactors count: {grabInteractable.interactorsSelecting.Count}");
        activeInteractorsCount = 0;

        // Mark artifact completed on grab
        if (!string.IsNullOrEmpty(activeArtifactId) && ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.MarkArtifactInteracted(activeArtifactId);
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        Debug.Log($"[RotateArtifact] OnReleased: Released by {args.interactorObject.transform.name}");
        activeInteractorsCount = 0;
    }

    private void Update()
    {
        if (grabInteractable == null) return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
        }

        if (mainCamera == null) return;

        int grabCount = grabInteractable.interactorsSelecting.Count;
        if (grabCount == 0)
        {
            activeInteractorsCount = 0;
            return;
        }

        if (grabCount == 1)
        {
            var interactor = grabInteractable.interactorsSelecting[0];
            Vector3 currentPos = GetInteractorPos(interactor);

            Debug.Log($"[RotateArtifact] Update single-grab: Current Position = {currentPos}, Last Position = {lastSingleInteractorPos}");

            if (activeInteractorsCount != 1)
            {
                lastSingleInteractorPos = currentPos;
                activeInteractorsCount = 1;
                return;
            }

            Vector3 delta = currentPos - lastSingleInteractorPos;
            lastSingleInteractorPos = currentPos;

            if (delta.sqrMagnitude < 0.000001f) return;

            // Project the hand-movement delta into camera-relative directions
            Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
            Vector3 camUp = mainCamera.transform.up;

            float horizontal = Vector3.Dot(delta, camRight);
            float vertical = Vector3.Dot(delta, camUp);

            // Hand moves right  → positive Y rotation (spawner spins right)
            // Hand moves left   → negative Y rotation (spawner spins left)
            transform.Rotate(Vector3.up, -horizontal * rotationSensitivity, Space.World);

            // Hand moves up     → spawner tilts upward
            // Hand moves down   → spawner tilts downward
            transform.Rotate(camRight, vertical * rotationSensitivity, Space.World);
        }
        else if (grabCount >= 2)
        {
            var interactor1 = grabInteractable.interactorsSelecting[0];
            var interactor2 = grabInteractable.interactorsSelecting[1];

            Vector3 pos1 = GetInteractorPos(interactor1);
            Vector3 pos2 = GetInteractorPos(interactor2);

            Vector3 currentMidpoint = (pos1 + pos2) * 0.5f;
            float currentDistance = Vector3.Distance(pos1, pos2);
            Vector3 currentDir = (pos2 - pos1).normalized;

            if (activeInteractorsCount != 2)
            {
                lastMidpoint = currentMidpoint;
                lastDistance = currentDistance;
                lastDir = currentDir;
                activeInteractorsCount = 2;
                return;
            }

            // Translate (move) the spawner with the midpoint of the hands
            Vector3 translationDelta = currentMidpoint - lastMidpoint;
            transform.position += translationDelta;

            // Scale (zoom) the spawner based on distance between hands
            if (lastDistance > 0.001f && currentDistance > 0.001f)
            {
                float scaleRatio = currentDistance / lastDistance;
                transform.localScale *= scaleRatio;
            }

            // Rotate based on direction change between the two hands
            if (lastDir.sqrMagnitude > 0.001f && currentDir.sqrMagnitude > 0.001f)
            {
                Quaternion handRotation = Quaternion.FromToRotation(lastDir, currentDir);
                transform.rotation = handRotation * transform.rotation;
            }

            lastMidpoint = currentMidpoint;
            lastDistance = currentDistance;
            lastDir = currentDir;
        }
    }

    private Vector3 GetInteractorPos(IXRSelectInteractor interactor)
    {
        // Use the interactor's actual attach/pinch point rather than its root transform.
        // The root transform (e.g. a hand's palm/wrist anchor) can be offset from where the
        // fingers are actually pinching, and that offset changes as the wrist rotates while
        // walking - using the raw root position caused the held model to visibly drift/slide
        // relative to the hands whenever the player moved.
        return interactor.GetAttachTransform(grabInteractable).position;
    }

    #region UI Pointer Handlers (For Graphic Raycasting / Trigger Dragging)
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[RotateArtifact] OnPointerDown UI Drag start by pointer: {eventData.pointerId}");
        
        // Mark artifact completed on grab/drag interaction
        if (!string.IsNullOrEmpty(activeArtifactId) && ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.MarkArtifactInteracted(activeArtifactId);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Drag delta in pixels
        Vector2 delta = eventData.delta;

        if (delta.sqrMagnitude < 0.0001f) return;

        // Dynamic camera resolution
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
        }

        // Rotate the model
        // Dragging horizontally rotates around local Y-axis (spins it left/right)
        // Dragging vertically rotates around the camera's right direction (tilts it up/down)
        float sensitivity = 0.25f; // Adjust sensitivity for comfortable dragging
        transform.Rotate(Vector3.up, -delta.x * sensitivity, Space.World);

        if (mainCamera != null)
        {
            Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
            transform.Rotate(camRight, delta.y * sensitivity, Space.World);
        }
    }
    #endregion
}
