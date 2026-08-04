using UnityEngine;
using UnityEngine.Video;

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
    public string description = "It is a long established fact that a reader will be distracted by the readable content of a page when looking at its layout...";

    [Header("Media & Visuals")]
    [Tooltip("Main static display image / clip thumbnail.")]
    public Sprite displaySprite;

    [Tooltip("Short 3-5 second video clip played when holding down on the media display box.")]
    public VideoClip videoClip;

    [Header("Audio")]
    [Tooltip("Audio clip for narration or historical audio.")]
    public AudioClip narrationClip;
}
