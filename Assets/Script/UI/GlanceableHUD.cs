using UnityEngine;

public class GlanceableHUD : MonoBehaviour
{
    [Header("Follow Target")]
    [Tooltip("The player's camera to follow. Will default to Camera.main if not assigned.")]
    public Transform cameraTransform;

    [Header("HUD Offset")]
    [Tooltip("Position offset relative to the camera. Negative X is Left, Positive Y is Up, Positive Z is Forward.")]
    public Vector3 positionOffset = new Vector3(-0.4f, 0.2f, 1.2f);

    [Header("Lazy Follow Tuning")]
    [Tooltip("How fast the position catches up to the target.")]
    [Range(0.1f, 10f)]
    public float positionLerpSpeed = 2.0f;

    [Tooltip("How fast the rotation catches up to the target.")]
    [Range(0.1f, 10f)]
    public float rotationSlerpSpeed = 3.5f;

    [Header("Deadzone Thresholds")]
    [Tooltip("If true, the HUD only moves when the headset leaves the comfort zone threshold.")]
    public bool useDeadzone = true;

    [Tooltip("Distance threshold (meters) before the HUD starts moving.")]
    public float distanceThreshold = 0.15f;

    [Tooltip("Angle threshold (degrees) before the HUD starts rotating to face the player.")]
    public float angleThreshold = 15.0f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isMoving = false;

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Snap to initial position at start to avoid a long travel animation
        if (cameraTransform != null)
        {
            transform.position = cameraTransform.TransformPoint(positionOffset);
            transform.rotation = GetTargetRotation();
        }
    }

    private void Update()
    {
        if (cameraTransform == null)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                Camera cam = FindObjectOfType<Camera>();
                if (cam != null)
                {
                    cameraTransform = cam.transform;
                }
            }
        }

        if (cameraTransform == null) return;

        // Calculate where the HUD *should* ideally be in world space
        Vector3 idealPosition = cameraTransform.TransformPoint(positionOffset);
        Quaternion idealRotation = GetTargetRotation();

        if (useDeadzone)
        {
            float dist = Vector3.Distance(transform.position, idealPosition);
            float angle = Quaternion.Angle(transform.rotation, idealRotation);

            // If we exceed either threshold, we start moving
            if (dist > distanceThreshold || angle > angleThreshold)
            {
                isMoving = true;
            }

            // Once moving, we continue until we get very close to the ideal pose
            if (isMoving)
            {
                transform.position = Vector3.Lerp(transform.position, idealPosition, Time.deltaTime * positionLerpSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, idealRotation, Time.deltaTime * rotationSlerpSpeed);

                if (Vector3.Distance(transform.position, idealPosition) < 0.02f && Quaternion.Angle(transform.rotation, idealRotation) < 1.0f)
                {
                    isMoving = false;
                }
            }
        }
        else
        {
            // Continuous smooth follow
            transform.position = Vector3.Lerp(transform.position, idealPosition, Time.deltaTime * positionLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, idealRotation, Time.deltaTime * rotationSlerpSpeed);
        }
    }

    private Quaternion GetTargetRotation()
    {
        // The HUD should billboard to face the player's camera
        Vector3 directionToCamera = cameraTransform.position - transform.position;
        if (directionToCamera != Vector3.zero)
        {
            // Assuming the HUD canvas is facing forward, we want it to look at the camera.
            // If the canvas faces its -Z direction, we look in the direction pointing away from the camera.
            // For general canvas HUDs, LookRotation(-directionToCamera) makes it face the camera.
            return Quaternion.LookRotation(-directionToCamera);
        }
        return transform.rotation;
    }
}
