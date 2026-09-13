using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Video;

public class MainMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject mainMenuCanvas;

    [Header("Video Intro References")]
    public GameObject introVideoPanel;
    public GameObject enterNamePanel;
    public VideoPlayer videoPlayer;
    public XRButtonSelection skipButtonXR;
    public Button skipButton;

    [Header("Theme Selection References")]
    public GameObject themeSelectionPanel;

    [Header("Background Fade Settings")]
    [Tooltip("Duration in seconds to smoothly transition background between Black and Passthrough (Transparent).")]
    public float fadeDuration = 0.8f;

    private GameObject backgroundFadeOverlayObj;
    private CanvasGroup backgroundFadeCanvasGroup;
    private Coroutine currentFadeCoroutine;

    [Header("Exploration References")]
    public GameObject wayfindingSystem;
    public GameObject wristMenuSystem;

    [Header("Name Entry")]
    public GameObject nameErrorLabel;
    private const string DefaultPlayerName = "Pengunjung";

    private TMP_InputField activeInputField;
    private TouchScreenKeyboard keyboard;
    private bool menuPositioned = false;
    private string currentName = DefaultPlayerName;
    private readonly System.Collections.Generic.Dictionary<XRRayInteractor, float> originalRayDistances = new System.Collections.Generic.Dictionary<XRRayInteractor, float>();
    public static bool IsExplorationStarted { get; set; } = false;
    public static MainMenu Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        IsExplorationStarted = false;
    }

    void Start()
    {
        // Ensure the exploration-specific visuals are disabled at startup
        if (wayfindingSystem != null) wayfindingSystem.SetActive(false);

        // Resolve video/panels if null
        if (introVideoPanel == null && mainMenuCanvas != null)
        {
            Transform t = mainMenuCanvas.transform.Find("IntoVideoPanel");
            if (t != null) introVideoPanel = t.gameObject;
        }
        if (enterNamePanel == null && mainMenuCanvas != null)
        {
            Transform t = mainMenuCanvas.transform.Find("EnterNamePanel");
            if (t != null) enterNamePanel = t.gameObject;
        }
        if (themeSelectionPanel == null && mainMenuCanvas != null)
        {
            Transform t = mainMenuCanvas.transform.Find("ThemeSelectionPanel");
            if (t != null) themeSelectionPanel = t.gameObject;
        }
        if (themeSelectionPanel != null)
        {
            themeSelectionPanel.SetActive(false);
        }

        if (nameErrorLabel == null && mainMenuCanvas != null)
        {
            foreach (Transform child in mainMenuCanvas.GetComponentsInChildren<Transform>(true))
            {
                string n = child.name;
                if (n == "NameErrorLabel" || n == "ErrorLabel" || n == "NameErrorText" || n.Contains("Error") || n.Contains("Warning"))
                {
                    nameErrorLabel = child.gameObject;
                    break;
                }
            }
        }
        if (nameErrorLabel != null) nameErrorLabel.SetActive(false);
        if (videoPlayer == null && introVideoPanel != null)
        {
            videoPlayer = introVideoPanel.GetComponentInChildren<VideoPlayer>(true);
        }
        if (skipButton == null && introVideoPanel != null)
        {
            Transform t = introVideoPanel.transform.Find("SkipButton");
            if (t != null) skipButton = t.GetComponent<Button>();
            if (skipButton == null) skipButton = introVideoPanel.GetComponentInChildren<Button>(true);
        }
        if (skipButtonXR == null && introVideoPanel != null)
        {
            Transform t = introVideoPanel.transform.Find("SkipButton");
            if (t != null) skipButtonXR = t.GetComponent<XRButtonSelection>();
            if (skipButtonXR == null) skipButtonXR = introVideoPanel.GetComponentInChildren<XRButtonSelection>(true);
        }

        // Hook video events
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.errorReceived += OnVideoError;
        }
        if (skipButton != null)
        {
            skipButton.onClick.AddListener(EndVideoAndShowNameEntry);
        }
        if (skipButtonXR != null)
        {
            skipButtonXR.onClick.AddListener(EndVideoAndShowNameEntry);
        }

        // Auto-wire StartButton (MULAI button) so clicking/pinching it always triggers StartExploration
        if (mainMenuCanvas != null)
        {
            foreach (Transform t in mainMenuCanvas.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "StartButton" || t.name == "MulaiButton" || t.name.Contains("Start"))
                {
                    Button btn = t.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(StartExploration);
                        if (btn.targetGraphic != null) btn.targetGraphic.raycastTarget = true;
                    }

                    XRButtonSelection selection = t.GetComponent<XRButtonSelection>();
                    if (selection != null)
                    {
                        selection.onClick.RemoveAllListeners();
                        selection.onClick.AddListener(StartExploration);
                    }

                    if (t.GetComponent<UIButtonAudio>() == null)
                    {
                        t.gameObject.AddComponent<UIButtonAudio>();
                    }
                }
            }
        }

        // Initialize panel state: show video first, hide name entry
        if (introVideoPanel != null)
        {
            introVideoPanel.SetActive(true);
            // Ensure introduction video panel and button start in default Dark Mode
            if (mainMenuCanvas != null)
            {
                ThemeManager.Instance.ApplyToHierarchy(mainMenuCanvas, UIThemeMode.Dark);
            }
            FadeBackground(1f, fadeDuration);

            // EVERYTHING the player ever sees (name entry, main menu, wrist button) is
            // gated behind this video finishing, and the background is faded to black
            // while it plays. If the clip silently fails to decode on the headset the
            // app would be a permanent black screen - so watchdog it.
            StartCoroutine(VideoStartWatchdog(8f));
        }
        else
        {
            FadeBackground(0f, 0f);
        }
        if (enterNamePanel != null)
        {
            enterNamePanel.SetActive(false);
        }

        // Automatically extend all hand/controller raycast pointer lengths in the scene (including inactive ones) so players can reach the menu.
        // Remember each one's original distance so it can be restored once exploration/gameplay starts -
        // a 10m ray makes hand-pinch precision (e.g. dragging Batik game cards) unusably twitchy, since a
        // small hand rotation sweeps a huge arc at that distance.
        XRRayInteractor[] rayInteractors = FindObjectsOfType<XRRayInteractor>(true);
        foreach (var ray in rayInteractors)
        {
            if (ray != null)
            {
                if (!originalRayDistances.ContainsKey(ray))
                {
                    originalRayDistances[ray] = ray.maxRaycastDistance;
                }
                ray.maxRaycastDistance = 10f; // Extend pointer length to 10 meters!
                Debug.Log($"Extended raycast pointer distance for: {ray.gameObject.name} to 10m.");
            }
        }

        currentName = PlayerPrefs.GetString("PlayerName", DefaultPlayerName);

        // Hook InputField selection event and XRI click/pinch event to trigger VR Keyboard
        if (mainMenuCanvas != null)
        {
            UnityEngine.UI.Image menuBg = mainMenuCanvas.GetComponent<UnityEngine.UI.Image>();
            if (menuBg == null) menuBg = mainMenuCanvas.transform.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
            if (menuBg == null) menuBg = mainMenuCanvas.GetComponentInChildren<UnityEngine.UI.Image>();
            if (menuBg != null && (menuBg.material == null || menuBg.material.name == "Default UI"))
            {
                foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m != null && (m.name == "Mat_MainMenu" || m.name == "Mat_OptionsCardBackground"))
                    {
                        menuBg.material = m;
                        break;
                    }
                }
            }

            TMP_InputField inputField = mainMenuCanvas.GetComponentInChildren<TMP_InputField>(true);
            XRSimpleInteractable interactable = mainMenuCanvas.GetComponentInChildren<XRSimpleInteractable>(true);

            if (inputField != null)
            {
                inputField.onSelect.AddListener((x) => OpenVRKeyboard(inputField));
                inputField.text = currentName;
            }
            if (interactable != null && inputField != null)
            {
                interactable.selectEntered.AddListener((args) => {
                    inputField.Select();
                    inputField.ActivateInputField();
                    OpenVRKeyboard(inputField);
                });
            }
        }
    }

    /// <summary>
    /// Invoked by the NameButton's onClick (pinch/click) to open the Meta Quest
    /// system keyboard and edit the player's name.
    /// </summary>
    public void StartEditingName()
    {
        activeInputField = null;
#if !UNITY_EDITOR
        keyboard = TouchScreenKeyboard.Open(currentName, TouchScreenKeyboardType.Default, false, false, false, false, "Taip Nama Anda");
#else
        Debug.Log("StartEditingName: system keyboard only appears on the headset, not in the Editor.");
#endif
    }

    private void OpenVRKeyboard(TMP_InputField inputField)
    {
        activeInputField = inputField;
#if !UNITY_EDITOR
        // Open the native Oculus/Meta Quest virtual overlay keyboard
        keyboard = TouchScreenKeyboard.Open(inputField.text, TouchScreenKeyboardType.Default, false, false, false, false, "Taip Nama Anda");
#endif
    }

    // Re-run the "place in front of the player" logic every time this menu is shown, so it
    // always appears in the player's current gaze instead of at a stale position latched on
    // frame 0 (before head-tracking has settled) and never updated after HUDManager re-reveals it.
    private void OnEnable()
    {
        menuPositioned = false;
    }

    // Camera.main is null if the active camera isn't tagged MainCamera (can happen with some
    // XR rig setups); fall back to any camera so positioning still works on the headset.
    private Camera cachedCamera;
    private Transform ResolveCameraTransform()
    {
        if (Camera.main != null) return Camera.main.transform;
        if (cachedCamera == null) cachedCamera = FindObjectOfType<Camera>();
        return cachedCamera != null ? cachedCamera.transform : null;
    }

    void Update()
    {
        // Gate the ray-extension purely on whether the menu is actually visible right now,
        // rather than on whether StartExploration() was ever called - a player can reach a
        // mini-game (e.g. by scanning its QR directly) without ever pressing the menu's Start
        // button, and the previous "explorationStarted" latch would then never trip, leaving
        // every ray interactor (including hand-pinch rays used to drag Batik game cards)
        // stuck at 10m forever.
        bool menuActive = mainMenuCanvas != null && mainMenuCanvas.activeInHierarchy;
        if (!menuActive)
        {
            RestoreRayDistances();
            return;
        }

        // Ensure background fade overlay is created & fading to black if intro video panel is currently active
        if (backgroundFadeOverlayObj == null && introVideoPanel != null && introVideoPanel.activeInHierarchy)
        {
            EnsureBackgroundFadeOverlay();
            if (backgroundFadeOverlayObj != null)
            {
                FadeBackground(1f, fadeDuration);
            }
        }

        // 1. Position the menu dynamically at eye level 1 meter in front of the player ONLY when headset tracking starts
        GameObject targetMenu = mainMenuCanvas != null ? mainMenuCanvas : gameObject;
        if (!menuPositioned)
        {
            Transform camTransform = ResolveCameraTransform();
            if (camTransform != null && camTransform.position.y > 0.1f)
            {
                // Spawn exactly 1.0 meter in front of the player gaze at eye level
                Vector3 pos = camTransform.position + camTransform.forward * 1.0f;
                pos.y = camTransform.position.y; 
                targetMenu.transform.position = pos;
                menuPositioned = true;
                Debug.Log($"Main Menu positioned 1.0m in front of player and fixed at world position: {pos}");
            }
        }

        // 2. Continuously rotate the menu to face toward the player as they move around (while keeping world position fixed)
        Transform currentCam = ResolveCameraTransform();
        if (currentCam != null && targetMenu != null)
        {
            Vector3 directionToPlayer = currentCam.position - targetMenu.transform.position;
            directionToPlayer.y = 0; // Keep canvas upright
            if (directionToPlayer.sqrMagnitude > 0.0001f)
            {
                targetMenu.transform.rotation = Quaternion.LookRotation(-directionToPlayer, Vector3.up);
            }
        }

        // 2. Ensure all active hand/controller rays are extended to 10m continuously (in case they initialized after Start)
        XRRayInteractor[] activeRays = FindObjectsOfType<XRRayInteractor>();
        foreach (var ray in activeRays)
        {
            if (ray != null && ray.maxRaycastDistance < 9.5f)
            {
                ray.maxRaycastDistance = 10f;
                Debug.Log($"Dynamically extended active ray: {ray.gameObject.name} to 10m.");
            }
        }

        // 3. Synchronize VR Keyboard input (into the input field if one exists, otherwise the name label)
#if !UNITY_EDITOR
        if (keyboard != null)
        {
            if (activeInputField != null)
            {
                activeInputField.text = keyboard.text;
            }
            else
            {
                currentName = keyboard.text;
            }

            if (keyboard.status == TouchScreenKeyboard.Status.Done || keyboard.status == TouchScreenKeyboard.Status.Canceled || !keyboard.active)
            {
                string finalName = keyboard.text.Trim();
                if (string.IsNullOrEmpty(finalName))
                {
                    finalName = DefaultPlayerName;
                }

                currentName = finalName;
                if (activeInputField != null)
                {
                    activeInputField.text = finalName;
                }
                PlayerPrefs.SetString("PlayerName", finalName);
                PlayerPrefs.Save();

                keyboard = null;
                activeInputField = null;
            }
        }
#endif
    }

    /// <summary>
    /// Restores every ray interactor touched by this script back to its original
    /// raycast distance. Safe to call repeatedly - a no-op once already restored.
    /// </summary>
    private void RestoreRayDistances()
    {
        foreach (var kvp in originalRayDistances)
        {
            if (kvp.Key != null)
            {
                // Cap raycast distance to a comfortable 2.5 meters for hand interaction precision.
                // At 10m distance, natural hand micro-tremors sweep large arcs, causing hand ray jitter.
                float targetDist = Mathf.Min(kvp.Value, 2.5f);
                if (kvp.Key.maxRaycastDistance > targetDist)
                {
                    kvp.Key.maxRaycastDistance = targetDist;
                }
            }
        }
    }

    public void StartExploration()
    {
        string finalName = "";
        if (mainMenuCanvas != null)
        {
            TMP_InputField inputField = mainMenuCanvas.GetComponentInChildren<TMP_InputField>(true);
            if (inputField != null)
            {
                finalName = inputField.text.Trim();
            }
        }

        // If player left name empty, auto-default to "Pengunjung" so exploration starts smoothly
        if (string.IsNullOrEmpty(finalName))
        {
            finalName = DefaultPlayerName;
        }

        if (nameErrorLabel != null)
        {
            nameErrorLabel.SetActive(false);
        }
 
        currentName = finalName;
        PlayerPrefs.SetString("PlayerName", finalName);
        PlayerPrefs.Save();
        Debug.Log($"Player registered name: {finalName}");
 
        ProceedStartExploration();
    }
 
    private void ProceedStartExploration()
    {
        Debug.Log("Museum Exploration: Starting gameplay!");
        IsExplorationStarted = true;

        if (QRCodeScanner.Instance != null)
        {
            QRCodeScanner.Instance.ResetScanState();
        }
 
        // Hide the Main Menu
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.SetActive(false);
        }
 
        // Show standard references if assigned
        if (wayfindingSystem != null) wayfindingSystem.SetActive(true);

        // Find and activate the WristMenuSystem
        if (wristMenuSystem == null)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "WristMenuSystem" && go.scene.isLoaded)
                {
                    wristMenuSystem = go;
                    break;
                }
            }
        }

        if (wristMenuSystem != null)
        {
            wristMenuSystem.SetActive(true);
            Debug.Log("MainMenu: Activated WristMenuSystem.");
        }

        // Tell the Room Manager to start populating and setting up the wayfinding paths
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.StartExploration();
        }

        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.ApplyTheme(ThemeManager.Instance.currentTheme);
        }
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        EndVideoAndShowNameEntry();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"MainMenu: Intro video failed on this device - skipping the intro. {message}");
        EndVideoAndShowNameEntry();
    }

    /// <summary>
    /// Skips the intro automatically if the video never actually starts rendering frames
    /// (codec/decode failures on Android often DON'T raise errorReceived - the player just
    /// sits there forever, leaving the app on the black fade overlay with nothing visible).
    /// </summary>
    private System.Collections.IEnumerator VideoStartWatchdog(float timeoutSeconds)
    {
        float elapsed = 0f;
        while (elapsed < timeoutSeconds)
        {
            // Intro already ended or was skipped - nothing to guard anymore.
            if (introVideoPanel == null || !introVideoPanel.activeInHierarchy) yield break;

            // Video is genuinely playing frames - the normal flow will take it from here.
            if (videoPlayer != null && videoPlayer.isPlaying && videoPlayer.frame > 0) yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning($"MainMenu: Intro video did not start within {timeoutSeconds}s - skipping the intro so the app remains usable.");
        EndVideoAndShowNameEntry();
    }

    private void EndVideoAndShowNameEntry()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (introVideoPanel != null)
        {
            introVideoPanel.SetActive(false);
        }

        // Smoothly transition background back to transparent (passthrough turned on)
        FadeBackground(0f, fadeDuration);

        // Prompt player to choose between Light Mode and Dark Mode directly after video
        ShowThemeSelectionPrompt();
    }

    /// <summary>
    /// Displays the theme selection screen where the user can pick Light Mode (olive-sage) or Dark Mode (slate).
    /// </summary>
    public void ShowThemeSelectionPrompt()
    {
        EnsureThemeSelectionPanel();

        if (enterNamePanel != null)
        {
            enterNamePanel.SetActive(false);
        }

        // Hide canvas background so only the two minimalist buttons float in pass-through space
        if (mainMenuCanvas != null)
        {
            Transform bg = mainMenuCanvas.transform.Find("Background");
            if (bg != null) bg.gameObject.SetActive(false);
        }

        if (themeSelectionPanel != null)
        {
            themeSelectionPanel.SetActive(true);
            ThemeManager.Instance.ApplyToHierarchy(mainMenuCanvas);
        }
        else
        {
            if (mainMenuCanvas != null)
            {
                Transform bg = mainMenuCanvas.transform.Find("Background");
                if (bg != null) bg.gameObject.SetActive(true);
            }
            if (enterNamePanel != null) enterNamePanel.SetActive(true);
        }
    }

    private void OnThemeSelected(UIThemeMode chosenMode)
    {
        Debug.Log($"[MainMenu] User selected theme: {chosenMode}");
        ThemeManager.Instance.SetTheme(chosenMode);

        if (themeSelectionPanel != null)
        {
            themeSelectionPanel.SetActive(false);
        }

        // Restore canvas background for name entry panel
        if (mainMenuCanvas != null)
        {
            Transform bg = mainMenuCanvas.transform.Find("Background");
            if (bg != null) bg.gameObject.SetActive(true);
        }

        if (enterNamePanel != null)
        {
            enterNamePanel.SetActive(true);
            ThemeManager.Instance.ApplyToHierarchy(mainMenuCanvas);
        }
    }

    private void EnsureThemeSelectionPanel()
    {
        if (mainMenuCanvas == null) return;

        // Clean up any stale or partial panel instance
        if (themeSelectionPanel != null)
        {
            Destroy(themeSelectionPanel);
            themeSelectionPanel = null;
        }

        Transform existing = mainMenuCanvas.transform.Find("ThemeSelectionPanel");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        // Cache existing TMP font asset
        TMP_FontAsset font = null;
        TextMeshProUGUI existingTmp = mainMenuCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
        if (existingTmp != null) font = existingTmp.font;

        // 1. Root Container: Pure transparent container (No background panel!)
        GameObject panelObj = new GameObject("ThemeSelectionPanel");
        panelObj.transform.SetParent(mainMenuCanvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.localScale = Vector3.one;

        // Two horizontal 3:1 glass pill buttons side-by-side
        // Aspect ratio 3:1 (138x46), spaced apart with a 32px gap
        Vector2 buttonSize = new Vector2(138f, 46f);
        float xOffset = 85f; // -85 for Left (Dark), +85 for Right (Light), 32px gap between them

        // 2. Dark Mode Floating Glass Button (Translucent Slate glass with Moon icon)
        CreateMinimalThemeButton(
            panelObj.transform,
            "Button_DarkMode",
            new Vector2(-xOffset, 0f),
            buttonSize,
            ThemeManager.Instance.GetOrCreateMoonIconSprite(),
            new Color(0.92f, 0.95f, 1.0f, 1.0f), // Soft luminous silver-white
            "Mode Gelap",
            new Color(0.10f, 0.13f, 0.18f, 0.65f), // Deep frosted slate glass
            new Color(0.48f, 0.65f, 0.95f, 0.92f), // Luminous ice-blue glass rim glow
            font,
            () => OnThemeSelected(UIThemeMode.Dark)
        );

        // 3. Light Mode Floating Glass Button (Translucent Olive-Sage glass with Sun icon)
        CreateMinimalThemeButton(
            panelObj.transform,
            "Button_LightMode",
            new Vector2(xOffset, 0f),
            buttonSize,
            ThemeManager.Instance.GetOrCreateSunIconSprite(),
            new Color(1.0f, 0.88f, 0.38f, 1.0f), // Warm golden radiant sun
            "Mode Terang",
            new Color(0.32f, 0.38f, 0.28f, 0.65f), // Frosted olive-sage museum glass
            new Color(0.82f, 0.88f, 0.65f, 0.92f), // Luminous sage-gold glass rim glow
            font,
            () => OnThemeSelected(UIThemeMode.Light)
        );

        themeSelectionPanel = panelObj;
    }

    private GameObject CreateMinimalThemeButton(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Sprite iconSprite,
        Color iconColor,
        string titleStr,
        Color cardBgColor,
        Color cardBorderColor,
        TMP_FontAsset font,
        UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;

        Image img = buttonObj.AddComponent<Image>();

        // Dedicated Glass Shader for mathematical vector smoothness & physical glass sheen
        Shader glassShader = Shader.Find("UI/GlassButton");
        if (glassShader == null && ThemeManager.Instance != null)
        {
            glassShader = ThemeManager.Instance.GetGlassButtonShader();
        }
        if (glassShader == null)
        {
            glassShader = Shader.Find("UI/RoundedCorners");
        }

        if (glassShader != null)
        {
            Material glassMat = new Material(glassShader);
            glassMat.name = $"Mat_{name}";
            glassMat.SetFloat("_Aspect", sizeDelta.x / sizeDelta.y); // 3.0
            glassMat.SetFloat("_CornerRadius", 0.45f); // Smooth pill capsule
            glassMat.SetFloat("_BorderWidth", 0.024f); // Crisp luminous rim
            glassMat.SetFloat("_SheenIntensity", 0.40f); // Curved glass reflection
            glassMat.SetColor("_Color", cardBgColor);
            glassMat.SetColor("_BorderColor", cardBorderColor);
            glassMat.SetColor("_SheenColor", new Color(1f, 1f, 1f, 0.35f));
            img.material = glassMat;
            img.type = Image.Type.Simple;
        }
        else
        {
            // High-resolution fallback sprite
            img.sprite = ThemeManager.CreateRoundedBoxSprite(
                512, 170,
                80f, 6f,
                cardBgColor,
                cardBorderColor,
                new Vector4(85, 80, 85, 80)
            );
            img.type = Image.Type.Sliced;
        }

        img.color = Color.white;
        img.raycastTarget = true;

        Button btn = buttonObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClickAction);

        XRButtonSelection xrBtn = buttonObj.AddComponent<XRButtonSelection>();
        xrBtn.buttonImage = img;
        xrBtn.normalColor = Color.white;
        xrBtn.hoverColor = new Color(1.12f, 1.15f, 1.22f, 1.0f);
        xrBtn.hoverScaleMultiplier = 1.06f;
        xrBtn.scaleTarget = buttonObj.transform;
        xrBtn.interactionLayers = ~0;
        xrBtn.onClick.AddListener(onClickAction);

        BoxCollider boxCol = buttonObj.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(sizeDelta.x, sizeDelta.y, 25f);
        boxCol.center = Vector3.zero;

        buttonObj.AddComponent<UIButtonAudio>();

        // Minimalist Vector Icon on Left
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(buttonObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(26f, 26f);
        iconRect.anchoredPosition = new Vector2(24f, 0f);
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = iconSprite;
        iconImg.color = iconColor;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // Minimalist Single-Line Title on Right
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(buttonObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.offsetMin = new Vector2(44f, 0f);
        titleRect.offsetMax = new Vector2(-12f, 0f);
        TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        if (font != null) titleTmp.font = font;
        titleTmp.text = titleStr;
        titleTmp.fontSize = 11.5f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
        titleTmp.enableWordWrapping = false;
        titleTmp.overflowMode = TextOverflowModes.Ellipsis;
        titleTmp.color = Color.white;
        titleTmp.raycastTarget = false;

        return buttonObj;
    }

    /// <summary>
    /// Creates a screen-covering black overlay canvas attached to the main camera if it doesn't already exist.
    /// </summary>
    private void EnsureBackgroundFadeOverlay()
    {
        if (backgroundFadeOverlayObj != null) return;

        Transform camTransform = ResolveCameraTransform();
        if (camTransform == null) return;

        backgroundFadeOverlayObj = new GameObject("BackgroundFadeOverlay");
        backgroundFadeOverlayObj.transform.SetParent(camTransform, false);
        backgroundFadeOverlayObj.transform.localPosition = new Vector3(0, 0, 0.4f);
        backgroundFadeOverlayObj.transform.localRotation = Quaternion.identity;

        Canvas canvas = backgroundFadeOverlayObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = -1000;

        RectTransform rect = backgroundFadeOverlayObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(20f, 20f);

        backgroundFadeCanvasGroup = backgroundFadeOverlayObj.AddComponent<CanvasGroup>();
        backgroundFadeCanvasGroup.alpha = 0f;
        backgroundFadeCanvasGroup.blocksRaycasts = false;
        backgroundFadeCanvasGroup.interactable = false;

        GameObject imageObj = new GameObject("BlackImage");
        imageObj.transform.SetParent(backgroundFadeOverlayObj.transform, false);

        RectTransform imgRect = imageObj.AddComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.sizeDelta = Vector2.zero;

        UnityEngine.UI.Image img = imageObj.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        img.raycastTarget = false;
    }

    /// <summary>
    /// Smoothly fades the background between Black (targetAlpha = 1) and Passthrough/Transparent (targetAlpha = 0).
    /// </summary>
    public void FadeBackground(float targetAlpha, float duration)
    {
        EnsureBackgroundFadeOverlay();
        if (backgroundFadeOverlayObj == null || backgroundFadeCanvasGroup == null) return;

        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        currentFadeCoroutine = StartCoroutine(AnimateBackgroundFade(targetAlpha, duration));
    }

    private System.Collections.IEnumerator AnimateBackgroundFade(float targetAlpha, float duration)
    {
        backgroundFadeOverlayObj.SetActive(true);
        float startAlpha = backgroundFadeCanvasGroup.alpha;
        float elapsed = 0f;

        if (duration <= 0f)
        {
            backgroundFadeCanvasGroup.alpha = targetAlpha;
        }
        else
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                backgroundFadeCanvasGroup.alpha = Mathf.SmoothStep(startAlpha, targetAlpha, t);
                yield return null;
            }
            backgroundFadeCanvasGroup.alpha = targetAlpha;
        }

        if (Mathf.Approximately(targetAlpha, 0f))
        {
            backgroundFadeOverlayObj.SetActive(false);
        }

        currentFadeCoroutine = null;
    }
}