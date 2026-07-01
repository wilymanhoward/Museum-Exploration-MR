using UnityEngine;

[CreateAssetMenu(fileName = "NewArtifactData", menuName = "Museum MR/Artifact Data")]
public class ArtifactData : ScriptableObject
{
    [Header("Identifications")]
    [Tooltip("Unique ID matching the QR code payload for this artifact.")]
    public string artifactId;

    [Header("Exhibit Details")]
    public string artifactName;
    public string artist;
    public string year;

    [TextArea(5, 12)]
    public string description;

    [Header("3D Representation")]
    [Tooltip("Prefab containing the 3D model of the artifact to spawn near the player when scanned.")]
    public GameObject modelPrefab;
}
