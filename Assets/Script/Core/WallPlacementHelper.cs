using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Utility helper that calculates the optimal spawn pose for Mixed Reality detail panels
/// (Artifact Detail Panels and History Detail Panels).
///
/// When the user opens a panel, this helper detects whether the player is near a wall.
/// - If near a wall: Spawns the panel mounted flush on the wall surface at eye level facing the room.
/// - If not near a wall: Spawns the panel floating in front of the player at eye level.
///
/// Also handles horizontal tiling when multiple panels are open simultaneously.
/// </summary>
public static class WallPlacementHelper
{
    public const float DefaultMaxGazeDistance = 3.0f;
    public const float DefaultMaxProximityDistance = 2.0f;
    public const float DefaultWallOffset = 0.02f;
    public const float DefaultFloatingDistance = 1.0f;

    private static readonly LabelFilter WallOnlyFilter = new LabelFilter(MRUKAnchor.SceneLabels.WALL_FACE);

    /// <summary>
    /// Calculates the optimal spawn pose for a UI panel.
    /// </summary>
    /// <param name="playerTransform">Transform of the player's camera or headset.</param>
    /// <param name="panelIndex">Index for staggering multiple open panels side-by-side (0 = center, 1 = right, 2 = left, etc.).</param>
    /// <param name="panelSpacing">Horizontal spacing between adjacent panels in meters.</param>
    /// <param name="isWallMounted">Outputs true if a wall was detected and the panel is mounted on it; false if floating.</param>
    /// <param name="maxGazeDistance">Maximum distance in meters to look for a wall in front of the player.</param>
    /// <param name="maxProximityDistance">Maximum distance in meters to check for wall proximity around the player.</param>
    /// <param name="wallOffset">Gap in meters between wall surface and panel to prevent z-fighting.</param>
    /// <param name="defaultFloatingDistance">Distance in front of player when no wall is nearby.</param>
    /// <returns>World-space Pose (position and rotation) for the panel.</returns>
    public static Pose CalculatePlacementPose(
        Transform playerTransform,
        int panelIndex,
        float panelSpacing,
        out bool isWallMounted,
        float maxGazeDistance = DefaultMaxGazeDistance,
        float maxProximityDistance = DefaultMaxProximityDistance,
        float wallOffset = DefaultWallOffset,
        float defaultFloatingDistance = DefaultFloatingDistance)
    {
        isWallMounted = false;

        // Resolve camera / player transform
        Transform cam = playerTransform;
        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.transform;
        }

        Vector3 headPos = cam != null ? cam.position : Vector3.zero;
        Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
        if (forward == Vector3.zero) forward = Vector3.forward;

        float eyeLevelY = headPos.y;

        // Calculate horizontal offset for multiple open panels:
        // Index 0: 0, Index 1: +spacing, Index 2: -spacing, Index 3: +2*spacing, etc.
        float sideOffset = 0f;
        if (panelIndex > 0)
        {
            sideOffset = (panelIndex % 2 == 1)
                ? ((panelIndex + 1) / 2) * panelSpacing
                : -(panelIndex / 2) * panelSpacing;
        }

        Vector3 wallPoint = Vector3.zero;
        Vector3 wallNormal = Vector3.zero;
        bool foundWall = false;

        // 1. Meta MRUK Scene Model Check (Primary)
        if (MRUK.Instance != null)
        {
            MRUKRoom room = MRUK.Instance.GetCurrentRoom();
            if (room != null)
            {
                // A) Cast ray straight ahead at eye level in the forward direction
                Ray gazeRay = new Ray(headPos, forward);
                if (room.Raycast(gazeRay, maxGazeDistance, WallOnlyFilter, out RaycastHit gazeHit, out MRUKAnchor hitAnchor))
                {
                    wallPoint = gazeHit.point;
                    wallNormal = gazeHit.normal;
                    foundWall = true;
                }
                // B) If direct gaze didn't hit, check for closest wall surface within proximity
                else
                {
                    float dist = room.TryGetClosestSurfacePosition(headPos, out Vector3 surfacePos, out MRUKAnchor closestAnchor, out Vector3 surfaceNormal, WallOnlyFilter);
                    if (dist <= maxProximityDistance && closestAnchor != null)
                    {
                        // Ensure the wall is in the forward/side arc of the player (not directly behind)
                        Vector3 dirToSurface = (surfacePos - headPos).normalized;
                        if (Vector3.Dot(forward, dirToSurface) > -0.2f)
                        {
                            wallPoint = surfacePos;
                            wallNormal = surfaceNormal;
                            foundWall = true;
                        }
                    }
                }
            }
        }

        // 2. Physics Raycast Fallback (for Editor testing or colliders in scene)
        if (!foundWall)
        {
            Ray physRay = new Ray(headPos, forward);
            if (Physics.Raycast(physRay, out RaycastHit physHit, maxGazeDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                // Check if hit surface is vertical (like a wall, normal.y near 0)
                if (Mathf.Abs(physHit.normal.y) < 0.35f)
                {
                    wallPoint = physHit.point;
                    wallNormal = physHit.normal;
                    foundWall = true;
                }
            }
        }

        // 3. Construct Wall-Mounted Pose at Eye Level
        if (foundWall)
        {
            // Wall normal projected on horizontal plane
            Vector3 hNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up).normalized;
            if (hNormal == Vector3.zero) hNormal = -forward;

            // Horizontal tangent along the wall (to the right when facing the wall)
            Vector3 wallRight = Vector3.Cross(Vector3.up, hNormal).normalized;

            // Set height to player eye level
            Vector3 targetPos = new Vector3(wallPoint.x, eyeLevelY, wallPoint.z);

            // Push out from wall surface along normal so panel sits flush and doesn't clip
            targetPos += hNormal * wallOffset;

            // Apply horizontal offset for multiple open panels
            targetPos += wallRight * sideOffset;

            // Face away from the wall (into the room / towards the player)
            Quaternion targetRot = Quaternion.LookRotation(-hNormal, Vector3.up);

            isWallMounted = true;
            Debug.Log($"[WallPlacementHelper] Wall detected! Spawning panel on wall at eye level (Y={eyeLevelY:F2}m, Dist={Vector3.Distance(headPos, targetPos):F2}m).");
            return new Pose(targetPos, targetRot);
        }

        // 4. Fallback: Eye-Level Floating Pose in Front of Player
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 floatPos = headPos + forward * defaultFloatingDistance + right * sideOffset;
        floatPos.y = eyeLevelY; // Eye level!

        // Rotation facing the player
        Quaternion floatRot = Quaternion.LookRotation(forward, Vector3.up);

        isWallMounted = false;
        Debug.Log($"[WallPlacementHelper] No wall nearby. Spawning floating panel at eye level in front of player (Y={eyeLevelY:F2}m).");
        return new Pose(floatPos, floatRot);
    }
}
