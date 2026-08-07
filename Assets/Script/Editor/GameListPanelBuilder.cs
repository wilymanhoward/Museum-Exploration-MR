#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor helper that creates a persistent, editable "GameListPanel" in the open scene by
/// cloning the existing "RoomListPanel" (List Ruang). Once built, the panel lives in the
/// hierarchy so its layout can be edited, and GameListMenu will use it at runtime instead of
/// cloning one on the fly.
/// </summary>
public static class GameListPanelBuilder
{
    [MenuItem("Tools/Museum MR/Build Game List Panel In Scene")]
    public static void Build()
    {
        // If one already exists, just select it.
        GameObject existing = FindInScene("GameListPanel");
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            EditorGUIUtility.PingObject(existing);
            EditorUtility.DisplayDialog("Game List Panel",
                "'GameListPanel' already exists in the scene. Selected it in the hierarchy.", "OK");
            return;
        }

        GameObject source = FindInScene("RoomListPanel");
        if (source == null)
        {
            EditorUtility.DisplayDialog("Game List Panel",
                "Could not find 'RoomListPanel' in the open scene to use as a style template.\n\n" +
                "Open the scene that has the List Ruang panel, then try again.", "OK");
            return;
        }

        GameObject panel = Object.Instantiate(source, source.transform.parent);
        panel.name = "GameListPanel";

        // Remove the room populator entirely so this panel never fills with rooms and is never
        // grabbed by WristWatch.ShowRoomListPanel (which would open it on the Explore button).
        // GameListMenu gets its row-button prefab from the original RoomListPanel instead.
        foreach (RoomList rl in panel.GetComponentsInChildren<RoomList>(true))
            Object.DestroyImmediate(rl);

        // Retitle the header to "Senarai Permainan".
        foreach (TMPro.TextMeshProUGUI tmp in panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
        {
            if (tmp.name == "RoomTitleText" || (!string.IsNullOrEmpty(tmp.text) && tmp.text.Contains("Ruang")))
                tmp.text = "Senarai Permainan";
        }

        // Clear any room rows the template carried over so the panel starts empty (GameListMenu
        // populates the game rows at runtime), and switch the container to a single column.
        Transform container = FindChild(panel.transform, "RoomList");
        if (container != null)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(container.GetChild(i).gameObject);

            UnityEngine.UI.GridLayoutGroup grid = container.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (grid != null)
            {
                int oldColumns = grid.constraint == UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0
                    ? grid.constraintCount
                    : 2;
                float fullWidth = grid.cellSize.x * oldColumns + grid.spacing.x * Mathf.Max(0, oldColumns - 1);
                grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 1;
                grid.cellSize = new Vector2(fullWidth, grid.cellSize.y);
            }
        }

        // Lower it a bit relative to the room list so it doesn't sit too high on the wrist.
        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition += new Vector2(0f, -130f);

        panel.SetActive(false);

        Undo.RegisterCreatedObjectUndo(panel, "Build Game List Panel");
        EditorSceneManager.MarkSceneDirty(panel.scene);
        Selection.activeGameObject = panel;
        EditorGUIUtility.PingObject(panel);

        Debug.Log("GameListPanelBuilder: created 'GameListPanel' in the scene. Edit its layout in the hierarchy; " +
                  "GameListMenu will populate the game rows at runtime.");
    }

    private static GameObject FindInScene(string name)
    {
        foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t.name == name && t.gameObject.scene.IsValid())
                return t.gameObject;
        }
        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }
}
#endif
