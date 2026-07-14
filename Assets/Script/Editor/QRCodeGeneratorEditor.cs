using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class QRCodeGeneratorEditor : EditorWindow
{
    private string customPayload = "room_1";
    private string customFileName = "Room_1_QR";
    private string statusMessage = "";

    [MenuItem("Tools/Museum MR/QR Code Generator")]
    public static void ShowWindow()
    {
        GetWindow<QRCodeGeneratorEditor>("MR QR Generator");
    }

    [MenuItem("Tools/Museum MR/Generate All QR Codes")]
    public static void TriggerGenerateAll()
    {
        var window = GetWindow<QRCodeGeneratorEditor>("MR QR Generator");
        window.GenerateAllMuseumQRCodes();
    }

    private void OnGUI()
    {
        GUILayout.Label("Museum MR QR Code Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        GUILayout.Label("Batch Generation", EditorStyles.boldLabel);
        if (GUILayout.Button("Scan Project & Generate All QR Codes"))
        {
            GenerateAllMuseumQRCodes();
        }

        EditorGUILayout.Space();
        GUILayout.Label("Single Custom QR Code", EditorStyles.boldLabel);
        customPayload = EditorGUILayout.TextField("QR Content (Payload)", customPayload);
        customFileName = EditorGUILayout.TextField("File Name", customFileName);

        if (GUILayout.Button("Generate Custom QR Code"))
        {
            GenerateCustomQRCode(customPayload, customFileName);
        }

        EditorGUILayout.Space();
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }
    }

    private void GenerateCustomQRCode(string payload, string fileName)
    {
        string folderPath = Path.Combine(Application.dataPath, "QRCodes");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string filePath = Path.Combine(folderPath, $"{fileName}.png");
        string url = $"https://api.qrserver.com/v1/create-qr-code/?size=400x400&margin=10&data={UnityWebRequest.EscapeURL(payload)}";

        statusMessage = "Downloading...";
        Repaint();

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        var operation = request.SendWebRequest();

        // Editor update loop to wait for download since we are in an editor window
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
                    statusMessage = $"Successfully saved custom QR code to:\nAssets/QRCodes/{fileName}.png";
                }
                else
                {
                    statusMessage = $"Failed to download QR code: {request.error}";
                }
                request.Dispose();
                Repaint();
            }
        };

        EditorApplication.update += checkProgress;
    }

    private void GenerateAllMuseumQRCodes()
    {
        string folderPath = Path.Combine(Application.dataPath, "QRCodes");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Find all RoomData asset GUIDs
        string[] roomGuids = AssetDatabase.FindAssets("t:RoomData");
        int generatedCount = 0;

        statusMessage = $"Scanning project... Found {roomGuids.Length} RoomData assets.";
        Repaint();

        foreach (string guid in roomGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RoomData room = AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (room != null && !string.IsNullOrEmpty(room.roomId))
            {
                // Generate Room QR
                DownloadAndSaveQR(room.roomId, $"Room_{room.roomName.Replace(" ", "_")}_{room.roomId}");
                generatedCount++;

                // Generate QR for each artifact inside this room
                foreach (ArtifactData artifact in room.artifacts)
                {
                    if (artifact != null && !string.IsNullOrEmpty(artifact.artifactId))
                    {
                        DownloadAndSaveQR(artifact.artifactId, $"Artifact_{artifact.artifactName.Replace(" ", "_")}_{artifact.artifactId}");
                        generatedCount++;
                    }
                }
            }
        }

        // Also search for standalone ArtifactData that might not be assigned to a room
        string[] artifactGuids = AssetDatabase.FindAssets("t:ArtifactData");
        foreach (string guid in artifactGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ArtifactData artifact = AssetDatabase.LoadAssetAtPath<ArtifactData>(path);
            if (artifact != null && !string.IsNullOrEmpty(artifact.artifactId))
            {
                string expectedFileName = $"Artifact_{artifact.artifactName.Replace(" ", "_")}_{artifact.artifactId}";
                string fileFullPath = Path.Combine(folderPath, $"{expectedFileName}.png");

                // If not already downloaded during the room scan, download now
                if (!File.Exists(fileFullPath))
                {
                    DownloadAndSaveQR(artifact.artifactId, expectedFileName);
                    generatedCount++;
                }
            }
        }

        statusMessage = $"Successfully initiated download for {generatedCount} QR codes.\nCheck 'Assets/QRCodes/' folder in a few seconds.";
        Repaint();
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
}
