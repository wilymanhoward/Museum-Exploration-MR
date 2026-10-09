using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Suppresses the Meta Quest Guardian boundary grid so players can walk freely through
/// this passthrough museum experience, instead of the boundary fading in and interrupting
/// immersion whenever they approach its edge.
///
/// Important: this does NOT disable Meta's underlying safety system - the headset OS can
/// still intervene if it judges necessary, and no third-party app is allowed to override
/// that. What this DOES do is request that the VISUAL grid be suppressed, which the OS
/// grants for passthrough experiences since the player can already see their real
/// surroundings through the camera and doesn't need a grid warning drawn over it - the
/// same behaviour you see in other passthrough/MR apps that don't show Guardian walls.
/// See OVRManager.shouldBoundaryVisibilityBeSuppressed. Per Meta's own docs this only
/// takes effect once passthrough is active - this app already has that running via AR
/// Foundation's Meta OpenXR camera feature, so this script only drives the boundary
/// request; it does not touch passthrough itself.
///
/// Uses Meta's Boundary API ("contextual boundaryless"): the boundary is suppressed only once
/// exploration has started (MULAI pressed, background faded to passthrough) and restored while
/// the black intro/main menu is showing, when the player can't see the room. Requires
/// com.oculus.permission.BOUNDARY_VISIBILITY (manifest) and Boundary Visibility Support in the
/// OVR project config. The request goes straight to OVRPlugin: OVRManager only forwards it when
/// its own Insight passthrough is enabled, but this app's passthrough runs through AR
/// Foundation, so OVRManager's path never sent it. Self-installing via
/// RuntimeInitializeOnLoadMethod, so no scene wiring is needed and it survives scene loads.
/// </summary>
public class BoundarySuppressor : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_EDITOR
        // Mirrors EditorMRUKGuard's own guard: without a live OVR/OpenXR session (no Quest
        // Link, no Meta XR Simulator), there is nothing to suppress the boundary on and
        // calling into OVRPlugin here would just risk the same "no XR session" log spam
        // MRUK has in that situation.
        if (!XRSettings.isDeviceActive) return;
#endif
        if (FindObjectOfType<BoundarySuppressor>() != null) return;

        GameObject go = new GameObject("BoundarySuppressor");
        DontDestroyOnLoad(go);
        go.AddComponent<BoundarySuppressor>();
    }

    // How often to retry while the system state doesn't match the desired state yet
    // (e.g. passthrough not composited yet right after the fade, or the request was refused).
    private const float RetryInterval = 0.5f;

    private float nextRequestTime;
    private bool? lastAppliedSuppressed;
    private OVRPlugin.Result lastLoggedResult;

    private void Update()
    {
        if (Time.unscaledTime < nextRequestTime) return;
        nextRequestTime = Time.unscaledTime + RetryInterval;

        bool wantSuppressed = MainMenu.IsExplorationStarted;
        if (lastAppliedSuppressed == wantSuppressed) return;

        OVRPlugin.Result result = OVRPlugin.RequestBoundaryVisibility(wantSuppressed
            ? OVRPlugin.BoundaryVisibility.Suppressed
            : OVRPlugin.BoundaryVisibility.NotSuppressed);

        if (result == OVRPlugin.Result.Success)
        {
            lastAppliedSuppressed = wantSuppressed;
            Debug.Log($"BoundarySuppressor: boundary {(wantSuppressed ? "suppressed" : "restored")}.");
        }
        else if (result == OVRPlugin.Result.Failure_Unsupported || result == OVRPlugin.Result.Failure_NotYetImplemented)
        {
            // Runtime/OS without the Boundary API - nothing to retry.
            Debug.LogWarning($"BoundarySuppressor: Boundary API unavailable ({result}).");
            enabled = false;
        }
        else if (result != lastLoggedResult)
        {
            // Warning_BoundaryVisibilitySuppressionNotAllowed = the OS didn't see passthrough yet; keep retrying.
            lastLoggedResult = result;
            Debug.LogWarning($"BoundarySuppressor: request refused ({result}), retrying.");
        }
    }
}
