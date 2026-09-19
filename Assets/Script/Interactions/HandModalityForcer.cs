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
    [Tooltip("Grace period in seconds when optical hands are temporarily lost (e.g. edge of vision) before falling back to controllers.")]
    public float handLostGracePeriod = 0.25f;

    [Tooltip("Minimum time controllers must remain resting/idle before allowing optical hands to take over.")]
    public float controllerIdleDelay = 0.20f;

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
    private float lastHandTrackedTime = -10f;
    private GameObject leftVisualInstance;
    private GameObject rightVisualInstance;

    private Vector3 lastLeftPos;
    private Vector3 lastRightPos;

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

        if (leftController != null && leftVisualInstance == null)
        {
            var t = leftController.transform.Find("MetaQuestTouchPlus_Left_Visual");
            if (t != null) leftVisualInstance = t.gameObject;
        }
        if (rightController != null && rightVisualInstance == null)
        {
            var t = rightController.transform.Find("MetaQuestTouchPlus_Right_Visual");
            if (t != null) rightVisualInstance = t.gameObject;
        }
    }

    private void Update()
    {
        if (!autoDetectModality) return;
        EvaluateModality(false);
    }

    private void LateUpdate()
    {
        if (CurrentModality == Modality.Controllers)
        {
            if (leftVisualInstance != null && leftController != null)
            {
                UpdateControllerVisualPose(leftVisualInstance, true, leftController);
            }
            if (rightVisualInstance != null && rightController != null)
            {
                UpdateControllerVisualPose(rightVisualInstance, false, rightController);
            }
        }
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
        bool controllerButtonsInUse = CheckControllerButtonsInUse();
        bool controllerMoving = CheckControllersMoving();
        bool controllerInUse = controllerButtonsInUse || (!CheckHandsActive() && controllerMoving);

        if (controllerButtonsInUse || controllerMoving)
        {
            lastControllerActiveTime = Time.unscaledTime;
        }

        bool handsTracked = CheckHandsActive();
        if (handsTracked)
        {
            lastHandTrackedTime = Time.unscaledTime;
        }

        bool controllersTracked = CheckControllersTracked();

        Modality target = CurrentModality;

        // Priority 1: User actively presses or touches controller buttons/triggers/thumbsticks -> instant Controllers mode
        if (controllerButtonsInUse)
        {
            target = Modality.Controllers;
        }
        // Priority 2: Optical hands detected by headset cameras and user isn't pressing controller buttons -> instant Hands mode
        else if (handsTracked)
        {
            target = Modality.Hands;
        }
        // Priority 3: Optical hands not tracked, but controllers are held/moving or tracked
        else if (controllerInUse || controllersTracked)
        {
            bool handRecentlyTracked = (Time.unscaledTime - lastHandTrackedTime) < handLostGracePeriod;
            if (!handRecentlyTracked)
            {
                target = Modality.Controllers;
            }
        }
        // Priority 4: Neither is tracked, retain current modality (or default to Hands if uninitialized)
        else
        {
            if (CurrentModality == Modality.None)
                target = Modality.Hands;
        }

        if (target != CurrentModality || forceImmediate)
        {
            ApplyModality(target);
        }
    }

    private bool CheckControllerInUse()
    {
        return CheckControllerButtonsInUse() || CheckControllersMoving();
    }

    private bool CheckControllerButtonsInUse()
    {
        InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        return CheckDeviceInput(leftDevice) || CheckDeviceInput(rightDevice);
    }

    private bool CheckControllersMoving()
    {
        InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        return CheckDeviceMoving(leftDevice, ref lastLeftPos) || CheckDeviceMoving(rightDevice, ref lastRightPos);
    }

    private bool CheckDeviceInput(InputDevice device)
    {
        if (!device.isValid) return false;
        if ((device.characteristics & InputDeviceCharacteristics.Controller) == 0) return false;

        // Digital Buttons
        if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool pb) && pb) return true;
        if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool sb) && sb) return true;
        if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool tb) && tb) return true;
        if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gb) && gb) return true;
        if (device.TryGetFeatureValue(CommonUsages.menuButton, out bool mb) && mb) return true;
        if (device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool stickClick) && stickClick) return true;
        if (device.TryGetFeatureValue(CommonUsages.secondary2DAxisClick, out bool secClick) && secClick) return true;

        // Analog Triggers & Grips (> 0.08f threshold)
        if (device.TryGetFeatureValue(CommonUsages.trigger, out float trig) && trig > 0.08f) return true;
        if (device.TryGetFeatureValue(CommonUsages.grip, out float grip) && grip > 0.08f) return true;

        // Thumbstick Movement (> 0.04f sqrMagnitude)
        if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis) && axis.sqrMagnitude > 0.04f) return true;
        if (device.TryGetFeatureValue(CommonUsages.secondary2DAxis, out Vector2 secAxis) && secAxis.sqrMagnitude > 0.04f) return true;

        // Capacitive Touch (resting thumb/fingers on buttons, thumbstick, or trigger)
        if (device.TryGetFeatureValue(CommonUsages.primaryTouch, out bool pt) && pt) return true;
        if (device.TryGetFeatureValue(CommonUsages.secondaryTouch, out bool st) && st) return true;
        if (device.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out bool axisTouch) && axisTouch) return true;

        return false;
    }

    private bool CheckDeviceMoving(InputDevice device, ref Vector3 lastPos)
    {
        if (!device.isValid) return false;
        if ((device.characteristics & InputDeviceCharacteristics.Controller) == 0) return false;

        if (device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 vel))
        {
            if (vel.sqrMagnitude > 0.04f) // ~20 cm/s intentional movement (filters sensor drift)
                return true;
        }

        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 currentPos))
        {
            if (lastPos == Vector3.zero)
            {
                lastPos = currentPos;
                return false;
            }
            float dist = Vector3.Distance(currentPos, lastPos);
            lastPos = currentPos;
            if (dist > 0.015f) // moved > 15mm in a single frame
                return true;
        }

        return false;
    }

    private bool CheckControllersTracked()
    {
        InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        return IsDeviceTracked(leftDevice) || IsDeviceTracked(rightDevice);
    }

    private bool IsDeviceTracked(InputDevice device)
    {
        if (!device.isValid) return false;
        if ((device.characteristics & InputDeviceCharacteristics.Controller) == 0) return false;

        if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked))
        {
            return isTracked;
        }

        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
        {
            return pos != Vector3.zero;
        }

        return false;
    }

    private bool CheckHandsActive()
    {
        FindHandSubsystem();
        if (handSubsystem != null && handSubsystem.running)
        {
            if (handSubsystem.leftHand.isTracked || handSubsystem.rightHand.isTracked)
                return true;
        }

        // Also check if any hand tracking device is connected via InputDevices
        InputDevice leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHandDevice.isValid && (leftHandDevice.characteristics & InputDeviceCharacteristics.HandTracking) != 0)
        {
            if (leftHandDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool lt) && lt)
                return true;
        }

        InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHandDevice.isValid && (rightHandDevice.characteristics & InputDeviceCharacteristics.HandTracking) != 0)
        {
            if (rightHandDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool rt) && rt)
                return true;
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

            // 3. Configure controller interactors, Quest 3 3D models, and white circle cursor
            ConfigureControllerInteractors(leftController, true);
            ConfigureControllerInteractors(rightController, false);

            if (leftVisualInstance != null) leftVisualInstance.SetActive(true);
            if (rightVisualInstance != null) rightVisualInstance.SetActive(true);

            HandRayReticle.SetupAllRayInteractors();
        }
        else if (modality == Modality.Hands)
        {
            if (debugLogging) Debug.Log("[HandModalityForcer] Switched to HANDS mode.");

            // 1. Activate optical hands (FIXED: !rightHand.activeSelf so right hand activates!)
            if (leftHand != null && !leftHand.activeSelf) leftHand.SetActive(true);
            if (rightHand != null && !rightHand.activeSelf) rightHand.SetActive(true);

            // 2. Deactivate controllers
            if (leftController != null && leftController.activeSelf) leftController.SetActive(false);
            if (rightController != null && rightController.activeSelf) rightController.SetActive(false);

            // Hide Quest 3 controller visuals
            if (leftVisualInstance != null) leftVisualInstance.SetActive(false);
            if (rightVisualInstance != null) rightVisualInstance.SetActive(false);

            // 3. Deduplicate hand rays: ensure only 1 ray visual is active per hand
            DeduplicateHandRays(leftHand);
            DeduplicateHandRays(rightHand);

            HandRayReticle.SetupAllRayInteractors();
        }
    }

    private void ConfigureControllerInteractors(GameObject controllerObj, bool isLeft)
    {
        if (controllerObj == null) return;

        // 1. Prevent ActionBasedController from instantiating any generic white controller model
        var abc = controllerObj.GetComponent<ActionBasedController>();
        if (abc != null)
        {
            abc.modelPrefab = null;
            if (abc.model != null && !abc.model.name.Contains("MetaQuestTouchPlus"))
            {
                Destroy(abc.model.gameObject);
                abc.model = null;
            }
        }

        // 2. COMPLETELY DISABLE any Teleport Interactors to eliminate curving red line
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

        // 3. Enable straight UI / Interaction Ray (raycast only, no visual line)
        var rayInteractors = controllerObj.GetComponentsInChildren<XRRayInteractor>(true);
        foreach (var ray in rayInteractors)
        {
            if (ray != null && !ray.name.Contains("Teleport"))
            {
                if (!ray.gameObject.activeSelf) ray.gameObject.SetActive(true);
                ray.enabled = true;
                ray.enableUIInteraction = true;
                ray.lineType = XRRayInteractor.LineType.StraightLine;
                ray.maxRaycastDistance = MainMenu.IsExplorationStarted ? 6f : 10f;
            }
        }

        // 4. Permanently DISABLE all Line Visuals and Line Renderers (Zero lines!)
        var lineVisuals = controllerObj.GetComponentsInChildren<XRInteractorLineVisual>(true);
        foreach (var visual in lineVisuals)
        {
            if (visual != null) visual.enabled = false;
        }

        var lineRenderers = controllerObj.GetComponentsInChildren<LineRenderer>(true);
        foreach (var lr in lineRenderers)
        {
            if (lr != null)
            {
                lr.enabled = false;
                lr.widthMultiplier = 0f;
                lr.startWidth = 0f;
                lr.endWidth = 0f;
                if (lr.positionCount > 0) lr.positionCount = 0;
            }
        }

        // Attach HandRayReticle (white circle cursor on canvas) to active ray interactors
        foreach (var ray in rayInteractors)
        {
            if (ray != null && !ray.name.Contains("Teleport"))
            {
                var reticle = ray.GetComponent<HandRayReticle>();
                if (reticle == null)
                {
                    reticle = ray.gameObject.AddComponent<HandRayReticle>();
                }
                reticle.SuppressLineRenderer();
            }
        }

        // 5. Ensure authentic Meta Quest 3 Touch Plus controller 3D model
        EnsureQuest3ControllerVisual(controllerObj, isLeft);
    }

    /// <summary>
    /// Ensures the authentic dark graphite Meta Quest 3 Touch Plus controller model is attached,
    /// sized to real-world 1:1 scale (0.01f), eliminates any generic white starter controllers,
    /// and pre-positions the visual to track physical controller grip pose.
    /// </summary>
    private void EnsureQuest3ControllerVisual(GameObject controllerObj, bool isLeft)
    {
        if (controllerObj == null) return;

        // 1. Hide and destroy all legacy generic white starter controller meshes, Vive trackpads, etc.
        foreach (Transform child in controllerObj.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == controllerObj.transform) continue;
            if (child.name.StartsWith("MetaQuestTouchPlus")) continue;

            string n = child.name;
            if (n.Contains("XR Controller") || n.Contains("TouchPad") || n.Contains("XRController") ||
                n.Contains("Controller_Base") || n.Contains("Button_") || n.Contains("ThumbStick") ||
                n.Contains("Trigger") || n.Contains("Bumper"))
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        // 2. Check if authentic Quest 3 model is already attached
        string visualName = isLeft ? "MetaQuestTouchPlus_Left_Visual" : "MetaQuestTouchPlus_Right_Visual";
        Transform existing = controllerObj.transform.Find(visualName);
        GameObject visualInstance = null;

        if (existing != null)
        {
            visualInstance = existing.gameObject;
        }
        else
        {
            // Load and instantiate the authentic Meta Quest 3 Touch Plus model from Resources
            string resPath = isLeft ? "Controllers/MetaQuestTouchPlus_Left" : "Controllers/MetaQuestTouchPlus_Right";
            GameObject prefab = Resources.Load<GameObject>(resPath);
            if (prefab != null)
            {
                visualInstance = Instantiate(prefab, controllerObj.transform);
                visualInstance.name = visualName;
            }
        }

        if (visualInstance != null)
        {
            visualInstance.transform.localScale = controllerVisualScale;
            visualInstance.SetActive(CurrentModality == Modality.Controllers && controllerObj.activeInHierarchy);

            if (isLeft)
                leftVisualInstance = visualInstance;
            else
                rightVisualInstance = visualInstance;

            // Ensure ONLY the authentic Quest 3 renderers are enabled under the controller
            foreach (var r in controllerObj.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                r.enabled = r.transform.IsChildOf(visualInstance.transform);
            }

            // Immediately set initial pose
            UpdateControllerVisualPose(visualInstance, isLeft, controllerObj);
        }
    }

    /// <summary>
    /// Synchronizes the 3D controller visual with the physical controller's Grip Pose.
    /// On Meta Quest 3 hardware, CommonUsages.devicePosition and deviceRotation represent
    /// the real physical grip pose in tracking space (Camera Offset).
    /// </summary>
    private void UpdateControllerVisualPose(GameObject visualObj, bool isLeft, GameObject controllerObj)
    {
        if (visualObj == null || controllerObj == null) return;

        if (!controllerObj.activeInHierarchy || CurrentModality != Modality.Controllers)
        {
            if (visualObj.activeSelf) visualObj.SetActive(false);
            return;
        }

        XRNode node = isLeft ? XRNode.LeftHand : XRNode.RightHand;
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);

        // Check if device is tracked
        bool isTracked = true;
        if (device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool trackedState))
        {
            isTracked = trackedState;
        }

        if (!isTracked)
        {
            if (visualObj.activeSelf) visualObj.SetActive(false);
            return;
        }

        if (!visualObj.activeSelf) visualObj.SetActive(true);

        // Parented directly under controllerObj (which tracks the physical controller grip anchor).
        // Align visual directly with controller anchor with optional Inspector fine-tuning offsets.
        visualObj.transform.localPosition = controllerVisualPositionOffset;
        visualObj.transform.localRotation = Quaternion.Euler(controllerVisualRotationOffset);
        visualObj.transform.localScale = controllerVisualScale;
    }

    /// <summary>
    /// Disables all line visuals and line renderers on the hand hierarchy (Zero lines!),
    /// and ensures HandRayReticle (white circle cursor on canvas) is active on the hand ray.
    /// </summary>
    private void DeduplicateHandRays(GameObject handObj)
    {
        if (handObj == null) return;

        // 1. Permanently disable ALL line visuals
        var lineVisuals = handObj.GetComponentsInChildren<XRInteractorLineVisual>(true);
        foreach (var visual in lineVisuals)
        {
            if (visual != null) visual.enabled = false;
        }

        // 2. Permanently disable ALL line renderers
        var lineRenderers = handObj.GetComponentsInChildren<LineRenderer>(true);
        foreach (var lr in lineRenderers)
        {
            if (lr != null)
            {
                lr.enabled = false;
                lr.widthMultiplier = 0f;
                lr.startWidth = 0f;
                lr.endWidth = 0f;
                if (lr.positionCount > 0) lr.positionCount = 0;
            }
        }

        // 3. Ensure HandRayReticle is attached to the hand's ray interactor
        var rayInteractors = handObj.GetComponentsInChildren<XRRayInteractor>(true);
        foreach (var ray in rayInteractors)
        {
            if (ray != null && !ray.name.Contains("Teleport"))
            {
                if (!ray.gameObject.activeSelf) ray.gameObject.SetActive(true);
                ray.enabled = true;
                ray.enableUIInteraction = true;
                ray.lineType = XRRayInteractor.LineType.StraightLine;
                var reticle = ray.GetComponent<HandRayReticle>();
                if (reticle == null)
                {
                    reticle = ray.gameObject.AddComponent<HandRayReticle>();
                }
                reticle.SuppressLineRenderer();
            }
        }
    }
}
