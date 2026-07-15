using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CustomMuseumRoomsSetup : EditorWindow
{
    [MenuItem("Tools/Museum MR/Setup Custom Museum Rooms")]
    public static void SetupCustomRooms()
    {
        Debug.Log("Starting Custom Museum Rooms and Artifacts Setup...");

        // Ensure directory exists
        string museumDataDir = "Assets/MuseumData";
        if (!Directory.Exists(museumDataDir))
        {
            Directory.CreateDirectory(museumDataDir);
        }

        // List to hold all rooms
        List<RoomData> allRooms = new List<RoomData>();

        // 1. Galeri Tekstil: Batik & Sutera
        var textileArtifacts = new List<ArtifactData>()
        {
            CreateOrUpdateArtifact("artifact_batik", "Batik", "Traditional Weaver", "Historical", 
                "A traditional wax-resist dyed fabric representing deep cultural heritage and intricate patterning."),
            CreateOrUpdateArtifact("artifact_sutera", "Sutera", "Traditional Weaver", "Historical", 
                "Fine traditional silk fabric known for its smooth texture, elegance, and traditional craftsmanship.")
        };
        RoomData roomTextile = CreateOrUpdateRoom("room_textile", "Galeri Tekstil", textileArtifacts, 
            new Vector3[] { new Vector3(0, 0, 0), new Vector3(0, 0, 2) });
        allRooms.Add(roomTextile);

        // 2. Galeri Seni: Gamelan, Rodat
        var artArtifacts = new List<ArtifactData>()
        {
            // Reuse existing prefab if we have gamelan
            CreateOrUpdateArtifact("artifact_gamelan", "Gamelan", "Traditional Smith", "c. 1200 CE", 
                "Traditional bronze percussion instrument producing resonant and melodic metallic tones.", "model_artifact_gamelan"),
            CreateOrUpdateArtifact("artifact_rodat", "Rodat", "Traditional Drummer", "Historical", 
                "A traditional performing art drum and dance instrument originating from historical cultural exchanges.")
        };
        RoomData roomArt = CreateOrUpdateRoom("room_art", "Galeri Seni", artArtifacts, 
            new Vector3[] { new Vector3(0, 0, 0), new Vector3(2, 0, 2) });
        allRooms.Add(roomArt);

        // 3. Galeri Kraf: Keris, Menenun
        var craftArtifacts = new List<ArtifactData>()
        {
            CreateOrUpdateArtifact("artifact_keris", "Keris", "Empu Bladesmith", "c. 1400 CE", 
                "An asymmetrical wavy-bladed dagger representing spiritual heritage, social status, and bladesmithing excellence.", "model_artifact_keris"),
            CreateOrUpdateArtifact("artifact_menenun", "Menenun", "Traditional Artisan", "Historical", 
                "A traditional handloom weaving artifact demonstrating historical textile craft techniques.")
        };
        RoomData roomCraft = CreateOrUpdateRoom("room_craft", "Galeri Kraf", craftArtifacts, 
            new Vector3[] { new Vector3(0, 0, 0), new Vector3(-2, 0, 2) });
        allRooms.Add(roomCraft);

        // 4. Galeri Sejarah: Asal usul nama terengganu, pertarungan megat panji alam, pemberontakan tani
        var historyArtifacts = new List<ArtifactData>()
        {
            CreateOrUpdateArtifact("artifact_terengganu", "Asal Usul Nama Terengganu", "Historical Chronicler", "Historical", 
                "Historical details and manuscripts explaining the etymological origins of the name Terengganu."),
            CreateOrUpdateArtifact("artifact_megat", "Pertarungan Megat Panji Alam", "Historical Chronicler", "Historical", 
                "Legends and chronicles detailing the famous and epic duel of Megat Panji Alam."),
            CreateOrUpdateArtifact("artifact_tani", "Pemberontakan Tani", "Historical Chronicler", "1928", 
                "Exhibits documenting the historical peasant uprising led by Haji Abdul Rahman Limbong against colonial policies.")
        };
        RoomData roomHistory = CreateOrUpdateRoom("room_history", "Galeri Sejarah", historyArtifacts, 
            new Vector3[] { new Vector3(0, 0, 0), new Vector3(0, 0, 4) });
        allRooms.Add(roomHistory);

        // 5. Serambi Mandalika: Batu bersurat, Perahu besar
        var mandalikaArtifacts = new List<ArtifactData>()
        {
            CreateOrUpdateArtifact("artifact_batu", "Batu Bersurat", "Ancient Carver", "1303 CE", 
                "The Terengganu Inscribed Stone, a monumental historical artifact documenting early Islamic law and Jawi writing.", "model_artifact_batu"),
            CreateOrUpdateArtifact("artifact_perahu", "Perahu Besar", "Traditional Boatbuilder", "Historical", 
                "A large traditional wooden ship model displaying historic maritime transport and sea-faring heritage.")
        };
        RoomData roomMandalika = CreateOrUpdateRoom("room_mandalika", "Serambi Mandalika", mandalikaArtifacts, 
            new Vector3[] { new Vector3(0, 0, 0), new Vector3(3, 0, 0) });
        allRooms.Add(roomMandalika);

        // 6. Assign to MuseumManager in the scene
        MuseumManager museumManager = FindObjectOfType<MuseumManager>();
        if (museumManager != null)
        {
            Undo.RecordObject(museumManager, "Update Museum Rooms");
            museumManager.rooms = allRooms;
            museumManager.startingRoom = allRooms[0];
            EditorUtility.SetDirty(museumManager);
            
            // Mark scene dirty and save
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            
            Debug.Log("Successfully assigned rooms to MuseumManager in the active scene!");
        }
        else
        {
            Debug.LogWarning("MuseumManager was not found in the active scene. Please open the correct scene and run this setup tool again.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Custom Museum Rooms Setup Completed Successfully!");
    }

    private static ArtifactData CreateOrUpdateArtifact(string id, string name, string artist, string year, string desc, string preferredPrefabName = "")
    {
        string filename = id + "_" + name.Replace(" ", "").Replace("&", "And");
        string path = $"Assets/MuseumData/{filename}.asset";
        
        ArtifactData asset = AssetDatabase.LoadAssetAtPath<ArtifactData>(path);
        bool isNew = (asset == null);
        
        if (isNew)
        {
            asset = ScriptableObject.CreateInstance<ArtifactData>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.artifactId = id;
        asset.artifactName = name;
        asset.artist = artist;
        asset.year = year;
        asset.description = desc;

        // Try to link preferred prefab, or fall back to mock placeholder
        GameObject modelPrefab = null;
        if (!string.IsNullOrEmpty(preferredPrefabName))
        {
            modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{preferredPrefabName}.prefab");
        }

        if (modelPrefab == null)
        {
            // Try matching by ID name
            modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/model_{id}.prefab");
        }

        if (modelPrefab == null)
        {
            // Spawn a simple geometric mock model as placeholder
            string placeholderName = $"Placeholder_{id}";
            string placeholderPath = $"Assets/Prefabs/{placeholderName}.prefab";
            GameObject mockModel = AssetDatabase.LoadAssetAtPath<GameObject>(placeholderPath);
            if (mockModel == null)
            {
                mockModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mockModel.name = placeholderName;
                
                // shrink to fit nice
                mockModel.transform.localScale = Vector3.one * 0.25f;
                
                // assign a standard material
                Renderer renderer = mockModel.GetComponent<Renderer>();
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.2f, 0.6f, 0.9f);
                AssetDatabase.CreateAsset(mat, $"Assets/Prefabs/Mat_{placeholderName}.mat");
                renderer.sharedMaterial = mat;

                PrefabUtility.SaveAsPrefabAsset(mockModel, placeholderPath);
                DestroyImmediate(mockModel);
                mockModel = AssetDatabase.LoadAssetAtPath<GameObject>(placeholderPath);
            }
            modelPrefab = mockModel;
        }

        asset.modelPrefab = modelPrefab;
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static RoomData CreateOrUpdateRoom(string id, string name, List<ArtifactData> artifacts, Vector3[] waypoints)
    {
        string filename = id + "_" + name.Replace(" ", "").Replace("&", "And");
        string path = $"Assets/MuseumData/{filename}.asset";
        
        RoomData asset = AssetDatabase.LoadAssetAtPath<RoomData>(path);
        bool isNew = (asset == null);
        
        if (isNew)
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
}
