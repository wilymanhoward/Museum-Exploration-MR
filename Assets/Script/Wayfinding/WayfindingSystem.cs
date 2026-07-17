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
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false; // Disable the renderer
        }
    }

    private void Start()
    {
    }

    private void Update()
    {
    }

    /// <summary>
    /// Configures the wayfinding path to lead to a new set of coordinates.
    /// </summary>
    public void SetPath(Vector3[] waypoints)
    {
        // Wayfinding floor lines have been disabled by user request
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Clears the path and hides the wayfinding system.
    /// </summary>
    public void ClearPath()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        gameObject.SetActive(false);
    }
}
