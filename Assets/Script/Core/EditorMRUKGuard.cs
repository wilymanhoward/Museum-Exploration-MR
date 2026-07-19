#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Editor-only: when entering Play mode WITHOUT a live XR session (no Meta XR
/// Simulator, no Quest Link), disables the MRUK GameObject so it stops logging
/// "Open XR session is not available" every frame while trying to configure the
/// QR-code tracker. QR scans can still be simulated with the QRCodeScannerDebugger
/// hotkeys (1-5 rooms, 6-8 artifacts, 9/G/0 games, X = lost).
/// Uses a type-name lookup instead of a hard reference so it compiles regardless
/// of scripting defines.
/// </summary>
public static class EditorMRUKGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void DisableMRUKWithoutXRSession()
    {
        // A device is active when the Meta XR Simulator or Quest Link provides a
        // real OpenXR session - in that case leave MRUK running.
        if (XRSettings.isDeviceActive) return;

        foreach (MonoBehaviour mb in Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (mb != null && mb.GetType().FullName == "Meta.XR.MRUtilityKit.MRUK")
            {
                mb.gameObject.SetActive(false);
                Debug.Log("EditorMRUKGuard: No XR session detected in Editor Play mode - " +
                          "disabled the MRUK GameObject to stop 'Open XR session is not available' spam. " +
                          "Use QRCodeScannerDebugger hotkeys to simulate QR scans (G = batik game).");
                break;
            }
        }
    }
}
#endif
