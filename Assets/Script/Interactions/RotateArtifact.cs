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
    public float grabColliderPadding = 0.05f;

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
        initialLocalScale = (transform.localScale != Vector3.zero) ? transform.localScale : Vector3.one;

        // Ensure RectTransform on ObjectSpawner has a clean interaction size for GraphicRaycaster without shifting position
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            if (rect.sizeDelta.x < 200f || rect.sizeDelta.y < 200f)
            {
                rect.sizeDelta = new Vector2(240f, 240f);
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
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll; // Freeze physical position/rotation changes
        }

        // Configure XRGrabInteractable automatically for single-finger/hand rotation dragging
        if (grabInteractable != null)
        {
            grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grabInteractable.trackPosition = false;   // Stay anchored to panel, only rotate
            grabInteractable.trackRotation = false;   // Rotation calculated manually in Update
            grabInteractable.selectMode = InteractableSelectMode.Multiple; // Support left or right hand pinch
            grabInteractable.useDynamicAttach = false; // Do NOT snap transform to hand attach pose on pinch!
            grabInteractable.matchAttachPosition = false; // Prevents 3D artifact from jumping/disappearing on pinch!
            grabInteractable.matchAttachRotation = false;
            grabInteractable.interactionLayers = ~0;  // Allow all interactors (hands, rays, controllers)

            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
    }

    private void LateUpdate()
    {
        // Enforce fixed local position - prevents ANY direct touch interactor or physics event from moving 3D model off the panel!
        if (transform.localPosition != initialLocalPosition)
        {
            transform.localPosition = initialLocalPosition;
        }
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

    public GameObject SpawnModel(GameObject prefab, string artifactId, float targetSizeMeters = 0.22f)
    {
        // 1. Clear previous models
        ClearModel();

        // 2. Restore the spawner's original intended layout pose (guarantee non-zero scale)
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        if (initialLocalScale != Vector3.zero)
        {
            transform.localScale = initialLocalScale;
        }
        else
        {
            transform.localScale = Vector3.one;
        }

        if (prefab == null) return null;

        activeArtifactId = artifactId;

        string key = ((prefab.name ?? "") + " " + (artifactId ?? "")).ToLower();

        // 3. Create a pivot container at (0, 0, -0.15f) in spawner space.
        // This container becomes spawnedModel, ensuring its local origin (0,0,0) is placed
        // PRECISELY at the geometric center of the mesh. Rotating spawnedModel will now rotate
        // cleanly around the true visual center of any artifact (e.g., Wayang Kulit).
        spawnedModel = new GameObject("ArtifactPivot");
        spawnedModel.transform.SetParent(transform, false);
        spawnedModel.transform.localPosition = new Vector3(0f, 0f, -0.15f);
        spawnedModel.transform.localRotation = Quaternion.identity;
        spawnedModel.transform.localScale = Vector3.one;

        // 4. Instantiate the model prefab under the pivot container
        GameObject modelInstance = Instantiate(prefab, spawnedModel.transform);
        modelInstance.name = prefab.name;
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = GetUprightOrientation(prefab.name, artifactId);
        modelInstance.transform.localScale = Vector3.one;

        // Filter sub-objects (e.g. for Batik Canting, remove BatikCanting2 and BatikPelangi so only BatikCanting1 remains)
        FilterArtifactSubObjects(modelInstance, key, artifactId);

        // 5. Determine target world size in meters
        float targetSize = targetSizeMeters > 0f ? targetSizeMeters : GetTargetWorldSize(key);

        // 6. Fit model bounds and offset modelInstance inside pivot so geometric center is at pivot origin (0,0,0)
        FitModelToBounds(spawnedModel, modelInstance, targetSize);

        // 7. Setup colliders so raycasts can detect model without blocking UI buttons below
        SetupCollidersAndInteractable(spawnedModel);

        return spawnedModel;
    }

    private float GetTargetWorldSize(string key)
    {
        if (key.Contains("wayang")) return 0.26f;
        if (key.Contains("batu")) return 0.28f;
        if (key.Contains("songket") || key.Contains("batik") || key.Contains("canting") || key.Contains("pelangi")) return 0.30f;
        return 0.18f;
    }

    private Quaternion GetUprightOrientation(string prefabName, string artifactId)
    {
        string key = ((prefabName ?? "") + " " + (artifactId ?? "")).ToLower();

        if (key.Contains("batu"))
        {
            // Batu Bersurat Terengganu: Stand upright and face forward toward player
            return Quaternion.Euler(-90f, 180f, 0f);
        }
        if (key.Contains("songket") || key.Contains("pelangi"))
        {
            // Kain Songket & Kain Pelangi: Stand upright and face forward toward player
            return Quaternion.Euler(-90f, 180f, 0f);
        }
        if (key.Contains("gamelan") || key.Contains("gamelen"))
        {
            // Gamelan Terengganu: Rotate 180 degrees around Y so instrument faces forward toward player
            return Quaternion.Euler(0f, 180f, 0f);
        }

        return Quaternion.identity;
    }

    private void FilterArtifactSubObjects(GameObject modelInstance, string key, string artifactId)
    {
        if (modelInstance == null) return;

        bool isPelangi = key.Contains("pelangi") || (artifactId != null && artifactId.Contains("room_1_2"));
        bool isCanting = key.Contains("canting") || key.Contains("tulis") || (artifactId != null && artifactId.Contains("room_1_1")) || (!isPelangi && key.Contains("batik"));

        if (isCanting && !isPelangi)
        {
            // Keep ONLY BatikCanting1 for Batik Canting artifact; remove BatikCanting2 and BatikPelangi
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (Transform child in modelInstance.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child == modelInstance.transform) continue;

                string cName = child.name.ToLower();
                if (cName.Contains("canting2") || cName.Contains("batikcanting2") || cName.Contains("pelangi") || cName.Contains("batikpelangi"))
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            foreach (GameObject go in toDestroy)
            {
                if (go != null)
                {
                    go.SetActive(false);
                    DestroyImmediate(go);
                }
            }
        }
        else if (isPelangi)
        {
            // Keep ONLY BatikPelangi for Batik Pelangi artifact; remove BatikCanting1 and BatikCanting2
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (Transform child in modelInstance.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child == modelInstance.transform) continue;

                string cName = child.name.ToLower();
                if (cName.Contains("canting") || cName.Contains("batikcanting"))
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            foreach (GameObject go in toDestroy)
            {
                if (go != null)
                {
                    go.SetActive(false);
                    DestroyImmediate(go);
                }
            }
        }
    }

    private void FitModelToBounds(GameObject pivot, GameObject modelInstance, float targetWorldSizeMeters = 0.22f)
    {
        if (pivot == null || modelInstance == null) return;

        // Reset local scale to 1 and position to 0 to measure natural unscaled bounds
        modelInstance.transform.localScale = Vector3.one;
        modelInstance.transform.localPosition = Vector3.zero;

        MeshFilter[] filters = modelInstance.GetComponentsInChildren<MeshFilter>(true);
        SkinnedMeshRenderer[] skinned = modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        Bounds combinedLocalBounds = new Bounds();
        bool hasBounds = false;

        foreach (MeshFilter mf in filters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds b = GetMeshBoundsInRootSpace(pivot.transform, mf.transform, mf.sharedMesh.bounds);
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
            Bounds b = GetMeshBoundsInRootSpace(pivot.transform, smr.transform, smr.sharedMesh.bounds);
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

        float spawnerScale = Mathf.Max(transform.lossyScale.x, 0.0001f);

        if (!hasBounds)
        {
            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && renderers[i].enabled)
                    {
                        b.Encapsulate(renderers[i].bounds);
                    }
                }

                float worldDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (worldDim > 0.0001f)
                {
                    float scaleFactor = targetWorldSizeMeters / (worldDim * spawnerScale);
                    modelInstance.transform.localScale = Vector3.one * scaleFactor;
                }
                Vector3 centerInPivot = pivot.transform.InverseTransformPoint(b.center);
                modelInstance.transform.localPosition = -centerInPivot;
            }
            return;
        }

        // 1. Calculate scale to achieve targetWorldSizeMeters in world space
        float localMaxDim = Mathf.Max(combinedLocalBounds.size.x, Mathf.Max(combinedLocalBounds.size.y, combinedLocalBounds.size.z));
        float desiredLocalScale = 1.0f;

        if (localMaxDim > 0.00001f)
        {
            desiredLocalScale = targetWorldSizeMeters / (localMaxDim * spawnerScale);

            if (float.IsNaN(desiredLocalScale) || float.IsInfinity(desiredLocalScale) || desiredLocalScale <= 0.00001f)
            {
                desiredLocalScale = 1.0f;
            }

            modelInstance.transform.localScale = Vector3.one * desiredLocalScale;
        }

        // 2. Position modelInstance inside pivot container so its geometric center aligns EXACTLY at pivot origin (0, 0, 0)
        Vector3 centerOffset = combinedLocalBounds.center;
        Vector3 localPos = -centerOffset * desiredLocalScale;
        modelInstance.transform.localPosition = localPos;

        Debug.Log($"[RotateArtifact] FitModelToBounds for '{modelInstance.name}': localMaxDim={localMaxDim}, spawnerScale={spawnerScale}, set localScale={desiredLocalScale}, modelInstance localPos={localPos}");
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

        // 1. Spawner's SphereCollider (isTrigger = true so UI buttons below receive raycasts cleanly)
        if (grabCollider == null) grabCollider = GetComponent<SphereCollider>();
        if (grabCollider == null) grabCollider = gameObject.AddComponent<SphereCollider>();

        float worldRadius = Mathf.Max(bounds.extents.magnitude, 0.12f);
        float uniformScale = Mathf.Max(transform.lossyScale.x, 0.0001f);

        grabCollider.radius = worldRadius / uniformScale;
        grabCollider.center = transform.InverseTransformPoint(bounds.center);
        grabCollider.isTrigger = true; // TRIGGER so UI buttons below receive raycasts cleanly!

        // 2. Spawned model's BoxCollider (isTrigger = true so UI raycasts pass to buttons below)
        BoxCollider modelBox = model.GetComponent<BoxCollider>();
        if (modelBox == null) modelBox = model.AddComponent<BoxCollider>();
        modelBox.isTrigger = true; // TRIGGER so UI raycasts pass to buttons below!
        modelBox.center = model.transform.InverseTransformPoint(bounds.center);
        modelBox.size = model.transform.InverseTransformDirection(bounds.size);

        // 3. Ensure Kinematic Rigidbody
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;

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

        // Rotate spawnedModel directly so the object NEVER hides or moves off-screen!
        Transform targetTransform = spawnedModel != null ? spawnedModel.transform : transform;

        targetTransform.Rotate(Vector3.up, -horizontal * rotationSensitivity, Space.World);
        targetTransform.Rotate(camRight, vertical * rotationSensitivity, Space.World);
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
        // Rotate spawnedModel directly so the object NEVER hides or moves off-screen!
        Transform targetTransform = spawnedModel != null ? spawnedModel.transform : transform;

        targetTransform.Rotate(Vector3.up, -delta.x * sensitivity, Space.World);

        if (mainCamera != null)
        {
            Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
            targetTransform.Rotate(camRight, delta.y * sensitivity, Space.World);
        }
    }
    #endregion
}
