using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Handles intuitive rotation of a pinned artifact via XR interaction.
/// Hand moving right → artifact rotates right (from viewer's perspective).
/// Hand moving up → artifact tilts upward.
/// This replaces XRGrabInteractable's built-in trackRotation which has inverted behavior.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class ArtifactRotationDriver : MonoBehaviour
{
    [Tooltip("Degrees of rotation per meter of hand movement. Higher = more responsive.")]
    public float rotationSensitivity = 350f;

    private XRGrabInteractable grabInteractable;
    private IXRSelectInteractor activeInteractor;
    private Vector3 lastInteractorPos;
    private Camera mainCamera;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        activeInteractor = args.interactorObject;
        lastInteractorPos = GetInteractorPos();

        // Resolve the camera reference
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Camera cam = FindObjectOfType<Camera>();
            if (cam != null) mainCamera = cam;
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        activeInteractor = null;
    }

    private void Update()
    {
        if (activeInteractor == null || mainCamera == null) return;

        Vector3 currentPos  = GetInteractorPos();
        Vector3 delta       = currentPos - lastInteractorPos;
        lastInteractorPos   = currentPos;

        if (delta.sqrMagnitude < 0.000001f) return;

        // Project the hand-movement delta into the viewer's camera-relative directions:
        //   Horizontal component → spin around world-up axis (Y)
        //   Vertical component   → tilt around the camera's right axis
        Vector3 camRight    = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
        Vector3 camUp       = mainCamera.transform.up;

        float horizontal    = Vector3.Dot(delta, camRight);
        float vertical      = Vector3.Dot(delta, camUp);

        // Hand moves right  → positive Y rotation (artifact face turns right) ✅
        // Hand moves left   → negative Y rotation (artifact face turns left)  ✅
        transform.Rotate(Vector3.up, horizontal * rotationSensitivity, Space.World);

        // Hand moves up     → artifact tilts upward  ✅
        // Hand moves down   → artifact tilts downward ✅
        transform.Rotate(camRight, -vertical * rotationSensitivity, Space.World);
    }

    private Vector3 GetInteractorPos()
    {
        return activeInteractor.GetAttachTransform(grabInteractable).position;
    }
}
