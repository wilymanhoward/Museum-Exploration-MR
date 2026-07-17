using UnityEngine;

public class QRCodeScannerDebugger : MonoBehaviour
{
    [Tooltip("If true, keyboard inputs can be used in the Editor to simulate QR scanning.")]
    public bool enableDebugHotkeys = true;

    private void Update()
    {
        if (!enableDebugHotkeys || !Application.isEditor) return;

        if (QRCodeScanner.Instance == null) return;

        // Simulate scanning Room transition QR codes (Galeri Tekstil, Galeri Kraf, Galeri Sejarah, etc.)
        if (Input.GetKeyDown(KeyCode.Alpha1)) QRCodeScanner.Instance.SimulateScan("room_textile");
        else if (Input.GetKeyDown(KeyCode.Alpha2)) QRCodeScanner.Instance.SimulateScan("room_craft");
        else if (Input.GetKeyDown(KeyCode.Alpha3)) QRCodeScanner.Instance.SimulateScan("room_history");
        else if (Input.GetKeyDown(KeyCode.Alpha4)) QRCodeScanner.Instance.SimulateScan("room_mandalika");
        else if (Input.GetKeyDown(KeyCode.Alpha5)) QRCodeScanner.Instance.SimulateScan("room_art");

        // Simulate scanning Artifact QR codes for the starting gallery (Batik, Sutera, Batu Bersurat)
        else if (Input.GetKeyDown(KeyCode.Alpha6)) QRCodeScanner.Instance.SimulateScan("artifact_batik");
        else if (Input.GetKeyDown(KeyCode.Alpha7)) QRCodeScanner.Instance.SimulateScan("artifact_sutera");
        else if (Input.GetKeyDown(KeyCode.Alpha8)) QRCodeScanner.Instance.SimulateScan("artifact_batu");

        // Simulate scanning Mini-Game QR codes (Game 1, Game 3)
        else if (Input.GetKeyDown(KeyCode.Alpha9)) QRCodeScanner.Instance.SimulateScan("game_1");
        else if (Input.GetKeyDown(KeyCode.Alpha0)) QRCodeScanner.Instance.SimulateScan("game_3");

        // Simulate walking away / losing QR code
        else if (Input.GetKeyDown(KeyCode.X))
        {
            QRCodeScanner.Instance.SimulateLostScan();
        }
    }
}
