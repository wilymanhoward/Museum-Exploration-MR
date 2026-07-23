using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Unity Editor Window tool to easily create, edit, manage, and generate Museum Room & Artifact Data
/// along with high-resolution QR codes.
/// Accessible via Unity menu: Tools -> Museum MR -> Room & Artifact Data Manager
/// </summary>
public class MuseumDataManagerWindow : EditorWindow
{
    [System.Serializable]
    public class ArtifactDraft
    {
        public string artifactId = "";
        public string artifactName = "";
        public string timePeriod = "";
        public string location = "";
        public string material = "";
        public string description = "";
        public GameObject modelPrefab;
        public Sprite primarySprite;
        public bool isExpanded = true;
    }

    [System.Serializable]
    public class RoomDraft
    {
        public string roomId = "";
        public string roomName = "";
        public string roomSubtitle = "";
        public List<ArtifactDraft> artifacts = new List<ArtifactDraft>();
        public bool isExpanded = true;
    }

    private List<RoomDraft> roomDrafts = new List<RoomDraft>();
    private Vector2 scrollPos;
    private string statusMessage = "";
    private bool isGenerating = false;

    [MenuItem("Tools/Museum MR/Room & Artifact Data Manager")]
    public static void ShowWindow()
    {
        var win = GetWindow<MuseumDataManagerWindow>("Museum Data & QR Manager");
        win.minSize = new Vector2(550, 650);
        win.LoadExistingProjectData();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        GUILayout.Label("Museum Room & Artifact Data Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use this tool to input room and artifact details, clear old sample data, and automatically generate ScriptableObject assets and QR Codes for your Meta Quest 3 MR project.",
            MessageType.Info
        );

        EditorGUILayout.Space(5);

        // --- TOP TOOLBAR BUTTONS ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add New Room", GUILayout.Height(30)))
        {
            AddNewRoom();
        }

        if (GUILayout.Button("Load Existing Data", GUILayout.Height(30)))
        {
            LoadExistingProjectData();
        }

        if (GUILayout.Button("Clear All Old Data & QR Codes", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Clear All Museum Data", "Are you sure you want to delete all room assets, artifact assets, and QR codes?", "Yes, Delete", "Cancel"))
            {
                ClearAllProjectMuseumData();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // --- STATUS BOX ---
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, isGenerating ? MessageType.Warning : MessageType.Info);
        }

        EditorGUILayout.Space(5);

        // --- ROOMS & ARTIFACTS DRAFT LIST ---
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (roomDrafts.Count == 0)
        {
            EditorGUILayout.HelpBox("No rooms defined yet. Click '+ Add New Room' above or 'Load Existing Data' to get started.", MessageType.Warning);
        }

        for (int r = 0; r < roomDrafts.Count; r++)
        {
            RoomDraft room = roomDrafts[r];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Room Header
            EditorGUILayout.BeginHorizontal();
            room.isExpanded = EditorGUILayout.Foldout(room.isExpanded, string.IsNullOrEmpty(room.roomName) ? $"Room {r + 1}" : room.roomName, true, EditorStyles.foldoutHeader);

            if (GUILayout.Button("Remove Room", GUILayout.Width(100)))
            {
                roomDrafts.RemoveAt(r);
                r--;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                continue;
            }
            EditorGUILayout.EndHorizontal();

            if (room.isExpanded)
            {
                EditorGUI.indentLevel++;
                room.roomId = EditorGUILayout.TextField("Room ID (QR Payload)", room.roomId);
                room.roomName = EditorGUILayout.TextField("Room Name", room.roomName);
                room.roomSubtitle = EditorGUILayout.TextField("Room Subtitle", room.roomSubtitle);

                EditorGUILayout.Space(5);
                GUILayout.Label($"Artifacts in {room.roomName} ({room.artifacts.Count}):", EditorStyles.boldLabel);

                for (int a = 0; a < room.artifacts.Count; a++)
                {
                    ArtifactDraft art = room.artifacts[a];
                    EditorGUILayout.BeginVertical("box");

                    EditorGUILayout.BeginHorizontal();
                    art.isExpanded = EditorGUILayout.Foldout(art.isExpanded, string.IsNullOrEmpty(art.artifactName) ? $"Artifact {a + 1}" : art.artifactName, true);
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        room.artifacts.RemoveAt(a);
                        a--;
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        continue;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (art.isExpanded)
                    {
                        art.artifactId = EditorGUILayout.TextField("Artifact ID (QR Payload)", art.artifactId);
                        art.artifactName = EditorGUILayout.TextField("Artifact Name", art.artifactName);
                        art.timePeriod = EditorGUILayout.TextField("Time Period / Origin", art.timePeriod);
                        art.location = EditorGUILayout.TextField("Location / Origin", art.location);
                        art.material = EditorGUILayout.TextField("Material / Medium", art.material);
                        art.modelPrefab = (GameObject)EditorGUILayout.ObjectField("3D Model Prefab", art.modelPrefab, typeof(GameObject), false);
                        art.primarySprite = (Sprite)EditorGUILayout.ObjectField("Thumbnail Image", art.primarySprite, typeof(Sprite), false);

                        GUILayout.Label("Description:");
                        art.description = EditorGUILayout.TextArea(art.description, GUILayout.Height(60));
                    }

                    EditorGUILayout.EndVertical();
                }

                if (GUILayout.Button("+ Add Artifact to " + (string.IsNullOrEmpty(room.roomName) ? "Room" : room.roomName)))
                {
                    AddNewArtifactToRoom(room);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        // --- BOTTOM GENERATE BUTTON ---
        GUI.backgroundColor = new Color(0.35f, 0.75f, 0.45f);
        if (GUILayout.Button("Generate Assets & All QR Codes", GUILayout.Height(40)))
        {
            SaveAndGenerateAllData();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
    }

    private void AddNewRoom()
    {
        int count = roomDrafts.Count + 1;
        RoomDraft r = new RoomDraft
        {
            roomId = $"room_{count}",
            roomName = $"Galeri {count}",
            roomSubtitle = $"Pameran Artefak Galeri {count}"
        };
        AddNewArtifactToRoom(r);
        roomDrafts.Add(r);
    }

    private void AddNewArtifactToRoom(RoomDraft room)
    {
        int artCount = room.artifacts.Count + 1;
        ArtifactDraft art = new ArtifactDraft
        {
            artifactId = $"artifact_{room.roomId}_{artCount}",
            artifactName = $"Artefak {artCount}",
            timePeriod = "Abad ke-19",
            location = "Malaysia",
            material = "Batu / Kayu",
            description = $"Keterangan ringkas bagi Artefak {artCount}."
        };
        room.artifacts.Add(art);
    }

    /// <summary>
    /// Loads existing RoomData and ArtifactData assets in the project into draft memory.
    /// </summary>
    public void LoadExistingProjectData()
    {
        roomDrafts.Clear();
        string[] roomGuids = AssetDatabase.FindAssets("t:RoomData");

        foreach (string guid in roomGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RoomData room = AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (room == null) continue;

            RoomDraft rd = new RoomDraft
            {
                roomId = room.roomId,
                roomName = room.roomName,
                roomSubtitle = room.roomSubtitle
            };

            if (room.artifacts != null)
            {
                foreach (ArtifactData art in room.artifacts)
                {
                    if (art == null) continue;
                    ArtifactDraft ad = new ArtifactDraft
                    {
                        artifactId = art.artifactId,
                        artifactName = art.artifactName,
                        timePeriod = art.timePeriod,
                        location = art.location,
                        material = art.material,
                        description = art.description,
                        modelPrefab = art.modelPrefab,
                        primarySprite = (art.images != null && art.images.Length > 0) ? art.images[0].sprite : null
                    };
                    rd.artifacts.Add(ad);
                }
            }

            roomDrafts.Add(rd);
        }

        statusMessage = $"Loaded {roomDrafts.Count} rooms from project assets.";
    }

    /// <summary>
    /// Clears all existing room/artifact data assets and QR codes from project folders.
    /// </summary>
    public void ClearAllProjectMuseumData()
    {
        int deletedCount = 0;

        // Delete Assets/MuseumData/
        string dataFolder = Path.Combine(Application.dataPath, "MuseumData");
        if (Directory.Exists(dataFolder))
        {
            Directory.Delete(dataFolder, true);
            deletedCount++;
        }

        // Delete Assets/QRCodes/
        string qrFolder = Path.Combine(Application.dataPath, "QRCodes");
        if (Directory.Exists(qrFolder))
        {
            Directory.Delete(qrFolder, true);
            deletedCount++;
        }

        // Re-create clean empty directories
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "MuseumData/Rooms"));
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "MuseumData/Artifacts"));
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "QRCodes"));

