using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Floating "press to reveal" button for the drag/rotate tutorial step. The player points
/// the hand ray at it and pinches, same as any real button in the app (XRButtonSelection) -
/// this is the exact gesture used to press the actual "3D View" button on an artifact panel.
///
/// Billboards toward the player every frame (yaw only) so its label always stays readable.
/// Built entirely at runtime via Create() - no prefab needed.
/// </summary>
public class PinchButtonPracticeTarget : MonoBehaviour
{
    public event Action Pressed;

    private Camera cachedCamera;

    /// <summary>Creates a ready-to-use practice button at the given world position, facing the player.</summary>
    public static PinchButtonPracticeTarget Create(Vector3 position, string label)
    {
        GameObject root = new GameObject("TutorialViewButton");
        root.transform.position = position;
        PinchButtonPracticeTarget target = root.AddComponent<PinchButtonPracticeTarget>();
        target.Build(label);
        return target;
    }

    private void Build(string label)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "ButtonBody";
        body.transform.SetParent(transform, false);
        body.transform.localScale = new Vector3(0.22f, 0.09f, 0.02f);
        Destroy(body.GetComponent<Collider>()); // one generous collider on the root instead
        body.GetComponent<Renderer>().material =
            TutorialGestureGizmo.MakeRuntimeMaterial(new Color(0.25f, 0.55f, 0.95f, 1f), false);

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0f, -0.0115f);
        TextMeshPro tmp = labelGo.AddComponent<TextMeshPro>();
        tmp.text = label;
        tmp.fontSize = 6f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 3f;
        tmp.fontSizeMax = 6f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.rectTransform.sizeDelta = new Vector2(0.2f, 0.08f);

        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.24f, 0.11f, 0.05f);

        XRButtonSelection xr = gameObject.AddComponent<XRButtonSelection>();
        xr.onClick.AddListener(() => Pressed?.Invoke());
    }

    private void Update()
    {
        // Billboard toward the player (yaw only), same convention as TutorialGestureGizmo.
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        }
        if (cachedCamera == null) return;

        Vector3 toCam = cachedCamera.transform.position - transform.position;
        toCam.y = 0f;
        if (toCam.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
        }
    }
}
