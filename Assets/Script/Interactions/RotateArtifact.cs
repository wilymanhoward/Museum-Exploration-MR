using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class RotateArtifact : MonoBehaviour
{
    [Tooltip("Degrees of rotation per meter of hand movement. Higher = more responsive.")]
    public float rotationSensitivity = 350f;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Camera mainCamera;

    // Track active spawned model and its ID
    private GameObject spawnedModel;
    private string activeArtifactId;

    // Track single and double hand selection states across frames
    private int activeInteractorsCount = 0;
    private Vector3 lastSingleInteractorPos;
    private Vector3 lastMidpoint;
    private float lastDistance;
    private Vector3 lastDir;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

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

        // 2. Reset local rotation
        transform.localRotation = Quaternion.identity;

        if (prefab == null) return null;

        activeArtifactId = artifactId;

        // 3. Instantiate under the spawner
        spawnedModel = Instantiate(prefab, transform.position, transform.rotation, transform);
        spawnedModel.transform.localPosition = Vector3.zero;
        spawnedModel.transform.localRotation = Quaternion.identity;

        // Compensate for parent canvas scale so the model is rendered at its true physical size (1:1 with prefab scale)
        Vector3 worldScale = transform.lossyScale;
        Vector3 prefabScale = prefab.transform.localScale;
        spawnedModel.transform.localScale = new Vector3(
            worldScale.x != 0 ? prefabScale.x / worldScale.x : prefabScale.x,
            worldScale.y != 0 ? prefabScale.y / worldScale.y : prefabScale.y,
            worldScale.z != 0 ? prefabScale.z / worldScale.z : prefabScale.z
        );

        return spawnedModel;
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
        activeInteractorsCount = 0;

        // Mark artifact completed on grab
        if (!string.IsNullOrEmpty(activeArtifactId) && ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.MarkArtifactInteracted(activeArtifactId);
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        activeInteractorsCount = 0;
    }

    private void Update()
    {
        if (grabInteractable == null || mainCamera == null) return;

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

    private Vector3 GetInteractorPos(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor)
    {
        if (interactor is MonoBehaviour mb)
        {
            return mb.transform.position;
        }
        return transform.position;
    }
}
