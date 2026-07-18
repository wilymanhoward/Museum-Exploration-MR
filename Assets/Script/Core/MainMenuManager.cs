using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject mainMenuCanvas;

    [Header("Exploration References")]
    public GameObject wayfindingSystem;
    public GameObject artifactsContainer;

    [Header("Name Entry")]
    [Tooltip("Label on the NameButton that displays the player's name. Auto-resolved from the menu canvas if left empty.")]
    public TextMeshProUGUI nameLabel;

    private const string NameLabelPrefix = "Nama: ";
    private const string DefaultPlayerName = "Pelawat";

    private TMP_InputField activeInputField;
    private TouchScreenKeyboard keyboard;
    private bool menuPositioned = false;
    private string currentName = DefaultPlayerName;

    void Start()
    {
        // Ensure the exploration-specific visuals are disabled at startup
        if (wayfindingSystem != null) wayfindingSystem.SetActive(false);
        if (artifactsContainer != null) artifactsContainer.SetActive(false);

        // Automatically extend all hand/controller raycast pointer lengths in the scene (including inactive ones) so players can reach the menu
        XRRayInteractor[] rayInteractors = FindObjectsOfType<XRRayInteractor>(true);
        foreach (var ray in rayInteractors)
        {
            if (ray != null)
            {
                ray.maxRaycastDistance = 10f; // Extend pointer length to 10 meters!
                Debug.Log($"Extended raycast pointer distance for: {ray.gameObject.name} to 10m.");
            }
        }

        currentName = PlayerPrefs.GetString("PlayerName", DefaultPlayerName);

        // Hook InputField selection event and XRI click/pinch event to trigger VR Keyboard
        if (mainMenuCanvas != null)
        {
            TMP_InputField inputField = mainMenuCanvas.GetComponentInChildren<TMP_InputField>();
            XRSimpleInteractable interactable = mainMenuCanvas.GetComponentInChildren<XRSimpleInteractable>();

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

            // Resolve the name label on the NameButton (used when there is no TMP_InputField in the scene)
            if (nameLabel == null)
            {
                foreach (TextMeshProUGUI label in mainMenuCanvas.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (label.transform.parent != null && label.transform.parent.name == "NameButton")
                    {
                        nameLabel = label;
                        break;
                    }
                }
            }
            RefreshNameLabel();
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

    private void RefreshNameLabel()
    {
        if (nameLabel != null)
        {
            nameLabel.text = NameLabelPrefix + currentName;
        }
    }

    private void OpenVRKeyboard(TMP_InputField inputField)
    {
        activeInputField = inputField;
#if !UNITY_EDITOR
        // Open the native Oculus/Meta Quest virtual overlay keyboard
        keyboard = TouchScreenKeyboard.Open(inputField.text, TouchScreenKeyboardType.Default, false, false, false, false, "Taip Nama Anda");
#endif
    }

    void Update()
    {
        // 1. Position the menu dynamically at eye level in front of the player ONLY when headset tracking starts
        if (!menuPositioned)
        {
            Transform camTransform = Camera.main != null ? Camera.main.transform : null;
            if (camTransform != null && camTransform.position.y > 0.1f)
            {
                GameObject targetMenu = mainMenuCanvas != null ? mainMenuCanvas : gameObject;
                // Spawn exactly 1.4 meters in front of the player gaze at eye level
                Vector3 pos = camTransform.position + camTransform.forward * 1.4f;
                pos.y = camTransform.position.y; 
                targetMenu.transform.position = pos;
      
                // Rotate Main Menu to face the player
                Vector3 directionToPlayer = camTransform.position - targetMenu.transform.position;
                directionToPlayer.y = 0; // Keep it upright
                if (directionToPlayer != Vector3.zero)
                {
                    targetMenu.transform.rotation = Quaternion.LookRotation(-directionToPlayer);
                }
                menuPositioned = true;
                Debug.Log($"Main Menu positioned at eye level: {pos.y}m and oriented to face player.");
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
                RefreshNameLabel();
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
                RefreshNameLabel();
                PlayerPrefs.SetString("PlayerName", finalName);
                PlayerPrefs.Save();

                keyboard = null;
                activeInputField = null;
            }
        }
#endif
    }
 
    /// <summary>
    /// Invoked when the user taps/clicks the Start button.
    /// </summary>
    public void StartExploration()
    {
        string finalName = currentName;
        if (mainMenuCanvas != null)
        {
            TMP_InputField inputField = mainMenuCanvas.GetComponentInChildren<TMP_InputField>();
            if (inputField != null)
            {
                finalName = inputField.text.Trim();
            }
        }

        if (string.IsNullOrEmpty(finalName))
        {
            finalName = DefaultPlayerName;
        }
 
        PlayerPrefs.SetString("PlayerName", finalName);
        PlayerPrefs.Save();
        Debug.Log($"Player registered name: {finalName}");
 
        ProceedStartExploration();
    }
 
    private void ProceedStartExploration()
    {
        Debug.Log("Museum Exploration: Starting gameplay!");
 
        // Hide the Main Menu
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.SetActive(false);
        }
 
        // Show standard references if assigned
        if (wayfindingSystem != null) wayfindingSystem.SetActive(true);
        if (artifactsContainer != null) artifactsContainer.SetActive(true);
 
        // Tell the Room Manager to start populating and setting up the wayfinding paths
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.StartExploration();
        }
    }
}