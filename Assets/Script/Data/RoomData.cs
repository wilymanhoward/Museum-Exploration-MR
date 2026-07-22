using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewRoomData", menuName = "Museum MR/Room Data")]
public class RoomData : ScriptableObject
{
    [Header("Identifications")]
    [Tooltip("Unique ID matching the QR code payload placed at the entrance of this room.")]
    public string roomId;

    [Header("Details")]
    public string roomName;
    public string roomSubtitle;

    [Header("Exhibit Content")]
    [Tooltip("List of artifacts located in this specific room.")]
    public List<ArtifactData> artifacts = new List<ArtifactData>();

    [Header("Wayfinding Navigation")]
    [Tooltip("List of spatial positions (waypoints) leading to this room's entrance from a central point.")]
    public Vector3[] waypoints;
}
