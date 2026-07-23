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
    private const string DefaultPlayerName = "Pengunjung";

    private TMP_InputField activeInputField;
    private TouchScreenKeyboard keyboard;
    private bool menuPositioned = false;
    private string currentName = DefaultPlayerName;
    private readonly System.Collections.Generic.Dictionary<XRRayInteractor, float> originalRayDistances = new System.Collections.Generic.Dictionary<XRRayInteractor, float>();

    void Start()
    {
        // Ensure the exploration-specific visuals are disabled at startup
        if (wayfindingSystem != null) wayfindingSystem.SetActive(false);
        if (artifactsContainer != null) artifactsContainer.SetActive(false);

        // Automatically extend all hand/controller raycast pointer lengths in the scene (including inactive ones) so players can reach the menu.
        // Remember each one's original distance so it can be restored once exploration/gameplay starts -
        // a 10m ray makes hand-pinch precision (e.g. dragging Batik game cards) unusably twitchy, since a
        // small hand rotation sweeps a huge arc at that distance.
        XRRayInteractor[] rayInteractors = FindObjectsOfType<XRRayInteractor>(true);
        foreach (var ray in rayInteractors)
        {
            if (ray != null)
            {
                if (!originalRayDistances.ContainsKey(ray))
                {
                    originalRayDistances[ray] = ray.maxRaycastDistance;
                }
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
        // Gate the ray-extension purely on whether the menu is actually visible right now,
        // rather than on whether StartExploration() was ever called - a player can reach a
        // mini-game (e.g. by scanning its QR directly) without ever pressing the menu's Start
        // button, and the previous "explorationStarted" latch would then never trip, leaving
        // every ray interactor (including hand-pinch rays used to drag Batik game cards)
        // stuck at 10m forever.
        bool menuActive = mainMenuCanvas != null && mainMenuCanvas.activeInHierarchy;
        if (!menuActive)
        {
            RestoreRayDistances();
            return;
        }

        // 1. Position the menu dynamically at eye level 1 meter in front of the player ONLY when headset tracking starts
        GameObject targetMenu = mainMenuCanvas != null ? mainMenuCanvas : gameObject;
        if (!menuPositioned)
        {
            Transform camTransform = Camera.main != null ? Camera.main.transform : null;
            if (camTransform != null && camTransform.position.y > 0.1f)
            {
                // Spawn exactly 1.0 meter in front of the player gaze at eye level
                Vector3 pos = camTransform.position + camTransform.forward * 1.0f;
                pos.y = camTransform.position.y; 
                targetMenu.transform.position = pos;
                menuPositioned = true;
                Debug.Log($"Main Menu positioned 1.0m in front of player and fixed at world position: {pos}");
            }
        }

        // 2. Continuously rotate the menu to face toward the player as they move around (while keeping world position fixed)
        Transform currentCam = Camera.main != null ? Camera.main.transform : null;
        if (currentCam != null && targetMenu != null)
        {
            Vector3 directionToPlayer = currentCam.position - targetMenu.transform.position;
            directionToPlayer.y = 0; // Keep canvas upright
            if (directionToPlayer.sqrMagnitude > 0.0001f)
            {
                targetMenu.transform.rotation = Quaternion.LookRotation(-directionToPlayer);
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
    /// Restores every ray interactor touched by this script back to its original
    /// raycast distance. Safe to call repeatedly - a no-op once already restored.
    /// </summary>
    private void RestoreRayDistances()
    {
        foreach (var kvp in originalRayDistances)
        {
            if (kvp.Key != null && kvp.Key.maxRaycastDistance > kvp.Value)
            {
                kvp.Key.maxRaycastDistance = kvp.Value;
            }
        }
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