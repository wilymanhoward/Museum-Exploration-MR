using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.EventSystems;

/// <summary>
/// Constant-length glowing stub drawn from the hand along the ray's aim direction -
/// the visible part of the Quest-style hand ray. XRI's own line visual is made fully
/// transparent (it stretches to whatever the ray hits, so it can't give a constant
/// "few inches" look); this stub replaces it while the interactor, the raycast and the
/// HandRayReticle cursor dot all keep working at full range.
///
/// Added to each ray interactor by TutorialManager.ConfigureHandRayVisuals.
/// </summary>
public class HandRayStub : MonoBehaviour
{
    [Tooltip("Visible length of the ray stub, in meters (~5 inches by default).")]
    public float stubLength = 0.12f;

    [Tooltip("Line width at the hand, in meters.")]
    public float width = 0.005f;

    private XRRayInteractor rayInteractor;
    private LineRenderer stubRenderer;

    private void Awake()
    {
        rayInteractor = GetComponent<XRRayInteractor>();

        GameObject go = new GameObject("HandRayStubLine");
        go.transform.SetParent(transform, false);
        stubRenderer = go.AddComponent<LineRenderer>();
        stubRenderer.useWorldSpace = true;
        stubRenderer.positionCount = 2;
        stubRenderer.startWidth = width;
        stubRenderer.endWidth = width * 0.35f;
        stubRenderer.numCapVertices = 4;
        stubRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        stubRenderer.receiveShadows = false;

        // Reuse the XRI line's material so the stub matches the app's existing ray look.
        LineRenderer xriLine = GetComponent<LineRenderer>();
        stubRenderer.material = xriLine != null && xriLine.sharedMaterial != null
            ? xriLine.sharedMaterial
            : TutorialGestureGizmo.MakeRuntimeMaterial(Color.white, true);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.45f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        stubRenderer.colorGradient = gradient;
    }

    private void LateUpdate()
    {
        if (stubRenderer == null) return;

        Transform origin = rayInteractor != null && rayInteractor.rayOriginTransform != null
            ? rayInteractor.rayOriginTransform
            : transform;

        // If the ray hits something closer than the stub (panel right at the hand),
        // end the stub at the hit so it never pokes through the surface.
        float length = stubLength;
        if (rayInteractor != null)
        {
            if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                length = Mathf.Min(length, hit.distance);
            }
            if (rayInteractor.TryGetCurrentUIRaycastResult(out RaycastResult uiHit))
            {
                length = Mathf.Min(length, uiHit.distance);
            }
        }

        Vector3 start = origin.position;
        stubRenderer.SetPosition(0, start);
        stubRenderer.SetPosition(1, start + origin.forward * Mathf.Max(length, 0.01f));
    }
}
