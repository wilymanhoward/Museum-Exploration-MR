using System;
using System.Collections.Generic;
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

    // Fast Scan Parameters (private constants to keep exact Unity serialization layout compatibility)
    private const float MaxScanDistance = 4.0f; // Maximum distance (meters) to scan QR code
    private const float MaxScanAngle = 45.0f;    // Maximum gaze angle (degrees) to scan QR code
    private const float ScanCooldown = 1.0f;    // Cooldown (seconds) before re-triggering same QR code

    // Events raised when a QR code is scanned or lost
    public static event Action<string, Pose> OnQRCodeScanned;
    public static event Action<string> OnQRCodeLost;

    private string lastSimulatedPayload = "";
    private string lastHardwarePayload = "";
    private float hardwareScanCooldown = 0f;
    private List<MRUKTrackable> activeTrackables = new List<MRUKTrackable>();

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
            Debug.LogWarning("QR Code Scanner: MRUK.Instance or SceneSettings is null.");
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

#if META_XR_SDK_PRESENT
        if (MRUK.Instance != null)
        {
            Transform camTransform = Camera.main != null ? Camera.main.transform : transform;
            Vector3 camPos = camTransform.position;
            Vector3 camForward = camTransform.forward;

            MRUK.Instance.GetTrackables(activeTrackables);
            bool facingAnyTargetQR = false;

            for (int i = 0; i < activeTrackables.Count; i++)
            {
                var trackable = activeTrackables[i];
                if (trackable != null && trackable.TrackableType == OVRAnchor.TrackableType.QRCode && trackable.IsTracked)
                {
                    string payload = trackable.MarkerPayloadString;
                    if (string.IsNullOrEmpty(payload)) continue;

                    Vector3 qrPos = trackable.transform.position;
                    Vector3 toQR = qrPos - camPos;
                    float dist = toQR.magnitude;
                    float angle = dist > 0.001f ? Vector3.Angle(camForward, toQR / dist) : 0f;

                    // Check if player is facing towards the QR code within scanning distance
                    bool isFacing = dist <= MaxScanDistance && angle <= MaxScanAngle;

                    if (isFacing)
                    {
                        facingAnyTargetQR = true;

                        // Trigger instantly if different payload or if cooldown expired
                        if (payload != lastHardwarePayload || hardwareScanCooldown <= 0f)
                        {
                            lastHardwarePayload = payload;
                            hardwareScanCooldown = ScanCooldown;

                            Pose pose = new Pose(qrPos, trackable.transform.rotation);
                            Debug.Log($"[QR Scanner] Fast Scan Triggered for '{payload}' (dist: {dist:F2}m, angle: {angle:F1}°)");
                            OnQRCodeScanned?.Invoke(payload, pose);
                        }
                    }
                }
            }

            // Reset active payload if the user turns away from the QR code, allowing immediate re-scan when facing back
            if (!facingAnyTargetQR && !string.IsNullOrEmpty(lastHardwarePayload) && hardwareScanCooldown <= ScanCooldown * 0.5f)
            {
                lastHardwarePayload = "";
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
        if (lastSimulatedPayload.StartsWith("artifact_") && lastSimulatedPayload != payload)
        {
            OnQRCodeLost?.Invoke(lastSimulatedPayload);
        }

        Transform camTransform = Camera.main != null ? Camera.main.transform : transform;
        
        Vector3 pos = camTransform.position + camTransform.forward * simulatedSpawnDistance;
        Quaternion rot = Quaternion.LookRotation(-camTransform.forward, Vector3.up);
        Pose simulatedPose = new Pose(pos, rot);

        lastSimulatedPayload = payload;
        Debug.Log($"[Simulated] Scanned QR Code: {payload} at Pose: {pos}");
        OnQRCodeScanned?.Invoke(payload, simulatedPose);
    }

#if META_XR_SDK_PRESENT
    private void OnMRUKTrackableAdded(MRUKTrackable trackable)
    {
        if (trackable.TrackableType == OVRAnchor.TrackableType.QRCode)
        {
            string payload = trackable.MarkerPayloadString;
            if (!string.IsNullOrEmpty(payload))
            {
                Transform camTransform = Camera.main != null ? Camera.main.transform : transform;
                Vector3 toQR = trackable.transform.position - camTransform.position;
                float dist = toQR.magnitude;
                float angle = dist > 0.001f ? Vector3.Angle(camTransform.forward, toQR / dist) : 0f;

                if (dist <= MaxScanDistance && angle <= MaxScanAngle)
                {
                    lastHardwarePayload = payload;
                    hardwareScanCooldown = ScanCooldown;

                    Pose pose = new Pose(trackable.transform.position, trackable.transform.rotation);
                    Debug.Log($"[MRUK Event] Fast QR Code Tracked: '{payload}' at Pose: {pose.position}");
                    OnQRCodeScanned?.Invoke(payload, pose);
                }
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
