using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using Unity.XR.CoreUtils;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

    [Header("Theme Toggle")]
    [Tooltip("Small rounded button beside close button in Options panel to toggle theme.")]
    [System.NonSerialized] public GameObject themeToggleBtn;
    [System.NonSerialized] private UnityEngine.UI.Image themeIconImg;
    public GameObject themeRow;
    private TMPro.TextMeshProUGUI themeLabelText;

    [Tooltip("Pin the room list rigidly to the left wrist (moves AND rotates with the wrist, does not billboard to the head). Turn off to keep it a free-floating world canvas.")]
    public bool attachRoomListToWrist = true;
    [Tooltip("Position of the room list relative to the wrist, expressed in the wrist's local space (so it stays put as the wrist rotates). Tune to place it above/in front of the wrist.")]
    public Vector3 roomListWristOffset = new Vector3(0f, 0.48f, 0.02f);
    [Tooltip("Rotation offset (Euler) of the room list relative to the wrist. Tune so the panel faces you when you raise your wrist.")]
    public Vector3 roomListWristEuler = Vector3.zero;

    [Header("Offsets")]
    [Tooltip("World-space offset for the watch button relative to the left wrist (Y = up), so the icon hovers on top of the hand regardless of wrist rotation.")]
    public Vector3 watchOffset = new Vector3(0f, 0.09f, 0f);

    [Tooltip("World-space offset for the floating options panel relative to the left wrist (Y = up).")]
    public Vector3 panelOffset = new Vector3(0f, 0.20f, 0.02f);

    [Header("Fixed Scale Locking")]
    [Tooltip("If true, keeps the watch button scale completely fixed so hand tracking or distance never resizes it.")]
    public bool lockWatchButtonScale = true;
    [Tooltip("The fixed local scale to enforce on wristWatchButtonObj.")]
    public Vector3 fixedWatchScale = Vector3.one;

    // While true, the watch button (and any open options/games panel) is force-hidden
    // regardless of MainMenu.IsExplorationStarted - used by TutorialManager so the wrist
    // menu doesn't compete for attention during the tutorial. Must be respected INSIDE
    // LateUpdate below: the "always visible once exploring" logic there runs every frame,
    // so a one-off SetActive(false) from outside would be overridden within a frame.
    [HideInInspector] public bool forceHidden = false;

    private bool optionsPanelActive = false;

    // The watch button is wired to ToggleOptionsPanel through several event paths at once
    // (a UI Button, an XRButtonSelection, XR-select + UI-pointer), so a single physical click
    // fires this method multiple times and the toggles cancel out. Collapse a burst of calls
    // within this window into a single toggle.
    [Tooltip("Ignore repeat toggle calls within this many seconds so one click counts once.")]
    public float toggleDebounce = 0.3f;
    private float lastToggleTime = -1f;

    [Header("Gesture Detection (Thumbs Up)")]
    [Tooltip("If true, a Left Hand Thumbs-Up gesture will trigger the options panel to open.")]
    public bool enableThumbsUpGesture = true;

    [Tooltip("Minimum time (in seconds) the thumbs-up gesture must be steadily held before triggering.")]
    public float thumbsUpHoldDuration = 0.30f;

    [Tooltip("Cooldown period (in seconds) after a trigger during which the gesture will not re-trigger.")]
    public float thumbsUpCooldown = 1.20f;

    [Tooltip("Maximum allowed curl value for the thumb (0 = fully straight, 1 = fully curled). Lower means stricter straightness.")]
    [Range(0.1f, 0.6f)]
    public float thumbCurlThreshold = 0.35f;

    [Tooltip("Minimum required curl value for the other 4 fingers (Index, Middle, Ring, Little). Higher means tighter fist.")]
    [Range(0.3f, 0.9f)]
    public float fingerCurlThreshold = 0.55f;

    [Tooltip("Alignment threshold between thumb direction and world up (1.0 = straight up). Default 0.60 allows ~53 degrees tilt.")]
    [Range(0.4f, 0.95f)]
    public float thumbUpAlignmentThreshold = 0.60f;

    [Tooltip("If true, making a thumbs-up gesture while the options panel is open will close it (toggle). If false, it only opens if closed.")]
    public bool toggleMenuWithThumbsUp = false;

    [Tooltip("Log debug information to the Unity Console when a thumbs-up gesture is detected.")]
    public bool debugLogGesture = true;

    private float currentThumbsUpHoldTime = 0f;
    private float thumbsUpCooldownTimer = 0f;

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

    private Vector3 galleryPanelOffset = new Vector3(0f, 0.28f, 0.02f);

    private void Awake()
    {
        Instance = this;

        // Ensure Options Panel sits low near wrist button (Y = 0.20f)
        panelOffset = new Vector3(0f, 0.20f, 0.02f);

        // Ensure List Ruang (Room List) panel sits a little bit higher (Y = 0.54f)
        roomListWristOffset = new Vector3(0f, 0.54f, 0.02f);

        // Ensure Galery Panel (individual room view) sits lower (Y = 0.28f)
        galleryPanelOffset = new Vector3(0f, 0.28f, 0.02f);
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
            if (wristWatchButtonObj.GetComponent<WristWatchButtonGuard>() == null)
            {
                wristWatchButtonObj.AddComponent<WristWatchButtonGuard>();
            }
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

        SetupThemeButton();
        ThemeManager.OnThemeChanged += OnThemeChanged;
    }

    private void OnDestroy()
    {
        ThemeManager.OnThemeChanged -= OnThemeChanged;
    }

    private void SetupThemeButton()
    {
        if (optionsPanelObj == null) return;

        // 1. Restore original anchors and material of Row_Explore and Row_Artefak
        Material rowMat = null;
        foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (m != null && m.name == "Mat_OptionsRowCard") { rowMat = m; break; }
        }

        Transform artefakRow = FindDeepChild(optionsPanelObj.transform, "Row_Artefak");
        if (exploreRow != null)
        {
            RectTransform rtExplore = exploreRow.GetComponent<RectTransform>();
            if (rtExplore != null)
            {
                rtExplore.anchorMin = new Vector2(0.06f, 0.44f);
                rtExplore.anchorMax = new Vector2(0.94f, 0.72f);
            }
            if (rowMat != null)
            {
                UnityEngine.UI.Image img = exploreRow.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.material = rowMat;
                    img.sprite = null;
                    img.color = Color.white;
                }
            }
        }
        if (artefakRow != null)
        {
            RectTransform rtArtefak = artefakRow.GetComponent<RectTransform>();
            if (rtArtefak != null)
            {
                rtArtefak.anchorMin = new Vector2(0.06f, 0.10f);
                rtArtefak.anchorMax = new Vector2(0.94f, 0.38f);
            }
            if (rowMat != null)
            {
                UnityEngine.UI.Image img = artefakRow.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.material = rowMat;
                    img.sprite = null;
                    img.color = Color.white;
                }
            }
        }

        // 2. Remove legacy Row_Theme if present
        Transform oldRowTheme = FindDeepChild(optionsPanelObj.transform, "Row_Theme");
        if (oldRowTheme != null)
        {
            Destroy(oldRowTheme.gameObject);
        }

        // 3. Locate CloseButton in options panel
        Transform closeBtnT = optionsPanelObj.transform.Find("CloseButton");
        if (closeBtnT == null) closeBtnT = FindDeepChild(optionsPanelObj.transform, "CloseButton");

        // 4. Adjust TitleText width so it doesn't collide with the toggle button
        Transform titleT = optionsPanelObj.transform.Find("TitleText");
        if (titleT == null) titleT = FindDeepChild(optionsPanelObj.transform, "TitleText");
        if (titleT != null && closeBtnT != null)
        {
            RectTransform rtTitle = titleT.GetComponent<RectTransform>();
            RectTransform rtClose = closeBtnT.GetComponent<RectTransform>();
            if (rtTitle != null && rtClose != null)
            {
                float anchorW = rtClose.anchorMax.x - rtClose.anchorMin.x;
                float gap = 0.045f;
                float themeLeft = rtClose.anchorMin.x - gap - anchorW;
                rtTitle.anchorMax = new Vector2(Mathf.Min(rtTitle.anchorMax.x, themeLeft - 0.02f), rtTitle.anchorMax.y);
            }
        }

        if (themeToggleBtn == null)
        {
            Transform existing = optionsPanelObj.transform.Find("ThemeToggleButton");
            if (existing == null) existing = FindDeepChild(optionsPanelObj.transform, "ThemeToggleButton");
            if (existing != null) themeToggleBtn = existing.gameObject;
        }

        if (themeToggleBtn == null && closeBtnT != null)
        {
            // Clone CloseButton to inherit identical circular background and collider
            themeToggleBtn = Instantiate(closeBtnT.gameObject, optionsPanelObj.transform);
            themeToggleBtn.name = "ThemeToggleButton";
        }

        if (themeToggleBtn != null && closeBtnT != null)
        {
            RectTransform rtClose = closeBtnT.GetComponent<RectTransform>();
            RectTransform rtTheme = themeToggleBtn.GetComponent<RectTransform>();
            if (rtClose != null && rtTheme != null)
            {
                // Align pivot, scale, rotation, and size delta with CloseButton
                rtTheme.pivot = rtClose.pivot;
                rtTheme.sizeDelta = rtClose.sizeDelta;
                rtTheme.localScale = rtClose.localScale;
                rtTheme.localEulerAngles = rtClose.localEulerAngles;

                float width = rtClose.anchorMax.x - rtClose.anchorMin.x;
                float gap = 0.045f;

                // Match vertical row anchors and position EXACTLY with CloseButton
                rtTheme.anchorMin = new Vector2(rtClose.anchorMin.x - gap - width, rtClose.anchorMin.y);
                rtTheme.anchorMax = new Vector2(rtClose.anchorMin.x - gap, rtClose.anchorMax.y);
                rtTheme.anchoredPosition = new Vector2(rtClose.anchoredPosition.x, rtClose.anchoredPosition.y);
            }
        }

        if (themeToggleBtn != null)
        {
            // 1. Completely eliminate any cloned "X" text or other non-icon children
            for (int i = themeToggleBtn.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = themeToggleBtn.transform.GetChild(i);
                if (child.name != "ThemeIcon")
                {
                    child.gameObject.SetActive(false);
                    DestroyImmediate(child.gameObject);
                }
            }
            foreach (TMPro.TMP_Text t in themeToggleBtn.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                t.text = "";
                t.gameObject.SetActive(false);
                DestroyImmediate(t.gameObject);
            }
            foreach (UnityEngine.UI.Text t in themeToggleBtn.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                t.text = "";
                t.gameObject.SetActive(false);
                DestroyImmediate(t.gameObject);
            }

            // 2. Remove old XRButtonSelection / Button which cloned CloseOptionsPanel persistent calls from CloseButton
            foreach (XRButtonSelection oldXr in themeToggleBtn.GetComponents<XRButtonSelection>())
            {
                DestroyImmediate(oldXr);
            }
            foreach (UnityEngine.UI.Button oldBtn in themeToggleBtn.GetComponents<UnityEngine.UI.Button>())
            {
                DestroyImmediate(oldBtn);
            }

            // Ensure collider is enabled, trigger, and properly sized for hand-ray / poke pinch detection
            BoxCollider col = themeToggleBtn.GetComponent<BoxCollider>();
            if (col == null) col = themeToggleBtn.AddComponent<BoxCollider>();
            col.enabled = true;
            col.isTrigger = true;
            col.size = new Vector3(32f, 32f, 25f);
            col.center = Vector3.zero;

            // 3. Add fresh XRButtonSelection for MR/VR interactions (free of any CloseOptionsPanel call)
            XRButtonSelection newXr = themeToggleBtn.AddComponent<XRButtonSelection>();
            newXr.buttonImage = themeToggleBtn.GetComponent<UnityEngine.UI.Image>();
            newXr.scaleTarget = themeToggleBtn.transform;
            newXr.normalColor = new Color(0.9f, 0.9f, 0.93f, 0.8f);
            newXr.hoverColor = new Color(0.8f, 0.85f, 0.96f, 0.95f);
            newXr.hoverScaleMultiplier = 1.15f;
            newXr.transitionSpeed = 10f;
            newXr.interactionLayers = ~0; // Accept all interactors (hand rays, pokes, controllers)
            newXr.colliders.Clear();
            newXr.colliders.Add(col);
            newXr.onClick.AddListener(OnClickThemeToggle);

            // 4. Add fresh UGUI Button for standard clicks
            UnityEngine.UI.Button newBtn = themeToggleBtn.AddComponent<UnityEngine.UI.Button>();
            newBtn.targetGraphic = themeToggleBtn.GetComponent<UnityEngine.UI.Image>();
            newBtn.onClick.AddListener(OnClickThemeToggle);

            // 5. Setup ThemeIcon
            Transform iconT = themeToggleBtn.transform.Find("ThemeIcon");
            if (iconT == null)
            {
                GameObject iconObj = new GameObject("ThemeIcon");
                iconObj.transform.SetParent(themeToggleBtn.transform, false);
                RectTransform iconRt = iconObj.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.20f, 0.20f);
                iconRt.anchorMax = new Vector2(0.80f, 0.80f);
                iconRt.sizeDelta = Vector2.zero;
                iconRt.anchoredPosition = Vector2.zero;
                themeIconImg = iconObj.AddComponent<UnityEngine.UI.Image>();
                themeIconImg.raycastTarget = false;
                themeIconImg.preserveAspect = true;
            }
            else
            {
                themeIconImg = iconT.GetComponent<UnityEngine.UI.Image>();
                if (themeIconImg == null) themeIconImg = iconT.gameObject.AddComponent<UnityEngine.UI.Image>();
                themeIconImg.raycastTarget = false;
                themeIconImg.preserveAspect = true;
            }

            UpdateThemeButtonVisuals(ThemeManager.Instance != null ? ThemeManager.Instance.currentTheme : UIThemeMode.Dark);
        }
    }

    private float lastThemeToggleTime = -1f;
    private const float ThemeToggleDebounce = 0.35f;

    private void OnClickThemeToggle()
    {
        // Debounce redundant click events so a single quick pinch (which fires both PointerDown and PointerClick)
        // toggles the theme exactly once instead of toggling twice and cancelling out.
        if (Time.unscaledTime - lastThemeToggleTime < ThemeToggleDebounce) return;
        lastThemeToggleTime = Time.unscaledTime;

        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.ToggleTheme();
        }

        if (themeIconImg != null)
        {
            UIAnimationHelper.AnimateThemeIcon(themeIconImg.transform, 0.28f);
        }

        // Guarantee options panel remains open so user sees new theme instantly
        optionsPanelActive = true;
        if (optionsPanelObj != null && !optionsPanelObj.activeSelf)
        {
            UIAnimationHelper.FadeIn(optionsPanelObj, 0.20f, true);
        }
    }

    private void OnThemeChanged(UIThemeMode newTheme)
    {
        UpdateThemeButtonVisuals(newTheme);
        if (optionsPanelObj != null && ThemeManager.Instance != null)
        {
            ThemeManager.Instance.ApplyToHierarchy(optionsPanelObj, newTheme);
        }
    }

    private void UpdateThemeButtonVisuals(UIThemeMode theme)
    {
        if (themeIconImg != null && ThemeManager.Instance != null)
        {
            if (theme == UIThemeMode.Light)
            {
                // In Light Mode, show crescent Moon icon (click to switch to Dark Mode)
                themeIconImg.sprite = ThemeManager.Instance.GetOrCreateMoonIconSprite();
                themeIconImg.color = new Color(0.95f, 0.95f, 0.95f, 1.0f);
            }
            else
            {
                // In Dark Mode, show radiant Sun icon (click to switch to Light Mode)
                themeIconImg.sprite = ThemeManager.Instance.GetOrCreateSunIconSprite();
                themeIconImg.color = new Color(1.0f, 0.92f, 0.55f, 1.0f); // Warm sunbeam gold
            }
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
            if (b.targetGraphic != null) b.targetGraphic.raycastTarget = true;
            if (b.GetComponent<UIButtonAudio>() == null)
            {
                b.gameObject.AddComponent<UIButtonAudio>();
            }
        }
        foreach (XRButtonSelection xr in row.GetComponentsInChildren<XRButtonSelection>(true))
        {
            xr.onClick.RemoveListener(handler);
            xr.onClick.AddListener(handler);
            if (xr.GetComponent<UIButtonAudio>() == null)
            {
                xr.gameObject.AddComponent<UIButtonAudio>();
            }
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
        UpdateThumbsUpGestureDetection();
        CheckControllerMenuButton();

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
            if (!wristWatchButtonObj.activeSelf) wristWatchButtonObj.SetActive(true);

            bool hideWatchButton = IsWatchButtonHidden();
            SetWatchButtonVisualsVisible(!hideWatchButton);

            if (!MainMenu.IsExplorationStarted || forceHidden)
            {
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
            FollowHand(optionsPanelObj, playerCam, panelOffset);
        }

        if (roomHudCanvas != null && roomHudCanvas.activeInHierarchy)
        {
            // Do not follow hand if showing an Artifact Detail Panel - detail panels must stay fixed in world space!
            bool showingArtifactDetail = false;
            bool showingGalleryRoomPanel = false;

            foreach (Transform child in roomHudCanvas.transform)
            {
                if (child != null && child.gameObject.activeInHierarchy)
                {
                    if (child.name.Contains("ArtifactDetail") || child.name.Contains("ArtifactUI"))
                    {
                        showingArtifactDetail = true;
                    }
                    if (child.name == "RoomPanel" || child.GetComponent<Room>() != null)
                    {
                        showingGalleryRoomPanel = true;
                    }
                }
            }

            if (!showingArtifactDetail)
            {
                GlanceableHUD gHUD = roomHudCanvas.GetComponent<GlanceableHUD>();
                if (gHUD != null && gHUD.enabled) gHUD.enabled = false;

                // Use lower galleryPanelOffset (0.28f) when showing a specific gallery room view,
                // and higher roomListWristOffset (0.54f) when showing the list room chooser panel.
                Vector3 activeOffset = showingGalleryRoomPanel ? galleryPanelOffset : roomListWristOffset;
                FollowHand(roomHudCanvas, playerCam, activeOffset);

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
            FollowHand(roomPanelObj, playerCam, galleryPanelOffset);
        }

        if (roomListPanel != null && roomListPanel.activeInHierarchy && roomListPanel != roomHudCanvas)
        {
            FollowHand(roomListPanel, playerCam, roomListWristOffset);
        }
        // Note: gamesPanel is kept fixed in world space and does NOT follow wrist
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
    /// <summary>
    /// Smoothly keeps a panel hovering above the left hand, billboarded to the player.
    /// Runs in LateUpdate so it wins over any other script moving the panel's parent.
    /// </summary>
    /// <summary>
    /// Smoothly keeps a panel hovering above the left hand, billboarded to the player.
    /// Runs in LateUpdate so it wins over any other script moving the panel's parent.
    /// </summary>
    private void FollowHand(GameObject panel, Transform playerCam, Vector3 offset = default)
    {
        if (panel == null || !panel.activeInHierarchy) return;

        if (offset == default) offset = panelOffset;

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

        Vector3 targetPos = handPos + offset;

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
    /// <summary>
    /// Checks whether the wrist watch button icon is currently hidden (e.g. before exploration, during tutorials, while another panel is open).
    /// </summary>
    public bool IsWatchButtonHidden()
    {
        return !MainMenu.IsExplorationStarted || forceHidden || optionsPanelActive || (optionsPanelObj != null && optionsPanelObj.activeInHierarchy) || IsContentPanelActive();
    }

    /// <summary>
    /// Opens the main wrist options panel explicitly if it is currently closed.
    /// </summary>
    public void OpenOptionsPanel()
    {
        if (!MainMenu.IsExplorationStarted || forceHidden) return;
        if (!optionsPanelActive)
        {
            ToggleOptionsPanel();
        }
    }

    /// <summary>
    /// Toggles the main wrist options panel open/closed.
    /// </summary>
    public void ToggleOptionsPanel()
    {
        if (IsWatchButtonHidden() && !optionsPanelActive)
        {
            return;
        }

        // Debounce redundant click events so one physical click = one toggle.
        if (Time.unscaledTime - lastToggleTime < toggleDebounce) return;
        lastToggleTime = Time.unscaledTime;

        optionsPanelActive = !optionsPanelActive;
        if (optionsPanelObj != null)
        {
            if (optionsPanelActive)
            {
                // Ensure other wrist list panels are hidden so they don't overlap on the wrist
                if (roomListPanel != null) UIAnimationHelper.FadeOut(roomListPanel, 0.12f);
                if (gamesPanel != null) UIAnimationHelper.FadeOut(gamesPanel, 0.12f);
                if (HistoryListPanel.Instance != null) HistoryListPanel.Instance.gameObject.SetActive(false);

                GameObject roomPanelObj = GameObject.Find("RoomPanel");
                if (roomPanelObj != null && roomPanelObj.activeSelf) roomPanelObj.SetActive(false);

                if (hasAnchorPose)
                {
                    optionsPanelObj.transform.position = AnchorTransformPoint(panelOffset);
                }

                UIAnimationHelper.FadeIn(optionsPanelObj, 0.20f, true);
            }
            else
            {
                UIAnimationHelper.FadeOut(optionsPanelObj, 0.15f);
            }
        }
        else if (!optionsPanelActive)
        {
            if (roomListPanel != null) UIAnimationHelper.FadeOut(roomListPanel, 0.12f);
            if (gamesPanel != null) UIAnimationHelper.FadeOut(gamesPanel, 0.12f);
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
            UIAnimationHelper.FadeOut(optionsPanelObj, 0.15f);
        }
        if (wristWatchButtonObj != null)
        {
            wristWatchButtonObj.SetActive(true);
        }
        Debug.Log("WristWatch: Options Panel Closed.");
    }

    /// <summary>
    /// Checks for controller Menu button (or Y button) press on Meta Quest Touch controllers
    /// to toggle the WristWatch Options Panel directly when holding controllers.
    /// </summary>
    private void CheckControllerMenuButton()
    {
        if (!MainMenu.IsExplorationStarted || forceHidden) return;

        UnityEngine.XR.InputDevice leftControllerDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (leftControllerDevice.isValid)
        {
            if (leftControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool menuPressed) && menuPressed)
            {
                ToggleOptionsPanel();
                return;
            }
            if (leftControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool yPressed) && yPressed)
            {
                ToggleOptionsPanel();
                return;
            }
        }

        UnityEngine.XR.InputDevice rightControllerDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (rightControllerDevice.isValid)
        {
            if (rightControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool rMenuPressed) && rMenuPressed)
            {
                ToggleOptionsPanel();
                return;
            }
        }
    }

    /// <summary>
    /// Evaluates left hand joints in real time to detect a Thumbs-Up gesture.
    /// When held for thumbsUpHoldDuration, triggers OpenOptionsPanel() or ToggleOptionsPanel().
    /// Also supports pressing 'T' in the Unity Editor for desktop simulation.
    /// </summary>
    private void UpdateThumbsUpGestureDetection()
    {
        if (!enableThumbsUpGesture) return;
        if (!MainMenu.IsExplorationStarted || forceHidden) return;

        if (thumbsUpCooldownTimer > 0f)
        {
            thumbsUpCooldownTimer -= Time.unscaledDeltaTime;
        }

#if UNITY_EDITOR
        // Desktop simulation: Press 'T' to simulate Left Hand Thumbs Up
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
            Debug.Log("<color=cyan>WristWatch: [Editor Simulation] Thumbs Up triggered via 'T' key!</color>");
            ExecuteThumbsUpTrigger();
            return;
        }
#endif

        if (handSubsystem == null || !handSubsystem.running || !handSubsystem.leftHand.isTracked)
        {
            currentThumbsUpHoldTime = 0f;
            return;
        }

        XRHand leftHand = handSubsystem.leftHand;
        bool isThumbsUp = CheckThumbsUpGesture(leftHand);

        if (isThumbsUp)
        {
            currentThumbsUpHoldTime += Time.unscaledDeltaTime;
            if (currentThumbsUpHoldTime >= thumbsUpHoldDuration)
            {
                if (thumbsUpCooldownTimer <= 0f)
                {
                    ExecuteThumbsUpTrigger();
                }
                currentThumbsUpHoldTime = 0f;
            }
        }
        else
        {
            currentThumbsUpHoldTime = Mathf.Max(0f, currentThumbsUpHoldTime - Time.unscaledDeltaTime * 2f);
        }
    }

    private bool CheckThumbsUpGesture(XRHand hand)
    {
        // 1. Thumb must be extended (low curl)
        XRFingerShape thumbShape = hand.CalculateFingerShape(XRHandFingerID.Thumb, XRFingerShapeTypes.FullCurl);
        if (thumbShape.TryGetFullCurl(out float thumbCurl))
        {
            if (thumbCurl > thumbCurlThreshold) return false;
        }

        // 2. The four fingers (Index, Middle, Ring, Little) must be curled into palm
        XRFingerShape indexShape = hand.CalculateFingerShape(XRHandFingerID.Index, XRFingerShapeTypes.FullCurl);
        XRFingerShape middleShape = hand.CalculateFingerShape(XRHandFingerID.Middle, XRFingerShapeTypes.FullCurl);
        XRFingerShape ringShape = hand.CalculateFingerShape(XRHandFingerID.Ring, XRFingerShapeTypes.FullCurl);
        XRFingerShape littleShape = hand.CalculateFingerShape(XRHandFingerID.Little, XRFingerShapeTypes.FullCurl);

        int curledCount = 0;
        if (indexShape.TryGetFullCurl(out float indexCurl) && indexCurl >= fingerCurlThreshold) curledCount++;
        if (middleShape.TryGetFullCurl(out float middleCurl) && middleCurl >= fingerCurlThreshold) curledCount++;
        if (ringShape.TryGetFullCurl(out float ringCurl) && ringCurl >= fingerCurlThreshold) curledCount++;
        if (littleShape.TryGetFullCurl(out float littleCurl) && littleCurl >= fingerCurlThreshold) curledCount++;

        // At least 3 fingers must be curled, and index + middle must be curled with tolerance
        if (curledCount < 3) return false;
        if (indexShape.TryGetFullCurl(out float ic) && ic < (fingerCurlThreshold - 0.15f)) return false;
        if (middleShape.TryGetFullCurl(out float mc) && mc < (fingerCurlThreshold - 0.15f)) return false;

        // 3. Thumb direction must point upward in world space
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

        float upDot = Vector3.Dot(thumbWorldDir, Vector3.up);
        if (upDot < thumbUpAlignmentThreshold) return false;

        // 4. Palm should not be facing downward
        XRHandJoint palm = hand.GetJoint(XRHandJointID.Palm);
        if (palm.TryGetPose(out Pose palmPose))
        {
            Vector3 palmNormalLocal = palmPose.up; // In OpenXR standard, palm.up points outwards from palm
            Vector3 palmNormalWorld = sessionSpaceRoot != null
                ? sessionSpaceRoot.TransformDirection(palmNormalLocal)
                : palmNormalLocal;

            // Reject if palm points towards the ground (such as resting hand on lap)
            if (Vector3.Dot(palmNormalWorld, Vector3.down) > 0.65f) return false;
        }

        return true;
    }

    private void ExecuteThumbsUpTrigger()
    {
        thumbsUpCooldownTimer = thumbsUpCooldown;

        if (debugLogGesture)
        {
            Debug.Log("<color=green>WristWatch: Thumbs Up gesture detected on Left Hand! Opening options menu.</color>");
        }

        if (toggleMenuWithThumbsUp)
        {
            ToggleOptionsPanel();
        }
        else
        {
            OpenOptionsPanel();
        }
    }

    /// <summary>
    /// Checks if any wrist-attached chooser/game panel (Room list, Games, History list, Leaderboard)
    /// is currently open and active on the wrist.
    /// Floating world-space detail panels (ArtifactDetailPanel, HistoryPanel) deliberately DO NOT
    /// block the wrist watch button, so players can open multiple panels and place them side-by-side.
    /// </summary>
    public bool IsContentPanelActive()
    {
        if (gamesPanel != null && gamesPanel.activeInHierarchy) return true;
        if (roomListPanel != null && roomListPanel.activeInHierarchy) return true;

        if (roomHudCanvas != null && roomHudCanvas.activeInHierarchy)
        {
            for (int i = 0; i < roomHudCanvas.transform.childCount; i++)
            {
                Transform child = roomHudCanvas.transform.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                {
                    if (optionsPanelObj != null && (child.gameObject == optionsPanelObj || child.IsChildOf(optionsPanelObj.transform)))
                        continue;
                    // Ignore floating detail panels
                    if (child.name.Contains("ArtifactDetail") || child.name.Contains("ArtifactUI") || child.GetComponent<Artifact>() != null)
                        continue;
                    if (child.name.Contains("HistoryPanel") || child.GetComponent<HistoryPanel>() != null)
                        continue;
                    return true;
                }
            }
        }

        if (HistoryListPanel.Instance != null && HistoryListPanel.Instance.gameObject.activeInHierarchy) return true;
        if (LeaderboardPanel.Instance != null && LeaderboardPanel.Instance.gameObject.activeInHierarchy) return true;
        if (GameListMenu.Instance != null)
        {
            if (GameListMenu.Instance.gameListPanel != null && GameListMenu.Instance.gameListPanel.activeInHierarchy) return true;
        }

        return false;
    }

    /// <summary>
    /// Smoothly toggles the visual graphics, renderers, colliders, and interactables of the wrist watch button
    /// without disabling WristMenuCanvas or affecting child panels (optionsPanelObj, roomHudCanvas, gamesPanel).
    /// </summary>
    private void SetWatchButtonVisualsVisible(bool visible)
    {
        if (wristWatchButtonObj == null) return;

        if (!wristWatchButtonObj.activeSelf)
        {
            wristWatchButtonObj.SetActive(true);
        }

        // Ensure CanvasGroup on wristWatchButtonObj does not force 0 alpha onto child panels
        CanvasGroup cg = wristWatchButtonObj.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        // Deactivate or activate the child Button object directly so all its graphics, colliders, and interactables are completely shut off
        foreach (Transform child in wristWatchButtonObj.transform)
        {
            if (child == null) continue;
            if (optionsPanelObj != null && (child.gameObject == optionsPanelObj || child.IsChildOf(optionsPanelObj.transform))) continue;
            if (roomHudCanvas != null && (child.gameObject == roomHudCanvas || child.IsChildOf(roomHudCanvas.transform))) continue;
            if (gamesPanel != null && (child.gameObject == gamesPanel || child.IsChildOf(gamesPanel.transform))) continue;
            if (child.name.Contains("ArtifactDetail") || child.name.Contains("HistoryPanel")) continue;

            if (child.gameObject.activeSelf != visible)
            {
                child.gameObject.SetActive(visible);
            }
        }

        // Toggle Graphic components belonging ONLY to the watch button icon
        UnityEngine.UI.Graphic[] graphics = wristWatchButtonObj.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        foreach (UnityEngine.UI.Graphic g in graphics)
        {
            if (g == null) continue;
            if (optionsPanelObj != null && g.transform.IsChildOf(optionsPanelObj.transform)) continue;
            if (roomHudCanvas != null && g.transform.IsChildOf(roomHudCanvas.transform)) continue;
            if (gamesPanel != null && g.transform.IsChildOf(gamesPanel.transform)) continue;
            if (g.name.Contains("ArtifactDetail") || g.name.Contains("HistoryPanel")) continue;

            g.enabled = visible;
            g.raycastTarget = visible;
        }

        // Toggle Renderer components belonging ONLY to the watch button icon
        Renderer[] renderers = wristWatchButtonObj.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (optionsPanelObj != null && r.transform.IsChildOf(optionsPanelObj.transform)) continue;
            if (roomHudCanvas != null && r.transform.IsChildOf(roomHudCanvas.transform)) continue;
            if (gamesPanel != null && r.transform.IsChildOf(gamesPanel.transform)) continue;
            if (r.name.Contains("ArtifactDetail") || r.name.Contains("HistoryPanel")) continue;

            r.enabled = visible;
        }

        // Disable button interactions on watch button icon when hidden
        UnityEngine.UI.Button[] buttons = wristWatchButtonObj.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (UnityEngine.UI.Button b in buttons)
        {
            if (b == null) continue;
            if (optionsPanelObj != null && b.transform.IsChildOf(optionsPanelObj.transform)) continue;
            if (roomHudCanvas != null && b.transform.IsChildOf(roomHudCanvas.transform)) continue;
            if (gamesPanel != null && b.transform.IsChildOf(gamesPanel.transform)) continue;
            if (b.name.Contains("ArtifactDetail") || b.name.Contains("HistoryPanel")) continue;

            b.enabled = visible;
            b.interactable = visible;
        }

        XRButtonSelection[] xrButtons = wristWatchButtonObj.GetComponentsInChildren<XRButtonSelection>(true);
        foreach (XRButtonSelection xr in xrButtons)
        {
            if (xr == null) continue;
            if (optionsPanelObj != null && xr.transform.IsChildOf(optionsPanelObj.transform)) continue;
            if (roomHudCanvas != null && xr.transform.IsChildOf(roomHudCanvas.transform)) continue;
            if (gamesPanel != null && xr.transform.IsChildOf(gamesPanel.transform)) continue;
            if (xr.name.Contains("ArtifactDetail") || xr.name.Contains("HistoryPanel")) continue;

            xr.enabled = visible;
        }

        UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable[] xrInteractables = wristWatchButtonObj.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable>(true);
        foreach (var xrInt in xrInteractables)
        {
            if (xrInt == null) continue;
            if (optionsPanelObj != null && xrInt.transform.IsChildOf(optionsPanelObj.transform)) continue;
            if (roomHudCanvas != null && xrInt.transform.IsChildOf(roomHudCanvas.transform)) continue;
            if (gamesPanel != null && xrInt.transform.IsChildOf(gamesPanel.transform)) continue;
            if (xrInt.name.Contains("ArtifactDetail") || xrInt.name.Contains("HistoryPanel")) continue;

            xrInt.enabled = visible;
        }

        Collider[] colliders = wristWatchButtonObj.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in colliders)
        {
            if (c == null) continue;
            if (optionsPanelObj != null && c.transform.IsChildOf(optionsPanelObj.transform)) continue;
            if (roomHudCanvas != null && c.transform.IsChildOf(roomHudCanvas.transform)) continue;
            if (gamesPanel != null && c.transform.IsChildOf(gamesPanel.transform)) continue;
            if (c.name.Contains("ArtifactDetail") || c.name.Contains("HistoryPanel")) continue;

            c.enabled = visible;
        }
    }

    public void EnsureWatchButtonVisible()
    {
        optionsPanelActive = false;
        if (optionsPanelObj != null) optionsPanelObj.SetActive(false);
        if (gamesPanel != null) gamesPanel.SetActive(false);
        if (roomListPanel != null) roomListPanel.SetActive(false);
        if (HistoryListPanel.Instance != null) HistoryListPanel.Instance.gameObject.SetActive(false);

        GameObject roomPanelObj = GameObject.Find("RoomPanel");
        if (roomPanelObj != null && roomPanelObj.activeSelf) roomPanelObj.SetActive(false);

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

            if (ThemeManager.Instance != null)
            {
                ThemeManager.Instance.ApplyToHierarchy(roomHudCanvas);
            }

            ShowRoomListPanel();

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.PopulateRoomListUI();
            }

            if (hasAnchorPose)
            {
                roomHudCanvas.transform.position = AnchorTransformPoint(roomListWristOffset);
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

        // Make sure the List Game panel isn't left showing alongside the room list.
        if (GameListMenu.Instance != null) GameListMenu.Instance.HidePanel();

        // Resolve the REAL room-list panel by name. The cloned GameListPanel may also carry a
        // RoomList component, so we must not rely on GetComponentInChildren<RoomList> here -
        // that could grab the game panel and open it when the player taps Explore.
        if (roomListPanel == null)
        {
            Transform t = FindDeepChild(roomHudCanvas.transform, "RoomListPanel");
            roomListPanel = t != null ? t.gameObject : FindInactiveObject("RoomListPanel");
        }
        if (roomListPanel == null) return;

        // Show ONLY the room list among the canvas panels (hides GameListPanel, RoomPanel, etc.).
        Transform parent = roomListPanel.transform.parent;
        if (parent != null)
        {
            foreach (Transform sibling in parent)
            {
                if (sibling.gameObject == roomListPanel)
                {
                    UIAnimationHelper.FadeIn(sibling.gameObject, 0.22f, true);
                }
                else
                {
                    sibling.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            UIAnimationHelper.FadeIn(roomListPanel, 0.22f, true);
        }

        RoomList rList = roomListPanel.GetComponent<RoomList>();
        if (rList == null) rList = roomListPanel.GetComponentInChildren<RoomList>(true);
        if (rList != null)
        {
            rList.enabled = true;
            if (rList.roomPanel != null) rList.roomPanel.gameObject.SetActive(false);
            rList.PopulateRoomsList();
        }
    }

    /// <summary>
    /// Invoked when player taps the Close ('X') button on the Room panel.
    /// </summary>
    public void CloseRoomPanel()
    {
        if (roomHudCanvas != null)
        {
            UIAnimationHelper.FadeOut(roomHudCanvas, 0.15f);
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

        GameObject targetPanel = gamesPanel;

        // 1. If gamesPanel is explicitly assigned in Inspector, activate it
        if (targetPanel != null)
        {
            targetPanel.SetActive(true);
        }
        // 2. If MiniGames.Instance exists, activate its canvas GameObject directly
        else if (MiniGames.Instance != null)
        {
            targetPanel = MiniGames.Instance.gameObject;
            targetPanel.SetActive(true);
        }
        // 3. Fallback: Search scene for MiniGamesCanvas or MiniGames root GameObject
        else
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if ((t.name == "MiniGamesCanvas" || t.name == "MiniGames") && t.gameObject.scene.IsValid())
                {
                    gamesPanel = t.gameObject;
                    targetPanel = gamesPanel;
                    targetPanel.SetActive(true);
                    break;
                }
            }
        }

        if (targetPanel != null)
        {
            UIAnimationHelper.FadeIn(targetPanel, 0.22f, true);

            if (ThemeManager.Instance != null)
            {
                ThemeManager.Instance.ApplyToHierarchy(targetPanel);
            }

            if (MiniGames.Instance != null)
            {
                MiniGames.Instance.PositionInFrontOfUser();
            }
            else
            {
                Transform cam = Camera.main != null ? Camera.main.transform : null;
                if (cam != null)
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                    if (fwd == Vector3.zero) fwd = Vector3.forward;
                    Vector3 targetPos = cam.position + fwd * 0.7f - Vector3.up * 0.1f;
                    targetPanel.transform.position = targetPos;
                    Vector3 toPlayer = cam.position - targetPanel.transform.position;
                    toPlayer.y = 0;
                    if (toPlayer.sqrMagnitude > 0.0001f)
                    {
                        targetPanel.transform.rotation = Quaternion.LookRotation(-toPlayer, Vector3.up);
                    }
                }
            }
            return;
        }

        Debug.LogWarning("WristWatch: Could not find MiniGamesCanvas in scene.");
    }

    /// <summary>
    /// Invoked when player taps the Close ('X') button on the Games panel.
    /// </summary>
    public void CloseGamesPanel()
    {
        if (gamesPanel != null)
        {
            UIAnimationHelper.FadeOut(gamesPanel, 0.15f);
        }
        Debug.Log("WristWatch: Games Panel Closed.");
    }
}
