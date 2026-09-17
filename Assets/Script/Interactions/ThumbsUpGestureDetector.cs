using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MuseumMR.Interactions
{
    /// <summary>
    /// Dedicated component that detects when a hand performs a "Thumbs Up" gesture.
    ///
    /// Evaluates:
    /// 1. Hand tracking state (from XRHandSubsystem).
    /// 2. Four fingers (Index, Middle, Ring, Little) curled into a fist.
    /// 3. Thumb extended and uncurled.
    /// 4. Thumb pointing upward in world space (Vector3.up dot product).
    /// 5. Palm not facing downward.
    /// 6. Minimum hold duration to avoid false triggers, plus cooldown debounce.
    ///
    /// Can trigger custom UnityEvents in the Inspector, invoke static C# actions,
    /// or directly tell WristWatch.Instance to open the menu panel.
    /// </summary>
    public class ThumbsUpGestureDetector : MonoBehaviour
    {
        [Header("Target Hand")]
        [Tooltip("Which hand to monitor for the thumbs-up gesture.")]
        public Handedness targetHand = Handedness.Left;

        [Header("Gesture Settings")]
        [Tooltip("Enable or disable gesture detection.")]
        public bool enableDetection = true;

        [Tooltip("Minimum time (in seconds) the gesture must be held steadily before firing.")]
        public float holdDuration = 0.30f;

        [Tooltip("Cooldown period (in seconds) after a trigger before it can fire again.")]
        public float cooldown = 1.20f;

        [Header("Curl & Alignment Thresholds")]
        [Tooltip("Maximum allowed curl value for the thumb (0 = straight, 1 = curled).")]
        [Range(0.1f, 0.6f)]
        public float thumbCurlThreshold = 0.35f;

        [Tooltip("Minimum required curl value for the other 4 fingers.")]
        [Range(0.3f, 0.9f)]
        public float fingerCurlThreshold = 0.55f;

        [Tooltip("Alignment threshold between thumb direction and world up (1.0 = straight up).")]
        [Range(0.4f, 0.95f)]
        public float thumbUpAlignmentThreshold = 0.60f;

        [Header("Wrist Menu Integration")]
        [Tooltip("Automatically call WristWatch.Instance.OpenOptionsPanel() when thumbs up is detected.")]
        public bool triggerWristWatchMenu = true;

        [Tooltip("If true, toggles the menu (opens if closed, closes if open). If false, only opens.")]
        public bool toggleInsteadOfOpen = false;

        [Header("Events")]
        [Tooltip("Fired when the thumbs-up gesture is recognized and held for the required duration.")]
        public UnityEvent onThumbsUpDetected;

        /// <summary>
        /// Global C# event fired whenever any ThumbsUpGestureDetector detects a gesture.
        /// Passes the handedness of the hand that triggered it.
        /// </summary>
        public static event Action<Handedness> OnAnyThumbsUp;

        [Header("Debug")]
        [Tooltip("Print debug messages to Unity console upon gesture detection.")]
        public bool debugLogging = true;

        private float currentHoldTime = 0f;
        private float cooldownTimer = 0f;
        private XRHandSubsystem handSubsystem;
        private static List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();
        private Transform sessionSpaceRoot;

        private void Start()
        {
            FindSessionSpaceRoot();
        }

        private void Update()
        {
            if (!enableDetection) return;

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.unscaledDeltaTime;
            }

#if UNITY_EDITOR
            // Desktop simulation support
            bool editorHotkeyPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                editorHotkeyPressed = true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.T))
            {
                editorHotkeyPressed = true;
            }
#endif
            if (editorHotkeyPressed)
            {
                if (debugLogging)
                {
                    Debug.Log($"<color=cyan>ThumbsUpGestureDetector: [Editor Simulation] Thumbs Up triggered for {targetHand} hand via 'T' key!</color>");
                }
                FireGestureTrigger();
                return;
            }
