using UnityEngine;
using UnityEngine.Video;

[System.Serializable]
public struct HistoryImage
{
    public Sprite sprite;
    [Tooltip("Optional short caption shown under the photo (e.g. a date or place). Leave empty for none.")]
    public string caption;
}

[CreateAssetMenu(fileName = "NewHistoryData", menuName = "Museum MR/History Data")]
public class HistoryData : ScriptableObject
{
    [Header("Identifications")]
    [Tooltip("Unique ID for this historical topic/item.")]
    public string historyId;

    [Header("Header Info")]
    [Tooltip("Main title (e.g. 'Sejarah' or 'Artefak').")]
    public string topTitle = "Sejarah";

    [Tooltip("Category or artifact type subtitle (e.g. 'Gamelan', 'Batu Bersurat').")]
    public string category = "Gamelan";

    [Tooltip("Specific historical event or story title displayed under the media clip (e.g. 'Pertarungan Megat Panji Alam').")]
    public string eventTitle = "Pertarungan Megat Panji Alam";

    [Header("Detail Sejarah")]
    [Tooltip("Time period / Year (e.g. '1879').")]
    public string timePeriod = "1879";

    [Tooltip("Location / Origin (e.g. 'Trengganu').")]
    public string location = "Trengganu";

    [Header("Description")]
    [TextArea(6, 14)]
    public string description = "Keterangan sejarah akan dipaparkan di sini.";

    [Header("Media & Visuals")]
    [Tooltip("Photo gallery for this topic. Can hold any number of photos, including zero - " +
             "topics with no photo simply show the 'no image' placeholder in HistoryPanel " +
             "and the gallery arrows stay hidden. Preferred over 'Legacy Display Sprite' below " +
             "when both are set.")]
    public HistoryImage[] images;

    [Tooltip("Legacy single-image field kept for backward compatibility with older HistoryData " +
             "assets. Only used as a fallback when 'images' above is empty - new topics should " +
             "use 'images' instead, even for a single photo.")]
    public Sprite displaySprite;

    [Tooltip("Short 3-5 second video clip played when holding down on the media display box.")]
    public VideoClip videoClip;

    [Header("Audio")]
    [Tooltip("Audio clip for narration or historical audio.")]
    public AudioClip narrationClip;
}
