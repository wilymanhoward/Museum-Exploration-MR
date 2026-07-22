using UnityEngine;

/// <summary>
/// Rotates its transform continuously around the world Y axis. Shared across mini-games
/// for slowly spinning a displayed 3D model.
/// </summary>
public class Spinner : MonoBehaviour
{
    public float spinSpeed = 20f;

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }
}