#endif

            FindHandSubsystem();
            if (handSubsystem == null || !handSubsystem.running)
            {
                currentHoldTime = 0f;
                return;
            }

            XRHand hand = targetHand == Handedness.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (!hand.isTracked)
            {
                currentHoldTime = 0f;
                return;
            }

            bool isThumbsUp = CheckThumbsUp(hand);
            if (isThumbsUp)
            {
                currentHoldTime += Time.unscaledDeltaTime;
                if (currentHoldTime >= holdDuration)
                {
                    if (cooldownTimer <= 0f)
                    {
                        FireGestureTrigger();
                    }
                    currentHoldTime = 0f;
                }
            }
            else
            {
                currentHoldTime = Mathf.Max(0f, currentHoldTime - Time.unscaledDeltaTime * 2f);
            }
        }

        private bool CheckThumbsUp(XRHand hand)
        {
            // 1. Thumb straightness
            XRFingerShape thumbShape = hand.CalculateFingerShape(XRHandFingerID.Thumb, XRFingerShapeTypes.FullCurl);
            if (thumbShape.TryGetFullCurl(out float thumbCurl))
            {
                if (thumbCurl > thumbCurlThreshold) return false;
            }

            // 2. 4 fingers curled
            XRFingerShape indexShape = hand.CalculateFingerShape(XRHandFingerID.Index, XRFingerShapeTypes.FullCurl);
            XRFingerShape middleShape = hand.CalculateFingerShape(XRHandFingerID.Middle, XRFingerShapeTypes.FullCurl);
            XRFingerShape ringShape = hand.CalculateFingerShape(XRHandFingerID.Ring, XRFingerShapeTypes.FullCurl);
            XRFingerShape littleShape = hand.CalculateFingerShape(XRHandFingerID.Little, XRFingerShapeTypes.FullCurl);

            int curledCount = 0;
            if (indexShape.TryGetFullCurl(out float ic) && ic >= fingerCurlThreshold) curledCount++;
            if (middleShape.TryGetFullCurl(out float mc) && mc >= fingerCurlThreshold) curledCount++;
            if (ringShape.TryGetFullCurl(out float rc) && rc >= fingerCurlThreshold) curledCount++;
            if (littleShape.TryGetFullCurl(out float lc) && lc >= fingerCurlThreshold) curledCount++;

            if (curledCount < 3) return false;
            if (indexShape.TryGetFullCurl(out float iVal) && iVal < (fingerCurlThreshold - 0.15f)) return false;
            if (middleShape.TryGetFullCurl(out float mVal) && mVal < (fingerCurlThreshold - 0.15f)) return false;

            // 3. Thumb upward vector
            XRHandJoint thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
            XRHandJoint thumbProximal = hand.GetJoint(XRHandJointID.ThumbProximal);
            if (!thumbProximal.TryGetPose(out Pose proxPose))
            {
                thumbProximal = hand.GetJoint(XRHandJointID.ThumbMetacarpal);
                thumbProximal.TryGetPose(out proxPose);
            }

            if (!thumbTip.TryGetPose(out Pose tipPose)) return false;

            Vector3 thumbLocalDir = (tipPose.position - proxPose.position).normalized;
            Vector3 thumbWorldDir = sessionSpaceRoot != null
                ? sessionSpaceRoot.TransformDirection(thumbLocalDir)
                : thumbLocalDir;

            if (Vector3.Dot(thumbWorldDir, Vector3.up) < thumbUpAlignmentThreshold) return false;

            // 4. Palm not facing down
            XRHandJoint palm = hand.GetJoint(XRHandJointID.Palm);
            if (palm.TryGetPose(out Pose palmPose))
            {
                Vector3 palmNormalLocal = palmPose.up;
                Vector3 palmNormalWorld = sessionSpaceRoot != null
                    ? sessionSpaceRoot.TransformDirection(palmNormalLocal)
                    : palmNormalLocal;

                if (Vector3.Dot(palmNormalWorld, Vector3.down) > 0.65f) return false;
            }

            return true;
        }

        private void FireGestureTrigger()
        {
            cooldownTimer = cooldown;

            if (debugLogging)
            {
                Debug.Log($"<color=green>ThumbsUpGestureDetector: Thumbs-Up recognized on {targetHand} hand!</color>");
            }

            onThumbsUpDetected?.Invoke();
            OnAnyThumbsUp?.Invoke(targetHand);

            if (triggerWristWatchMenu && targetHand == Handedness.Left && WristWatch.Instance != null)
            {
                if (toggleInsteadOfOpen)
                {
                    WristWatch.Instance.ToggleOptionsPanel();
                }
                else
                {
                    WristWatch.Instance.OpenOptionsPanel();
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

        private void FindSessionSpaceRoot()
        {
            if (sessionSpaceRoot != null) return;

            GameObject cameraOffset = GameObject.Find("Camera Offset");
            if (cameraOffset != null)
            {
                sessionSpaceRoot = cameraOffset.transform;
                return;
            }

            if (Camera.main != null && Camera.main.transform.parent != null)
            {
                sessionSpaceRoot = Camera.main.transform.parent;
            }
        }
    }
}
