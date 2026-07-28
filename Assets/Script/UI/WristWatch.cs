using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;

public class WristWatch : MonoBehaviour
{
    [Header("Hand Tracking Anchors")]
    [Tooltip("Left Hand / Wrist transform. Auto-resolved if left empty.")]
    public Transform leftHandAnchor;

    [Header("UI Objects")]
    public GameObject wristWatchButtonObj;
    public GameObject optionsPanelObj;

    [Header("Options Targets")]
    public GameObject roomHudCanvas; // Mapped to ExplorationCanvas
    public GameObject gamesPanel;    // Mapped to Minigames panel/canvas

    [Header("Explore / Room List")]
    [Tooltip("The 'Ruang'/Explore row button in the Options panel. Auto-found by name (Row_Explore/Row_Ruang) if left empty.")]
    public GameObject exploreRow;
    [Tooltip("The RoomListPanel (List Ruang - the 5-gallery chooser) shown when Explore is pressed. Auto-found under roomHudCanvas if empty.")]
    public GameObject roomListPanel;

    [Tooltip("Pin the room list rigidly to the left wrist (moves AND rotates with the wrist, does not billboard to the head). Turn off to keep it a free-floating world canvas.")]
    public bool attachRoomListToWrist = true;
    [Tooltip("Position of the room list relative to the wrist, expressed in the wrist's local space (so it stays put as the wrist rotates). Tune to place it above/in front of the wrist.")]
    public Vector3 roomListWristOffset = new Vector3(0f, 0.12f, 0.02f);
    [Tooltip("Rotation offset (Euler) of the room list relative to the wrist. Tune so the panel faces you when you raise your wrist.")]
    public Vector3 roomListWristEuler = Vector3.zero;

    [Header("Offsets")]
    [Tooltip("World-space offset for the watch button relative to the left wrist (Y = up), so the icon hovers on top of the hand regardless of wrist rotation.")]
    public Vector3 watchOffset = new Vector3(0f, 0.09f, 0f);

    [Tooltip("World-space offset for the floating options panel relative to the left wrist (Y = up).")]
    public Vector3 panelOffset = new Vector3(0f, 0.22f, 0f);

    [Header("Fixed Scale Locking")]
    [Tooltip("If true, keeps the watch button scale completely fixed so hand tracking or distance never resizes it.")]
    public bool lockWatchButtonScale = true;
    [Tooltip("The fixed local scale to enforce on wristWatchButtonObj.")]
    public Vector3 fixedWatchScale = Vector3.one;

    private bool optionsPanelActive = false;

    // The watch button is wired to ToggleOptionsPanel through several event paths at once
    // (a UI Button, an XRButtonSelection, XR-select + UI-pointer), so a single physical click
    // fires this method multiple times and the toggles cancel out. Collapse a burst of calls
    // within this window into a single toggle.
    [Tooltip("Ignore repeat toggle calls within this many seconds so one click counts once.")]
    public float toggleDebounce = 0.3f;
    private float lastToggleTime = -1f;

    // Wrist pose sources. The "Left Hand" rig object is NOT pose-driven (only its joint
    // visuals are), so hand-tracking poses must come from the XRHandSubsystem wrist joint.
    // The "Left Controller" rig object IS pose-driven, so it works as a direct anchor when
    // the player holds controllers (HandModalityForcer swaps the two at runtime).
    private XRHandSubsystem handSubsystem;
    private static List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();
    private Transform leftControllerCandidate;
    private Transform leftHandCandidate;
    private Transform sessionSpaceRoot; // "Camera Offset" object: converts session-space poses to world
    private float nextCandidateSearchTime = 0f;

    public static WristWatch Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private bool hasAnchorPose;
    private Vector3 anchorPos;
    private Quaternion anchorRot;

    [Header("Anchor Stabilization")]
    [Tooltip("When the other (right) hand comes this close to the watch button, the menu locks in place instead of chasing the left hand. Reaching in to click occludes the left hand from the headset cameras, so its tracked pose gets predicted/jittery - freezing keeps the button still and clickable.")]
    public float freezeReachDistance = 0.18f;

    // Last pose we got from SOLID left-hand tracking. We fall back to this (frozen in place)
    // whenever the left hand is occluded/untracked or the right hand is reaching in to click,
    // so the button never jumps around chasing a predicted left-hand pose.
    private bool hasLastGoodAnchor;
    private Vector3 lastGoodAnchorPos;
    private Quaternion lastGoodAnchorRot;
    private bool leftHandSolidlyTracked;

