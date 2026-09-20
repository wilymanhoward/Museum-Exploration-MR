using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Utility helper that calculates the optimal spawn pose for Mixed Reality detail panels
/// (Artifact Detail Panels and History Detail Panels).
///
/// When the user opens a panel, this helper ensures it ALWAYS spawns in front of the player
/// at eye level (never on the floor or below the player):
/// - If facing a real-world wall in front of the player (<= 2.5m, forward cone): Spawns the panel
///   mounted flush on the wall surface at eye level facing the room.
/// - If not facing a wall: Spawns the panel floating directly in front of the player at eye level.
///
/// Also handles horizontal tiling when multiple panels are open simultaneously,
/// and provides robust camera resolution even when Camera.main lacks the 'MainCamera' tag.
/// </summary>
public static class WallPlacementHelper
{
    public const float DefaultMaxGazeDistance = 1.30f;
    public const float DefaultMaxProximityDistance = 1.20f;
    public const float DefaultWallOffset = 0.02f;
    public const float DefaultFloatingDistance = 0.90f;
    public const float DefaultEyeLevelHeight = 1.45f;

    private static readonly LabelFilter WallOnlyFilter = new LabelFilter(MRUKAnchor.SceneLabels.WALL_FACE);
    private static Camera cachedCamera;

    /// <summary>
    /// Resolves the active camera or headset camera in the scene.
    /// Handles cases where Camera.main is null (e.g. XR camera without 'MainCamera' tag).
    /// If an untagged active camera is found, tags it 'MainCamera' for future lookups.
    /// </summary>
    public static Camera ResolveCamera(Camera preferred = null)
    {
        if (preferred != null) return preferred;
        if (Camera.main != null) return Camera.main;
        if (cachedCamera != null && cachedCamera.gameObject.activeInHierarchy) return cachedCamera;

        Camera[] cameras = Object.FindObjectsOfType<Camera>();
        foreach (Camera c in cameras)
        {
            if (c != null && c.enabled && c.gameObject.activeInHierarchy)
            {
                if (c.name.Contains("Main") || c.name.Contains("Eye") || c.name.Contains("Center") || c.name.Contains("Head"))
                {
                    cachedCamera = c;
                    try { c.tag = "MainCamera"; } catch { }
                    return c;
                }
            }
        }
        foreach (Camera c in cameras)
        {
            if (c != null && c.enabled && c.gameObject.activeInHierarchy)
            {
                cachedCamera = c;
                try { c.tag = "MainCamera"; } catch { }
                return c;
            }
        }
        return null;
    }

    /// <summary>
    /// Resolves the Transform of the player's camera or headset.
    /// </summary>
    public static Transform ResolveCameraTransform(Transform overrideTransform = null)
    {
        if (overrideTransform != null) return overrideTransform;
        Camera cam = ResolveCamera();
        return cam != null ? cam.transform : null;
    }

