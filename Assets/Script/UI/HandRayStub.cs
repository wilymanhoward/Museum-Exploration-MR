using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Constant-length glowing stub drawn from the hand along the ray's aim direction -
/// the visible part of the Quest-style hand ray. XRI's own line visual is made fully
/// transparent (it stretches to whatever the ray hits, so it can't give a constant
/// "few inches" look); this stub replaces it while the interactor, the raycast and the
/// HandRayReticle cursor dot all keep working at full range.
///
/// The stub is ALWAYS the same fixed length and always visible while its host object
/// is active: no hit-based logic that could collapse or hide it. The renderer is built
/// lazily (Awake, OnEnable or first LateUpdate - whichever happens first), so it works
/// no matter when or on which object it gets added.
///
/// Added by TutorialManager.ConfigureHandRayVisuals.
/// </summary>
public class HandRayStub : MonoBehaviour
{
    [Tooltip("Visible length of the ray stub, in meters (~5 inches by default).")]
    public float stubLength = 0.12f;

    [Tooltip("Line width at the hand, in meters.")]
    public float width = 0.008f;

    private XRRayInteractor rayInteractor;
    private LineRenderer stubRenderer;

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnEnable()
    {
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (stubRenderer != null) return;

        rayInteractor = GetComponent<XRRayInteractor>();
        if (rayInteractor == null) rayInteractor = GetComponentInParent<XRRayInteractor>(true);
        if (rayInteractor == null) rayInteractor = GetComponentInChildren<XRRayInteractor>(true);

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

        // Same unlit alpha-blended shader combo as the tutorial's pulse ring - proven
        // to render on this project's URP + Quest setup.
        stubRenderer.material = TutorialGestureGizmo.MakeRuntimeMaterial(Color.white, true);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.55f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        stubRenderer.colorGradient = gradient;

        Debug.Log($"[HandRay] Short ray stub built on '{name}' (interactor: {(rayInteractor != null ? rayInteractor.name : "none - using own transform")}).");
    }

    private void LateUpdate()
    {
        EnsureBuilt();

        Transform origin = rayInteractor != null && rayInteractor.rayOriginTransform != null
            ? rayInteractor.rayOriginTransform
            : transform;

        Vector3 start = origin.position;
        stubRenderer.SetPosition(0, start);
        stubRenderer.SetPosition(1, start + origin.forward * stubLength);
    }
}
