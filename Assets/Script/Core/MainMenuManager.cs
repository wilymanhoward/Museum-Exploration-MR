using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject mainMenuCanvas;

    [Header("Exploration References")]
    public GameObject wayfindingSystem;
    public GameObject artifactsContainer;

    private TextMeshProUGUI nameButtonText;
    private bool isTyping = false;
    private string currentInputName = "";

    // Virtual Keyboard UI Elements
    private GameObject keyboardPanel;
    private TextMeshProUGUI keyboardTitleText;

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

            // Build our custom on-screen virtual keyboard to bypass all EventSystem & OS keyboard issues!
            BuildVirtualKeyboard();
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

    private void BuildVirtualKeyboard()
    {
        // Create keyboard parent GameObject
        keyboardPanel = new GameObject("KeyboardPanel");
        keyboardPanel.transform.SetParent(mainMenuCanvas.transform, false);
        keyboardPanel.SetActive(false); // Hidden by default

        // Add a background plate for the keyboard (translucent slate)
        GameObject bg = new GameObject("Bg");
        bg.transform.SetParent(keyboardPanel.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.material = FindMaterial("Mat_MainMenu");
        bgImg.color = new Color(0.96f, 0.96f, 0.98f, 0.95f);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Title/Header
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(keyboardPanel.transform, false);
        keyboardTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        keyboardTitleText.text = "TAIP NAMA ANDA";
        keyboardTitleText.fontSize = 16;
        keyboardTitleText.alignment = TextAlignmentOptions.Center;
        keyboardTitleText.color = new Color(0.1f, 0.1f, 0.15f);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.75f);
        titleRect.anchorMax = new Vector2(0.95f, 0.95f);
        titleRect.sizeDelta = Vector2.zero;

        // Button Material
        Material buttonMat = FindMaterial("Mat_Button");

        // Define Rows of Keys
        string[] row1 = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
        string[] row2 = { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
        string[] row3 = { "Z", "X", "C", "V", "B", "N", "M", "<-" };
        string[] row4 = { "SPACE", "DONE" };

        CreateKeyboardRow(row1, 28f, 22f, buttonMat);
        CreateKeyboardRow(row2, 0f, 22f, buttonMat);
        CreateKeyboardRow(row3, -28f, 22f, buttonMat);
        CreateKeyboardRow(row4, -56f, 50f, buttonMat);
    }

    private void CreateKeyboardRow(string[] keys, float yPos, float keyWidth, Material btnMat)
    {
        float spacing = keyWidth + 3f;
        float totalWidth = 0f;

        // Calculate total row width considering variable sizes for special keys
        foreach (string k in keys)
        {
            if (k == "SPACE") totalWidth += 85f;
            else if (k == "DONE") totalWidth += 55f;
            else totalWidth += keyWidth;
            totalWidth += 3f;
        }
        totalWidth -= 3f; // remove trailing spacing

        float startX = -totalWidth * 0.5f;

        float currentX = startX;
        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            float actualWidth = keyWidth;
            if (key == "SPACE") actualWidth = 85f;
            else if (key == "DONE") actualWidth = 55f;

            GameObject keyBtn = new GameObject($"Key_{key}");
            keyBtn.transform.SetParent(keyboardPanel.transform, false);

            RectTransform rect = keyBtn.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(actualWidth, 22f);
            rect.anchoredPosition = new Vector2(currentX + actualWidth * 0.5f, yPos);

            Image img = keyBtn.AddComponent<Image>();
            img.material = btnMat;
            img.color = new Color(0.9f, 0.9f, 0.93f, 0.8f);

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(keyBtn.transform, false);
            TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
            txt.text = key;
            txt.fontSize = 10;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(0.1f, 0.1f, 0.15f);

            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            XRButtonSelection select = keyBtn.AddComponent<XRButtonSelection>();
            select.buttonImage = img;
            select.scaleTarget = keyBtn.transform;

            BoxCollider col = keyBtn.AddComponent<BoxCollider>();
            col.size = new Vector3(actualWidth, 22f, 15f);
            col.isTrigger = true;

            select.onClick.AddListener(() => OnKeyClicked(key));

            currentX += actualWidth + 3f;
        }
    }

    private void OnKeyClicked(string key)
    {
        if (key == "DONE")
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

            // Hide keyboard and restore menu
            keyboardPanel.SetActive(false);
            RestoreMainMenuUI(true);
        }
        else if (key == "<-")
        {
            if (currentInputName.Length > 0)
            {
                currentInputName = currentInputName.Substring(0, currentInputName.Length - 1);
            }
        }
        else if (key == "SPACE")
        {
            if (currentInputName.Length < 14)
            {
                currentInputName += " ";
            }
        }
        else
        {
            if (currentInputName.Length < 14)
            {
                currentInputName += key;
            }
        }

        if (keyboardTitleText != null)
        {
            keyboardTitleText.text = "TAIP NAMA: " + currentInputName + "_";
        }
    }

    private void RestoreMainMenuUI(bool active)
    {
        if (mainMenuCanvas == null) return;
        Transform title = mainMenuCanvas.transform.Find("Title");
        Transform start = mainMenuCanvas.transform.Find("StartButton");
        Transform nameBtn = mainMenuCanvas.transform.Find("NameButton");

        if (title != null) title.gameObject.SetActive(active);
        if (start != null) start.gameObject.SetActive(active);
        if (nameBtn != null) nameBtn.gameObject.SetActive(active);
    }

    /// <summary>
    /// Invoked when the user taps/clicks the Name button.
    /// </summary>
    public void StartEditingName()
    {
        isTyping = true;
        currentInputName = PlayerPrefs.GetString("PlayerName", "Pelawat");
        
        // Hide Main Menu elements, show keyboard
        RestoreMainMenuUI(false);
        if (keyboardPanel != null)
        {
            keyboardPanel.SetActive(true);
        }
        
        if (keyboardTitleText != null)
        {
            keyboardTitleText.text = "TAIP NAMA: " + currentInputName + "_";
        }
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

    private Material FindMaterial(string matName)
    {
        Material[] mats = Resources.FindObjectsOfTypeAll<Material>();
        foreach (Material m in mats)
        {
            if (m.name == matName) return m;
        }
        return null;
    }
}