using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;

public class WristWatchMenu : MonoBehaviour
{
    [Header("Hand Tracking Anchors")]
    [Tooltip("Left Hand / Wrist transform. Auto-resolved if left empty.")]
    public Transform leftHandAnchor;

    [Header("UI Objects")]
    public GameObject wristWatchButtonObj;
    public GameObject optionsPanelObj;

    [Header("Options Targets")]
    public GameObject roomHudCanvas;
    public GameObject artifactContainerObj;

    [Header("Offsets")]
    [Tooltip("World-space offset for the watch button relative to the left wrist (Y = up), so the icon hovers on top of the hand regardless of wrist rotation.")]
    public Vector3 watchOffset = new Vector3(0f, 0.09f, 0f);

    [Tooltip("World-space offset for the floating options panel relative to the left wrist (Y = up).")]
    public Vector3 panelOffset = new Vector3(0f, 0.22f, 0f);

    private bool optionsPanelActive = false;

    // Wrist pose sources. The "Left Hand" rig object is NOT pose-driven (only its joint
    // visuals are), so hand-tracking poses must come from the XRHandSubsystem wrist joint.
    // The "Left Controller" rig object IS pose-driven, so it works as a direct anchor when
    // the player holds controllers (HandModalityForcer swaps the two at runtime).
    private XRHandSubsystem handSubsystem;
    private static List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();
    private Transform leftControllerCandidate;
    private Transform sessionSpaceRoot; // "Camera Offset" object: converts session-space poses to world
    private float nextCandidateSearchTime = 0f;

    private bool hasAnchorPose;
    private Vector3 anchorPos;
    private Quaternion anchorRot;

    void Start()
    {
        RefreshAnchorCandidates();

        // Hidden until a wrist pose exists, so the watch never floats at world origin
        if (wristWatchButtonObj != null)
        {
            wristWatchButtonObj.SetActive(false);
        }

        if (optionsPanelObj != null)
        {
            optionsPanelObj.SetActive(false);
        }

        if (roomHudCanvas == null)
        {
            roomHudCanvas = GameObject.Find("RoomHUDCanvas");
        }
    }

    private void RefreshAnchorCandidates()
    {
        // Session-space root: the XROrigin's floor offset object ("Camera Offset"), which
        // parents the camera and controllers and defines the space hand poses arrive in
        if (sessionSpaceRoot == null)
        {
            XROrigin origin = FindObjectOfType<XROrigin>();
            if (origin != null && origin.CameraFloorOffsetObject != null)
            {
                sessionSpaceRoot = origin.CameraFloorOffsetObject.transform;
            }
        }

        if (leftControllerCandidate == null)
        {
            foreach (Transform t in FindObjectsOfType<Transform>(true))
            {
                string n = t.name;
                if ((n == "Left Controller" || n == "LeftHand Controller") && HasAncestorNamed(t, "Camera Offset"))
                {
                    leftControllerCandidate = t;
                    break;
                }
            }
        }
    }

    private static bool HasAncestorNamed(Transform t, string ancestorName)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
        {
            if (p.name == ancestorName) return true;
        }
        return false;
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

    /// <summary>
    /// Computes this frame's wrist anchor pose. Priority: Inspector-assigned transform,
    /// then the tracked hand's wrist joint, then the pose-driven controller transform.
    /// </summary>
    private void UpdateAnchorPose()
    {
        hasAnchorPose = false;

        if (leftHandAnchor != null && leftHandAnchor.gameObject.activeInHierarchy)
        {
            anchorPos = leftHandAnchor.position;
            anchorRot = leftHandAnchor.rotation;
            hasAnchorPose = true;
            return;
        }

        FindHandSubsystem();
        if (handSubsystem != null && handSubsystem.running && handSubsystem.leftHand.isTracked)
        {
            XRHandJoint wrist = handSubsystem.leftHand.GetJoint(XRHandJointID.Wrist);
            if (wrist.TryGetPose(out Pose pose))
            {
                if (sessionSpaceRoot != null)
                {
                    anchorPos = sessionSpaceRoot.TransformPoint(pose.position);
                    anchorRot = sessionSpaceRoot.rotation * pose.rotation;
                }
                else
                {
                    anchorPos = pose.position;
                    anchorRot = pose.rotation;
                }
                hasAnchorPose = true;
                return;
            }
        }

        if (leftControllerCandidate != null && leftControllerCandidate.gameObject.activeInHierarchy)
        {
            anchorPos = leftControllerCandidate.position;
            anchorRot = leftControllerCandidate.rotation;
            hasAnchorPose = true;
        }
    }

    private Vector3 AnchorTransformPoint(Vector3 offset)
    {
        // World-space offset: keeps UI hovering above the hand no matter how the wrist twists
        return anchorPos + offset;
    }

