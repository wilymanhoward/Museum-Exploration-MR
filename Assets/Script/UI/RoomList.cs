using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomList : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("Reference to the Room Panel to transition to for standard rooms.")]
    public Room roomPanel;

    [Tooltip("Reference to the History List Panel to transition to when selecting Ruang Sejarah.")]
    public HistoryListPanel historyListPanel;

    [Header("Special Sejarah Room Reference")]
    [Tooltip("Drag your Galeri Sejarah button directly here.")]
    public Button sejarahRoomButton;
    public XRButtonSelection sejarahRoomButtonXR;

    [Header("Canvas Reference")]
    [Tooltip("The canvas GameObject to hide. If null, will automatically find the parent Canvas's GameObject.")]
    public GameObject canvasObject;

    [Header("Hierarchy References")]
    [Tooltip("The Layout Group (Grid Layout Group) transform where room buttons are located or instantiated.")]
    public Transform listContainer;

    [Header("Prefabs")]
    [Tooltip("The room button prefab to instantiate in the list. Leave empty if buttons are manually placed in scene.")]
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

        WireSejarahButton();
        PopulateRoomsList();
    }

    private void OnEnable()
    {
        WireSejarahButton();
        PopulateRoomsList();
    }

    private void WireSejarahButton()
    {
        if (sejarahRoomButton != null)
        {
            sejarahRoomButton.onClick.RemoveAllListeners();
            sejarahRoomButton.onClick.AddListener(() => OpenHistoryFromRoomList("Ruang Sejarah", "Sejarah Terengganu"));
        }
        if (sejarahRoomButtonXR != null)
        {
            sejarahRoomButtonXR.onClick.RemoveAllListeners();
            sejarahRoomButtonXR.onClick.AddListener(() => OpenHistoryFromRoomList("Ruang Sejarah", "Sejarah Terengganu"));
        }
    }

    public void PopulateRoomsList()
    {
        // Fetch room data list
        roomsList.Clear();
        if (RoomManager.Instance != null && RoomManager.Instance.rooms != null && RoomManager.Instance.rooms.Count > 0)
        {
            roomsList.AddRange(RoomManager.Instance.rooms);
        }
        else
        {
            RoomData[] loadedRooms = Resources.LoadAll<RoomData>("MuseumData/Rooms");
            if (loadedRooms != null)
            {
                roomsList.AddRange(loadedRooms);
            }
        }

        // If listContainer already has manually placed buttons in scene (e.g. Button 05 for Sejarah), wire them directly
        if (listContainer != null && listContainer.childCount > 0 && roomButtonPrefab == null)
        {
            WireExistingChildButtons();
            return;
        }

        // Otherwise auto-instantiate if prefab is provided
        if (listContainer != null && roomButtonPrefab != null)
        {
            foreach (Transform child in listContainer)
            {
                Destroy(child.gameObject);
            }

            int roomIndex = 1;
            foreach (RoomData room in roomsList)
            {
                if (room == null) continue;

                GameObject btnObj = Instantiate(roomButtonPrefab, listContainer);
                btnObj.SetActive(true);

                string formattedNum = roomIndex.ToString("D2");

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

                if (numTextComp != null) numTextComp.text = formattedNum;
                if (nameTextComp != null) nameTextComp.text = room.roomName;

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

            // The instantiate loop above already wired every new button. The fallback pass
            // below must NOT also run: the old children destroyed above still exist until
            // end of frame, so the new buttons would sit at child indices beyond roomsList
            // and get their listeners stripped (RemoveAllListeners) with nothing added
            // back - leaving every room button dead.
            return;
        }

        // Scene-authored buttons only (no prefab): wire whatever is in the hierarchy.
        WireExistingChildButtons();
    }

    private void WireExistingChildButtons()
    {
        if (listContainer == null || listContainer.childCount == 0) return;

        int index = 1;
        for (int i = 0; i < listContainer.childCount; i++)
        {
            Transform child = listContainer.GetChild(i);
            if (child == null || !child.gameObject.activeSelf) continue;

            // Skip if this child is explicitly assigned as sejarahRoomButton
            if (sejarahRoomButton != null && child.gameObject == sejarahRoomButton.gameObject)
            {
                index++;
                continue;
            }

            TMP_Text nameTxt = child.Find("RoomName")?.GetComponent<TMP_Text>();
            TMP_Text numTxt = child.Find("Room Number")?.GetComponent<TMP_Text>();

            if (nameTxt == null) nameTxt = child.GetComponentInChildren<TMP_Text>();

            string roomName = nameTxt != null ? nameTxt.text : child.name;
            string roomNum = numTxt != null ? numTxt.text : index.ToString("D2");

            bool isSejarah = index == 5 || roomNum == "05" ||
                             (!string.IsNullOrEmpty(roomName) &&
                              (roomName.Contains("Sejarah") || roomName.Contains("History")));

            Button btn = child.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                if (isSejarah)
                {
                    btn.onClick.AddListener(() => OpenHistoryFromRoomList("Ruang Sejarah", "Sejarah Terengganu"));
                }
                else
                {
                    RoomData matchedData = (roomsList != null && index - 1 < roomsList.Count) ? roomsList[index - 1] : null;
                    if (matchedData != null)
                    {
                        btn.onClick.AddListener(() => OnRoomSelected(matchedData));
                    }
                }
            }

            XRButtonSelection xrBtn = child.GetComponent<XRButtonSelection>();
            if (xrBtn != null)
            {
                xrBtn.onClick.RemoveAllListeners();
                if (isSejarah)
                {
                    xrBtn.onClick.AddListener(() => OpenHistoryFromRoomList("Ruang Sejarah", "Sejarah Terengganu"));
                }
                else
                {
                    RoomData matchedData = (roomsList != null && index - 1 < roomsList.Count) ? roomsList[index - 1] : null;
                    if (matchedData != null)
                    {
                        xrBtn.onClick.AddListener(() => OnRoomSelected(matchedData));
                    }
                }
            }

            index++;
        }
    }

    private void OpenHistoryFromRoomList(string title, string subtitle)
    {
        if (HistoryManager.Instance != null)
        {
            HistoryManager.Instance.ShowHistoryList(title, subtitle);
        }
        else
        {
            if (historyListPanel == null) historyListPanel = FindObjectOfType<HistoryListPanel>(true);
            if (historyListPanel != null)
            {
                historyListPanel.ShowList(null, title, subtitle);
            }
        }

        gameObject.SetActive(false);
    }

    private void OnRoomSelected(RoomData room)
    {
        if (room == null) return;

        // If selecting Ruang Sejarah / History room -> Route directly to HistoryListPanel
        bool isHistoryRoom = !string.IsNullOrEmpty(room.roomName) &&
            (room.roomName.IndexOf("Sejarah", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             room.roomName.IndexOf("History", System.StringComparison.OrdinalIgnoreCase) >= 0);

        if (isHistoryRoom)
        {
            OpenHistoryFromRoomList(room.roomName, string.IsNullOrEmpty(room.roomSubtitle) ? "Sejarah Terengganu" : room.roomSubtitle);
            return;
        }

        // Standard Room handling
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.ChangeRoom(room);
        }

        if (roomPanel != null)
        {
            roomPanel.ShowRoom(room);
            gameObject.SetActive(false);
        }
    }

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
