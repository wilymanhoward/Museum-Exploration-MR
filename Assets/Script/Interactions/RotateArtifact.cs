using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.EventSystems;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class RotateArtifact : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerExitHandler
{
    [Tooltip("Degrees of rotation per meter of single-finger/hand movement. Higher = more responsive.")]
    public float rotationSensitivity = 500f;

    [Tooltip("Padding added around the spawned model's bounds when sizing the grab collider, in meters.")]
    public float grabColliderPadding = 0.15f;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private SphereCollider grabCollider;
    private Camera mainCamera;

    // Track active spawned model and its ID
    private GameObject spawnedModel;
    private string activeArtifactId;

    // Original local pose of the spawner, restored on every new spawn
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;

    // Track hand selection states across frames
    private int activeInteractorsCount = 0;
    private Vector3 lastSingleInteractorPos;

    // UI Pointer tracking for single-finger UI Raycast drag
    private readonly Dictionary<int, Vector2> activePointers = new Dictionary<int, Vector2>();

    public bool IsBeingRotated
    {
        get
        {
            if (grabInteractable != null && grabInteractable.interactorsSelecting.Count > 0) return true;
            if (activePointers != null && activePointers.Count > 0) return true;
            return false;
        }
    }

    private void Awake()
    {
        // Record original intended layout pose BEFORE any modifications
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;

        // Ensure RectTransform on ObjectSpawner has a clean interaction size for GraphicRaycaster without shifting position
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            if (rect.sizeDelta.x < 300f || rect.sizeDelta.y < 300f)
            {
                rect.sizeDelta = new Vector2(350f, 350f);
            }
        }

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

        // Configure Rigidbody automatically for static/kinematic XRI dragging
        rb.useGravity = false;
        rb.isKinematic = true;

        // Configure XRGrabInteractable automatically for single-finger/hand rotation dragging
        grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grabInteractable.trackPosition = false;   // Stay anchored to panel, only rotate
        grabInteractable.trackRotation = false;   // Rotation calculated manually in Update
        grabInteractable.selectMode = InteractableSelectMode.Multiple; // Support left or right hand pinch
        grabInteractable.useDynamicAttach = true;
        grabInteractable.matchAttachPosition = true;
        grabInteractable.matchAttachRotation = true;
        grabInteractable.interactionLayers = ~0;  // Allow all interactors (hands, rays, controllers)

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

        // 2. Restore the spawner's original intended layout pose
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;

        if (prefab == null) return null;

        activeArtifactId = artifactId;

        // 3. Instantiate under the spawner, floating 15cm forward in front of its intended layout position
        spawnedModel = Instantiate(prefab, transform.position, transform.rotation, transform);
        
        spawnedModel.transform.localPosition = new Vector3(0f, 0f, -0.15f);
        spawnedModel.transform.localRotation = GetUprightOrientation(prefab.name, artifactId);

        // 4. Fit model bounds so it is cleanly sized ~0.25 meters wide in world space
        FitModelToBounds(spawnedModel, 0.25f);

        // 5. Setup non-trigger colliders so Physics Raycasts hit the 3D model instantly
        SetupCollidersAndInteractable(spawnedModel);

        return spawnedModel;
    }

    private Quaternion GetUprightOrientation(string prefabName, string artifactId)
    {
        string key = ((prefabName ?? "") + " " + (artifactId ?? "")).ToLower();

        if (key.Contains("batu"))
        {
            // Batu Bersurat Terengganu: Stand upright and face forward toward player
            return Quaternion.Euler(-90f, 180f, 0f);
        }
        if (key.Contains("songket"))
        {
            // Kain Songket: Stand upright and face forward toward player
            return Quaternion.Euler(-90f, 180f, 0f);
        }

        return Quaternion.identity;
    }

    private void FitModelToBounds(GameObject model, float targetWorldSizeMeters = 0.25f)
    {
        if (model == null) return;

        // Reset local scale to 1 to measure natural unscaled bounds
        model.transform.localScale = Vector3.one;

        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
        SkinnedMeshRenderer[] skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        Bounds combinedLocalBounds = new Bounds();
        bool hasBounds = false;

        foreach (MeshFilter mf in filters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds b = GetMeshBoundsInRootSpace(model.transform, mf.transform, mf.sharedMesh.bounds);
            if (!hasBounds)
            {
                combinedLocalBounds = b;
                hasBounds = true;
            }
            else
            {
                combinedLocalBounds.Encapsulate(b);
            }
        }

        foreach (SkinnedMeshRenderer smr in skinned)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            Bounds b = GetMeshBoundsInRootSpace(model.transform, smr.transform, smr.sharedMesh.bounds);
            if (!hasBounds)
            {
                combinedLocalBounds = b;
                hasBounds = true;
            }
            else
            {
                combinedLocalBounds.Encapsulate(b);
            }
        }

        if (!hasBounds)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float worldDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (worldDim > 0.0001f)
                {
                    float scaleFactor = targetWorldSizeMeters / worldDim;
                    model.transform.localScale = Vector3.one * scaleFactor;
                }
            }
            return;
        }

        // 1. Re-center model geometry so it spins around its exact geometric center
        Vector3 centerOffset = combinedLocalBounds.center;
        if (centerOffset.sqrMagnitude > 0.00001f)
        {
            foreach (Transform child in model.transform)
            {
                child.localPosition -= centerOffset;
            }
            Debug.Log($"[RotateArtifact] Re-centered model '{model.name}' geometry offset by: {centerOffset}");
        }

        // 2. Scale model to targetWorldSizeMeters
        float localMaxDim = Mathf.Max(combinedLocalBounds.size.x, Mathf.Max(combinedLocalBounds.size.y, combinedLocalBounds.size.z));
        float spawnerScale = Mathf.Max(transform.lossyScale.x, 0.0001f);

        if (localMaxDim > 0.00001f)
        {
            float desiredLocalScale = targetWorldSizeMeters / (localMaxDim * spawnerScale);

            if (float.IsNaN(desiredLocalScale) || float.IsInfinity(desiredLocalScale) || desiredLocalScale <= 0.00001f)
            {
                desiredLocalScale = 1.0f;
            }

            model.transform.localScale = Vector3.one * desiredLocalScale;
            Debug.Log($"[RotateArtifact] FitModelToBounds for '{model.name}': localMaxDim={localMaxDim}, spawnerScale={spawnerScale}, set localScale={desiredLocalScale}");
        }
    }

    private Bounds GetMeshBoundsInRootSpace(Transform root, Transform child, Bounds meshBounds)
    {
        Vector3 center = meshBounds.center;
        Vector3 extents = meshBounds.extents;

        Vector3[] corners = new Vector3[8]
        {
            center + new Vector3(-extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y,  extents.z),
            center + new Vector3(-extents.x,  extents.y, -extents.z),
            center + new Vector3(-extents.x,  extents.y,  extents.z),
            center + new Vector3( extents.x, -extents.y, -extents.z),
            center + new Vector3( extents.x, -extents.y,  extents.z),
            center + new Vector3( extents.x,  extents.y, -extents.z),
            center + new Vector3( extents.x,  extents.y,  extents.z),
        };

        Bounds rootBounds = new Bounds();
        bool first = true;

        for (int i = 0; i < 8; i++)
        {
            Vector3 worldCorner = child.TransformPoint(corners[i]);
            Vector3 rootCorner = root.InverseTransformPoint(worldCorner);

            if (first)
            {
                rootBounds = new Bounds(rootCorner, Vector3.zero);
                first = false;
            }
            else
            {
                rootBounds.Encapsulate(rootCorner);
            }
        }

        return rootBounds;
    }

    private void SetupCollidersAndInteractable(GameObject model)
    {
        if (model == null) return;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        // 1. Spawner's SphereCollider (isTrigger = false so Physics.Raycast hits it!)
        if (grabCollider == null) grabCollider = GetComponent<SphereCollider>();
        if (grabCollider == null) grabCollider = gameObject.AddComponent<SphereCollider>();

        float worldRadius = Mathf.Max(bounds.extents.magnitude + grabColliderPadding, 0.30f);
        float uniformScale = Mathf.Max(transform.lossyScale.x, 0.0001f);

        grabCollider.radius = worldRadius / uniformScale;
        grabCollider.center = transform.InverseTransformPoint(bounds.center);
        grabCollider.isTrigger = false; // NON-TRIGGER so Physics Raycasts detect hand pinches!

        // 2. Spawned model's BoxCollider (isTrigger = false so hand rays hit the model directly)
        BoxCollider modelBox = model.GetComponent<BoxCollider>();
        if (modelBox == null) modelBox = model.AddComponent<BoxCollider>();
        modelBox.isTrigger = false; // NON-TRIGGER
        modelBox.center = model.transform.InverseTransformPoint(bounds.center);
        modelBox.size = model.transform.InverseTransformDirection(bounds.size * 1.3f);

        // 3. Ensure Kinematic Rigidbody
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 4. Register both colliders explicitly with XRGrabInteractable
        if (grabInteractable != null)
        {
            grabInteractable.colliders.Clear();
            grabInteractable.colliders.Add(grabCollider);
            grabInteractable.colliders.Add(modelBox);
            grabInteractable.interactionLayers = ~0; // All interactors
        }
    }

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
        Debug.Log($"[RotateArtifact] OnGrabbed by {args.interactorObject.transform.name}. Total selecting interactors: {grabInteractable.interactorsSelecting.Count}");
        activeInteractorsCount = 0; // Force re-anchoring on state change

        if (!string.IsNullOrEmpty(activeArtifactId) && ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.MarkArtifactInteracted(activeArtifactId);
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        Debug.Log($"[RotateArtifact] OnReleased by {args.interactorObject.transform.name}. Clean release.");
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

        // SINGLE FINGER / SINGLE HAND PINCH & DRAG ROTATION
        var interactor = grabInteractable.interactorsSelecting[0];
        Vector3 currentPos = GetInteractorPos(interactor);

        if (activeInteractorsCount == 0)
        {
            lastSingleInteractorPos = currentPos;
            activeInteractorsCount = 1;
            return;
        }

        Vector3 delta = currentPos - lastSingleInteractorPos;
        lastSingleInteractorPos = currentPos;

        if (delta.sqrMagnitude < 0.000001f) return;

        Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
        Vector3 camUp = mainCamera.transform.up;

        float horizontal = Vector3.Dot(delta, camRight);
        float vertical = Vector3.Dot(delta, camUp);

        // Moving finger right -> spins model right around Y axis
        // Moving finger left  -> spins model left around Y axis
        transform.Rotate(Vector3.up, -horizontal * rotationSensitivity, Space.World);

        // Moving finger up    -> tilts model upward around camera right axis
        // Moving finger down  -> tilts model downward around camera right axis
        transform.Rotate(camRight, vertical * rotationSensitivity, Space.World);
    }

    private Vector3 GetInteractorPos(IXRSelectInteractor interactor)
    {
        if (interactor == null) return transform.position;
        Transform attachT = interactor.GetAttachTransform(grabInteractable);
        if (attachT != null) return attachT.position;
        Component comp = interactor as Component;
        if (comp != null) return comp.transform.position;
        return transform.position;
    }

    #region UI Pointer Handlers (For Graphic Raycasting / Single Finger Pointer Support)
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[RotateArtifact] OnPointerDown: Pointer {eventData.pointerId}");
        activePointers[eventData.pointerId] = eventData.position;

        if (!string.IsNullOrEmpty(activeArtifactId) && ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.MarkArtifactInteracted(activeArtifactId);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (activePointers.ContainsKey(eventData.pointerId))
        {
            activePointers.Remove(eventData.pointerId);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (activePointers.ContainsKey(eventData.pointerId))
        {
            activePointers.Remove(eventData.pointerId);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        activePointers[eventData.pointerId] = eventData.position;

        // Single Finger UI Pinch & Drag Rotation
        Vector2 delta = eventData.delta;
        if (delta.sqrMagnitude < 0.0001f) return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = FindObjectOfType<Camera>();
        }

        float sensitivity = 0.40f;
        transform.Rotate(Vector3.up, -delta.x * sensitivity, Space.World);

        if (mainCamera != null)
        {
            Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
            transform.Rotate(camRight, delta.y * sensitivity, Space.World);
        }
    }
    #endregion
}