    void LateUpdate()
    {
        if ((sessionSpaceRoot == null || leftControllerCandidate == null) && Time.time >= nextCandidateSearchTime)
        {
            nextCandidateSearchTime = Time.time + 1f;
            RefreshAnchorCandidates();
        }

        UpdateAnchorPose();
        Transform playerCam = Camera.main != null ? Camera.main.transform : null;

        // 1. Keep Watch Button attached to Left Wrist
        if (wristWatchButtonObj != null)
        {
            if (!hasAnchorPose)
            {
                if (wristWatchButtonObj.activeSelf) wristWatchButtonObj.SetActive(false);
            }
            else
            {
                if (!wristWatchButtonObj.activeSelf) wristWatchButtonObj.SetActive(true);
                wristWatchButtonObj.transform.position = AnchorTransformPoint(watchOffset);

                // Billboard the (one-sided) canvas to the player so it is always readable,
                // regardless of how the wrist is rotated
                if (playerCam != null)
                {
                    Vector3 lookDir = playerCam.position - wristWatchButtonObj.transform.position;
                    if (lookDir.sqrMagnitude > 0.0001f)
                    {
                        wristWatchButtonObj.transform.rotation = Quaternion.LookRotation(-lookDir, Vector3.up);
                    }
                }
            }
        }

        // 2. Keep hand panels (Options + Room) floating near the hand, facing the player
        FollowHand(optionsPanelObj, playerCam);
        FollowHand(roomHudCanvas, playerCam);
    }

    /// <summary>
    /// Smoothly keeps a panel hovering above the left hand, billboarded to the player.
    /// Runs in LateUpdate so it wins over any other script moving the panel's parent.
    /// </summary>
    private void FollowHand(GameObject panel, Transform playerCam)
    {
        if (!hasAnchorPose || panel == null || !panel.activeInHierarchy) return;

        Vector3 targetPos = AnchorTransformPoint(panelOffset);
        panel.transform.position = Vector3.Lerp(panel.transform.position, targetPos, Time.deltaTime * 10f);

        if (playerCam != null)
        {
            Vector3 lookDir = playerCam.position - panel.transform.position;
            lookDir.y = 0; // Keep canvas upright
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(-lookDir, Vector3.up);
                panel.transform.rotation = Quaternion.Slerp(panel.transform.rotation, targetRot, Time.deltaTime * 10f);
            }
        }
    }

    /// <summary>
    /// Invoked when player taps the Wrist Watch button.
    /// </summary>
    public void ToggleOptionsPanel()
    {
        optionsPanelActive = !optionsPanelActive;
        if (optionsPanelObj != null)
        {
            optionsPanelObj.SetActive(optionsPanelActive);
            if (optionsPanelActive && hasAnchorPose)
            {
                optionsPanelObj.transform.position = AnchorTransformPoint(panelOffset);
            }
        }
        Debug.Log($"WristWatchMenu: Options Panel Toggled -> {optionsPanelActive}");
    }

    /// <summary>
    /// Invoked when player taps the Close ('X') button on Options Panel.
    /// </summary>
    public void CloseOptionsPanel()
    {
        optionsPanelActive = false;
        if (optionsPanelObj != null)
        {
            optionsPanelObj.SetActive(false);
        }
        Debug.Log("WristWatchMenu: Options Panel Closed.");
    }

    /// <summary>
    /// Invoked when player taps the 'Ruang' (Rooms) row in Options Panel.
    /// Swaps the options panel for the room panel, which then follows the hand.
    /// </summary>
    public void OnClickRuang()
    {
        Debug.Log("WristWatchMenu: 'Ruang' button clicked!");
        if (roomHudCanvas == null)
        {
            roomHudCanvas = GameObject.Find("RoomHUDCanvas");
        }

        if (roomHudCanvas != null)
        {
            CloseOptionsPanel();
            roomHudCanvas.SetActive(true);
            if (hasAnchorPose)
            {
                // Snap to the hand so it doesn't lerp in from wherever it last was
                roomHudCanvas.transform.position = AnchorTransformPoint(panelOffset);
            }
        }
    }

    /// <summary>
    /// Invoked when player taps the Close ('X') button on the Room panel.
    /// </summary>
    public void CloseRoomPanel()
    {
        if (roomHudCanvas != null)
        {
            roomHudCanvas.SetActive(false);
        }
        Debug.Log("WristWatchMenu: Room Panel Closed.");
    }

    /// <summary>
    /// Invoked when player taps the 'Artefak' (Artifacts) row in Options Panel.
    /// </summary>
    public void OnClickArtefak()
    {
        Debug.Log("WristWatchMenu: 'Artefak' button clicked!");
        if (artifactContainerObj != null)
        {
            artifactContainerObj.SetActive(!artifactContainerObj.activeSelf);
        }
        else
        {
            // Toggle Artifact Panel inside current scene
            GameObject artifactPanel = GameObject.Find("ArtifactDetailPanel") ?? GameObject.Find("ArtifactPanelPrefab");
            if (artifactPanel != null)
            {
                artifactPanel.SetActive(!artifactPanel.activeSelf);
            }
        }
    }
}
