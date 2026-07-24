using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomListPanel : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("Reference to the Room Panel to transition to.")]
    public RoomPanel roomPanel;

    [Header("Canvas Reference")]
    [Tooltip("The canvas GameObject to hide. If null, will automatically find the parent Canvas's GameObject.")]
    public GameObject canvasObject;

    [Header("Hierarchy References")]
    [Tooltip("The Layout Group (Grid Layout Group) transform where room buttons will be instantiated.")]
    public Transform listContainer;

    [Header("Prefabs")]
    [Tooltip("The room button prefab to instantiate in the list.")]
    public GameObject roomButtonPrefab;

    [Header("Close Buttons")]
    public Button closeButton;
    public XRButtonSelection closeButtonXR;

    private List<RoomData> roomsList = new List<RoomData>();

    private void Start()
    {
        // Hook close button click if assigned to hide the canvas
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseCanvas);
        }
        if (closeButtonXR != null)
        {
            closeButtonXR.onClick.AddListener(CloseCanvas);
        }

        // Populate and display rooms list on start
        PopulateRoomsList();
    }

    private void OnEnable()
    {
        // Refresh/re-populate when panel is enabled
        PopulateRoomsList();
    }

    public void PopulateRoomsList()
    {
        // Clear old list items
        if (listContainer != null)
        {
            foreach (Transform child in listContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // Get the list of rooms
        roomsList.Clear();
        if (RoomManager.Instance != null && RoomManager.Instance.rooms != null && RoomManager.Instance.rooms.Count > 0)
        {
            roomsList.AddRange(RoomManager.Instance.rooms);
        }
        else
        {
            // Fallback load from resources
            RoomData[] loadedRooms = Resources.LoadAll<RoomData>("MuseumData/Rooms");
            if (loadedRooms != null)
            {
                roomsList.AddRange(loadedRooms);
            }
        }

        // Instantiate a button for each room
        foreach (RoomData room in roomsList)
        {
            if (room == null || roomButtonPrefab == null || listContainer == null) continue;

            GameObject btnObj = Instantiate(roomButtonPrefab, listContainer);
            
            // Set name text
            TextMeshProUGUI tmpText = btnObj.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpText != null)
            {
                tmpText.text = room.roomName;
            }
            else
            {
                Text uiText = btnObj.GetComponentInChildren<Text>(true);
                if (uiText != null)
                {
                    uiText.text = room.roomName;
                }
            }

            // Hook click event
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnRoomSelected(room));
            }

            XRButtonSelection xrBtn = btnObj.GetComponent<XRButtonSelection>();
            if (xrBtn != null)
            {
                xrBtn.onClick.AddListener(() => OnRoomSelected(room));
            }
        }
    }

    private void OnRoomSelected(RoomData room)
    {
        if (roomPanel != null)
        {
            // Show the room panel with details
            roomPanel.ShowRoom(room);
            // Hide this panel (canvas remains active)
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("RoomListPanel: roomPanel reference is missing!");
        }
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
