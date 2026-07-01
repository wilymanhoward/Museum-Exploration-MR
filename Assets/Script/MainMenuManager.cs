using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject mainMenuCanvas;

    [Header("Exploration References")]
    public GameObject wayfindingSystem;
    public GameObject artifactsContainer;

    void Start()
    {
        // Ensure the exploration-specific visuals are disabled at startup
        if (wayfindingSystem != null) wayfindingSystem.SetActive(false);
        if (artifactsContainer != null) artifactsContainer.SetActive(false);

        // Dynamically align the Main Menu to the player's eye level (camera height) at runtime
        GameObject targetMenu = mainMenuCanvas != null ? mainMenuCanvas : gameObject;
        Transform camTransform = Camera.main != null ? Camera.main.transform : null;
        if (targetMenu != null && camTransform != null)
        {
            Vector3 pos = targetMenu.transform.position;
            pos.y = camTransform.position.y;
            targetMenu.transform.position = pos;
            Debug.Log($"Main Menu aligned to active Camera height: {pos.y}m");
        }
    }

    /// <summary>
    /// Invoked when the user taps/clicks the Start button.
    /// </summary>
    public void StartExploration()
    {
        Debug.Log("Museum Exploration: Started!");

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