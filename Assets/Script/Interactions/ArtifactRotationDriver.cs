using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Handles intuitive rotation of a pinned artifact via XR interaction when grabbed with one hand,
/// and translation/scaling/rotation when grabbed with both hands.
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class ArtifactRotationDriver : MonoBehaviour
{
    [Tooltip("Degrees of rotation per meter of hand movement. Higher = more responsive.")]
    public float rotationSensitivity = 350f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Camera mainCamera;

    // Track states across frames
    private int activeInteractorsCount = 0;
    private Vector3 lastSingleInteractorPos;
    private Vector3 lastMidpoint;
    private float lastDistance;
    private Vector3 lastDir;

    private void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        
        // Allow both hands to grab the artifact simultaneously
        grabInteractable.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple;
        
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
        // Resolve the camera reference
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Camera cam = FindObjectOfType<Camera>();
                if (cam != null) mainCamera = cam;
            }
        }
        
        // Reset tracking on grab state change
        activeInteractorsCount = 0;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Reset tracking on grab state change
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
            UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor = grabInteractable.interactorsSelecting[0];
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

            // Project the hand-movement delta into the viewer's camera-relative directions:
            Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
            Vector3 camUp = mainCamera.transform.up;

            float horizontal = Vector3.Dot(delta, camRight);
            float vertical = Vector3.Dot(delta, camUp);

            // Hand moves right  → positive Y rotation (artifact face turns right)
            // Hand moves left   → negative Y rotation (artifact face turns left)
            transform.Rotate(Vector3.up, -horizontal * rotationSensitivity, Space.World);

            // Hand moves up     → artifact tilts upward
            // Hand moves down   → artifact tilts downward
            transform.Rotate(camRight, vertical * rotationSensitivity, Space.World);
        }
        else if (grabCount >= 2)
        {
            UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor1 = grabInteractable.interactorsSelecting[0];
            UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor2 = grabInteractable.interactorsSelecting[1];

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

            // Translate (move) the artifact along with the midpoint of the hands
            Vector3 translationDelta = currentMidpoint - lastMidpoint;
            transform.position += translationDelta;

            // Scale (zoom) the artifact based on distance between hands
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
        return interactor.GetAttachTransform(grabInteractable).position;
    }
}
