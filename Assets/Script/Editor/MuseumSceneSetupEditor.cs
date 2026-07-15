using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MuseumSceneSetupEditor : EditorWindow
{
    [MenuItem("Tools/Museum MR/Setup Complete Scene")]
    public static void SetupScene()
    {
        // 1. Create Folders
        CreateRequiredFolders();

        // 2. Create Sample Assets
        List<RoomData> rooms = CreateSampleMuseumData();

        // 3. Create Prefabs
        GameObject itemPrefab = CreateArtifactListItemPrefab();
        GameObject panelPrefab = CreateArtifactPanelPrefab();

        // 4. Set Up Scene GameObjects
        SetupSceneObjects(rooms, itemPrefab, panelPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Automatically save the open scenes to disk so changes persist immediately
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("Museum MR Scene Setup Completed Successfully and saved to disk!");
    }

    private static void CreateRequiredFolders()
    {
        string[] folders = { "MuseumData", "Prefabs", "QRCodes" };
        foreach (string folder in folders)
        {
            string path = Path.Combine(Application.dataPath, folder);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }

    private static List<RoomData> CreateSampleMuseumData()
    {
        List<RoomData> rooms = new List<RoomData>();

        // Create Artifacts for Room 1
        ArtifactData monaLisa = CreateArtifactAsset("artifact_1", "Mona Lisa", "Leonardo da Vinci", "1503", 
            "A world-famous portrait painting, representing the pinnacle of Renaissance realism. Renowned for the subject's elusive smile.");
        
        ArtifactData david = CreateArtifactAsset("artifact_2", "Statue of David", "Michelangelo", "1504", 
            "A masterpiece of Renaissance sculpture, depicting the biblical hero David in marble. Stands as a symbol of strength and youth.");

        // Create Room 1
        RoomData room1 = CreateRoomAsset("room_1", "Renaissance Gallery", 
            new List<ArtifactData> { monaLisa, david }, 
            new Vector3[] { new Vector3(0, 0, 0), new Vector3(0, 0, 3), new Vector3(2, 0, 3) });
        rooms.Add(room1);

        // Create Artifacts for Room 2
        ArtifactData starryNight = CreateArtifactAsset("artifact_3", "The Starry Night", "Vincent van Gogh", "1889", 
            "An iconic Post-Impressionist masterpiece depicting the view from the painter's asylum room window just before sunrise.");

        // Create Room 2
        RoomData room2 = CreateRoomAsset("room_2", "Modern Art Room", 
            new List<ArtifactData> { starryNight }, 
            new Vector3[] { new Vector3(0, 0, 0), new Vector3(0, 0, 3), new Vector3(-2, 0, 3), new Vector3(-2, 0, 6) });
        rooms.Add(room2);

        return rooms;
    }

    private static ArtifactData CreateArtifactAsset(string id, string name, string artist, string year, string desc)
    {
        string path = $"Assets/MuseumData/{id}_{name.Replace(" ", "")}.asset";
        ArtifactData asset = AssetDatabase.LoadAssetAtPath<ArtifactData>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<ArtifactData>();
            AssetDatabase.CreateAsset(asset, path);
        }
        asset.artifactId = id;
        asset.artifactName = name;
        asset.artist = artist;
        asset.year = year;
        asset.description = desc;

        // Create a simple colorful sphere/cube as placeholder model
        string modelName = $"Placeholder_{name.Replace(" ", "")}";
        string modelPath = $"Assets/Prefabs/{modelName}.prefab";
        GameObject mockModel = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (mockModel == null)
        {
            mockModel = GameObject.CreatePrimitive(name.Contains("Statue") ? PrimitiveType.Cube : PrimitiveType.Sphere);
            mockModel.name = modelName;
            
            // Add a beautiful material color
            Renderer renderer = mockModel.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = name.Contains("Mona") ? new Color(0.8f, 0.6f, 0.2f) : (name.Contains("David") ? Color.white : Color.blue);
            AssetDatabase.CreateAsset(mat, $"Assets/Prefabs/Mat_{modelName}.mat");
            renderer.sharedMaterial = mat;

            // Shrink primitive to fit nicely on the panel
            mockModel.transform.localScale = Vector3.one * 0.25f;

            PrefabUtility.SaveAsPrefabAsset(mockModel, modelPath);
            DestroyImmediate(mockModel);
            mockModel = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        }
        asset.modelPrefab = mockModel;

        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static RoomData CreateRoomAsset(string id, string name, List<ArtifactData> artifacts, Vector3[] waypoints)
    {
        string path = $"Assets/MuseumData/{id}_{name.Replace(" ", "")}.asset";
        RoomData asset = AssetDatabase.LoadAssetAtPath<RoomData>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<RoomData>();
            AssetDatabase.CreateAsset(asset, path);
        }
        asset.roomId = id;
        asset.roomName = name;
        asset.artifacts = artifacts;
        asset.waypoints = waypoints;

        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static GameObject CreateArtifactListItemPrefab()
    {
        string path = "Assets/Prefabs/ArtifactListItemPrefab.prefab";

        GameObject container = new GameObject("ArtifactListItem");
        RectTransform rect = container.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 35);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(container.transform);
        TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
        textComp.fontSize = 18;
        textComp.alignment = TextAlignmentOptions.Left;
        textComp.text = "○ Artifact Name";

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(container, path);
        DestroyImmediate(container);
        return prefab;
    }

    private static GameObject CreateArtifactPanelPrefab()
    {
        string path = "Assets/Prefabs/ArtifactPanelPrefab.prefab";

        GameObject panelObj = new GameObject("ArtifactDetailPanel");
        Canvas canvas = panelObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        panelObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        
        RectTransform rect = panelObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240, 320);
        panelObj.transform.localScale = Vector3.one * 0.0022f; // Float nicely in VR

        // Background Image (Glassmorphism Minimalist Light Gray/White + Round corners)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(panelObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        
        // Generate and assign the custom Rounded Box Material
        Material panelMat = GetOrCreateRoundedMaterial("Mat_ArtifactDetailPanel", 
            new Color(0.96f, 0.96f, 0.98f, 0.92f), // Fill: Translucent light gray/white
            new Color(0.82f, 0.82f, 0.86f, 0.85f), // Border: Soft silver-gray
            0.008f, // Border Width
            0.06f,  // Corner Radius
            240f / 320f // Aspect Ratio (width/height)
        );
        bgImg.material = panelMat;

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Title text (Slate Gray / Minimalist Regular spaced)
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 22;
        titleText.fontStyle = FontStyles.Normal;
        titleText.characterSpacing = 10f; // Elegant letter spacing
        titleText.color = new Color(0.1f, 0.1f, 0.15f);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.85f);
        titleRect.anchorMax = new Vector2(0.95f, 0.95f);
        titleRect.sizeDelta = Vector2.zero;

        // Artist and Year text (Muted Slate Blue)
        GameObject subObj = new GameObject("ArtistYearText");
        subObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI subText = subObj.AddComponent<TextMeshProUGUI>();
        subText.fontSize = 15;
        subText.fontStyle = FontStyles.Italic;
        subText.color = new Color(0.4f, 0.4f, 0.48f);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.05f, 0.77f);
        subRect.anchorMax = new Vector2(0.95f, 0.85f);
        subRect.sizeDelta = Vector2.zero;

        // Description text (Charcoal Dark + Line Spacing)
        GameObject descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.fontSize = 14;
        descText.lineSpacing = 12f; // Clean paragraph spacing
        descText.color = new Color(0.2f, 0.2f, 0.25f);
        descText.enableWordWrapping = true;
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.05f, 0.15f);
        descRect.anchorMax = new Vector2(0.95f, 0.75f);
        descRect.sizeDelta = Vector2.zero;

        // Model Spawn Anchor (Offset to the right side of the canvas)
        GameObject anchorObj = new GameObject("ModelSpawnAnchor");
        anchorObj.transform.SetParent(panelObj.transform, false);
        anchorObj.transform.localPosition = new Vector3(170f, 0f, -50f);

        // Attach ArtifactInteraction
        ArtifactInteraction interaction = panelObj.AddComponent<ArtifactInteraction>();
        interaction.titleText = titleText;
        interaction.artistYearText = subText;
        interaction.descriptionText = descText;
        interaction.modelSpawnAnchor = anchorObj.transform;
        interaction.maxInteractionDistance = 2.5f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(panelObj, path);
        DestroyImmediate(panelObj);
        return prefab;
    }

    private static void SetupSceneObjects(List<RoomData> rooms, GameObject listItemPrefab, GameObject panelPrefab)
    {
        // Deleting existing camera and rigs to avoid duplicates (using loops to catch all duplicates)
        while (true)
        {
            GameObject obj = GameObject.Find("XR Interaction Hands Setup");
            if (obj == null) break;
            DestroyImmediate(obj);
        }

        while (true)
        {
            GameObject obj = GameObject.Find("XR Origin (XR Rig)");
            if (obj == null) break;
            DestroyImmediate(obj);
        }

        while (true)
        {
            GameObject obj = GameObject.Find("XR Interaction Setup");
            if (obj == null) break;
            DestroyImmediate(obj);
        }
        
        while (true)
        {
            GameObject obj = GameObject.Find("XR Interaction Manager");
            if (obj == null) break;
            DestroyImmediate(obj);
        }

        foreach (var cam in GameObject.FindGameObjectsWithTag("MainCamera"))
        {
            DestroyImmediate(cam);
        }

        // Load unified XR Interaction Hands Setup from imported Hands Interaction Demo
        string setupPath = "Assets/Samples/XR Interaction Toolkit/2.5.4/Hands Interaction Demo/Prefabs/XR Interaction Hands Setup.prefab";
        GameObject setupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(setupPath);

        Camera mainCam = null;

        if (setupPrefab != null)
        {
            GameObject setupInstance = (GameObject)PrefabUtility.InstantiatePrefab(setupPrefab);
            
            // Find the XR Origin (XR Rig) child in the instantiated setup
            GameObject rigInstance = setupInstance.transform.Find("XR Origin (XR Rig)")?.gameObject;
            if (rigInstance == null)
            {
                rigInstance = GameObject.Find("XR Origin (XR Rig)");
            }
            
            if (rigInstance != null)
            {
                mainCam = rigInstance.GetComponentInChildren<Camera>();
                if (mainCam != null)
                {
                    mainCam.clearFlags = CameraClearFlags.SolidColor;
                    mainCam.backgroundColor = new Color(0f, 0f, 0f, 0f);

                    // Add ARCameraManager to enable the OpenXR Passthrough Camera Subsystem
                    var camManager = mainCam.GetComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();
                    if (camManager == null)
                    {
                        mainCam.gameObject.AddComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();
                    }

                    // Add ARCameraBackground to handle rendering the passthrough feed to the background
                    var camBackground = mainCam.GetComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
                    if (camBackground == null)
                    {
                        mainCam.gameObject.AddComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
                    }
                }

                // Disable default XRInputModalityManager to prevent it from fighting with our custom tracking state logic
                var modalityManager = rigInstance.GetComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager>();
                if (modalityManager != null)
                {
                    modalityManager.enabled = false;
                }

                // Add and wire up the custom HandModalityForcer
                var forcer = rigInstance.GetComponent<HandModalityForcer>();
                if (forcer == null)
                {
                    forcer = rigInstance.AddComponent<HandModalityForcer>();
                }
                
                forcer.leftController = rigInstance.transform.Find("Camera Offset/Left Controller")?.gameObject;
                forcer.rightController = rigInstance.transform.Find("Camera Offset/Right Controller")?.gameObject;
                forcer.leftHand = rigInstance.transform.Find("Camera Offset/Left Hand")?.gameObject;
                forcer.rightHand = rigInstance.transform.Find("Camera Offset/Right Hand")?.gameObject;
            }
        }
        
        if (mainCam == null)
        {
            Debug.LogWarning("Hands Interaction Setup prefab not found or camera missing. Falling back to default camera setup.");
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            camObj.transform.position = new Vector3(0, 1.6f, 0);
        }

        // Create or configure AR Session GameObject to initialize subsystems in mixed reality
        GameObject arSessionObj = GameObject.Find("AR Session");
        if (arSessionObj == null)
        {
            arSessionObj = new GameObject("AR Session");
            arSessionObj.AddComponent<UnityEngine.XR.ARFoundation.ARSession>();
            arSessionObj.AddComponent<UnityEngine.XR.ARFoundation.ARInputManager>();
        }

        // Create MRUK
        GameObject mrukObj = GameObject.Find("MRUK");
        if (mrukObj == null)
        {
            mrukObj = new GameObject("MRUK");
            var mrukComp = mrukObj.AddComponent<Meta.XR.MRUtilityKit.MRUK>();
            var settings = new Meta.XR.MRUtilityKit.MRUK.MRUKSettings();
            var trackerConfig = new OVRAnchor.TrackerConfiguration();
            trackerConfig.QRCodeTrackingEnabled = true;
            settings.TrackerConfiguration = trackerConfig;
            mrukComp.SceneSettings = settings;
        }

        // Create Scanner
        GameObject scannerObj = GameObject.Find("QRCodeScanner");
        if (scannerObj == null)
        {
            scannerObj = new GameObject("QRCodeScanner");
            scannerObj.AddComponent<QRCodeScanner>();
        }

        // Create MiniGameManager
        GameObject gameManagerObj = GameObject.Find("MiniGameManager");
        if (gameManagerObj == null)
        {
            gameManagerObj = new GameObject("MiniGameManager");
            gameManagerObj.AddComponent<MiniGameManager>();
        }

        // Create Floor Wayfinding Line Renderer
        GameObject wayfindingObj = GameObject.Find("WayfindingSystem");
        WayfindingSystem wayfinding = null;
        if (wayfindingObj == null)
        {
            wayfindingObj = new GameObject("WayfindingSystem");
            wayfinding = wayfindingObj.AddComponent<WayfindingSystem>();
            
            LineRenderer lr = wayfindingObj.GetComponent<LineRenderer>();
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            // Set up a glowing gradient
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0f, 1f, 0.8f), 0f), new GradientColorKey(new Color(0f, 0.5f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.2f, 1f) }
            );
            lr.colorGradient = grad;

            // Create a default scrolling line material if possible
            Material lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lineMat.color = new Color(0f, 1f, 0.8f, 0.8f);
            AssetDatabase.CreateAsset(lineMat, "Assets/Prefabs/WayfindingLineMaterial.mat");
            lr.sharedMaterial = lineMat;
        }
        else
        {
            wayfinding = wayfindingObj.GetComponent<WayfindingSystem>();
        }

        // Create Museum Manager
        GameObject managerObj = GameObject.Find("MuseumManager");
        MuseumManager museumManager = null;
        if (managerObj == null)
        {
            managerObj = new GameObject("MuseumManager");
            museumManager = managerObj.AddComponent<MuseumManager>();
        }
        else
        {
            museumManager = managerObj.GetComponent<MuseumManager>();
        }

        museumManager.rooms = rooms;
        museumManager.startingRoom = rooms[0];
        museumManager.wayfindingSystem = wayfinding;
        museumManager.artifactPanelPrefab = panelPrefab;
        museumManager.artifactListItemPrefab = listItemPrefab;

        // Set up World Space Main Menu
        GameObject menuCanvas = BuildMainMenuUI(museumManager);
        Canvas menuCanvasComp = menuCanvas.GetComponent<Canvas>();
        if (menuCanvasComp != null) menuCanvasComp.worldCamera = mainCam;
        
        // Set up World Space Room HUD
        GameObject hudCanvas = BuildRoomHUDUI(museumManager);
        Canvas hudCanvasComp = hudCanvas.GetComponent<Canvas>();
        if (hudCanvasComp != null) hudCanvasComp.worldCamera = mainCam;

        // Link manager GUI toggles
        MainMenuManager mainMenuController = menuCanvas.GetComponent<MainMenuManager>();
        mainMenuController.mainMenuCanvas = menuCanvas;
        mainMenuController.wayfindingSystem = wayfindingObj;
        mainMenuController.artifactsContainer = hudCanvas;

        // Force register GameObjects inside hierarchy
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    private static GameObject BuildMainMenuUI(MuseumManager manager)
    {
        GameObject canvasObj = GameObject.Find("MainMenuCanvas");
        if (canvasObj != null) DestroyImmediate(canvasObj);

        canvasObj = new GameObject("MainMenuCanvas");
        float cameraY = Camera.main != null ? Camera.main.transform.position.y : 1.6f;
        canvasObj.transform.position = new Vector3(0f, cameraY, 1.4f); // Same level as camera, 1.4m forward
        canvasObj.transform.rotation = Quaternion.identity;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 200);
        canvasObj.transform.localScale = Vector3.one * 0.003f;

        // Add MainMenuManager controller
        MainMenuManager menuMgr = canvasObj.AddComponent<MainMenuManager>();

        // Background Glass Panel (Translucent light white + Rounded corners)
        GameObject panel = new GameObject("Background");
        panel.transform.SetParent(canvasObj.transform, false);
        Image img = panel.AddComponent<Image>();
        
        Material menuMat = GetOrCreateRoundedMaterial("Mat_MainMenu", 
            new Color(0.96f, 0.96f, 0.98f, 0.92f), // Light translucent
            new Color(0.82f, 0.82f, 0.86f, 0.85f), // Border: Soft silver
            0.01f, // Border Width
            0.08f, // Corner Radius
            300f / 200f // Aspect Ratio
        );
        img.material = menuMat;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // Title text (Slate gray + letter spacing)
        GameObject title = new GameObject("Title");
        title.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI text = title.AddComponent<TextMeshProUGUI>();
        text.text = "MUSEUM EXPLORATION";
        text.fontSize = 18;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 12f; // Modern wide spacing
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.1f, 0.1f, 0.15f);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.7f);
        titleRect.anchorMax = new Vector2(1, 0.95f);
        titleRect.sizeDelta = Vector2.zero;

        // Shared Button Material (Rounded pill + border)
        Material buttonMat = GetOrCreateRoundedMaterial("Mat_Button", 
            new Color(0.9f, 0.9f, 0.93f, 0.8f), // Soft button fill
            new Color(0.72f, 0.72f, 0.78f, 0.9f), // Visible border
            0.035f, // Thicker border
            0.22f, // Rounded pill corners
            210f / 40f // Button Aspect
        );

        // Start button
        GameObject startBtn = new GameObject("StartButton");
        startBtn.transform.SetParent(canvasObj.transform, false);
        Image startImg = startBtn.AddComponent<Image>();
        startImg.material = buttonMat;
        
        RectTransform startRect = startBtn.GetComponent<RectTransform>();
        startRect.anchorMin = new Vector2(0.15f, 0.35f);
        startRect.anchorMax = new Vector2(0.85f, 0.55f);
        startRect.sizeDelta = Vector2.zero;

        GameObject startTxt = new GameObject("Text");
        startTxt.transform.SetParent(startBtn.transform, false);
        TextMeshProUGUI startTextComp = startTxt.AddComponent<TextMeshProUGUI>();
        startTextComp.text = "Start Exploration";
        startTextComp.fontSize = 14;
        startTextComp.alignment = TextAlignmentOptions.Center;
        startTextComp.color = new Color(0.1f, 0.1f, 0.15f);
        RectTransform startTextRect = startTxt.GetComponent<RectTransform>();
        startTextRect.anchorMin = Vector2.zero;
        startTextRect.anchorMax = Vector2.one;
        startTextRect.sizeDelta = Vector2.zero;

        // Settings button
        GameObject settingsBtn = new GameObject("SettingsButton");
        settingsBtn.transform.SetParent(canvasObj.transform, false);
        Image settingsImg = settingsBtn.AddComponent<Image>();
        settingsImg.material = buttonMat;
        
        RectTransform settingsRect = settingsBtn.GetComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(0.15f, 0.1f);
        settingsRect.anchorMax = new Vector2(0.85f, 0.3f);
        settingsRect.sizeDelta = Vector2.zero;

        GameObject settingsTxt = new GameObject("Text");
        settingsTxt.transform.SetParent(settingsBtn.transform, false);
        TextMeshProUGUI settingsTextComp = settingsTxt.AddComponent<TextMeshProUGUI>();
        settingsTextComp.text = "Settings";
        settingsTextComp.fontSize = 14;
        settingsTextComp.alignment = TextAlignmentOptions.Center;
        settingsTextComp.color = new Color(0.1f, 0.1f, 0.15f);
        RectTransform settingsTextRect = settingsTxt.GetComponent<RectTransform>();
        settingsTextRect.anchorMin = Vector2.zero;
        settingsTextRect.anchorMax = Vector2.one;
        settingsTextRect.sizeDelta = Vector2.zero;

        // Attach XR Button Selection to Start Button and hook events
        XRButtonSelection startSelection = startBtn.AddComponent<XRButtonSelection>();
        startSelection.buttonImage = startImg;
        startSelection.scaleTarget = startBtn.transform;
        
        // Add BoxCollider for XRI physics raycasting and physical finger poking (touching)
        BoxCollider startCollider = startBtn.AddComponent<BoxCollider>();
        startCollider.size = new Vector3(210f, 40f, 15f);
        startCollider.isTrigger = true;
        
        // Link StartButton click programmatically to menuMgr.StartExploration
        UnityEditor.Events.UnityEventTools.AddPersistentListener(startSelection.onClick, menuMgr.StartExploration);

        // Attach XR Button Selection to Settings Button
        XRButtonSelection settingsSelection = settingsBtn.AddComponent<XRButtonSelection>();
        settingsSelection.buttonImage = settingsImg;
        settingsSelection.scaleTarget = settingsBtn.transform;

        // Add BoxCollider for XRI physics raycasting and physical finger poking (touching)
        BoxCollider settingsCollider = settingsBtn.AddComponent<BoxCollider>();
        settingsCollider.size = new Vector3(210f, 40f, 15f);
        settingsCollider.isTrigger = true;

        return canvasObj;
    }

    private static GameObject BuildRoomHUDUI(MuseumManager manager)
    {
        GameObject canvasObj = GameObject.Find("RoomHUDCanvas");
        if (canvasObj != null) DestroyImmediate(canvasObj);

        canvasObj = new GameObject("RoomHUDCanvas");
        float cameraY = Camera.main != null ? Camera.main.transform.position.y : 1.6f;
        // Place it slightly to the left, angled towards the player
        canvasObj.transform.position = new Vector3(-0.4f, cameraY + 0.2f, 1.2f);
        canvasObj.transform.rotation = Quaternion.identity;

        // Attach GlanceableHUD to make the Room HUD Canvas follow the player and face them
        GlanceableHUD glanceableHUD = canvasObj.AddComponent<GlanceableHUD>();
        glanceableHUD.positionOffset = new Vector3(-0.4f, 0.2f, 1.2f); // Float top-left
        glanceableHUD.useDeadzone = true;
        glanceableHUD.distanceThreshold = 0.15f;
        glanceableHUD.angleThreshold = 15.0f;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250, 300);
        canvasObj.transform.localScale = Vector3.one * 0.002f;

        // Background (Translucent light white + rounded corners)
        GameObject panel = new GameObject("Background");
        panel.transform.SetParent(canvasObj.transform, false);
        Image img = panel.AddComponent<Image>();
        
        Material hudMat = GetOrCreateRoundedMaterial("Mat_RoomHUD", 
            new Color(0.96f, 0.96f, 0.98f, 0.92f), // Light translucent
            new Color(0.82f, 0.82f, 0.86f, 0.85f), // Border: Soft silver
            0.008f, // Border Width
            0.06f,  // Corner Radius
            250f / 300f // Aspect Ratio
        );
        img.material = hudMat;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // Room Name Title (slate gray + letter spacing)
        GameObject roomTitleObj = new GameObject("RoomTitleText");
        roomTitleObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI roomTitleText = roomTitleObj.AddComponent<TextMeshProUGUI>();
        roomTitleText.text = "Gallery Title";
        roomTitleText.fontSize = 18;
        roomTitleText.fontStyle = FontStyles.Bold;
        roomTitleText.characterSpacing = 10f; // Elegant letter-spacing
        roomTitleText.alignment = TextAlignmentOptions.Center;
        roomTitleText.color = new Color(0.1f, 0.1f, 0.15f);
        RectTransform titleRect = roomTitleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.85f);
        titleRect.anchorMax = new Vector2(0.95f, 0.97f);
        titleRect.sizeDelta = Vector2.zero;

        // Section header
        GameObject headerObj = new GameObject("ListHeader");
        headerObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "Exhibits in this room:";
        headerText.fontSize = 12;
        headerText.color = new Color(0.4f, 0.4f, 0.45f);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.08f, 0.77f);
        headerRect.anchorMax = new Vector2(0.92f, 0.84f);
        headerRect.sizeDelta = Vector2.zero;

        // List Container (Vertical Layout)
        GameObject listContainer = new GameObject("ArtifactList");
        listContainer.transform.SetParent(canvasObj.transform, false);
        
        VerticalLayoutGroup layout = listContainer.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        RectTransform listRect = listContainer.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.08f, 0.15f);
        listRect.anchorMax = new Vector2(0.92f, 0.75f);
        listRect.sizeDelta = Vector2.zero;

        // Scan Status Feedback Text
        GameObject statusObj = new GameObject("ScanStatusText");
        statusObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
        statusText.fontSize = 11;
        statusText.fontStyle = FontStyles.Italic;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.text = "";
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.08f, 0.02f);
        statusRect.anchorMax = new Vector2(0.92f, 0.13f);
        statusRect.sizeDelta = Vector2.zero;

        // Assign UI parameters to MuseumManager
        manager.roomHudContainer = canvasObj;
        manager.roomNameText = roomTitleText;
        manager.artifactListContainer = listContainer.transform;
        manager.scanStatusText = statusText;

        return canvasObj;
    }

    private static Material GetOrCreateRoundedMaterial(string matName, Color fillColor, Color borderColor, float borderWidth, float cornerRadius, float aspect)
    {
        string path = $"Assets/Prefabs/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("UI/RoundedCorners");
            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                // Fallback UI material if shader compilation fails
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            AssetDatabase.CreateAsset(mat, path);
        }

        // Apply shader values
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", fillColor);
        if (mat.HasProperty("_BorderColor")) mat.SetColor("_BorderColor", borderColor);
        if (mat.HasProperty("_BorderWidth")) mat.SetFloat("_BorderWidth", borderWidth);
        if (mat.HasProperty("_CornerRadius")) mat.SetFloat("_CornerRadius", cornerRadius);
        if (mat.HasProperty("_Aspect")) mat.SetFloat("_Aspect", aspect);

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
