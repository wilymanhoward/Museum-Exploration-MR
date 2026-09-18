using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Authoritative Dual-Modality Manager for Meta Quest 3 and OpenXR.
///
/// Seamlessly manages transitions between Meta Quest Touch Controllers and Optical Hand Tracking.
/// XRI's built-in <see cref="XRInputModalityManager"/> is disabled to prevent per-frame contention
/// and ray jitter.
/// </summary>
public class HandModalityForcer : MonoBehaviour
{
    [Header("Controllers")]
    public GameObject leftController;
    public GameObject rightController;

    [Header("Hands")]
    public GameObject leftHand;
    public GameObject rightHand;

    [Header("Modality Configuration")]
    [Tooltip("Delay in seconds before falling back to hands when controllers go idle/untracked, preventing rapid flickering.")]
    public float switchDebounceDuration = 0.30f;

    [Tooltip("Allow automatic modality switching between controllers and hand tracking.")]
    public bool autoDetectModality = true;

    [Tooltip("Log modality transitions to the console for debugging.")]
    public bool debugLogging = false;

    [Header("Controller Visual Sizing & Offsets")]
    [Tooltip("Scale for the authentic Meta Quest 3 controller model (0.01 converts original centimeter FBX units to Unity meters, matching real-world 1:1 scale).")]
    public Vector3 controllerVisualScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Tooltip("Position offset relative to the controller tracking anchor.")]
    public Vector3 controllerVisualPositionOffset = Vector3.zero;

    [Tooltip("Rotation offset relative to the controller tracking anchor.")]
    public Vector3 controllerVisualRotationOffset = Vector3.zero;

    public enum Modality
    {
        None,
        Controllers,
        Hands
    }

    public static Modality CurrentModality { get; private set; } = Modality.None;
    public static bool IsControllerMode => CurrentModality == Modality.Controllers;
    public static bool IsHandMode => CurrentModality == Modality.Hands;
    public static HandModalityForcer Instance { get; private set; }

    private XRHandSubsystem handSubsystem;
    private static List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();

    private float lastControllerActiveTime = -10f;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Stop the built-in modality manager from contending for the same objects.
        var modalityManager = GetComponent<XRInputModalityManager>();
        if (modalityManager == null)
            modalityManager = GetComponentInParent<XRInputModalityManager>();
        if (modalityManager == null)
            modalityManager = GetComponentInChildren<XRInputModalityManager>(true);
        if (modalityManager != null)
            modalityManager.enabled = false;

        // Start by checking initial hardware state
        EvaluateModality(true);
    }

    private void Start()
    {
        FindHandSubsystem();
        EvaluateModality(true);
    }

    private void Update()
    {
        if (!autoDetectModality) return;
        EvaluateModality(false);
    }

    private void FindHandSubsystem()
    {
        if (handSubsystem != null && handSubsystem.running) return;

        handSubsystem = null;
        SubsystemManager.GetSubsystems(s_Subsystems);
        for (int i = 0; i < s_Subsystems.Count; i++)
        {
            if (s_Subsystems[i].running)
            {
                handSubsystem = s_Subsystems[i];
                break;
            }
        }
    }

    private void EvaluateModality(bool forceImmediate)
    {
        bool controllersActive = CheckControllersActive();
        if (controllersActive)
        {
            lastControllerActiveTime = Time.unscaledTime;
        }

        bool recentControllerActivity = (Time.unscaledTime - lastControllerActiveTime) <= switchDebounceDuration;

        Modality target;
        if (controllersActive || recentControllerActivity)
        {
            target = Modality.Controllers;
        }
        else
        {
            // If controllers are not active, check optical hands
            FindHandSubsystem();
            bool handsTracked = CheckHandsActive();
            if (handsTracked)
            {
                target = Modality.Hands;
            }
            else
            {
                // Fallback: if neither is explicitly reporting, retain current or default to hands
                target = CurrentModality != Modality.None ? CurrentModality : Modality.Hands;
            }
        }

        if (target != CurrentModality || forceImmediate)
        {
            ApplyModality(target);
        }
    }

    private bool CheckControllersActive()
    {
        InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool leftIsController = leftDevice.isValid && (leftDevice.characteristics & InputDeviceCharacteristics.Controller) != 0;
        bool rightIsController = rightDevice.isValid && (rightDevice.characteristics & InputDeviceCharacteristics.Controller) != 0;

        bool leftTracked = false;
        if (leftIsController)
        {
            if (leftDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool lt))
                leftTracked = lt;
            else
                leftTracked = true;
        }

        bool rightTracked = false;
        if (rightIsController)
        {
            if (rightDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool rt))
                rightTracked = rt;
            else
                rightTracked = true;
        }

        // Check button or thumbstick inputs as immediate waking signals
        if (leftIsController && (CheckDeviceButtonPressed(leftDevice) || CheckDeviceStickMoved(leftDevice)))
            return true;

        if (rightIsController && (CheckDeviceButtonPressed(rightDevice) || CheckDeviceStickMoved(rightDevice)))
            return true;

        return leftTracked || rightTracked;
    }

    private bool CheckHandsActive()
    {
        if (handSubsystem != null && handSubsystem.running)
        {
            if (handSubsystem.leftHand.isTracked || handSubsystem.rightHand.isTracked)
                return true;
        }

        // Also check if any hand tracking device is connected via InputDevices
        InputDevice leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHandDevice.isValid && (leftHandDevice.characteristics & InputDeviceCharacteristics.HandTracking) != 0)
            return true;

        InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHandDevice.isValid && (rightHandDevice.characteristics & InputDeviceCharacteristics.HandTracking) != 0)
            return true;

        return false;
    }

    private bool CheckDeviceButtonPressed(InputDevice device)
    {
        if (!device.isValid) return false;
        if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool pb) && pb) return true;
        if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool sb) && sb) return true;
        if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool tb) && tb) return true;
        if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gb) && gb) return true;
        if (device.TryGetFeatureValue(CommonUsages.menuButton, out bool mb) && mb) return true;
        if (device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool stickClick) && stickClick) return true;
        return false;
    }

    private bool CheckDeviceStickMoved(InputDevice device)
    {
        if (!device.isValid) return false;
        if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
        {
            if (axis.sqrMagnitude > 0.04f) return true;
        }
        return false;
    }

    private void ApplyModality(Modality modality)
    {
        CurrentModality = modality;

        if (modality == Modality.Controllers)
        {
            if (debugLogging) Debug.Log("[HandModalityForcer] Switched to CONTROLLERS mode.");

            // 1. Activate controller GameObjects
            if (leftController != null && !leftController.activeSelf) leftController.SetActive(true);
            if (rightController != null && !rightController.activeSelf) rightController.SetActive(true);

            // 2. Deactivate optical hands
            if (leftHand != null && leftHand.activeSelf) leftHand.SetActive(false);
            if (rightHand != null && rightHand.activeSelf) rightHand.SetActive(false);

            // 3. Ensure controller ray line visuals and interactors are enabled and styled as sleek straight rays, and Quest 3 models are displayed
            ConfigureControllerInteractors(leftController, true);
            ConfigureControllerInteractors(rightController, false);
        }
        else if (modality == Modality.Hands)
        {
            if (debugLogging) Debug.Log("[HandModalityForcer] Switched to HANDS mode.");

            // 1. Activate optical hands
            if (leftHand != null && !leftHand.activeSelf) leftHand.SetActive(true);
            if (rightHand != null && !rightHand.activeSelf) rightHand.SetActive(true);

            // 2. Deactivate controllers
            if (leftController != null && leftController.activeSelf) leftController.SetActive(false);
            if (rightController != null && rightController.activeSelf) rightController.SetActive(false);

            // Hide Quest 3 controller visuals
            if (leftController != null)
            {
                Transform v = leftController.transform.Find("MetaQuestTouchPlus_Left_Visual");
                if (v != null) v.gameObject.SetActive(false);
            }
            if (rightController != null)
            {
                Transform v = rightController.transform.Find("MetaQuestTouchPlus_Right_Visual");
                if (v != null) v.gameObject.SetActive(false);
            }

            // 3. Deduplicate hand rays: ensure only 1 ray visual is active per hand
            DeduplicateHandRays(leftHand);
            DeduplicateHandRays(rightHand);
        }
    }

    private void ConfigureControllerInteractors(GameObject controllerObj, bool isLeft)
    {
        if (controllerObj == null) return;

        // 1. COMPLETELY DISABLE any Teleport Interactors to eliminate the curving red line!
        foreach (Transform t in controllerObj.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name.Contains("Teleport"))
            {
                t.gameObject.SetActive(false);
                var telRay = t.GetComponent<XRRayInteractor>();
                if (telRay != null) telRay.enabled = false;
                var telVisual = t.GetComponent<XRInteractorLineVisual>();
                if (telVisual != null) telVisual.enabled = false;
                var telLr = t.GetComponent<LineRenderer>();
                if (telLr != null) telLr.enabled = false;
            }
        }

        // 2. Enable and style ONLY the straight UI / Interaction Ray
        var rayInteractors = controllerObj.GetComponentsInChildren<XRRayInteractor>(true);
        foreach (var ray in rayInteractors)
        {
            if (ray != null && !ray.name.Contains("Teleport"))
            {
                if (!ray.gameObject.activeSelf) ray.gameObject.SetActive(true);
                ray.enabled = true;
                ray.lineType = XRRayInteractor.LineType.StraightLine; // Force straight line, never curving!
                ray.maxRaycastDistance = MainMenu.IsExplorationStarted ? 2.5f : 10f;
            }
        }

        // Clean modern pointer gradient: soft white fading to subtle translucent ice-blue
        Gradient straightRayGradient = new Gradient();
        straightRayGradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.92f, 0.95f, 1.0f), 0.0f),
                new GradientColorKey(new Color(0.80f, 0.90f, 1.0f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.85f, 0.0f),
                new GradientAlphaKey(0.15f, 1.0f)
            }
        );

        var lineVisuals = controllerObj.GetComponentsInChildren<XRInteractorLineVisual>(true);
        foreach (var visual in lineVisuals)
        {
            if (visual != null && !visual.name.Contains("Teleport"))
            {
                visual.enabled = true;
                visual.lineWidth = 0.005f; // Sleek 5mm thin laser pointer
                visual.validColorGradient = straightRayGradient;
                visual.invalidColorGradient = straightRayGradient; // NO RED!
                visual.lineLength = 10f;

                var lr = visual.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.enabled = true;
                    lr.startWidth = 0.005f;
                    lr.endWidth = 0.005f;
                    lr.colorGradient = straightRayGradient;
                }
            }
        }

        // 3. Ensure authentic Meta Quest 3 Touch Plus controller 3D model
        EnsureQuest3ControllerVisual(controllerObj, isLeft);
    }

    /// <summary>
    /// Ensures the authentic dark graphite Meta Quest 3 Touch Plus controller model is attached,
    /// sized to real-world 1:1 scale (0.01f), and hides any legacy white generic openxr meshes or Vive trackpads.
    /// </summary>
    private void EnsureQuest3ControllerVisual(GameObject controllerObj, bool isLeft)
    {
        if (controllerObj == null) return;

        // 1. Check if authentic Quest 3 model is already attached
        string visualName = isLeft ? "MetaQuestTouchPlus_Left_Visual" : "MetaQuestTouchPlus_Right_Visual";
        Transform existing = controllerObj.transform.Find(visualName);
        if (existing != null)
        {
            existing.localPosition = controllerVisualPositionOffset;
            existing.localRotation = Quaternion.Euler(controllerVisualRotationOffset);
            existing.localScale = controllerVisualScale;
            existing.gameObject.SetActive(true);
        }
        else
        {
            // Load and instantiate the authentic Meta Quest 3 Touch Plus model from Resources
            string resPath = isLeft ? "Controllers/MetaQuestTouchPlus_Left" : "Controllers/MetaQuestTouchPlus_Right";
            GameObject prefab = Resources.Load<GameObject>(resPath);
            if (prefab != null)
            {
                GameObject inst = Instantiate(prefab, controllerObj.transform);
                inst.name = visualName;
                inst.transform.localPosition = controllerVisualPositionOffset;
                inst.transform.localRotation = Quaternion.Euler(controllerVisualRotationOffset);
                inst.transform.localScale = controllerVisualScale;
                inst.SetActive(true);
            }
        }

        // 2. Hide all legacy generic Vive trackpads, white controller parts, and old controller meshes
        foreach (Transform child in controllerObj.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == controllerObj.transform) continue;
            if (child.name.StartsWith("MetaQuestTouchPlus")) continue;

            string n = child.name;
            if (n.StartsWith("XR Controller") || n.Contains("TouchPad") || n.Contains("XRController_") ||
                n.Contains("Controller_Base") || n.Contains("Button_") || n.Contains("ThumbStick") ||
                n.Contains("Trigger") || n.Contains("Bumper"))
            {
                child.gameObject.SetActive(false);
            }
        }

        // 3. Ensure only the authentic Quest 3 renderers are enabled under the controller
        foreach (var r in controllerObj.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r.transform.name.StartsWith("MetaQuestTouchPlus") || (r.transform.parent != null && r.transform.parent.name.StartsWith("MetaQuestTouchPlus")))
            {
                r.enabled = true;
            }
            else
            {
                r.enabled = false;
            }
        }
    }

    /// <summary>
    /// Ensures only 1 primary hand ray line visual is active under the hand hierarchy,
    /// turning off any duplicate or secondary ray lines.
    /// </summary>
    private void DeduplicateHandRays(GameObject handObj)
    {
        if (handObj == null) return;

        var lineVisuals = handObj.GetComponentsInChildren<XRInteractorLineVisual>(true);
        if (lineVisuals != null && lineVisuals.Length > 1)
        {
            // Keep the first primary ray visual active, disable all duplicate secondary ray lines!
            for (int i = 1; i < lineVisuals.Length; i++)
            {
                if (lineVisuals[i] != null)
                {
                    lineVisuals[i].enabled = false;
                    var lr = lineVisuals[i].GetComponent<LineRenderer>();
                    if (lr != null) lr.enabled = false;
                }
            }
        }

        var lineRenderers = handObj.GetComponentsInChildren<LineRenderer>(true);
        if (lineRenderers != null && lineRenderers.Length > 1)
        {
            LineRenderer primary = (lineVisuals != null && lineVisuals.Length > 0) ? lineVisuals[0].GetComponent<LineRenderer>() : lineRenderers[0];
            for (int i = 0; i < lineRenderers.Length; i++)
            {
                if (lineRenderers[i] != null && lineRenderers[i] != primary)
                {
                    lineRenderers[i].enabled = false;
                }
            }
        }
    }
}
