using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomList : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("Reference to the Room Panel to transition to.")]
    public Room roomPanel;

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
        int roomIndex = 1;
        foreach (RoomData room in roomsList)
        {
            if (room == null || roomButtonPrefab == null || listContainer == null) continue;

            GameObject btnObj = Instantiate(roomButtonPrefab, listContainer);
            btnObj.SetActive(true);

            string formattedNum = roomIndex.ToString("D2");

            // Update Number Text (01, 02, 03, 04, 05) and Room Name Text
            TextMeshProUGUI numTextComp = null;
            TextMeshProUGUI nameTextComp = null;

            TextMeshProUGUI[] tmps = btnObj.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI tmp in tmps)
            {
                string n = tmp.name.ToLower();
                if (n.Contains("num") || n.Contains("number") || n.Contains("index") || n.Contains("no"))
                {
                    numTextComp = tmp;
                }
                else if (n.Contains("name") || n.Contains("title") || n.Contains("text") || n.Contains("room"))
                {
                    if (nameTextComp == null) nameTextComp = tmp;
                }
            }

            if (numTextComp != null)
            {
                numTextComp.text = formattedNum;
            }
            else
            {
                Text[] legacyTexts = btnObj.GetComponentsInChildren<Text>(true);
                foreach (Text txt in legacyTexts)
                {
                    if (txt.name.ToLower().Contains("num") || txt.name.ToLower().Contains("number"))
                    {
                        txt.text = formattedNum;
                        break;
                    }
                }
            }

            if (nameTextComp != null)
            {
                nameTextComp.enableWordWrapping = true;
                nameTextComp.maxVisibleLines = 2;
                nameTextComp.overflowMode = TextOverflowModes.Ellipsis;

                string displayName = room.roomName;
                if (!string.IsNullOrEmpty(displayName))
                {
                    if (displayName.Equals("Serambi Mandalika", System.StringComparison.OrdinalIgnoreCase))
                    {
                        displayName = "Serambi\nMandalika";
                    }
                    else if (displayName.Contains("Serambi") && displayName.Contains("Mandalika"))
                    {
                        displayName = displayName.Replace("Serambi Mandalika", "Serambi\nMandalika");
                    }
                }
                nameTextComp.text = displayName;
            }
            else if (tmps.Length > 0 && numTextComp != tmps[0])
            {
                tmps[0].enableWordWrapping = true;
                tmps[0].maxVisibleLines = 2;
                string displayName = room.roomName;
                if (!string.IsNullOrEmpty(displayName) && displayName.Contains("Serambi Mandalika"))
                {
                    displayName = displayName.Replace("Serambi Mandalika", "Serambi\nMandalika");
                }
                tmps[0].text = displayName;
            }

            // Hook click event
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnRoomSelected(room));
            }

            XRButtonSelection xrBtn = btnObj.GetComponent<XRButtonSelection>();
            if (xrBtn != null)
            {
                xrBtn.onClick.RemoveAllListeners();
                xrBtn.onClick.AddListener(() => OnRoomSelected(room));
            }

            roomIndex++;
        }
    }

    private void OnRoomSelected(RoomData room)
    {
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.ChangeRoom(room);
        }

        if (roomPanel != null)
        {
            // Show the room panel with details
            roomPanel.ShowRoom(room);
            // Hide this panel (canvas remains active)
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("RoomList: roomPanel reference is missing!");
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

        if (WristWatch.Instance != null)
        {
            WristWatch.Instance.EnsureWatchButtonVisible();
        }
        else
        {
            WristWatch ww = FindObjectOfType<WristWatch>();
            if (ww != null) ww.EnsureWatchButtonVisible();
        }
    }
}
