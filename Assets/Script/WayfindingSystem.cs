using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WayfindingSystem : MonoBehaviour
{
    [Header("Line Configurations")]
    [Tooltip("The vertical offset above the floor to render the line (prevents z-fighting).")]
    public float floorOffset = 0.05f;

    [Tooltip("Speed at which the floor line arrows or texture scroll.")]
    public float scrollSpeed = 1.5f;

    [Tooltip("Player/Camera reference to start the line from.")]
    public Transform playerTransform;

    private LineRenderer lineRenderer;
    private Vector3[] pathWaypoints;
    private Material lineMaterial;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        
        // Ensure the line renderer does not use world space for position calculation if we manually set positions
        lineRenderer.useWorldSpace = true;
        
        // Grab material to scroll texture offset
        if (lineRenderer.material != null)
        {
            lineMaterial = lineRenderer.material;
        }
    }

    private void Start()
    {
        // Fallback for player transform
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        // 1. Update the line's starting point to follow the player
        if (pathWaypoints != null && pathWaypoints.Length > 0 && playerTransform != null)
        {
            // Project player position onto the waypoints' Y-axis (floor height)
            Vector3 playerFloorPos = playerTransform.position;
            playerFloorPos.y = pathWaypoints[0].y + floorOffset;

            lineRenderer.SetPosition(0, playerFloorPos);
        }

        // 2. Animate texture offset to create a moving/guiding effect (e.g. scrolling arrows)
        if (lineMaterial != null && scrollSpeed != 0)
        {
            float offset = Time.time * scrollSpeed;
            // Scroll along the U direction
            lineMaterial.SetTextureOffset("_MainTex", new Vector2(-offset, 0));
        }
    }

    /// <summary>
    /// Configures the wayfinding path to lead to a new set of coordinates.
    /// </summary>
    public void SetPath(Vector3[] waypoints)
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            pathWaypoints = null;
            lineRenderer.positionCount = 0;
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        pathWaypoints = waypoints;

        // Total positions = Player position (index 0) + all waypoints
        lineRenderer.positionCount = waypoints.Length + 1;

        // Populate waypoints starting from index 1 (index 0 is reserved for player in Update)
        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 offsetPoint = waypoints[i];
            offsetPoint.y += floorOffset; // Prevent z-fighting with the floor
            lineRenderer.SetPosition(i + 1, offsetPoint);
        }

        Debug.Log($"Wayfinding path updated with {waypoints.Length} room waypoints.");
    }

    /// <summary>
    /// Clears the path and hides the wayfinding system.
    /// </summary>
    public void ClearPath()
    {
        pathWaypoints = null;
        lineRenderer.positionCount = 0;
        gameObject.SetActive(false);
    }
}
