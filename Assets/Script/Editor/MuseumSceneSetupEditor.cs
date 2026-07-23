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

    [MenuItem("Tools/Museum MR/Update Artifact List Item Prefab Only")]
    public static void UpdatePrefabOnly()
    {
        CreateArtifactListItemPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("ArtifactListItemPrefab.prefab updated successfully without modifying scene!");
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
        rect.sizeDelta = new Vector2(300, 65);

        Image bgImage = container.AddComponent<Image>();
        Material rowMat = GetOrCreateRoundedMaterial("Mat_OptionRow",
            new Color(0.35f, 0.38f, 0.31f, 0.65f), // Translucent inner row (#505646)
            new Color(0.48f, 0.52f, 0.43f, 0.80f),
            0.015f,
            0.12f,
            300f / 65f
        );
        bgImage.material = rowMat;

        // Thumb Image
        GameObject thumbObj = new GameObject("Thumb");
        thumbObj.transform.SetParent(container.transform, false);
        Image thumbImg = thumbObj.AddComponent<Image>();
        RectTransform tRect = thumbObj.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.03f, 0.10f);
        tRect.anchorMax = new Vector2(0.20f, 0.90f);
        tRect.sizeDelta = Vector2.zero;

        // NumText
        GameObject numObj = new GameObject("NumText");
        numObj.transform.SetParent(container.transform, false);
        TextMeshProUGUI numTextComp = numObj.AddComponent<TextMeshProUGUI>();
        numTextComp.fontSize = 18;
        numTextComp.fontStyle = FontStyles.Bold;
        numTextComp.color = new Color(0.90f, 0.93f, 0.63f, 1.0f);
        numTextComp.text = "01";
        RectTransform nRect = numObj.GetComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0.22f, 0.48f);
        nRect.anchorMax = new Vector2(0.30f, 0.92f);
        nRect.sizeDelta = Vector2.zero;

        // NameText
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(container.transform, false);
        TextMeshProUGUI nameTextComp = nameObj.AddComponent<TextMeshProUGUI>();
        nameTextComp.fontSize = 22;
        nameTextComp.fontStyle = FontStyles.Bold;
        nameTextComp.color = new Color(0.90f, 0.93f, 0.63f, 1.0f);
        nameTextComp.text = "Mona Lisa";
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.31f, 0.48f);
        nameRect.anchorMax = new Vector2(0.96f, 0.92f);
        nameRect.sizeDelta = Vector2.zero;

        // StatusText
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(container.transform, false);
        TextMeshProUGUI statusTextComp = statusObj.AddComponent<TextMeshProUGUI>();
        statusTextComp.fontSize = 15;
        statusTextComp.color = new Color(0.55f, 0.89f, 0.63f, 1.0f);
        statusTextComp.text = "Sudah Dikunjungi";
        RectTransform sRect = statusObj.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.31f, 0.10f);
        sRect.anchorMax = new Vector2(0.96f, 0.48f);
        sRect.sizeDelta = Vector2.zero;

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

        // Attach ArtifactPanel
        ArtifactPanel interaction = panelObj.AddComponent<ArtifactPanel>();
        interaction.topTitleText = titleText;
        interaction.descriptionText = descText;
        anchorObj.AddComponent<RotateArtifact>();

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

        while (true)
        {
            GameObject obj = GameObject.Find("Canvas");
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

        // Create Room and Artifact Managers
        GameObject managerObj = GameObject.Find("MuseumManager");
        if (managerObj == null)
        {
            managerObj = new GameObject("MuseumManager");
        }

        RoomManager roomManager = managerObj.GetComponent<RoomManager>();
        if (roomManager == null) roomManager = managerObj.AddComponent<RoomManager>();

        ArtifactManager artifactManager = managerObj.GetComponent<ArtifactManager>();
        if (artifactManager == null) artifactManager = managerObj.AddComponent<ArtifactManager>();

        roomManager.rooms = rooms;
        roomManager.startingRoom = rooms[0];
        roomManager.wayfindingSystem = wayfinding;
        roomManager.artifactListItemPrefab = listItemPrefab;

        artifactManager.artifactPanelPrefab = panelPrefab;

        // Create EventSystem if none exists in the scene
        GameObject eventSystemObj = GameObject.Find("EventSystem");
        if (eventSystemObj == null)
        {
            eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
        }
        else
        {
            if (eventSystemObj.GetComponent<UnityEngine.EventSystems.EventSystem>() == null)
            {
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }
            if (eventSystemObj.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>() == null)
            {
                eventSystemObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            }
        }

        // Set up World Space Main Menu
        GameObject menuCanvas = BuildMainMenuUI();
        Canvas menuCanvasComp = menuCanvas.GetComponent<Canvas>();
        if (menuCanvasComp != null) menuCanvasComp.worldCamera = mainCam;
        
        // Set up World Space Room HUD
        GameObject hudCanvas = BuildRoomHUDUI(roomManager);
        Canvas hudCanvasComp = hudCanvas.GetComponent<Canvas>();
        if (hudCanvasComp != null) hudCanvasComp.worldCamera = mainCam;

        // Set up Left Hand Wrist Watch & Floating Options Panel
        BuildWristWatchAndOptionsUI(mainCam, hudCanvas);

        // Link manager GUI toggles
        MainMenuManager mainMenuController = menuCanvas.GetComponent<MainMenuManager>();
        mainMenuController.mainMenuCanvas = menuCanvas;
        mainMenuController.wayfindingSystem = wayfindingObj;
        mainMenuController.artifactsContainer = hudCanvas;

        // Force register GameObjects inside hierarchy
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    private static GameObject BuildMainMenuUI()
    {
        GameObject canvasObj = GameObject.Find("MainMenuCanvas");
        if (canvasObj != null) DestroyImmediate(canvasObj);

        canvasObj = new GameObject("MainMenuCanvas");
        float cameraY = Camera.main != null ? Camera.main.transform.position.y : 1.6f;
        canvasObj.transform.position = new Vector3(0f, cameraY, 1.0f); // 1.0m forward
        canvasObj.transform.rotation = Quaternion.identity;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360, 240); // 360x240 aspect card matching user image
        canvasObj.transform.localScale = Vector3.one * 0.0025f;

        // Add MainMenuManager controller
        MainMenuManager menuMgr = canvasObj.AddComponent<MainMenuManager>();

        // Background Card with Translucent Dotted Matrix (matching user image)
        GameObject cardPanel = new GameObject("BackgroundCard");
        cardPanel.transform.SetParent(canvasObj.transform, false);
        Image cardImg = cardPanel.AddComponent<Image>();
        
        Material cardMat = GetOrCreateTranslucentCardMaterial("Mat_TranslucentDottedCard",
            new Color(0.43f, 0.46f, 0.38f, 0.85f), // Translucent dark sage green card (#6E7562)
            new Color(0.28f, 0.30f, 0.25f, 0.50f), // Darker sage dot matrix overlay
            16.0f, // Grid density
            0.07f, // Dot radius
            0.08f, // Corner radius
            360f / 240f // Aspect ratio (1.5)
        );
        cardImg.material = cardMat;

        RectTransform cardRect = cardPanel.GetComponent<RectTransform>();
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.sizeDelta = Vector2.zero;

        // 1. Title Text: "Museum Exploration" (Crisp White, Elegant Serif)
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Museum Exploration";
        titleText.fontSize = 28;
        titleText.fontStyle = FontStyles.Normal;
        titleText.characterSpacing = 2f;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.76f);
        titleRect.anchorMax = new Vector2(0.95f, 0.94f);
        titleRect.sizeDelta = Vector2.zero;

        // 2. Name Label: "Nama Pengunjung" (Crisp White, Left Aligned)
        GameObject labelObj = new GameObject("NameLabelText");
        labelObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = "Nama Pengunjung";
        labelText.fontSize = 18;
        labelText.fontStyle = FontStyles.Normal;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.Left;
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.10f, 0.58f);
        labelRect.anchorMax = new Vector2(0.90f, 0.72f);
        labelRect.sizeDelta = Vector2.zero;

        // 3. Name Input Field Box (Light gray/white translucent pill input box)
        Material inputMat = GetOrCreateRoundedMaterial("Mat_InputBox",
            new Color(0.88f, 0.89f, 0.86f, 0.88f), // Soft light gray-white fill (#E0E2DB)
            new Color(0.78f, 0.80f, 0.75f, 0.95f), // Subtle border
            0.015f, // Border width
            0.18f,  // Rounded input field corners
            288f / 38f // Aspect ratio
        );

        GameObject inputObj = new GameObject("NameInputField");
        inputObj.transform.SetParent(canvasObj.transform, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.10f, 0.38f);
        inputRect.anchorMax = new Vector2(0.90f, 0.55f);
        inputRect.sizeDelta = Vector2.zero;

        Image inputImg = inputObj.AddComponent<Image>();
        inputImg.material = inputMat;

        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();

        // Add BoxCollider so XRI raycasts & finger pokes hit input field
        BoxCollider inputCollider = inputObj.AddComponent<BoxCollider>();
        inputCollider.size = new Vector3(288f, 40f, 15f);
        inputCollider.isTrigger = true;

        // Add XRSimpleInteractable for XRI interaction detection
        inputObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>();

        // Text Area inside Input Field
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(inputObj.transform, false);
        RectTransform areaRect = textArea.AddComponent<RectTransform>();
        areaRect.anchorMin = new Vector2(0.04f, 0.1f);
        areaRect.anchorMax = new Vector2(0.96f, 0.9f);
        areaRect.sizeDelta = Vector2.zero;
        textArea.AddComponent<RectMask2D>();

        // Text Display
        GameObject textDisplayObj = new GameObject("Text");
        textDisplayObj.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI textDisplay = textDisplayObj.AddComponent<TextMeshProUGUI>();
        textDisplay.fontSize = 17;
        textDisplay.color = new Color(0.18f, 0.20f, 0.16f); // Charcoal dark text
        textDisplay.alignment = TextAlignmentOptions.Left;
        RectTransform textDispRect = textDisplayObj.GetComponent<RectTransform>();
        textDispRect.anchorMin = Vector2.zero;
        textDispRect.anchorMax = Vector2.one;
        textDispRect.sizeDelta = Vector2.zero;

        // Placeholder Component
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholder.text = "Pengunjung";
        placeholder.fontSize = 17;
        placeholder.fontStyle = FontStyles.Normal;
        placeholder.color = new Color(0.55f, 0.58f, 0.52f, 0.9f); // Gray placeholder matching image
        placeholder.alignment = TextAlignmentOptions.Left;
        RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.sizeDelta = Vector2.zero;

        inputField.textViewport = areaRect;
        inputField.textComponent = textDisplay;
        inputField.placeholder = placeholder;
        inputField.text = PlayerPrefs.GetString("PlayerName", "Pengunjung");

        // 4. Start Button ("Mulai") - Pale Olive Lime Yellow Rounded Pill Button
        Material mulaiMat = GetOrCreateRoundedMaterial("Mat_MulaiButton",
            new Color(0.72f, 0.76f, 0.44f, 0.95f), // Pale olive-lime yellow fill (#B8C06C)
            new Color(0.62f, 0.66f, 0.36f, 0.98f), // Border
            0.02f, // Border width
            0.30f, // Rounded pill corners
            120f / 36f // Aspect ratio
        );

        GameObject startBtn = new GameObject("StartButton");
        startBtn.transform.SetParent(canvasObj.transform, false);
        Image startImg = startBtn.AddComponent<Image>();
        startImg.material = mulaiMat;

        RectTransform startRect = startBtn.GetComponent<RectTransform>();
        startRect.anchorMin = new Vector2(0.33f, 0.10f);
        startRect.anchorMax = new Vector2(0.67f, 0.27f);
        startRect.sizeDelta = Vector2.zero;

        GameObject startTxtObj = new GameObject("Text");
        startTxtObj.transform.SetParent(startBtn.transform, false);
        TextMeshProUGUI startText = startTxtObj.AddComponent<TextMeshProUGUI>();
        startText.text = "Mulai";
        startText.fontSize = 20;
        startText.fontStyle = FontStyles.Bold;
        startText.alignment = TextAlignmentOptions.Center;
        startText.color = Color.white; // Crisp white text
        RectTransform startTextRect = startTxtObj.GetComponent<RectTransform>();
        startTextRect.anchorMin = Vector2.zero;
        startTextRect.anchorMax = Vector2.one;
        startTextRect.sizeDelta = Vector2.zero;

        // Attach XRButtonSelection for hover animation and click handlers
        XRButtonSelection startSelection = startBtn.AddComponent<XRButtonSelection>();
        startSelection.buttonImage = startImg;
        startSelection.scaleTarget = startBtn.transform;
        startSelection.normalColor = new Color(0.72f, 0.76f, 0.44f, 0.95f);
        startSelection.hoverColor = new Color(0.80f, 0.84f, 0.50f, 1.00f);
        startSelection.hoverScaleMultiplier = 1.06f;

        // Add BoxCollider for XRI raycasts & finger pokes
        BoxCollider startCollider = startBtn.AddComponent<BoxCollider>();
        startCollider.size = new Vector3(120f, 40f, 15f);
        startCollider.isTrigger = true;

        // Link StartButton click programmatically to menuMgr.StartExploration
        UnityEditor.Events.UnityEventTools.AddPersistentListener(startSelection.onClick, menuMgr.StartExploration);

        return canvasObj;
    }

    [MenuItem("Tools/Museum MR/Update Gallery Room HUD Only")]
    public static void UpdateGalleryRoomHUDOnly()
    {
        RoomManager roomMgr = FindObjectOfType<RoomManager>();
        BuildRoomHUDUI(roomMgr);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("Gallery Room HUD UI updated successfully without resetting other UI!");
    }

    private static GameObject BuildRoomHUDUI(RoomManager manager)
    {
        GameObject canvasObj = GameObject.Find("RoomHUDCanvas");
        if (canvasObj != null) DestroyImmediate(canvasObj);

        canvasObj = new GameObject("RoomHUDCanvas");
        float cameraY = Camera.main != null ? Camera.main.transform.position.y : 1.6f;
        canvasObj.transform.position = new Vector3(-0.4f, cameraY + 0.2f, 1.2f);
        canvasObj.transform.rotation = Quaternion.identity;

        GlanceableHUD glanceableHUD = canvasObj.AddComponent<GlanceableHUD>();
        glanceableHUD.positionOffset = new Vector3(-0.4f, 0.2f, 1.2f);
        glanceableHUD.useDeadzone = true;
        glanceableHUD.distanceThreshold = 0.15f;
        glanceableHUD.angleThreshold = 15.0f;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360, 560);
        canvasObj.transform.localScale = Vector3.one * 0.0012f;

        Color paleYellow = new Color(0.90f, 0.93f, 0.63f, 1.0f); // #E5EE9C
        Color lightGreen = new Color(0.55f, 0.89f, 0.63f, 1.0f); // #8BE4A0

        // Translucent Dotted Card Background
        Image bgImg = canvasObj.AddComponent<Image>();
        Material cardMat = GetOrCreateTranslucentCardMaterial("Mat_OptionsCardBackground",
            new Color(0.43f, 0.46f, 0.38f, 0.88f),
            new Color(0.28f, 0.30f, 0.25f, 0.50f),
            14.0f,
            0.07f,
            0.08f,
            360f / 560f
        );
        bgImg.material = cardMat;

        // 1. Header Bar: Temple Icon + "Ruang Galeri" + Subtitle
        GameObject headerIconObj = new GameObject("HeaderIcon");
        headerIconObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI headerIcon = headerIconObj.AddComponent<TextMeshProUGUI>();
        headerIcon.text = "🏛";
        headerIcon.fontSize = 36;
        headerIcon.alignment = TextAlignmentOptions.Center;
        headerIcon.color = paleYellow;
        RectTransform headerIconRect = headerIconObj.GetComponent<RectTransform>();
        headerIconRect.anchorMin = new Vector2(0.05f, 0.88f);
        headerIconRect.anchorMax = new Vector2(0.20f, 0.97f);
        headerIconRect.sizeDelta = Vector2.zero;

        GameObject titleObj = new GameObject("RoomTitleText");
        titleObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI roomTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        roomTitleText.text = "Ruang Galeri";
        roomTitleText.fontSize = 24;
        roomTitleText.fontStyle = FontStyles.Bold;
        roomTitleText.alignment = TextAlignmentOptions.Left;
        roomTitleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.22f, 0.92f);
        titleRect.anchorMax = new Vector2(0.85f, 0.97f);
        titleRect.sizeDelta = Vector2.zero;

        GameObject subObj = new GameObject("RoomSubtitleText");
        subObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI subText = subObj.AddComponent<TextMeshProUGUI>();
        subText.text = "Traditional Malaysian Painting";
        subText.fontSize = 14;
        subText.alignment = TextAlignmentOptions.Left;
        subText.color = paleYellow;
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.22f, 0.88f);
        subRect.anchorMax = new Vector2(0.85f, 0.92f);
        subRect.sizeDelta = Vector2.zero;

        // Close Button ('X')
        GameObject closeBtn = new GameObject("CloseButton");
        closeBtn.transform.SetParent(canvasObj.transform, false);
        Image closeImg = closeBtn.AddComponent<Image>();
        closeImg.material = GetOrCreateRoundedMaterial("Mat_CloseBtn",
            new Color(0.22f, 0.24f, 0.20f, 0.90f),
            new Color(0.90f, 0.90f, 0.90f, 0.90f),
            0.04f,
            0.50f,
            1.0f
        );
        RectTransform closeRect = closeBtn.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.86f, 0.90f);
        closeRect.anchorMax = new Vector2(0.95f, 0.96f);
        closeRect.sizeDelta = Vector2.zero;

        GameObject closeTxtObj = new GameObject("Text");
        closeTxtObj.transform.SetParent(closeBtn.transform, false);
        TextMeshProUGUI closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "✕";
        closeTxt.fontSize = 16;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color = Color.white;
        RectTransform closeTxtRect = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.sizeDelta = Vector2.zero;

        // 2. Section Header: "Artefak di Ruangan ini" + "Jumlah Artefak: 5" + Line
        GameObject secHeaderObj = new GameObject("SectionHeader");
        secHeaderObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI secHeader = secHeaderObj.AddComponent<TextMeshProUGUI>();
        secHeader.text = "Artefak di Ruangan ini";
        secHeader.fontSize = 20;
        secHeader.fontStyle = FontStyles.Bold;
        secHeader.alignment = TextAlignmentOptions.Left;
        secHeader.color = Color.white;
        RectTransform secHeaderRect = secHeaderObj.GetComponent<RectTransform>();
        secHeaderRect.anchorMin = new Vector2(0.08f, 0.82f);
        secHeaderRect.anchorMax = new Vector2(0.92f, 0.86f);
        secHeaderRect.sizeDelta = Vector2.zero;

        GameObject countObj = new GameObject("ArtifactCountText");
        countObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI countText = countObj.AddComponent<TextMeshProUGUI>();
        countText.text = "Jumlah Artefak: 5";
        countText.fontSize = 14;
        countText.alignment = TextAlignmentOptions.Left;
        countText.color = lightGreen;
        RectTransform countRect = countObj.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0.08f, 0.78f);
        countRect.anchorMax = new Vector2(0.92f, 0.82f);
        countRect.sizeDelta = Vector2.zero;

        // Separator Line
        GameObject lineObj = new GameObject("SeparatorLine");
        lineObj.transform.SetParent(canvasObj.transform, false);
        Image lineImg = lineObj.AddComponent<Image>();
        lineImg.color = new Color(1f, 1f, 1f, 0.25f);
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.08f, 0.77f);
        lineRect.anchorMax = new Vector2(0.92f, 0.775f);
        lineRect.sizeDelta = Vector2.zero;

        // 3. Artifact List Container (Vertical Layout)
        GameObject listContainer = new GameObject("ArtifactList");
        listContainer.transform.SetParent(canvasObj.transform, false);
        
        VerticalLayoutGroup layout = listContainer.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        RectTransform listRect = listContainer.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.06f, 0.14f);
        listRect.anchorMax = new Vector2(0.94f, 0.76f);
        listRect.sizeDelta = Vector2.zero;

        // Populate 5 Sample Items for crisp editor preview
        Sprite monaSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/Placeholder_MonaLisa.mat");
        for (int i = 1; i <= 5; i++)
        {
            GameObject rowObj = new GameObject($"ArtifactItem_{i}");
            rowObj.transform.SetParent(listContainer.transform, false);
            RectTransform rRect = rowObj.AddComponent<RectTransform>();
            rRect.sizeDelta = new Vector2(300, 62);

            Image rowBg = rowObj.AddComponent<Image>();
            Material rowMat = GetOrCreateRoundedMaterial("Mat_OptionRow",
                new Color(0.35f, 0.38f, 0.31f, 0.65f),
                new Color(0.48f, 0.52f, 0.43f, 0.80f),
                0.015f,
                0.12f,
                300f / 62f
            );
            rowBg.material = rowMat;

            // Thumb
            GameObject tObj = new GameObject("Thumb");
            tObj.transform.SetParent(rowObj.transform, false);
            Image tImg = tObj.AddComponent<Image>();
            if (monaSprite != null) tImg.sprite = monaSprite;
            RectTransform tr = tObj.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.03f, 0.10f);
            tr.anchorMax = new Vector2(0.20f, 0.90f);
            tr.sizeDelta = Vector2.zero;

            // NumText
            GameObject nObj = new GameObject("NumText");
            nObj.transform.SetParent(rowObj.transform, false);
            TextMeshProUGUI nTxt = nObj.AddComponent<TextMeshProUGUI>();
            nTxt.text = i.ToString("00");
            nTxt.fontSize = 18;
            nTxt.fontStyle = FontStyles.Bold;
            nTxt.color = paleYellow;
            nTxt.alignment = TextAlignmentOptions.Left;
            RectTransform nr = nObj.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.22f, 0.48f);
            nr.anchorMax = new Vector2(0.30f, 0.92f);
            nr.sizeDelta = Vector2.zero;

            // NameText
            GameObject nmObj = new GameObject("NameText");
            nmObj.transform.SetParent(rowObj.transform, false);
            TextMeshProUGUI nmTxt = nmObj.AddComponent<TextMeshProUGUI>();
            nmTxt.text = "Mona Lisa";
            nmTxt.fontSize = 22;
            nmTxt.fontStyle = FontStyles.Bold;
            nmTxt.color = paleYellow;
            nmTxt.alignment = TextAlignmentOptions.Left;
            RectTransform nmr = nmObj.GetComponent<RectTransform>();
            nmr.anchorMin = new Vector2(0.31f, 0.48f);
            nmr.anchorMax = new Vector2(0.96f, 0.92f);
            nmr.sizeDelta = Vector2.zero;

            // StatusText
            GameObject stObj = new GameObject("StatusText");
            stObj.transform.SetParent(rowObj.transform, false);
            TextMeshProUGUI stTxt = stObj.AddComponent<TextMeshProUGUI>();
            bool isScanned = (i == 1 || i == 3);
            stTxt.text = isScanned ? "Sudah Dikunjungi" : "Belum Dikunjungi";
            stTxt.fontSize = 15;
            stTxt.color = isScanned ? lightGreen : new Color(0.88f, 0.88f, 0.88f);
            stTxt.alignment = TextAlignmentOptions.Left;
            RectTransform str = stObj.GetComponent<RectTransform>();
            str.anchorMin = new Vector2(0.31f, 0.10f);
            str.anchorMax = new Vector2(0.96f, 0.48f);
            str.sizeDelta = Vector2.zero;
        }

        // 4. Bottom Right Button ("Temukan")
        GameObject findBtn = new GameObject("FindButton");
        findBtn.transform.SetParent(canvasObj.transform, false);
        Image findBtnImg = findBtn.AddComponent<Image>();
        Material findMat = GetOrCreateRoundedMaterial("Mat_MulaiButton",
            new Color(0.72f, 0.76f, 0.44f, 0.95f),
            new Color(0.62f, 0.66f, 0.36f, 0.98f),
            0.02f,
            0.30f,
            120f / 36f
        );
        findBtnImg.material = findMat;

        RectTransform findBtnRect = findBtn.GetComponent<RectTransform>();
        findBtnRect.anchorMin = new Vector2(0.58f, 0.03f);
        findBtnRect.anchorMax = new Vector2(0.94f, 0.11f);
        findBtnRect.sizeDelta = Vector2.zero;

        GameObject findTxtObj = new GameObject("Text");
        findTxtObj.transform.SetParent(findBtn.transform, false);
        TextMeshProUGUI findText = findTxtObj.AddComponent<TextMeshProUGUI>();
        findText.text = "🔍  Temukan";
        findText.fontSize = 18;
        findText.fontStyle = FontStyles.Bold;
        findText.alignment = TextAlignmentOptions.Center;
        findText.color = Color.white;
        RectTransform findTxtRect = findTxtObj.GetComponent<RectTransform>();
        findTxtRect.anchorMin = Vector2.zero;
        findTxtRect.anchorMax = Vector2.one;
        findTxtRect.sizeDelta = Vector2.zero;

        // Assign UI parameters to RoomManager
        if (manager != null)
        {
            manager.roomHudContainer = canvasObj;
            manager.roomNameText = roomTitleText;
            manager.roomSubtitleText = subText;
            manager.roomArtifactCountText = countText;
            manager.artifactListContainer = listContainer.transform;
            manager.findButton = findBtn;
        }

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

    private static Material GetOrCreateDottedMaterial(string matName, Color bgColor, Color dotColor, float gridSize, float dotRadius)
    {
        string path = $"Assets/Prefabs/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("UI/DottedGrid");
            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_Color")) mat.SetColor("_Color", bgColor);
        if (mat.HasProperty("_DotColor")) mat.SetColor("_DotColor", dotColor);
        if (mat.HasProperty("_DotGridSize")) mat.SetFloat("_DotGridSize", gridSize);
        if (mat.HasProperty("_DotRadius")) mat.SetFloat("_DotRadius", dotRadius);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetOrCreateTranslucentCardMaterial(string matName, Color cardColor, Color dotColor, float gridSize, float dotRadius, float cornerRadius, float aspect)
    {
        string path = $"Assets/Prefabs/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("UI/TranslucentDottedCard");
            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_Color")) mat.SetColor("_Color", cardColor);
        if (mat.HasProperty("_DotColor")) mat.SetColor("_DotColor", dotColor);
        if (mat.HasProperty("_DotGridSize")) mat.SetFloat("_DotGridSize", gridSize);
        if (mat.HasProperty("_DotRadius")) mat.SetFloat("_DotRadius", dotRadius);
        if (mat.HasProperty("_CornerRadius")) mat.SetFloat("_CornerRadius", cornerRadius);
        if (mat.HasProperty("_Aspect")) mat.SetFloat("_Aspect", aspect);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject BuildWristWatchAndOptionsUI(Camera mainCam, GameObject hudCanvas)
    {
        while (true)
        {
            GameObject obj = GameObject.Find("WristMenuSystem");
            if (obj == null) break;
            DestroyImmediate(obj);
        }

        while (true)
        {
            GameObject obj = GameObject.Find("WristWatchButtonCanvas");
            if (obj == null) break;
            DestroyImmediate(obj);
        }

        while (true)
        {
            GameObject obj = GameObject.Find("OptionsPanelCanvas");
            if (obj == null) break;
            DestroyImmediate(obj);
        }

        GameObject menuContainerObj = new GameObject("WristMenuSystem");
        WristWatchMenu wristController = menuContainerObj.AddComponent<WristWatchMenu>();
        wristController.roomHudCanvas = hudCanvas;

        // -------------------------------------------------------------
        // 1. WRIST WATCH BUTTON (Using Menu Icon.png Picture Asset)
        // -------------------------------------------------------------
        Sprite menuIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/Menu Icon.png");

        GameObject watchCanvasObj = new GameObject("WristWatchButtonCanvas");
        watchCanvasObj.transform.SetParent(menuContainerObj.transform, false);

        Canvas watchCanvas = watchCanvasObj.AddComponent<Canvas>();
        watchCanvas.renderMode = RenderMode.WorldSpace;
        watchCanvas.worldCamera = mainCam;
        watchCanvasObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        RectTransform watchRect = watchCanvasObj.GetComponent<RectTransform>();
        watchRect.sizeDelta = new Vector2(160, 160);
        watchCanvasObj.transform.localScale = Vector3.one * 0.0004f; // ~6.4cm diameter with 2x high resolution

        // Menu Icon Sprite Image (Using Picture asset)
        Image watchImg = watchCanvasObj.AddComponent<Image>();
        if (menuIconSprite != null)
        {
            watchImg.sprite = menuIconSprite;
            watchImg.material = null;
        }
        else
        {
            Material watchMat = GetOrCreateRoundedMaterial("Mat_WristWatchButton",
                new Color(0.35f, 0.38f, 0.31f, 0.92f), // Dark olive sage green fill (#5A6050)
                new Color(0.48f, 0.52f, 0.43f, 0.95f), // Subtle border
                0.015f,
                0.50f, // Perfect circle
                1.0f
            );
            watchImg.material = watchMat;
        }

        // Attach XRButtonSelection & BoxCollider for interaction
        XRButtonSelection watchSelection = watchCanvasObj.AddComponent<XRButtonSelection>();
        watchSelection.buttonImage = watchImg;
        watchSelection.scaleTarget = watchCanvasObj.transform;
        watchSelection.normalColor = Color.white;
        watchSelection.hoverColor = new Color(0.9f, 0.9f, 0.9f, 1.0f);
        watchSelection.hoverScaleMultiplier = 1.10f;

        BoxCollider watchCollider = watchCanvasObj.AddComponent<BoxCollider>();
        watchCollider.size = new Vector3(160f, 160f, 15f);
        watchCollider.isTrigger = true;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(watchSelection.onClick, wristController.ToggleOptionsPanel);

        // -------------------------------------------------------------
        // 2. FLOATING OPTIONS PANEL (Image 2 - Options Card)
        // -------------------------------------------------------------
        GameObject optionsCanvasObj = new GameObject("OptionsPanelCanvas");
        optionsCanvasObj.transform.SetParent(menuContainerObj.transform, false);

        Canvas optionsCanvas = optionsCanvasObj.AddComponent<Canvas>();
        optionsCanvas.renderMode = RenderMode.WorldSpace;
        optionsCanvas.worldCamera = mainCam;
        optionsCanvasObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        RectTransform optionsRect = optionsCanvasObj.GetComponent<RectTransform>();
        optionsRect.sizeDelta = new Vector2(640, 420);
        optionsCanvasObj.transform.localScale = Vector3.one * 0.0006f;

        // Translucent Card Background with Dot Matrix
        Image optionsBgImg = optionsCanvasObj.AddComponent<Image>();
        Material cardMat = GetOrCreateTranslucentCardMaterial("Mat_OptionsCardBackground",
            new Color(0.43f, 0.46f, 0.38f, 0.88f), // Translucent dark sage green (#6E7562)
            new Color(0.28f, 0.30f, 0.25f, 0.50f), // Darker sage dot grid
            14.0f,
            0.07f,
            0.08f,
            320f / 210f
        );
        optionsBgImg.material = cardMat;

        // Header Title: "Options"
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(optionsCanvasObj.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Options";
        titleText.fontSize = 26;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.15f, 0.78f);
        titleRect.anchorMax = new Vector2(0.85f, 0.95f);
        titleRect.sizeDelta = Vector2.zero;

        // Close Button ("X") on Top Right
        GameObject closeBtn = new GameObject("CloseButton");
        closeBtn.transform.SetParent(optionsCanvasObj.transform, false);
        Image closeImg = closeBtn.AddComponent<Image>();
        closeImg.material = GetOrCreateRoundedMaterial("Mat_CloseBtn",
            new Color(0.22f, 0.24f, 0.20f, 0.90f),
            new Color(0.90f, 0.90f, 0.90f, 0.90f),
            0.04f,
            0.50f, // Circular
            1.0f
        );
        RectTransform closeRect = closeBtn.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.86f, 0.78f);
        closeRect.anchorMax = new Vector2(0.95f, 0.93f);
        closeRect.sizeDelta = Vector2.zero;

        GameObject closeTxtObj = new GameObject("Text");
        closeTxtObj.transform.SetParent(closeBtn.transform, false);
        TextMeshProUGUI closeText = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeText.text = "✕";
        closeText.fontSize = 16;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = Color.white;
        RectTransform closeTxtRect = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.sizeDelta = Vector2.zero;

        XRButtonSelection closeSelection = closeBtn.AddComponent<XRButtonSelection>();
        closeSelection.buttonImage = closeImg;
        closeSelection.scaleTarget = closeBtn.transform;
        closeSelection.hoverScaleMultiplier = 1.10f;
        BoxCollider closeCollider = closeBtn.AddComponent<BoxCollider>();
        closeCollider.size = new Vector3(30f, 30f, 15f);
        closeCollider.isTrigger = true;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(closeSelection.onClick, wristController.CloseOptionsPanel);

        // Row Helper Function to build "Ruang" and "Artefak" option cards
        Material rowMat = GetOrCreateRoundedMaterial("Mat_OptionRow",
            new Color(0.35f, 0.38f, 0.31f, 0.65f), // Translucent inner row (#505646)
            new Color(0.48f, 0.52f, 0.43f, 0.80f),
            0.015f,
            0.12f,
            280f / 56f
        );

        Material actionBtnMat = GetOrCreateRoundedMaterial("Mat_ActionBtn",
            new Color(0.25f, 0.28f, 0.23f, 0.90f),
            new Color(0.85f, 0.88f, 0.80f, 0.85f),
            0.03f,
            0.50f, // Circular button
            1.0f
        );

        Sprite expandSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/Expand button.png");

        // --- ROW 1: "Ruang" (Gallery Rooms) ---
        GameObject row1 = new GameObject("Row_Ruang");
        row1.transform.SetParent(optionsCanvasObj.transform, false);
        Image row1Img = row1.AddComponent<Image>();
        row1Img.material = rowMat;
        RectTransform row1Rect = row1.GetComponent<RectTransform>();
        row1Rect.anchorMin = new Vector2(0.06f, 0.44f);
        row1Rect.anchorMax = new Vector2(0.94f, 0.72f);
        row1Rect.sizeDelta = Vector2.zero;

        // Row 1 Icon: Museum Building Temple Icon (🏛)
        GameObject row1IconObj = new GameObject("Icon");
        row1IconObj.transform.SetParent(row1.transform, false);
        TextMeshProUGUI row1Icon = row1IconObj.AddComponent<TextMeshProUGUI>();
        row1Icon.text = "🏛";
        row1Icon.fontSize = 28;
        row1Icon.alignment = TextAlignmentOptions.Center;
        row1Icon.color = iconColor; // Pale yellow #E5EE9C
        RectTransform row1IconRect = row1IconObj.GetComponent<RectTransform>();
        row1IconRect.anchorMin = new Vector2(0.04f, 0.1f);
        row1IconRect.anchorMax = new Vector2(0.22f, 0.9f);
        row1IconRect.sizeDelta = Vector2.zero;

        // Row 1 Label: "Ruang"
        GameObject row1TxtObj = new GameObject("Label");
        row1TxtObj.transform.SetParent(row1.transform, false);
        TextMeshProUGUI row1Text = row1TxtObj.AddComponent<TextMeshProUGUI>();
        row1Text.text = "Ruang";
        row1Text.fontSize = 22;
        row1Text.fontStyle = FontStyles.Bold;
        row1Text.alignment = TextAlignmentOptions.Left;
        row1Text.color = Color.white;
        RectTransform row1TxtRect = row1TxtObj.GetComponent<RectTransform>();
        row1TxtRect.anchorMin = new Vector2(0.25f, 0.1f);
        row1TxtRect.anchorMax = new Vector2(0.70f, 0.9f);
        row1TxtRect.sizeDelta = Vector2.zero;

        // Row 1 Action Expand Button (Using Expand button.png)
        GameObject row1Btn = new GameObject("ActionButton");
        row1Btn.transform.SetParent(row1.transform, false);
        Image row1BtnImg = row1Btn.AddComponent<Image>();
        if (expandSprite != null)
        {
            row1BtnImg.sprite = expandSprite;
            row1BtnImg.material = null;
        }
        else
        {
            row1BtnImg.material = actionBtnMat;
        }

        RectTransform row1BtnRect = row1Btn.GetComponent<RectTransform>();
        row1BtnRect.anchorMin = new Vector2(0.80f, 0.15f);
        row1BtnRect.anchorMax = new Vector2(0.95f, 0.85f);
        row1BtnRect.sizeDelta = Vector2.zero;

        XRButtonSelection row1Selection = row1Btn.AddComponent<XRButtonSelection>();
        row1Selection.buttonImage = row1BtnImg;
        row1Selection.scaleTarget = row1Btn.transform;
        row1Selection.hoverScaleMultiplier = 1.10f;
        BoxCollider row1Collider = row1Btn.AddComponent<BoxCollider>();
        row1Collider.size = new Vector3(40f, 40f, 15f);
        row1Collider.isTrigger = true;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(row1Selection.onClick, wristController.OnClickRuang);


        // --- ROW 2: "Artefak" (Artifact Details) ---
        GameObject row2 = new GameObject("Row_Artefak");
        row2.transform.SetParent(optionsCanvasObj.transform, false);
        Image row2Img = row2.AddComponent<Image>();
        row2Img.material = rowMat;
        RectTransform row2Rect = row2.GetComponent<RectTransform>();
        row2Rect.anchorMin = new Vector2(0.06f, 0.10f);
        row2Rect.anchorMax = new Vector2(0.94f, 0.38f);
        row2Rect.sizeDelta = Vector2.zero;

        // Row 2 Icon: Urn / Vase Artifact Icon (🏺)
        GameObject row2IconObj = new GameObject("Icon");
        row2IconObj.transform.SetParent(row2.transform, false);
        TextMeshProUGUI row2Icon = row2IconObj.AddComponent<TextMeshProUGUI>();
        row2Icon.text = "🏺";
        row2Icon.fontSize = 28;
        row2Icon.alignment = TextAlignmentOptions.Center;
        row2Icon.color = iconColor; // Pale yellow #E5EE9C
        RectTransform row2IconRect = row2IconObj.GetComponent<RectTransform>();
        row2IconRect.anchorMin = new Vector2(0.04f, 0.1f);
        row2IconRect.anchorMax = new Vector2(0.22f, 0.9f);
        row2IconRect.sizeDelta = Vector2.zero;

        // Row 2 Label: "Artefak"
        GameObject row2TxtObj = new GameObject("Label");
        row2TxtObj.transform.SetParent(row2.transform, false);
        TextMeshProUGUI row2Text = row2TxtObj.AddComponent<TextMeshProUGUI>();
        row2Text.text = "Artefak";
        row2Text.fontSize = 22;
        row2Text.fontStyle = FontStyles.Bold;
        row2Text.alignment = TextAlignmentOptions.Left;
        row2Text.color = Color.white;
        RectTransform row2TxtRect = row2TxtObj.GetComponent<RectTransform>();
        row2TxtRect.anchorMin = new Vector2(0.25f, 0.1f);
        row2TxtRect.anchorMax = new Vector2(0.70f, 0.9f);
        row2TxtRect.sizeDelta = Vector2.zero;

        // Row 2 Action Expand Button (Using Expand button.png)
        GameObject row2Btn = new GameObject("ActionButton");
        row2Btn.transform.SetParent(row2.transform, false);
        Image row2BtnImg = row2Btn.AddComponent<Image>();
        if (expandSprite != null)
        {
            row2BtnImg.sprite = expandSprite;
            row2BtnImg.material = null;
        }
        else
        {
            row2BtnImg.material = actionBtnMat;
        }

        RectTransform row2BtnRect = row2Btn.GetComponent<RectTransform>();
        row2BtnRect.anchorMin = new Vector2(0.80f, 0.15f);
        row2BtnRect.anchorMax = new Vector2(0.95f, 0.85f);
        row2BtnRect.sizeDelta = Vector2.zero;

        XRButtonSelection row2Selection = row2Btn.AddComponent<XRButtonSelection>();
        row2Selection.buttonImage = row2BtnImg;
        row2Selection.scaleTarget = row2Btn.transform;
        row2Selection.hoverScaleMultiplier = 1.10f;
        BoxCollider row2Collider = row2Btn.AddComponent<BoxCollider>();
        row2Collider.size = new Vector3(40f, 40f, 15f);
        row2Collider.isTrigger = true;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(row2Selection.onClick, wristController.OnClickArtefak);

        // Link controller references
        wristController.wristWatchButtonObj = watchCanvasObj;
        wristController.optionsPanelObj = optionsCanvasObj;

        return menuContainerObj;
    }
}
