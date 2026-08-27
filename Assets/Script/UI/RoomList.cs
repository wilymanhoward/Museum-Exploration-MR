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
        ApplySejarahButtonLabel();
    }

    /// <summary>
    /// Sets the Sejarah button's number/name text in code, rather than relying on it being
    /// hand-authored in the scene - it wasn't (the button was still silently showing a
    /// leftover "Galeri Tekstil" / "01" placeholder from whatever it was cloned from).
    /// </summary>
    private void ApplySejarahButtonLabel()
    {
        if (sejarahRoomButton == null) return;
        GameObject sejarahObj = sejarahRoomButton.gameObject;

        TextMeshProUGUI numTextComp = null;
        TextMeshProUGUI nameTextComp = null;
        foreach (TextMeshProUGUI tmp in sejarahObj.GetComponentsInChildren<TextMeshProUGUI>(true))
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
        if (numTextComp != null) numTextComp.text = "05";
        if (nameTextComp != null) nameTextComp.text = "Sejarah Terengganu";
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
            WireSejarahButton();
            return;
        }

        // Otherwise auto-instantiate if prefab is provided
        if (listContainer != null && roomButtonPrefab != null)
        {
            for (int i = listContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(listContainer.GetChild(i).gameObject);
            }

            int roomIndex = 1;
            foreach (RoomData room in roomsList)
            {
                if (room == null) continue;

                GameObject btnObj = Instantiate(roomButtonPrefab, listContainer);
                btnObj.name = $"Button_{roomIndex:D2}_{room.roomName}";
                btnObj.SetActive(true);

                string formattedNum = roomIndex.ToString("D2");
                SetButtonTextValues(btnObj, formattedNum, room.roomName);

                RoomData capturedRoom = room;
                Button btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnRoomSelected(capturedRoom));
                }

                XRButtonSelection xrBtn = btnObj.GetComponent<XRButtonSelection>();
                if (xrBtn != null)
                {
                    xrBtn.onClick.RemoveAllListeners();
                    xrBtn.onClick.AddListener(() => OnRoomSelected(capturedRoom));
                }

                roomIndex++;
            }

            // ALWAYS CREATE BUTTON 05: SEJARAH TERENGGANU
            GameObject sejarahBtn = Instantiate(roomButtonPrefab, listContainer);
            sejarahBtn.name = "Button_05_Sejarah_Terengganu";
            sejarahBtn.SetActive(true);
            SetButtonTextValues(sejarahBtn, "05", "Sejarah Terengganu");

            Button sBtn = sejarahBtn.GetComponent<Button>();
            if (sBtn != null)
            {
                sBtn.onClick.RemoveAllListeners();
                sBtn.onClick.AddListener(() => OpenHistoryFromRoomList("Ruang Sejarah", "Sejarah Terengganu"));
            }

            XRButtonSelection sXr = sejarahBtn.GetComponent<XRButtonSelection>();
            if (sXr != null)
            {
                sXr.onClick.RemoveAllListeners();
                sXr.onClick.AddListener(() => OpenHistoryFromRoomList("Ruang Sejarah", "Sejarah Terengganu"));
            }

            return;
        }

        // Scene-authored buttons only (no prefab): wire whatever is in the hierarchy.
        WireExistingChildButtons();
        WireSejarahButton();
    }

    private void SetButtonTextValues(GameObject btnObj, string numStr, string nameStr)
    {
        if (btnObj == null) return;
        TextMeshProUGUI numTextComp = null;
        TextMeshProUGUI nameTextComp = null;

        foreach (TextMeshProUGUI tmp in btnObj.GetComponentsInChildren<TextMeshProUGUI>(true))
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

        if (numTextComp != null) numTextComp.text = numStr;
        if (nameTextComp != null) nameTextComp.text = nameStr;
    }

    private void WireExistingChildButtons()
    {
        if (listContainer == null || listContainer.childCount == 0) return;

        bool hasSejarahButton = false;
        int index = 1;
        for (int i = 0; i < listContainer.childCount; i++)
        {
            Transform child = listContainer.GetChild(i);
            if (child == null || !child.gameObject.activeSelf) continue;

            TMP_Text nameTxt = child.Find("RoomName")?.GetComponent<TMP_Text>();
            TMP_Text numTxt = child.Find("Room Number")?.GetComponent<TMP_Text>();

            if (nameTxt == null) nameTxt = child.GetComponentInChildren<TMP_Text>();

            string roomName = nameTxt != null ? nameTxt.text : child.name;
            string roomNum = numTxt != null ? numTxt.text : index.ToString("D2");

            bool isSejarah = index >= 5 || roomNum == "05" ||
                             (!string.IsNullOrEmpty(roomName) &&
                              (roomName.Contains("Sejarah") || roomName.Contains("History")));

            if (isSejarah) hasSejarahButton = true;

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

        // If only 4 buttons exist in scene, dynamically duplicate button 0 to create Button 05 for Sejarah!
        if (!hasSejarahButton && listContainer.childCount > 0)
        {
            Transform template = listContainer.GetChild(listContainer.childCount - 1);
            if (template != null)
            {
                GameObject sejarahObj = Instantiate(template.gameObject, listContainer);
                sejarahObj.name = "Button 05 Sejarah";
                sejarahObj.SetActive(true);
                SetButtonTextValues(sejarahObj, "05", "Sejarah Terengganu");

                Button btn = sejarahObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OpenHistoryFromRoomList("Ruang Sejarah", "Sejarah Terengganu"));
                }

                XRButtonSelection xrBtn = sejarahObj.GetComponent<XRButtonSelection>();
                if (xrBtn != null)
                {
                    xrBtn.onClick.RemoveAllListeners();
                    xrBtn.onClick.AddListener(() => OpenHistoryFromRoomList("Ruang Sejarah", "Sejarah Terengganu"));
                }
            }
        }
    }

    private void OpenHistoryFromRoomList(string title, string subtitle)
    {
        // 1. Hide RoomListPanel completely
        gameObject.SetActive(false);
        if (transform.parent != null && transform.parent.name == "RoomListPanel")
        {
            transform.parent.gameObject.SetActive(false);
        }

        // 2. Hide standard RoomPanel if active
        if (roomPanel != null)
        {
            roomPanel.gameObject.SetActive(false);
        }

        // 3. Show HistoryListPanel with full 7 items
        if (historyListPanel == null)
        {
            historyListPanel = FindObjectOfType<HistoryListPanel>(true);
        }

        if (historyListPanel != null)
        {
            historyListPanel.gameObject.SetActive(true);
            historyListPanel.ShowList(null, title, subtitle);
        }
        else if (HistoryManager.Instance != null)
        {
            HistoryManager.Instance.ShowHistoryList(title, subtitle);
        }
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
