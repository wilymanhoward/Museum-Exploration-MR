using UnityEngine;

/// <summary>
/// Routes QR scans for content that has no scene-level listener of its own:
/// history topics ("sejarah_...", matching HistoryData.historyId) and mini-games ("game_1".."game_3").
/// Rooms and artifacts are handled by RoomManager and ArtifactManager.
///
/// Subscribes from a static initializer instead of a scene component because the MiniGames
/// canvas is switched off while hidden, so a listener on it would miss scans.
/// </summary>
public static class QRContentRouter
{
    private const string HistoryPrefix = "sejarah_";
    private const string GamePrefix = "game_";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        QRCodeScanner.OnQRCodeScanned -= HandleQRCodeScanned;
        QRCodeScanner.OnQRCodeScanned += HandleQRCodeScanned;
    }

    private static void HandleQRCodeScanned(string payload, Pose pose)
    {
        if (!MainMenu.IsExplorationStarted || string.IsNullOrEmpty(payload)) return;

        string id = payload.Trim().ToLower();
        if (id.StartsWith(HistoryPrefix))
        {
            HistoryManager.Instance.ShowHistoryDetail(id);
        }
        else if (id.StartsWith(GamePrefix))
        {
            StartGame(id);
        }
    }

    private static void StartGame(string gameId)
    {
        MiniGames games = MiniGames.Instance;
        if (games == null)
        {
            // The canvas may never have been enabled yet, so Awake (which sets Instance) hasn't run.
            foreach (MiniGames candidate in Resources.FindObjectsOfTypeAll<MiniGames>())
            {
                if (candidate.gameObject.scene.isLoaded) { games = candidate; break; }
            }
        }
        if (games == null)
        {
            Debug.LogWarning($"QRContentRouter: No MiniGames canvas in the scene; cannot start '{gameId}'.");
            return;
        }

        // Same placement as the wrist menu's game list: 0.6 m ahead at head height, facing the player.
        Transform cam = WallPlacementHelper.ResolveCameraTransform();
        Pose gamePose = new Pose(Vector3.zero, Quaternion.identity);
        if (cam != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (forward == Vector3.zero) forward = Vector3.forward;
            gamePose.position = cam.position + forward * 0.6f;
            gamePose.position.y = cam.position.y;
            gamePose.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        games.StartGame(gameId, gamePose);
    }
}
