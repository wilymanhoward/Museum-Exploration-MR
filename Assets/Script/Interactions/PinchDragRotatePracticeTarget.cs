using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Practice object for the "Pinch &amp; Hold to Drag/Rotate" tutorial step.
/// Spins whichever visual it's given: a real Resources/Models/model_artifact_* prefab
/// when one is supplied (matching the real exhibit's 3D View), or a placeholder gem
/// (two nested cubes) otherwise. Uses the same grab configuration as RotateArtifact: the
/// object never leaves its anchor position - hand movement while the pinch is held is
/// converted to camera-relative rotation.
///
/// The step reads TotalRotationDegrees / LongestHoldSeconds to decide success.
/// Built entirely at runtime via Create() - no prefab needs to be wired in the Inspector.
/// </summary>
public class PinchDragRotatePracticeTarget : XRGrabInteractable
{
    [Tooltip("Degrees of rotation per meter of hand movement while pinching.")]
    public float rotationSensitivity = 420f;

    public event Action GrabStarted;
    public event Action GrabEnded;
    /// <summary>Fired with the unsigned degrees rotated this frame, while held.</summary>
    public event Action<float> Rotated;

    /// <summary>Total unsigned degrees the player has rotated the gem, across all grabs.</summary>
    public float TotalRotationDegrees { get; private set; }
    /// <summary>Longest single continuous pinch-hold so far, in seconds.</summary>
    public float LongestHoldSeconds { get; private set; }
    public bool IsHeld { get { return isSelected; } }

    private Vector3 anchorPosition;
    private Camera mainCamera;
    private bool hasLastPos;
    private Vector3 lastInteractorPos;
    private float currentHoldSeconds;
    private Transform visualRoot;

    /// <summary>
    /// Creates a ready-to-use practice object at the given world position. If modelPrefab is
    /// supplied and has renderers (e.g. one of the real Resources/Models/model_artifact_* prefabs),
    /// it is used as the spin visual, scaled/recentred to fit; otherwise a placeholder gem
    /// (two nested cubes) is built instead so the step always has something to show.
    /// </summary>
    public static PinchDragRotatePracticeTarget Create(Vector3 position, GameObject modelPrefab = null)
    {
        GameObject root = new GameObject("TutorialRotateTarget");
        root.transform.position = position;

        Transform visuals = new GameObject("Visuals").transform;
        visuals.SetParent(root.transform, false);

        bool builtFromModel = modelPrefab != null && TryBuildFromModel(visuals, modelPrefab);
        if (!builtFromModel)
        {
            BuildPlaceholderGem(visuals);
        }

        // One generous collider on the root so the hand ray hits it easily.
        BoxCollider grabCollider = root.AddComponent<BoxCollider>();
        grabCollider.size = Vector3.one * (builtFromModel ? 0.26f : 0.2f);

        PinchDragRotatePracticeTarget target = root.AddComponent<PinchDragRotatePracticeTarget>();
        target.visualRoot = visuals;
        return target;
    }

    /// <summary>Instantiates the real artifact prefab, strips its colliders/physics, and fits it to a comfortable practice size. Returns false (caller falls back to the gem) if the prefab has no renderers.</summary>
    private static bool TryBuildFromModel(Transform visuals, GameObject modelPrefab)
    {
        const float targetSizeMeters = 0.16f;

        GameObject model = Instantiate(modelPrefab, visuals, false);
        model.name = "ArtifactModel";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (Rigidbody rb in model.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(model);
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        float scale = maxDimension > 0.0001f ? targetSizeMeters / maxDimension : 1f;
        model.transform.localScale = Vector3.one * scale;

        // Re-measure after scaling and recentre so the model's visual centre sits at the spin pivot.
        Bounds scaledBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) scaledBounds.Encapsulate(renderers[i].bounds);
        model.transform.localPosition = -visuals.InverseTransformPoint(scaledBounds.center);

        return true;
    }

