using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Room : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("Reference to the Room List Panel to return to.")]
    public RoomList roomListPanel;
    [Tooltip("Reference to the Artifact Detail Panel to transition to.")]
    public Artifact artifactDetailPanel;

    [Header("Canvas Reference")]
    [Tooltip("The canvas GameObject to hide. If null, will automatically find the parent Canvas's GameObject.")]
    public GameObject canvasObject;

    [Header("Text Fields")]
    public TextMeshProUGUI roomTitleText;
    public TextMeshProUGUI roomSubtitleText;
    public TextMeshProUGUI artifactCountText;

    [Header("Hierarchy References")]
    [Tooltip("The Layout Group (Vertical/Horizontal Layout Group) transform where artifact item buttons will be instantiated.")]
    public Transform artifactListContainer;

    [Header("Prefabs")]
    [Tooltip("The artifact item button prefab to instantiate in the list.")]
    public GameObject artifactItemPrefab;

    [Header("Close Buttons (Hides entire Canvas)")]
    public Button closeButton;
    public XRButtonSelection closeButtonXR;

    [Header("Back Buttons (Goes back to Room List)")]
    public Button backButton;
    public XRButtonSelection backButtonXR;

    private RoomData currentRoomData;

    private void Start()
    {
        // Hook close button click to close the canvas
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseCanvas);
        }
        if (closeButtonXR != null)
        {
            closeButtonXR.onClick.AddListener(CloseCanvas);
        }

        // Hook back button click to return to the room list
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackPressed);
        }
        if (backButtonXR != null)
        {
            backButtonXR.onClick.AddListener(OnBackPressed);
        }
    }

    /// <summary>
    /// Call this from RoomList to show the room details.
    /// </summary>
    public void ShowRoom(RoomData roomData)
    {
        currentRoomData = roomData;
        gameObject.SetActive(true);

        if (roomListPanel != null)
        {
            roomListPanel.gameObject.SetActive(false);
        }

        UpdateRoomDetails();
    }

    private void UpdateRoomDetails()
    {
        if (currentRoomData == null) return;

        // Set room titles
        if (roomTitleText != null) roomTitleText.text = currentRoomData.roomName;
        if (roomSubtitleText != null) roomSubtitleText.text = currentRoomData.roomSubtitle;

        // Set artifact count text
        int count = currentRoomData.artifacts != null ? currentRoomData.artifacts.Count : 0;
        if (artifactCountText != null)
        {
            artifactCountText.text = "Jumlah Artefak: " + count;
        }

        // Clear existing artifact items
        if (artifactListContainer != null)
        {
            foreach (Transform child in artifactListContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // Instantiate artifact items
        if (currentRoomData.artifacts != null && artifactItemPrefab != null && artifactListContainer != null)
        {
            int index = 1;
            foreach (ArtifactData artifact in currentRoomData.artifacts)
            {
                if (artifact == null) continue;

                GameObject itemObj = Instantiate(artifactItemPrefab, artifactListContainer);

                // Set artifact text details
                string formattedText = $"{index:D2} {artifact.artifactName}";
                index++;

                TextMeshProUGUI tmpText = itemObj.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmpText != null)
                {
                    tmpText.text = formattedText;
                }
                else
                {
                    Text uiText = itemObj.GetComponentInChildren<Text>(true);
                    if (uiText != null)
                    {
                        uiText.text = formattedText;
                    }
                }

                // Try to find image container inside the item object (excluding the item itself)
                Image[] images = itemObj.GetComponentsInChildren<Image>(true);
                foreach (Image img in images)
                {
                    if (img.gameObject != itemObj && img.gameObject.name != "Background" && artifact.images != null && artifact.images.Length > 0)
                    {
                        img.sprite = artifact.images[0].sprite;
                        break; // Assign first found thumbnail image slot
                    }
                }

                // Hook click event
                Button btn = itemObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnArtifactSelected(artifact));
                }

                XRButtonSelection xrBtn = itemObj.GetComponent<XRButtonSelection>();
                if (xrBtn != null)
                {
                    xrBtn.onClick.AddListener(() => OnArtifactSelected(artifact));
                }
            }
        }
    }

    private void OnArtifactSelected(ArtifactData artifact)
    {
        if (artifactDetailPanel != null)
        {
            // Show the artifact details panel
            artifactDetailPanel.ShowArtifact(artifact, this);
            // Hide this panel (canvas remains active)
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Room: artifactDetailPanel reference is missing!");
        }
    }

    private void OnBackPressed()
    {
        // Go back to the RoomList
        if (roomListPanel != null)
        {
            roomListPanel.gameObject.SetActive(true);
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides the canvas/parent canvas. This function can be called from UnityEvents.
    /// Since the panel state is not altered, revealing the canvas again will show the last active panel.
    /// </summary>
    public void CloseCanvas()
    {
        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }
        else
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                parentCanvas.gameObject.SetActive(false);
            }
            else
            {
                if (transform.parent != null)
                {
                    transform.parent.gameObject.SetActive(false);
                }
            }
        }
    }
}
