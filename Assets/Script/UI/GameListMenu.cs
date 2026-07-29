using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds and shows the "List Game" chooser panel. It clones the existing RoomListPanel
/// (the "List Ruang" panel) so it matches its style exactly, retitles it "List Game", and
/// fills it with the mini-games. Because the clone is parented next to RoomListPanel under the
/// ExplorationCanvas, it rides the wrist via WristWatch just like the room list.
/// Selecting a game starts it through <see cref="Minigames"/>.
/// </summary>
public class GameListMenu : MonoBehaviour
{
    public static GameListMenu Instance { get; private set; }

    [System.Serializable]
    public class GameEntry
    {
        public string gameId;
        public string displayName;
    }

    [Header("Games shown in the list")]
    public List<GameEntry> games = new List<GameEntry>
    {
        new GameEntry { gameId = "game_1", displayName = "Kuis Artefak" },
        new GameEntry { gameId = "game_2", displayName = "Urutan Process Pembuatan Batik" },
        new GameEntry { gameId = "game_3", displayName = "Tebak Bayangan Artefak" },
    };

    [Header("References (auto-found if empty)")]
    [Tooltip("The canvas that rides the wrist and hosts the list panels (ExplorationCanvas).")]
    public GameObject explorationCanvas;
    [Tooltip("The RoomListPanel (List Ruang) used as the style template if no GameListPanel is built.")]
    public GameObject roomListPanelSource;
    [Tooltip("The GameListPanel in the scene. Build one via Tools > Museum MR > Build Game List Panel so its layout is editable in the hierarchy. If empty, one is cloned at runtime.")]
    public GameObject gameListPanel;

    [Header("Placement")]
    [Tooltip("Only applied to the auto-cloned fallback panel (not a scene-built one): shifts it in the canvas so it doesn't sit too high on the wrist. Negative Y = lower.")]
    public Vector2 fallbackAnchoredOffset = new Vector2(0f, -130f);

