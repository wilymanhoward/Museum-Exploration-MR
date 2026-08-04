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
/// The project doesn't otherwise use OVRManager (rendering/passthrough go through AR
/// Foundation, not the classic OVRCameraRig), so this bootstraps a minimal, persistent
/// one purely to run the boundary-suppression request every frame - OVRManager's own
/// Update loop is what actually syncs the request to the OS. Self-installing via
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
        // spinning up an OVRManager here would just risk the same "no XR session" log spam
        // MRUK has in that situation.
        if (!XRSettings.isDeviceActive) return;
#endif
        if (FindObjectOfType<BoundarySuppressor>() != null) return;

        GameObject go = new GameObject("BoundarySuppressor");
        DontDestroyOnLoad(go);
        go.AddComponent<BoundarySuppressor>();
    }

    private OVRManager ovrManager;

    private void Awake()
    {
        ovrManager = FindObjectOfType<OVRManager>();
        if (ovrManager == null)
        {
            GameObject ovrGo = new GameObject("OVRManager (Boundary Suppression)");
            ovrGo.transform.SetParent(transform, false);
            ovrManager = ovrGo.AddComponent<OVRManager>();
        }
    }

    private void Update()
    {
        // Cheap to re-assert every frame; OVRManager's own Update is what re-checks this
        // against the live passthrough/system state each frame and syncs it to the OS.
        if (ovrManager != null)
        {
            ovrManager.shouldBoundaryVisibilityBeSuppressed = true;
        }
    }
}
