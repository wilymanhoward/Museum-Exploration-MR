using UnityEngine;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject mainMenuCanvas;

    [Header("Exploration References")]
    public GameObject wayfindingSystem;
    public GameObject artifactsContainer;

    private TextMeshProUGUI nameButtonText;
    private TouchScreenKeyboard keyboard;
    private bool isTyping = false;
    private string currentInputName = "";

    void Start()
    {
        // Ensure the exploration-specific visuals are disabled at startup
        if (wayfindingSystem != null) wayfindingSystem.SetActive(false);
        if (artifactsContainer != null) artifactsContainer.SetActive(false);

        // Find the Text component of the NameButton
        if (mainMenuCanvas != null)
        {
            nameButtonText = mainMenuCanvas.transform.Find("NameButton/Text")?.GetComponent<TextMeshProUGUI>();
            if (nameButtonText != null)
            {
                nameButtonText.text = "Nama: " + PlayerPrefs.GetString("PlayerName", "Pelawat");
            }
        }

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

    /// <summary>
    /// Invoked when the user taps/clicks the Name button.
    /// </summary>
    public void StartEditingName()
    {
        if (isTyping) return;
        
        isTyping = true;
        currentInputName = PlayerPrefs.GetString("PlayerName", "Pelawat");

#if !UNITY_EDITOR
        // Open native Oculus / Meta Quest floating overlay keyboard in VR
        keyboard = TouchScreenKeyboard.Open(currentInputName, TouchScreenKeyboardType.Default, false, false, false, false, "Taip Nama Anda");
#else
        // Editor guide
        if (nameButtonText != null)
        {
            nameButtonText.text = "Taip: " + currentInputName + "|";
        }
#endif
    }

    void Update()
    {
#if !UNITY_EDITOR
        if (isTyping && keyboard != null)
        {
            currentInputName = keyboard.text;
            if (nameButtonText != null)
            {
                nameButtonText.text = "Taip: " + currentInputName + "|";
            }
            
            if (keyboard.status == TouchScreenKeyboard.Status.Done || keyboard.status == TouchScreenKeyboard.Status.Canceled || !keyboard.active)
            {
                string finalName = currentInputName.Trim();
                if (string.IsNullOrEmpty(finalName))
                {
                    finalName = "Pelawat";
                }
                
                PlayerPrefs.SetString("PlayerName", finalName);
                PlayerPrefs.Save();
                
                if (nameButtonText != null)
                {
                    nameButtonText.text = "Nama: " + finalName;
                }
                
                keyboard = null;
                isTyping = false;
            }
        }
#else
        if (isTyping)
        {
            // Capture keystrokes in Editor (no EventSystem needed!)
            foreach (char c in Input.inputString)
            {
                if (c == '\b') // Backspace
                {
                    if (currentInputName.Length > 0)
                    {
                        currentInputName = currentInputName.Substring(0, currentInputName.Length - 1);
                    }
                }
                else if (c == '\n' || c == '\r') // Enter key -> Save
                {
                    isTyping = false;
                    string finalName = currentInputName.Trim();
                    if (string.IsNullOrEmpty(finalName))
                    {
                        finalName = "Pelawat";
                    }
                    
                    PlayerPrefs.SetString("PlayerName", finalName);
                    PlayerPrefs.Save();
                    
                    if (nameButtonText != null)
                    {
                        nameButtonText.text = "Nama: " + finalName;
                    }
                    break;
                }
                else if (c != '\u001b') // Ignore Escape key
                {
                    currentInputName += c;
                }
                
                if (nameButtonText != null)
                {
                    nameButtonText.text = "Taip: " + currentInputName + "|";
                }
            }
        }
#endif
    }
 
    /// <summary>
    /// Invoked when the user taps/clicks the Start button.
    /// </summary>
    public void StartExploration()
    {
        if (isTyping)
        {
            isTyping = false;
            string finalName = currentInputName.Trim();
            if (string.IsNullOrEmpty(finalName))
            {
                finalName = "Pelawat";
            }
            PlayerPrefs.SetString("PlayerName", finalName);
            PlayerPrefs.Save();
            
            if (nameButtonText != null)
            {
                nameButtonText.text = "Nama: " + finalName;
            }
        }

        ProceedStartExploration();
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