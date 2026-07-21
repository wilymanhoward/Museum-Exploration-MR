using System;
using UnityEngine;
#if META_XR_SDK_PRESENT
using Meta.XR.MRUtilityKit;
#endif

public class QRCodeScanner : MonoBehaviour
{
    public static QRCodeScanner Instance { get; private set; }

    [Header("Editor Simulator Settings")]
    [Tooltip("If true, keyboard inputs can be used in the Editor to simulate QR scanning.")]
    public bool enableEditorSimulation = true;

    [Tooltip("Distance from the camera where a simulated QR code will be spawned.")]
    public float simulatedSpawnDistance = 1.5f;

    // Events raised when a QR code is scanned or lost
    public static event Action<string, Pose> OnQRCodeScanned;
    public static event Action<string> OnQRCodeLost;

    private string lastSimulatedPayload = "";
    private string lastHardwarePayload = "";
    private float hardwareScanCooldown = 0f;
    private System.Collections.Generic.List<MRUKTrackable> activeTrackables = new System.Collections.Generic.List<MRUKTrackable>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
#if META_XR_SDK_PRESENT
        // Register MRUK QR code tracking callbacks if using Meta XR SDK
        if (MRUK.Instance != null && MRUK.Instance.SceneSettings != null)
        {
            MRUK.Instance.SceneSettings.TrackableAdded.AddListener(OnMRUKTrackableAdded);
            MRUK.Instance.SceneSettings.TrackableRemoved.AddListener(OnMRUKTrackableRemoved);
            Debug.Log("QR Code Scanner: Subscribed to Meta MRUK events.");
        }
        else
        {
            Debug.LogWarning("QR Code Scanner: MRUK.Instance or SceneSettings is null. Headset QR scanning will not trigger.");
        }
#else
        Debug.Log("QR Code Scanner: META_XR_SDK_PRESENT define is not active. Running in Editor/Simulation mode.");
#endif
    }

    private void OnDestroy()
    {
#if META_XR_SDK_PRESENT
        if (MRUK.Instance != null && MRUK.Instance.SceneSettings != null)
        {
            MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(OnMRUKTrackableAdded);
            MRUK.Instance.SceneSettings.TrackableRemoved.RemoveListener(OnMRUKTrackableRemoved);
        }
#endif
    }

    private void Update()
    {
        // Update scan cooldown timer
        if (hardwareScanCooldown > 0f)
        {
            hardwareScanCooldown -= Time.deltaTime;
        }

        // Keyboard simulation handled externally by QRCodeScannerDebugger

#if META_XR_SDK_PRESENT
        // Poll active trackables every frame. GetTrackables() only reads an already-populated
        // in-memory dictionary (no native/IPC call), so this is cheap - polling every 5 frames
        // was adding up to ~80ms of unnecessary latency on top of the OS-level marker tracker's
        // own detection time for no real performance benefit.
        if (MRUK.Instance != null)
        {
            MRUK.Instance.GetTrackables(activeTrackables);
            for (int i = 0; i < activeTrackables.Count; i++)
            {
                var trackable = activeTrackables[i];
                if (trackable != null && trackable.TrackableType == OVRAnchor.TrackableType.QRCode && trackable.IsTracked)
                {
                    string payload = trackable.MarkerPayloadString;
                    if (!string.IsNullOrEmpty(payload))
                    {
                        // Trigger if it's a different QR code, or if the cooldown on the same code expired
                        if (payload != lastHardwarePayload || hardwareScanCooldown <= 0f)
                        {
                            lastHardwarePayload = payload;
                            hardwareScanCooldown = 3.0f; // 3s cooldown for the same QR to prevent duplicate triggers
                            
                            Pose pose = new Pose(trackable.transform.position, trackable.transform.rotation);
                            Debug.Log($"[MRUK Poll] QR Code Scan Triggered: {payload}");
                            OnQRCodeScanned?.Invoke(payload, pose);
                        }
                    }
                }
            }
        }
#endif
    }

    /// <summary>
    /// Simulates walking away or losing track of the active simulated QR code.
    /// </summary>
    public void SimulateLostScan()
    {
        if (!string.IsNullOrEmpty(lastSimulatedPayload))
        {
            Debug.Log($"[Simulated] QR Code Lost: {lastSimulatedPayload}");
            OnQRCodeLost?.Invoke(lastSimulatedPayload);
            lastSimulatedPayload = "";
        }
    }

    /// <summary>
    /// Simulates a QR code scan by generating a Pose in front of the main camera.
    /// </summary>
    public void SimulateScan(string payload)
    {
        // If scanning a new artifact/room, trigger loss of the previous one if it was an artifact
        if (lastSimulatedPayload.StartsWith("artifact_") && lastSimulatedPayload != payload)
        {
            OnQRCodeLost?.Invoke(lastSimulatedPayload);
        }

        Transform camTransform = Camera.main != null ? Camera.main.transform : transform;
        
        // Spawn the simulated QR code pose in front of the camera, looking at the camera
        Vector3 pos = camTransform.position + camTransform.forward * simulatedSpawnDistance;
        // Make the pose face the camera
        Quaternion rot = Quaternion.LookRotation(-camTransform.forward, Vector3.up);
        Pose simulatedPose = new Pose(pos, rot);

        lastSimulatedPayload = payload;
        Debug.Log($"[Simulated] Scanned QR Code: {payload} at Pose: {pos}");
        OnQRCodeScanned?.Invoke(payload, simulatedPose);
    }

#if META_XR_SDK_PRESENT
    private void OnMRUKTrackableAdded(MRUKTrackable trackable)
    {
        // Check if the trackable is a QR Code
        if (trackable.TrackableType == OVRAnchor.TrackableType.QRCode)
        {
            string payload = trackable.MarkerPayloadString;
            if (!string.IsNullOrEmpty(payload))
            {
                lastHardwarePayload = payload;
                hardwareScanCooldown = 3.0f;

                Pose pose = new Pose(trackable.transform.position, trackable.transform.rotation);
                Debug.Log($"[MRUK Event] QR Code Tracked: {payload} at Pose: {pose.position}");
                OnQRCodeScanned?.Invoke(payload, pose);
            }
        }
    }

    private void OnMRUKTrackableRemoved(MRUKTrackable trackable)
    {
        if (trackable.TrackableType == OVRAnchor.TrackableType.QRCode)
        {
            string payload = trackable.MarkerPayloadString;
            Debug.Log($"[MRUK Event] QR Code Lost: {payload}");
            OnQRCodeLost?.Invoke(payload);
        }
    }
#endif
}