    void Start()
    {
        // Detach the menu from the left-hand transform so this script is the ONLY thing that
        // moves it. While parented, the hand's (occlusion-jittered) transform would drag the
        // button around even when we want it held still. World pose is preserved on detach.
        if (wristWatchButtonObj != null)
        {
            if (wristWatchButtonObj.transform.localScale != Vector3.zero)
            {
                fixedWatchScale = wristWatchButtonObj.transform.localScale;
            }
            if (wristWatchButtonObj.transform.parent != null)
            {
                wristWatchButtonObj.transform.SetParent(null, true);
            }
        }

        RefreshAnchorCandidates();

        if (optionsPanelObj != null)
        {
            optionsPanelObj.SetActive(false);
            UnityEngine.UI.Image panelBg = optionsPanelObj.GetComponent<UnityEngine.UI.Image>();
            if (panelBg == null) panelBg = optionsPanelObj.transform.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
            if (panelBg == null) panelBg = optionsPanelObj.GetComponentInChildren<UnityEngine.UI.Image>();
            if (panelBg != null && (panelBg.material == null || panelBg.material.name == "Default UI"))
            {
                foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m != null && (m.name == "Mat_OptionsCardBackground" || m.name == "Mat_RoomHUD"))
                    {
                        panelBg.material = m;
                        break;
                    }
                }
            }
        }

        if (roomHudCanvas == null)
        {
            roomHudCanvas = FindInactiveObject("ExplorationCanvas") ?? FindInactiveObject("RoomHUDCanvas");
        }

        if (roomHudCanvas != null)
        {
            GlanceableHUD gHUD = roomHudCanvas.GetComponent<GlanceableHUD>();
            if (gHUD != null) gHUD.enabled = false;
            roomHudCanvas.SetActive(false);
        }

        // Resolve the room-list (List Ruang) panel - it's an inactive child of the room canvas,
        // so use Transform.Find (which sees inactive objects), not GameObject.Find.
        if (roomListPanel == null && roomHudCanvas != null)
        {
            Transform t = FindDeepChild(roomHudCanvas.transform, "RoomListPanel");
            if (t != null) roomListPanel = t.gameObject;
        }

        // Wire the option rows. The row and its "expand" ActionButton (Expand button.png) had no
        // onClick action in the scene, so hook them up here. We wire EVERY Button/XRButtonSelection
        // in each row's subtree so pressing the row OR its expand icon triggers the action.
        // ("Row_Explore" is the current name; "Row_Ruang" is the older name, kept as a fallback.)
        if (exploreRow == null && optionsPanelObj != null)
        {
            Transform t = FindDeepChild(optionsPanelObj.transform, "Row_Explore")
                          ?? FindDeepChild(optionsPanelObj.transform, "Row_Ruang");
            if (t != null) exploreRow = t.gameObject;
        }
        WireRow(exploreRow, OnClickRuang);

        if (optionsPanelObj != null)
        {
            Transform t = FindDeepChild(optionsPanelObj.transform, "Row_Artefak");
            WireRow(t != null ? t.gameObject : null, OnClickArtefak);
        }
    }

    /// <summary>
    /// Wires every UI Button and XRButtonSelection under <paramref name="row"/> to invoke the
    /// given handler, so tapping the row or its expand icon runs the action. Handlers here are
    /// idempotent (they show a panel), so being invoked more than once per tap is harmless.
    /// </summary>
    private void WireRow(GameObject row, UnityEngine.Events.UnityAction handler)
    {
        if (row == null) return;

        foreach (UnityEngine.UI.Button b in row.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            b.onClick.RemoveListener(handler);
            b.onClick.AddListener(handler);
        }
        foreach (XRButtonSelection xr in row.GetComponentsInChildren<XRButtonSelection>(true))
        {
            xr.onClick.RemoveListener(handler);
            xr.onClick.AddListener(handler);
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }
        return null;
    }

    private static GameObject FindInactiveObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;

        GameObject activeObj = GameObject.Find(objectName);
        if (activeObj != null) return activeObj;

        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go != null && go.name == objectName && go.hideFlags == HideFlags.None && go.scene.isLoaded)
            {
                return go;
            }
        }
        return null;
    }

    private void RefreshAnchorCandidates()
    {
        // Session-space root: the XROrigin's floor offset object ("Camera Offset"), which
        // parents the camera and controllers and defines the space hand poses arrive in
        if (sessionSpaceRoot == null)
        {
            XROrigin origin = FindObjectOfType<XROrigin>();
            if (origin != null)
            {
                sessionSpaceRoot = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;
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

        if (leftHandCandidate == null)
        {
            foreach (Transform t in FindObjectsOfType<Transform>(true))
            {
                string n = t.name;
                if ((n == "Left Hand" || n == "LeftHand") && HasAncestorNamed(t, "Camera Offset"))
                {
                    leftHandCandidate = t;
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

    private bool GetPoseFromCandidate(Transform root, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;
        if (root == null || !root.gameObject.activeInHierarchy) return false;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || !child.gameObject.activeInHierarchy || child == root) continue;
            string n = child.name.ToLower();
            if (n.Contains("wrist") || n.Contains("palm") || n.Contains("joint_0") || n.Contains("ray") || n.Contains("interactor") || n.Contains("aim"))
            {
                pos = child.position;
                rot = child.rotation;
                return true;
            }
        }

        pos = root.position;
        rot = root.rotation;
        return true;
    }

    /// <summary>
    /// Computes this frame's left-wrist anchor pose. Priority: the live tracked wrist joint
    /// (the pose that actually follows the hand), then the Inspector-assigned transform, then
    /// the active Left Hand object in the hierarchy, and finally the pose-driven controller.
    /// </summary>
    private void UpdateAnchorPose()
    {
        hasAnchorPose = false;
        leftHandSolidlyTracked = false;

        // 1. Prefer the native XRHandSubsystem left wrist joint or palm joint.
        FindHandSubsystem();
        if (handSubsystem != null && handSubsystem.running && handSubsystem.leftHand.isTracked)
        {
            XRHandJoint joint = handSubsystem.leftHand.GetJoint(XRHandJointID.Wrist);
            if (!joint.TryGetPose(out Pose pose))
            {
                joint = handSubsystem.leftHand.GetJoint(XRHandJointID.Palm);
                joint.TryGetPose(out pose);
            }
            if (pose.position != Vector3.zero || pose.rotation != Quaternion.identity)
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
                leftHandSolidlyTracked = true;

                hasLastGoodAnchor = true;
                lastGoodAnchorPos = anchorPos;
                lastGoodAnchorRot = anchorRot;
                return;
            }
        }

        // 2. Inspector-assigned anchor (if active)
        if (leftHandAnchor != null && GetPoseFromCandidate(leftHandAnchor, out anchorPos, out anchorRot))
        {
            hasAnchorPose = true;
            hasLastGoodAnchor = true;
            lastGoodAnchorPos = anchorPos;
            lastGoodAnchorRot = anchorRot;
            return;
        }

        // 3. Active left hand object found in hierarchy
        if (leftHandCandidate != null && GetPoseFromCandidate(leftHandCandidate, out anchorPos, out anchorRot))
        {
            hasAnchorPose = true;
            hasLastGoodAnchor = true;
            lastGoodAnchorPos = anchorPos;
            lastGoodAnchorRot = anchorRot;
            return;
        }

        // 4. Fallback to active left controller object found in hierarchy
        if (leftControllerCandidate != null && GetPoseFromCandidate(leftControllerCandidate, out anchorPos, out anchorRot))
        {
            hasAnchorPose = true;
            hasLastGoodAnchor = true;
            lastGoodAnchorPos = anchorPos;
            lastGoodAnchorRot = anchorRot;
            return;
        }

        // 5. Fallback search for any active left hand or left ray transform in the scene
        foreach (var t in FindObjectsOfType<Transform>(true))
        {
            if (t == null || !t.gameObject.activeInHierarchy || t == transform) continue;
            string n = t.name.ToLower();
            if ((n.Contains("left") || n.Contains("lhs")) && (n.Contains("hand") || n.Contains("wrist") || n.Contains("ray") || n.Contains("controller") || n.Contains("aim")))
            {
                if (GetPoseFromCandidate(t, out anchorPos, out anchorRot))
                {
                    hasAnchorPose = true;
                    hasLastGoodAnchor = true;
                    lastGoodAnchorPos = anchorPos;
                    lastGoodAnchorRot = anchorRot;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// World-space position of the right hand (index fingertip, falling back to palm), used to
    /// detect when the player is reaching in to press the watch button. Returns false when the
    /// right hand isn't tracked.
    /// </summary>
    private bool TryGetRightHandPoint(out Vector3 point)
    {
        point = Vector3.zero;

        FindHandSubsystem();
        if (handSubsystem == null || !handSubsystem.running || !handSubsystem.rightHand.isTracked)
            return false;

        XRHandJoint joint = handSubsystem.rightHand.GetJoint(XRHandJointID.IndexTip);
        if (!joint.TryGetPose(out Pose p))
        {
            joint = handSubsystem.rightHand.GetJoint(XRHandJointID.Palm);
            if (!joint.TryGetPose(out p))
                return false;
        }

        point = sessionSpaceRoot != null ? sessionSpaceRoot.TransformPoint(p.position) : p.position;
        return true;
    }

    private Vector3 AnchorTransformPoint(Vector3 offset)
    {
        // World-space offset: keeps UI hovering above the hand no matter how the wrist twists
        return anchorPos + offset;
    }

    void LateUpdate()
    {
        if ((sessionSpaceRoot == null || leftControllerCandidate == null || leftHandCandidate == null) && Time.time >= nextCandidateSearchTime)
        {
            nextCandidateSearchTime = Time.time + 1f;
            RefreshAnchorCandidates();
        }

        UpdateAnchorPose();

        // Stabilization: while the left hand is untracked and player reaches in to click,
        // hold the last solid pose so the button doesn't jump around. Otherwise, use live anchor pose.
        if (!hasAnchorPose && hasLastGoodAnchor)
        {
            anchorPos = lastGoodAnchorPos;
            anchorRot = lastGoodAnchorRot;
            hasAnchorPose = true;
        }

        Transform playerCam = Camera.main != null ? Camera.main.transform : null;

        // 1. Keep Watch Button attached to Left Wrist smoothly
        if (wristWatchButtonObj != null)
        {
            if (!MainMenu.IsExplorationStarted)
            {
                if (wristWatchButtonObj.activeSelf) wristWatchButtonObj.SetActive(false);

                if (optionsPanelObj != null && optionsPanelObj.activeSelf)
                {
                    optionsPanelObj.SetActive(false);
                    optionsPanelActive = false;
                }
                if (gamesPanel != null && gamesPanel.activeSelf)
                {
                    gamesPanel.SetActive(false);
                }
            }
            else
            {
                // Once exploration is started, the wrist watch menu button ALWAYS stays active and visible on the left hand
                if (!wristWatchButtonObj.activeSelf) wristWatchButtonObj.SetActive(true);

                if (hasAnchorPose)
                {
                    Vector3 targetWatchPos = AnchorTransformPoint(watchOffset);
                    wristWatchButtonObj.transform.position = Vector3.Lerp(wristWatchButtonObj.transform.position, targetWatchPos, Time.deltaTime * 15f);

                    if (lockWatchButtonScale && fixedWatchScale != Vector3.zero)
                    {
                        wristWatchButtonObj.transform.localScale = fixedWatchScale;
                    }

                    // Billboard the (one-sided) canvas to the player cleanly so it stays upright
                    if (playerCam != null)
                    {
                        Vector3 lookDir = playerCam.position - wristWatchButtonObj.transform.position;
                        lookDir.y = 0; // Keep canvas upright, preventing rapid tilt/rotation flips
                        if (lookDir.sqrMagnitude > 0.0001f)
                        {
                            Quaternion targetRot = Quaternion.LookRotation(-lookDir, Vector3.up);
                            wristWatchButtonObj.transform.rotation = Quaternion.Slerp(wristWatchButtonObj.transform.rotation, targetRot, Time.deltaTime * 25f);
                        }
                    }
                }
            }
        }

        // 2. Keep panels anchored to the left wrist hovering cleanly above the hand facing the user.
        if (optionsPanelObj != null && optionsPanelObj.activeInHierarchy)
        {
            FollowHand(optionsPanelObj, playerCam);
        }

        if (roomHudCanvas != null && roomHudCanvas.activeInHierarchy)
        {
            // Do not follow hand if showing an Artifact Detail Panel - detail panels must stay fixed in world space!
            bool showingArtifactDetail = false;
            foreach (Transform child in roomHudCanvas.transform)
            {
                if (child != null && child.gameObject.activeInHierarchy && (child.name.Contains("ArtifactDetail") || child.name.Contains("ArtifactUI")))
                {
                    showingArtifactDetail = true;
                    break;
                }
            }

            if (!showingArtifactDetail)
            {
                GlanceableHUD gHUD = roomHudCanvas.GetComponent<GlanceableHUD>();
                if (gHUD != null && gHUD.enabled) gHUD.enabled = false;
                FollowHand(roomHudCanvas, playerCam);

                foreach (Transform child in roomHudCanvas.transform)
                {
                    if (child != null && child.gameObject.activeInHierarchy)
                    {
                        child.localRotation = Quaternion.identity;
                    }
                }
            }
        }

        GameObject roomPanelObj = GameObject.Find("RoomPanel");
        if (roomPanelObj != null && roomPanelObj.activeInHierarchy && (roomHudCanvas == null || roomPanelObj.transform.parent != roomHudCanvas.transform))
        {
            FollowHand(roomPanelObj, playerCam);
        }

        if (roomListPanel != null && roomListPanel.activeInHierarchy && roomListPanel != roomHudCanvas)
        {
            FollowHand(roomListPanel, playerCam);
        }

        if (gamesPanel != null && gamesPanel.activeInHierarchy)
        {
            FollowHand(gamesPanel, playerCam);
        }
    }

    /// <summary>
    /// Direct, live left-wrist pose straight from the XR Hands subsystem (wrist joint, falling
    /// back to palm). This bypasses UpdateAnchorPose's fuzzy candidate fallbacks, which can latch
    /// onto a STATIC interactor transform and make wrist-attached UI stop following the hand.
    /// </summary>
    private bool TryGetLeftWristPose(out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;

        FindHandSubsystem();
        if (handSubsystem != null && handSubsystem.running && handSubsystem.leftHand.isTracked)
        {
            XRHandJoint joint = handSubsystem.leftHand.GetJoint(XRHandJointID.Wrist);
            if (!joint.TryGetPose(out Pose pose))
            {
                joint = handSubsystem.leftHand.GetJoint(XRHandJointID.Palm);
                joint.TryGetPose(out pose);
            }
            if (pose.position != Vector3.zero || pose.rotation != Quaternion.identity)
            {
                if (sessionSpaceRoot != null)
                {
                    pos = sessionSpaceRoot.TransformPoint(pose.position);
                    rot = sessionSpaceRoot.rotation * pose.rotation;
                }
                else
                {
                    pos = pose.position;
                    rot = pose.rotation;
                }
                return true;
            }
        }

        // Fallback: a live hand-visual wrist bone in the scene (whatever actually drives the
        // rendered hand), so we still follow even if the XR Hands subsystem reports untracked.
        Transform bone = FindLeftWristBone();
        if (bone != null)
        {
            pos = bone.position;
            rot = bone.rotation;
            return true;
        }

        if (leftHandAnchor != null && leftHandAnchor.gameObject.activeInHierarchy)
        {
            pos = leftHandAnchor.position;
            rot = leftHandAnchor.rotation;
            return true;
        }

        if (leftControllerCandidate != null && leftControllerCandidate.gameObject.activeInHierarchy)
        {
            pos = leftControllerCandidate.position;
            rot = leftControllerCandidate.rotation;
            return true;
        }

        if (leftHandCandidate != null && leftHandCandidate.gameObject.activeInHierarchy)
        {
            pos = leftHandCandidate.position;
            rot = leftHandCandidate.rotation;
            return true;
        }

        return false;
    }

    private Transform cachedWristBone;

    /// <summary>
    /// Finds a pose-driven left wrist/hand-joint transform among the hand objects, deliberately
    /// skipping interactor/ray/aim transforms (which can be static) so we track the moving hand.
    /// </summary>
    private Transform FindLeftWristBone()
    {
        if (cachedWristBone != null && cachedWristBone.gameObject.activeInHierarchy)
            return cachedWristBone;
        cachedWristBone = null;

        Transform[] roots = { leftHandCandidate, leftHandAnchor };
        // Prefer something explicitly named like a wrist, then any hand joint/bone.
        string[] preferred = { "wrist", "palm", "hand_l", "l_hand", "lefthand", "joint" };
        foreach (string key in preferred)
        {
            foreach (Transform root in roots)
            {
                if (root == null) continue;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == root || !t.gameObject.activeInHierarchy) continue;
                    string n = t.name.ToLower();
                    if (n.Contains("interactor") || n.Contains("ray") || n.Contains("aim") ||
                        n.Contains("poke") || n.Contains("stabil") || n.Contains("attach"))
                        continue;
                    if (n.Contains(key))
                    {
                        cachedWristBone = t;
                        return t;
                    }
                }
            }
        }
        // Fallback: search scene-wide for any active left hand/wrist transform
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            if (t == null || !t.gameObject.activeInHierarchy || t == transform) continue;
            string n = t.name.ToLower();
            if (n.Contains("interactor") || n.Contains("ray") || n.Contains("aim") || n.Contains("poke") || n.Contains("canvas"))
                continue;
            if ((n.Contains("left") || n.Contains("lhs")) && (n.Contains("wrist") || n.Contains("palm") || n.Contains("hand") || n.Contains("joint")))
            {
                cachedWristBone = t;
                return t;
            }
        }
        return null;
    }

    /// <summary>
    /// Rigidly pins a panel to the wrist: both its position and rotation come from the wrist
    /// pose, so it moves and rotates WITH the wrist and does not billboard to the head. The
    /// offset/rotation are expressed in wrist-local space so they stay fixed as the wrist turns.
    /// Prefers the live XR Hands wrist joint so it keeps following even if UpdateAnchorPose's
    /// stored anchor has fallen back to a static source.
    /// </summary>
    private void FollowWristRigid(GameObject panel)
    {
        if (panel == null || !panel.activeInHierarchy) return;

        Vector3 wristPos;
        Quaternion wristRot;
        if (!TryGetLeftWristPose(out wristPos, out wristRot))
        {
            if (!hasAnchorPose) return;
            wristPos = anchorPos;
            wristRot = anchorRot;
        }

        Vector3 targetPos = wristPos + wristRot * roomListWristOffset;
        Quaternion targetRot = wristRot * Quaternion.Euler(roomListWristEuler);

        // Immediate 1-to-1 position and rotation assignment so the panel follows simultaneously with zero lag
        panel.transform.position = targetPos;
        panel.transform.rotation = targetRot;
    }

    /// <summary>
    /// Smoothly keeps a panel hovering above the left hand, billboarded to the player.
    /// Runs in LateUpdate so it wins over any other script moving the panel's parent.
    /// </summary>
    private void FollowHand(GameObject panel, Transform playerCam)
    {
        if (panel == null || !panel.activeInHierarchy) return;

        if (playerCam == null && Camera.main != null)
        {
            playerCam = Camera.main.transform;
        }

        Vector3 handPos;
        Quaternion handRot;
        if (!TryGetLeftWristPose(out handPos, out handRot))
        {
            if (!hasAnchorPose) return;
            handPos = anchorPos;
            handRot = anchorRot;
        }

        Vector3 targetPos = handPos + panelOffset;

        if (Vector3.Distance(panel.transform.position, targetPos) > 0.4f)
        {
            panel.transform.position = targetPos;
        }
        else
        {
            panel.transform.position = Vector3.Lerp(panel.transform.position, targetPos, Time.deltaTime * 30f);
        }

        if (playerCam != null)
        {
            Vector3 lookDir = playerCam.position - panel.transform.position;
            lookDir.y = 0; // Keep canvas upright facing user
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(-lookDir, Vector3.up);
                panel.transform.rotation = Quaternion.Slerp(panel.transform.rotation, targetRot, Time.deltaTime * 30f);
            }
        }
    }

    /// <summary>
    /// Invoked when player taps the Wrist Watch button.
    /// </summary>
    public void ToggleOptionsPanel()
    {
        // Debounce redundant click events so one physical click = one toggle.
        if (Time.unscaledTime - lastToggleTime < toggleDebounce) return;
        lastToggleTime = Time.unscaledTime;

        optionsPanelActive = !optionsPanelActive;
        if (optionsPanelObj != null)
        {
            optionsPanelObj.SetActive(optionsPanelActive);
            if (optionsPanelActive)
            {
                // Ensure other list panels are hidden so both never appear at once
                if (roomHudCanvas != null) roomHudCanvas.SetActive(false);
                if (gamesPanel != null) gamesPanel.SetActive(false);

                if (hasAnchorPose)
                {
                    optionsPanelObj.transform.position = AnchorTransformPoint(panelOffset);
                }
            }
        }
        else if (!optionsPanelActive)
        {
            if (roomHudCanvas != null) roomHudCanvas.SetActive(false);
            if (gamesPanel != null) gamesPanel.SetActive(false);
        }
        Debug.Log($"WristWatch: Options Panel Toggled -> {optionsPanelActive}");
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
        if (wristWatchButtonObj != null)
        {
            wristWatchButtonObj.SetActive(true);
        }
        Debug.Log("WristWatch: Options Panel Closed.");
    }

    public void EnsureWatchButtonVisible()
    {
        optionsPanelActive = false;
        if (optionsPanelObj != null) optionsPanelObj.SetActive(false);
        if (gamesPanel != null) gamesPanel.SetActive(false);
        if (roomHudCanvas != null) roomHudCanvas.SetActive(false);

        if (wristWatchButtonObj != null)
        {
            wristWatchButtonObj.SetActive(true);
        }
        Debug.Log("WristWatch: EnsureWatchButtonVisible called.");
    }

    /// <summary>
    /// Invoked when player taps the 'Ruang' (Rooms) / 'Explore' row in Options Panel.
    /// Swaps the options panel for the room panel, which then follows the hand.
    /// </summary>
    public void OnClickRuang()
    {
        Debug.Log("WristWatch: 'Explore' button clicked!");
        MainMenu.IsExplorationStarted = true;

        CloseOptionsPanel();

        if (roomHudCanvas == null)
        {
            roomHudCanvas = FindInactiveObject("ExplorationCanvas") ?? FindInactiveObject("RoomHUDCanvas");
        }

        if (roomHudCanvas != null)
        {
            GlanceableHUD gHUD = roomHudCanvas.GetComponent<GlanceableHUD>();
            if (gHUD != null) Destroy(gHUD);

            roomHudCanvas.SetActive(true);

            ShowRoomListPanel();

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.PopulateRoomListUI();
            }

            if (hasAnchorPose)
            {
                roomHudCanvas.transform.position = AnchorTransformPoint(panelOffset);
            }
        }
        if (gamesPanel != null) gamesPanel.SetActive(false);
    }

    private void ShowRoomListPanel()
    {
        if (roomHudCanvas == null)
        {
            roomHudCanvas = FindInactiveObject("ExplorationCanvas") ?? FindInactiveObject("RoomHUDCanvas");
        }
        if (roomHudCanvas == null) return;

        RoomList rList = roomHudCanvas.GetComponentInChildren<RoomList>(true);
        if (rList != null)
        {
            if (rList.roomPanel != null)
            {
                rList.roomPanel.gameObject.SetActive(false);
            }
            rList.gameObject.SetActive(true);
            rList.PopulateRoomsList();
        }
        else
        {
            if (roomListPanel == null)
            {
                Transform t = FindDeepChild(roomHudCanvas.transform, "RoomListPanel");
                if (t != null) roomListPanel = t.gameObject;
            }
            if (roomListPanel == null) roomListPanel = FindInactiveObject("RoomListPanel");

            if (roomListPanel != null)
            {
                roomListPanel.SetActive(true);
                Transform parent = roomListPanel.transform.parent;
                if (parent != null)
                {
                    foreach (Transform sibling in parent)
                    {
                        if (sibling.name == "RoomListPanel")
                        {
                            sibling.gameObject.SetActive(true);
                        }
                        else if (sibling.name == "ArtifactDetailPanel" || sibling.name == "RoomPanel")
                        {
                            sibling.gameObject.SetActive(false);
                        }
                    }
                }
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
        Debug.Log("WristWatch: Room Panel Closed.");
    }

    /// <summary>
    /// Invoked when player taps the 'Artefak' (Artifacts) / 'Games' button in Options Panel.
    /// </summary>
    public void OnClickArtefak()
    {
        Debug.Log("WristWatch: 'Games' button clicked!");
        MainMenu.IsExplorationStarted = true;
        CloseOptionsPanel();

        if (gamesPanel != null)
        {
            gamesPanel.SetActive(true);
            if (hasAnchorPose)
            {
                gamesPanel.transform.position = AnchorTransformPoint(panelOffset);
            }
        }
    }

    /// <summary>
    /// Invoked when player taps the Close ('X') button on the Games panel.
    /// </summary>
    public void CloseGamesPanel()
    {
        if (gamesPanel != null)
        {
            gamesPanel.SetActive(false);
        }
        Debug.Log("WristWatch: Games Panel Closed.");
    }
}