        roomDrafts.Clear();
        AssetDatabase.Refresh();

        // Reset RoomManager references in open scene if available
        RoomManager rm = FindObjectOfType<RoomManager>();
        if (rm != null)
        {
            rm.rooms.Clear();
            rm.startingRoom = null;
            EditorUtility.SetDirty(rm);
        }

        statusMessage = "Cleared all old museum assets and QR codes successfully!";
        Debug.Log("MuseumDataManagerWindow: Cleared old museum assets and QR codes.");
    }

    /// <summary>
    /// Saves all room and artifact drafts as ScriptableObjects and downloads matching QR codes.
    /// </summary>
    private void SaveAndGenerateAllData()
    {
        if (roomDrafts.Count == 0)
        {
            statusMessage = "No rooms to save. Please add at least one room.";
            return;
        }

        isGenerating = true;
        statusMessage = "Saving assets and generating QR codes...";

        string roomsFolderPath = "Assets/MuseumData/Rooms";
        string artifactsFolderPath = "Assets/MuseumData/Artifacts";
        string qrFolderPath = Path.Combine(Application.dataPath, "QRCodes");

        if (!Directory.Exists(Path.Combine(Application.dataPath, "MuseumData/Rooms")))
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "MuseumData/Rooms"));
        if (!Directory.Exists(Path.Combine(Application.dataPath, "MuseumData/Artifacts")))
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "MuseumData/Artifacts"));
        if (!Directory.Exists(qrFolderPath))
            Directory.CreateDirectory(qrFolderPath);

        List<RoomData> createdRoomAssets = new List<RoomData>();

        for (int r = 0; r < roomDrafts.Count; r++)
        {
            RoomDraft rd = roomDrafts[r];
            if (string.IsNullOrEmpty(rd.roomId)) rd.roomId = $"room_{r + 1}";
            if (string.IsNullOrEmpty(rd.roomName)) rd.roomName = $"Room {r + 1}";

            List<ArtifactData> createdArtifactAssets = new List<ArtifactData>();

            for (int a = 0; a < rd.artifacts.Count; a++)
            {
                ArtifactDraft ad = rd.artifacts[a];
                if (string.IsNullOrEmpty(ad.artifactId)) ad.artifactId = $"artifact_{rd.roomId}_{a + 1}";
                if (string.IsNullOrEmpty(ad.artifactName)) ad.artifactName = $"Artifact {a + 1}";

                string artAssetName = SafeFileName($"artifact_{ad.artifactId}_{ad.artifactName}");
                string artAssetPath = $"{artifactsFolderPath}/{artAssetName}.asset";

                ArtifactData artAsset = AssetDatabase.LoadAssetAtPath<ArtifactData>(artAssetPath);
                if (artAsset == null)
                {
                    artAsset = ScriptableObject.CreateInstance<ArtifactData>();
                    AssetDatabase.CreateAsset(artAsset, artAssetPath);
                }

                artAsset.artifactId = ad.artifactId;
                artAsset.artifactName = ad.artifactName;
                artAsset.timePeriod = ad.timePeriod;
                artAsset.location = ad.location;
                artAsset.material = ad.material;
                artAsset.description = ad.description;
                artAsset.modelPrefab = ad.modelPrefab;

                if (ad.primarySprite != null)
                {
                    artAsset.images = new ArtifactImage[]
                    {
                        new ArtifactImage { sprite = ad.primarySprite, title = ad.artifactName }
                    };
                }

                EditorUtility.SetDirty(artAsset);
                createdArtifactAssets.Add(artAsset);

                // Download QR code for artifact
                string artQrFileName = SafeFileName($"Artifact_{ad.artifactName}_{ad.artifactId}");
                DownloadAndSaveQR(ad.artifactId, artQrFileName);
            }

            string roomAssetName = SafeFileName($"room_{rd.roomId}_{rd.roomName}");
            string roomAssetPath = $"{roomsFolderPath}/{roomAssetName}.asset";

            RoomData roomAsset = AssetDatabase.LoadAssetAtPath<RoomData>(roomAssetPath);
            if (roomAsset == null)
            {
                roomAsset = ScriptableObject.CreateInstance<RoomData>();
                AssetDatabase.CreateAsset(roomAsset, roomAssetPath);
            }

            roomAsset.roomId = rd.roomId;
            roomAsset.roomName = rd.roomName;
            roomAsset.roomSubtitle = rd.roomSubtitle;
            roomAsset.artifacts = createdArtifactAssets;

            EditorUtility.SetDirty(roomAsset);
            createdRoomAssets.Add(roomAsset);

            // Download QR code for room entrance
            string roomQrFileName = SafeFileName($"Room_{rd.roomName}_{rd.roomId}");
            DownloadAndSaveQR(rd.roomId, roomQrFileName);
        }

        // Generate mini-game QR codes for convenience
        DownloadAndSaveQR("game_1", "Game_Guess_The_Artifact_game_1");
        DownloadAndSaveQR("game_2", "Game_Batik_Process_game_2");
        DownloadAndSaveQR("game_3", "Game_Assemble_Batu_game_3");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Automatically assign generated rooms to RoomManager in scene
        RoomManager rm = FindObjectOfType<RoomManager>();
        if (rm != null)
        {
            rm.rooms = createdRoomAssets;
            if (createdRoomAssets.Count > 0)
            {
                rm.startingRoom = createdRoomAssets[0];
            }
            EditorUtility.SetDirty(rm);
        }

        isGenerating = false;
        statusMessage = $"Successfully saved {createdRoomAssets.Count} rooms and generated all QR codes in 'Assets/QRCodes/'!";
        Debug.Log($"MuseumDataManagerWindow: {statusMessage}");
    }

    private void DownloadAndSaveQR(string payload, string fileName)
    {
        string folderPath = Path.Combine(Application.dataPath, "QRCodes");
        string filePath = Path.Combine(folderPath, $"{fileName}.png");
        string url = $"https://api.qrserver.com/v1/create-qr-code/?size=400x400&margin=10&data={UnityWebRequest.EscapeURL(payload)}";

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        var operation = request.SendWebRequest();

        EditorApplication.CallbackFunction checkProgress = null;
        checkProgress = () =>
        {
            if (operation.isDone)
            {
                EditorApplication.update -= checkProgress;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(filePath, request.downloadHandler.data);
                    AssetDatabase.Refresh();
                }
                request.Dispose();
            }
        };

        EditorApplication.update += checkProgress;
    }

    private static string SafeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name.Replace(" ", "_");
    }
}
