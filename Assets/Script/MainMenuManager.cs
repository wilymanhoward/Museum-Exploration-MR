using UnityEngine;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject mainMenuCanvas;

    [Header("Exploration References")]
    public GameObject wayfindingSystem;
    public GameObject artifactsContainer;

    private TouchScreenKeyboard keyboard;
    private bool isEnteringName = false;

    void Start()
    {
        // Ensure the exploration-specific visuals are disabled at startup
        if (wayfindingSystem != null) wayfindingSystem.SetActive(false);
        if (artifactsContainer != null) artifactsContainer.SetActive(false);

        // Dynamically align the Main Menu to the player's eye level (camera height) and rotate to face them at runtime
        GameObject targetMenu = mainMenuCanvas != null ? mainMenuCanvas : gameObject;
        Transform camTransform = Camera.main != null ? Camera.main.transform : null;
        if (targetMenu != null && camTransform != null)
        {
            // Spawn exactly 1.4 meters in front of the player's headset gaze direction at eye level
            Vector3 pos = camTransform.position + camTransform.forward * 1.4f;
            pos.y = camTransform.position.y; 
            targetMenu.transform.position = pos;
 
            // Rotate Main Menu to face the player camera
            Vector3 directionToPlayer = camTransform.position - targetMenu.transform.position;
            directionToPlayer.y = 0; // Keep the menu upright
            if (directionToPlayer != Vector3.zero)
            {
                targetMenu.transform.rotation = Quaternion.LookRotation(-directionToPlayer);
            }
            Debug.Log($"Main Menu positioned in front of player gaze at height: {pos.y}m and oriented to face player.");
        }
    }

    void Update()
    {
        if (isEnteringName && keyboard != null)
        {
            if (keyboard.status == TouchScreenKeyboard.Status.Done || keyboard.status == TouchScreenKeyboard.Status.Canceled || !keyboard.active)
            {
                string finalName = keyboard.text.Trim();
                if (string.IsNullOrEmpty(finalName))
                {
                    finalName = "Pelawat";
                }
                PlayerPrefs.SetString("PlayerName", finalName);
                PlayerPrefs.Save();
                Debug.Log($"Player registered name: {finalName}");

                isEnteringName = false;
                keyboard = null;

                // Reset button text
                if (mainMenuCanvas != null)
                {
                    var startTextComp = mainMenuCanvas.transform.Find("StartButton/Text")?.GetComponent<TextMeshProUGUI>();
                    if (startTextComp != null) startTextComp.text = "Start Exploration";
                }

                ProceedStartExploration();
            }
        }
    }

    /// <summary>
    /// Invoked when the user taps/clicks the Start button.
    /// </summary>
    public void StartExploration()
    {
        if (isEnteringName) return;

        // Open the native system/VR overlay keyboard to ask for player's name
        string defaultName = PlayerPrefs.GetString("PlayerName", "Pelawat");
        keyboard = TouchScreenKeyboard.Open(defaultName, TouchScreenKeyboardType.Default, false, false, false, false, "Masukkan Nama Anda");
        isEnteringName = true;

        if (mainMenuCanvas != null)
        {
            var startTextComp = mainMenuCanvas.transform.Find("StartButton/Text")?.GetComponent<TextMeshProUGUI>();
            if (startTextComp != null)
            {
                startTextComp.text = "Sila Taip Nama...";
            }
        }
    }

    private void ProceedStartExploration()
    {
        Debug.Log("Museum Exploration: Starting actual gameplay!");

        // Hide the Main Menu
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.SetActive(false);
        }

        // Show standard references if assigned
        if (wayfindingSystem != null) wayfindingSystem.SetActive(true);
        if (artifactsContainer != null) artifactsContainer.SetActive(true);

        // Tell the Museum Manager to start populating and setting up the wayfinding paths
        if (MuseumManager.Instance != null)
        {
            MuseumManager.Instance.StartExploration();
        }
    }
}