    /// <summary>
    /// Calculates the optimal spawn pose for a UI panel.
    /// Always places the panel directly in front of the player at eye level.
    /// </summary>
    /// <param name="playerTransform">Transform of the player's camera or headset.</param>
    /// <param name="panelIndex">Index for staggering multiple open panels side-by-side (0 = center, 1 = right, 2 = left, etc.).</param>
    /// <param name="panelSpacing">Horizontal spacing between adjacent panels in meters.</param>
    /// <param name="isWallMounted">Outputs true if a wall was detected and the panel is mounted on it; false if floating.</param>
    /// <param name="preferFloating">If true, skips wall search and spawns floating directly in front of the player at arm's length.</param>
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
        bool preferFloating = false,
        float maxGazeDistance = DefaultMaxGazeDistance,
        float maxProximityDistance = DefaultMaxProximityDistance,
        float wallOffset = DefaultWallOffset,
        float defaultFloatingDistance = DefaultFloatingDistance)
    {
        isWallMounted = false;

        // Resolve camera / player transform robustly
        Transform cam = ResolveCameraTransform(playerTransform);

        Vector3 headPos;
        Vector3 forward;
        float eyeLevelY;

        if (cam != null)
        {
            headPos = cam.position;
            forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(cam.up, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

            // In VR room-scale, headset Y is usually 1.3m - 1.8m.
            // If headset tracking is near floor (Y < 0.5m) or uncalibrated, clamp to comfortable eye level (1.45m).
            eyeLevelY = (headPos.y > 0.5f) ? headPos.y : DefaultEyeLevelHeight;
            headPos.y = eyeLevelY;
        }
        else
        {
            headPos = new Vector3(0f, DefaultEyeLevelHeight, 0f);
            forward = Vector3.forward;
            eyeLevelY = DefaultEyeLevelHeight;
        }

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

        // Only search for walls if floating in front is not specifically requested
        if (!preferFloating)
        {
            // 1. Meta MRUK Scene Model Check (Primary)
            if (MRUK.Instance != null)
            {
                MRUKRoom room = MRUK.Instance.GetCurrentRoom();
                if (room != null)
                {
                    // A) Cast ray straight ahead at eye level in the forward gaze direction
                    Ray gazeRay = new Ray(headPos, forward);
                    if (room.Raycast(gazeRay, maxGazeDistance, WallOnlyFilter, out RaycastHit gazeHit, out MRUKAnchor hitAnchor))
                    {
                        wallPoint = gazeHit.point;
                        wallNormal = gazeHit.normal;
                        foundWall = true;
                    }
                    // B) Forward-cone proximity check: Only accept walls directly IN FRONT of player
                    else
                    {
                        float dist = room.TryGetClosestSurfacePosition(headPos, out Vector3 surfacePos, out MRUKAnchor closestAnchor, out Vector3 surfaceNormal, WallOnlyFilter);
                        if (dist <= maxProximityDistance && closestAnchor != null)
                        {
                            Vector3 dirToSurface = (surfacePos - headPos).normalized;
                            // ONLY accept walls in a forward cone (<= ~45 degrees, Dot > 0.70f).
                            // Never snap to walls beside or behind the player!
                            if (Vector3.Dot(forward, dirToSurface) > 0.70f)
                            {
                                wallPoint = surfacePos;
                                wallNormal = surfaceNormal;
                                foundWall = true;
                            }
                        }
                    }
                }
            }

            // 2. Physics Raycast Fallback (for Editor testing or physical scene colliders)
            if (!foundWall)
            {
                Ray physRay = new Ray(headPos, forward);
                int layerMask = ~LayerMask.GetMask("UI", "Ignore Raycast");
                if (Physics.Raycast(physRay, out RaycastHit physHit, maxGazeDistance, layerMask, QueryTriggerInteraction.Ignore))
                {
                    // Must be vertical surface and facing toward the player, and not a UI canvas/button
                    if (Mathf.Abs(physHit.normal.y) < 0.35f && Vector3.Dot(physHit.normal, forward) < -0.4f)
                    {
                        if (physHit.collider.GetComponentInParent<Canvas>() == null)
                        {
                            wallPoint = physHit.point;
                            wallNormal = physHit.normal;
                            foundWall = true;
                        }
                    }
                }
            }
        }

        // 3. Construct Wall-Mounted Pose at Eye Level
        if (foundWall)
        {
            Vector3 hNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up).normalized;
            if (hNormal == Vector3.zero) hNormal = -forward;

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
            Debug.Log($"[WallPlacementHelper] Wall detected in front of player! Spawning panel on wall at eye level (Y={eyeLevelY:F2}m, Dist={Vector3.Distance(headPos, targetPos):F2}m).");
            return new Pose(targetPos, targetRot);
        }

        // 4. Fallback: Eye-Level Floating Pose in Front of Player
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float targetY = (headPos.y > 0.5f) ? headPos.y - 0.05f : eyeLevelY;
        Vector3 floatPos = headPos + forward * defaultFloatingDistance + right * sideOffset;
        floatPos.y = targetY; // Guaranteed comfortable eye-level reading height!

        // Rotation facing the player
        Quaternion floatRot = Quaternion.LookRotation(forward, Vector3.up);

        isWallMounted = false;
        Debug.Log($"[WallPlacementHelper] Spawning floating panel directly in front of player at eye level (Pos={floatPos}, Dist={defaultFloatingDistance:F2}m).");
        return new Pose(floatPos, floatRot);
    }
}