    /// <summary>Placeholder gem (two nested cubes offset 45 degrees) used when no artifact model is available.</summary>
    private static void BuildPlaceholderGem(Transform visuals)
    {
        GameObject cubeA = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeA.name = "GemOuter";
        cubeA.transform.SetParent(visuals, false);
        cubeA.transform.localScale = Vector3.one * 0.1f;
        Destroy(cubeA.GetComponent<Collider>());
        cubeA.GetComponent<Renderer>().material =
            TutorialGestureGizmo.MakeRuntimeMaterial(new Color(1f, 0.62f, 0.2f, 1f), false);

        GameObject cubeB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeB.name = "GemInner";
        cubeB.transform.SetParent(visuals, false);
        cubeB.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        cubeB.transform.localScale = Vector3.one * 0.082f;
        Destroy(cubeB.GetComponent<Collider>());
        cubeB.GetComponent<Renderer>().material =
            TutorialGestureGizmo.MakeRuntimeMaterial(new Color(0.95f, 0.35f, 0.45f, 1f), false);
    }

    protected override void Awake()
    {
        base.Awake();

        anchorPosition = transform.position;
        if (visualRoot == null) visualRoot = transform;

        // Same recipe as RotateArtifact: kinematic, anchored, rotation done manually.
        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezeAll;

        movementType = MovementType.VelocityTracking;
        trackPosition = false;
        trackRotation = false;
        throwOnDetach = false;
        useDynamicAttach = false;
        matchAttachPosition = false;
        matchAttachRotation = false;
        selectMode = InteractableSelectMode.Multiple;
        interactionLayers = ~0;
    }

    private void Update()
    {
        // Slow idle spin while not held, hinting that the object is rotatable.
        if (!isSelected)
        {
            hasLastPos = false;
            currentHoldSeconds = 0f;
            if (visualRoot != null)
            {
                visualRoot.Rotate(Vector3.up, 25f * Time.deltaTime, Space.World);
            }
            return;
        }

        currentHoldSeconds += Time.deltaTime;
        if (currentHoldSeconds > LongestHoldSeconds) LongestHoldSeconds = currentHoldSeconds;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = FindObjectOfType<Camera>();
            if (mainCamera == null) return;
        }

        IXRSelectInteractor interactor = interactorsSelecting[0];
        Transform attach = interactor.GetAttachTransform(this);
        Vector3 currentPos = attach != null ? attach.position
            : (interactor as Component != null ? ((Component)interactor).transform.position : transform.position);

        if (!hasLastPos)
        {
            lastInteractorPos = currentPos;
            hasLastPos = true;
            return;
        }

        Vector3 delta = currentPos - lastInteractorPos;
        lastInteractorPos = currentPos;
        if (delta.sqrMagnitude < 0.000001f) return;

        Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
        Vector3 camUp = mainCamera.transform.up;

        float horizontal = Vector3.Dot(delta, camRight);
        float vertical = Vector3.Dot(delta, camUp);

        float yawDegrees = -horizontal * rotationSensitivity;
        float pitchDegrees = vertical * rotationSensitivity;

        visualRoot.Rotate(Vector3.up, yawDegrees, Space.World);
        visualRoot.Rotate(camRight, pitchDegrees, Space.World);

        float rotatedThisFrame = Mathf.Abs(yawDegrees) + Mathf.Abs(pitchDegrees);
        TotalRotationDegrees += rotatedThisFrame;
        Rotated?.Invoke(rotatedThisFrame);
    }

    private void LateUpdate()
    {
        // Hard-anchor: no interactor or physics event may drag the gem away.
        if (transform.position != anchorPosition)
        {
            transform.position = anchorPosition;
        }
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        hasLastPos = false;
        currentHoldSeconds = 0f;
        GrabStarted?.Invoke();
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        hasLastPos = false;
        GrabEnded?.Invoke();
    }
}