    private bool built;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    /// <summary>Shows the List Game panel (building it once), hiding sibling panels.</summary>
    public void Show()
    {
        EnsureBuilt();
        if (explorationCanvas != null) explorationCanvas.SetActive(true);
        if (gameListPanel == null)
        {
            Debug.LogWarning("GameListMenu: could not build the List Game panel (RoomListPanel template not found).");
            return;
        }

        gameListPanel.SetActive(true);

        // Only the game list should be visible among the canvas panels.
        Transform parent = gameListPanel.transform.parent;
        if (parent != null)
        {
            foreach (Transform sibling in parent)
            {
                if (sibling.gameObject != gameListPanel)
                    sibling.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>Hides just the game list panel (used when another panel takes over).</summary>
    public void HidePanel()
    {
        if (gameListPanel != null) gameListPanel.SetActive(false);
    }

    /// <summary>Closes the game list and its canvas (wired to the panel's Close button).</summary>
    public void Close()
    {
        if (gameListPanel != null) gameListPanel.SetActive(false);
        if (explorationCanvas != null) explorationCanvas.SetActive(false);
    }

    private void EnsureBuilt()
    {
        if (built && gameListPanel != null) return;

        ResolveReferences();

        // Prefer a GameListPanel that already exists in the scene (its layout is editable in the
        // hierarchy). Only clone one at runtime as a fallback if none has been built.
        if (gameListPanel == null)
            gameListPanel = FindInactiveObject("GameListPanel");

        bool cloned = false;
        if (gameListPanel == null)
        {
            if (roomListPanelSource == null) return;
            gameListPanel = Instantiate(roomListPanelSource, roomListPanelSource.transform.parent);
            gameListPanel.name = "GameListPanel";
            cloned = true;
        }

        gameListPanel.SetActive(false);
        ConfigurePanel(gameListPanel);

        // Nudge only the auto-cloned fallback lower; a scene-built panel is positioned by the user.
        if (cloned)
        {
            RectTransform rt = gameListPanel.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition += fallbackAnchoredOffset;
        }

        built = true;
    }

    /// <summary>
    /// Turns a clone/scene copy of the room-list panel into the game list: strips the room
    /// populator, retitles it, forces a single-column layout, fills in the game rows, and wires
    /// the Close button.
    /// </summary>
    private void ConfigurePanel(GameObject panel)
    {
        // Grab the row-button prefab before removing any RoomList components.
        GameObject buttonPrefab = GetRoomButtonPrefab(panel);

        // Remove every RoomList populator on the game panel. If one is left, it can fill the panel
        // with rooms and, worse, WristWatch.ShowRoomListPanel can grab it and open THIS panel when
        // the player taps Explore. The row container GameObject (named "RoomList") is untouched.
        foreach (RoomList rl in panel.GetComponentsInChildren<RoomList>(true))
            Destroy(rl);

        Transform listContainer = FindDeepChild(panel.transform, "RoomList");
        Transform closeT = FindDeepChild(panel.transform, "CloseButton");

        MakeSingleColumn(listContainer);
        SetTitle(panel, "List Game");
        Populate(listContainer, buttonPrefab);
        WireButton(closeT != null ? closeT.gameObject : null, Close);
    }

    private GameObject GetRoomButtonPrefab(GameObject panel)
    {
        RoomList rl = panel.GetComponentInChildren<RoomList>(true);
        if (rl != null && rl.roomButtonPrefab != null) return rl.roomButtonPrefab;

        if (roomListPanelSource != null)
        {
            RoomList src = roomListPanelSource.GetComponentInChildren<RoomList>(true);
            if (src != null && src.roomButtonPrefab != null) return src.roomButtonPrefab;
        }

        RoomList any = FindObjectOfType<RoomList>(true);
        return any != null ? any.roomButtonPrefab : null;
    }

    private void Populate(Transform container, GameObject buttonPrefab)
    {
        if (container == null || buttonPrefab == null)
        {
            Debug.LogWarning("GameListMenu: missing list container or button prefab; cannot build game rows.");
            return;
        }

        // Clear anything the template carried over.
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }

        int index = 1;
        foreach (GameEntry game in games)
        {
            if (game == null) continue;
            GameEntry captured = game;

            GameObject btnObj = Instantiate(buttonPrefab, container);
            btnObj.SetActive(true);

            string number = index.ToString("D2");

            // Same field-matching scheme RoomList uses (number field vs name field).
            TextMeshProUGUI numText = null;
            TextMeshProUGUI nameText = null;
            foreach (TextMeshProUGUI tmp in btnObj.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string n = tmp.name.ToLower();
                if (n.Contains("num") || n.Contains("number") || n.Contains("index") || n.Contains("no"))
                    numText = tmp;
                else if (nameText == null && (n.Contains("name") || n.Contains("title") || n.Contains("text") || n.Contains("room")))
                    nameText = tmp;
            }

            if (numText != null) numText.text = number;
            if (nameText != null) nameText.text = captured.displayName;
            else
            {
                TextMeshProUGUI[] all = btnObj.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (all.Length > 0 && all[0] != numText) all[0].text = captured.displayName;
            }

            WireButton(btnObj, () => OnGameSelected(captured.gameId));
            index++;
        }
    }

    /// <summary>
    /// Switches a GridLayoutGroup container to a single column (rows stacked vertically),
    /// widening the cell to span the width the previous columns occupied.
    /// </summary>
    private static void MakeSingleColumn(Transform container)
    {
        if (container == null) return;

        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        if (grid == null) return;

        int oldColumns = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0
            ? grid.constraintCount
            : 2;

        float fullWidth = grid.cellSize.x * oldColumns + grid.spacing.x * Mathf.Max(0, oldColumns - 1);

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
        grid.cellSize = new Vector2(fullWidth, grid.cellSize.y);
    }

    private void OnGameSelected(string gameId)
    {
        Debug.Log($"GameListMenu: selected game '{gameId}'.");

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Pose pose = new Pose(Vector3.zero, Quaternion.identity);
        if (cam != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (forward == Vector3.zero) forward = Vector3.forward;
            pose.position = cam.position + forward * 0.6f;
            pose.position.y = cam.position.y;
            pose.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        if (MiniGames.Instance != null)
        {
            MiniGames.Instance.StartGame(gameId, pose);
        }
        else
        {
            Debug.LogWarning("GameListMenu: MiniGames.Instance is null; cannot start the game.");
        }

        Close();
    }

    private void ResolveReferences()
    {
        if (explorationCanvas == null)
            explorationCanvas = FindInactiveObject("ExplorationCanvas") ?? FindInactiveObject("RoomHUDCanvas");

        if (roomListPanelSource == null)
        {
            Transform t = explorationCanvas != null ? FindDeepChild(explorationCanvas.transform, "RoomListPanel") : null;
            if (t == null)
            {
                GameObject go = FindInactiveObject("RoomListPanel");
                if (go != null) t = go.transform;
            }
            if (t != null) roomListPanelSource = t.gameObject;
        }
    }

    private static void SetTitle(GameObject panel, string title)
    {
        Transform t = FindDeepChild(panel.transform, "RoomTitleText");
        if (t != null)
        {
            TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null) { tmp.text = title; return; }
        }
        // Fallback: the header title still shows the room-list title.
        foreach (TextMeshProUGUI tmp in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (!string.IsNullOrEmpty(tmp.text) && tmp.text.Contains("Ruang"))
            {
                tmp.text = title;
                return;
            }
        }
    }

    private static void WireButton(GameObject buttonObj, UnityEngine.Events.UnityAction handler)
    {
        if (buttonObj == null) return;

        Button btn = buttonObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(handler);
        }
        XRButtonSelection xr = buttonObj.GetComponent<XRButtonSelection>();
        if (xr != null)
        {
            xr.onClick.RemoveAllListeners();
            xr.onClick.AddListener(handler);
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }
        return null;
    }

    private static GameObject FindInactiveObject(string name)
    {
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == name && t.gameObject.scene.IsValid())
                return t.gameObject;
        }
        return null;
    }
}
