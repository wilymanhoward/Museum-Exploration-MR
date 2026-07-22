using UnityEngine;

[System.Serializable]
public struct ArtifactImage
{
    public Sprite sprite;
    public string title;
}

[CreateAssetMenu(fileName = "NewArtifactData", menuName = "Museum MR/Artifact Data")]
public class ArtifactData : ScriptableObject
{
    [Header("Identifications")]
    [Tooltip("Unique ID matching the QR code payload for this artifact.")]
    public string artifactId;

    [Header("Exhibit Details")]
    public string artifactName;

    // Kept for editor script backward compatibility
    public string artist;
    public string year;

    [Header("Artifact Details")]
    public string timePeriod;
    public string location;
    public float height;
    public float width;
    public float length;
    public string material;

    [TextArea(5, 12)]
    public string description;

    [Header("3D Representation")]
    [Tooltip("Prefab containing the 3D model of the artifact to spawn near the player when scanned.")]
    public GameObject modelPrefab;

    [Header("Visuals")]
    public ArtifactImage[] images;
}
