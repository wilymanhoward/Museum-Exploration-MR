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

        EnsureTitleFitsAndClearsCornerButtons();
    }

    /// <summary>
    /// The title's box stretches almost the full width of the panel with nothing reserved
    /// for the back/close icon buttons in the top-right corner - a longer room name (e.g.
    /// "Serambi Mandalika") renders right underneath them. Pulls the title's right edge in
    /// to clear those buttons, and adds shrink-to-fit safety so future long room names wrap/
    /// shrink instead of overflowing. Runs ONCE (not per room shown): UpdateRoomDetails only
    /// changes the text content, not layout, and re-applying an offsetMax inset on every
    /// room switch would keep shrinking the box further each time.
    /// </summary>
    private void EnsureTitleFitsAndClearsCornerButtons()
    {
        if (roomTitleText == null) return;

        RectTransform titleRect = roomTitleText.rectTransform;
        titleRect.offsetMax -= new Vector2(65f, 0f);

        float authoredSize = roomTitleText.enableAutoSizing ? roomTitleText.fontSizeMax : roomTitleText.fontSize;
        if (authoredSize <= 0f) authoredSize = 24f;
        roomTitleText.enableAutoSizing = true;
        roomTitleText.fontSizeMax = authoredSize;
        roomTitleText.fontSizeMin = authoredSize * 0.55f;
        roomTitleText.enableWordWrapping = true;
        // Overflow, not Ellipsis: combined with auto-sizing, Ellipsis can fail to find a
        // size that fits and render garbled/overlapping text instead of shrinking cleanly
        // (confirmed on the History panel's and Tutorial panel's equivalent fields).
        roomTitleText.overflowMode = TextOverflowModes.Overflow;
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
            artifactCountText.text = "Jumlah Artifak: " + count;
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
        if (currentRoomData.artifacts != null && currentRoomData.artifacts.Count > 0 && artifactListContainer != null)
        {
            int index = 1;
            foreach (ArtifactData artifact in currentRoomData.artifacts)
            {
                if (artifact == null) continue;

                GameObject itemObj = null;
                if (artifactItemPrefab != null)
                {
                    itemObj = Instantiate(artifactItemPrefab, artifactListContainer);
                }
                else
                {
                    itemObj = new GameObject($"ArtifactItem_{index}");
                    itemObj.transform.SetParent(artifactListContainer, false);
                }

                itemObj.SetActive(true);
                itemObj.transform.localScale = Vector3.one;

                string formattedText = $"{index:D2} {artifact.artifactName}";

                // 1. Update ALL TMP text components, eliminating static template text like "MONA LISA"
                TextMeshProUGUI[] tmps = itemObj.GetComponentsInChildren<TextMeshProUGUI>(true);
                bool titleUpdated = false;
                foreach (TextMeshProUGUI tmp in tmps)
                {
                    if (tmp == null) continue;
                    string nameLower = tmp.name.ToLower();
                    string textLower = (tmp.text ?? "").ToLower();

                    if (!titleUpdated && (nameLower.Contains("name") || nameLower.Contains("title") || nameLower.Contains("text") || textLower.Contains("mona") || textLower.Contains("lisa")))
                    {
                        tmp.text = formattedText;
                        tmp.gameObject.SetActive(true);
                        titleUpdated = true;
                    }
                    else if (textLower.Contains("mona") || textLower.Contains("lisa"))
                    {
                        // Hide static template text object
                        tmp.gameObject.SetActive(false);
                    }
                }

                if (!titleUpdated && tmps.Length > 0)
                {
                    tmps[0].text = formattedText;
                    tmps[0].gameObject.SetActive(true);
                }
                else if (tmps.Length == 0)
                {
                    // Fallback to legacy UI Text
                    Text[] legacyTexts = itemObj.GetComponentsInChildren<Text>(true);
                    foreach (Text txt in legacyTexts)
                    {
                        if (txt.text != null && (txt.text.ToLower().Contains("mona") || txt.name.ToLower().Contains("text")))
                        {
                            txt.text = formattedText;
                            txt.gameObject.SetActive(true);
                        }
                    }
                }

                // 2. Set thumbnail image
                Sprite artSprite = (artifact.images != null && artifact.images.Length > 0) ? artifact.images[0].sprite : null;
                if (artSprite == null && !string.IsNullOrEmpty(artifact.artifactId))
                {
                    foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
                    {
                        if (s != null && s.name.ToLower().Contains(artifact.artifactId.ToLower()))
                        {
                            artSprite = s;
                            break;
                        }
                    }
                }

                Image[] images = itemObj.GetComponentsInChildren<Image>(true);
                foreach (Image img in images)
                {
                    if (img != null && img.gameObject != itemObj && img.gameObject.name.ToLower() != "background")
                    {
                        if (artSprite != null)
                        {
                            img.sprite = artSprite;
                            img.color = Color.white;
                            img.gameObject.SetActive(true);
                        }
                        break;
                    }
                }

                // 3. Hook click event
                ArtifactData currentArtifact = artifact;
                Button btn = itemObj.GetComponent<Button>();
                if (btn == null) btn = itemObj.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnArtifactSelected(currentArtifact));
                }

                XRButtonSelection xrBtn = itemObj.GetComponent<XRButtonSelection>();
                if (xrBtn == null) xrBtn = itemObj.GetComponentInChildren<XRButtonSelection>();
                if (xrBtn != null)
                {
                    xrBtn.onClick.RemoveAllListeners();
                    xrBtn.onClick.AddListener(() => OnArtifactSelected(currentArtifact));
                }

                index++;
            }
        }
    }

    private void OnArtifactSelected(ArtifactData artifact)
    {
        if (artifact == null) return;

        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.SpawnArtifactDetailPanel(artifact);
            if (WristWatch.Instance != null)
            {
                WristWatch.Instance.EnsureWatchButtonVisible();
            }
        }
        else if (artifactDetailPanel != null)
        {
            artifactDetailPanel.ShowArtifact(artifact, this);
            if (WristWatch.Instance != null)
            {
                WristWatch.Instance.EnsureWatchButtonVisible();
            }
        }
        else
        {
            Debug.LogError("Room: No ArtifactManager or artifactDetailPanel available to show artifact details!");
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
