using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Floating practice sphere for the "Pinch to Click" tutorial step.
/// The player aims the hand ray at it and pinches; a successful select fires
/// Clicked. Idle it gently pulses to draw the eye; on hover it brightens and
/// grows; on click it pops.
///
/// Built entirely at runtime via Create() - no prefab needed.
/// </summary>
public class PinchClickPracticeTarget : XRSimpleInteractable
{
    public event Action Clicked;

    private const float BaseScale = 0.11f;

    private Material mat;
    private Color baseColor = new Color(0.25f, 0.75f, 1f, 1f);
    private Color hoverColor = new Color(0.55f, 0.95f, 1f, 1f);
    private bool hovered;
    private float popTimer;
    private float idlePhase;

    /// <summary>Creates a ready-to-use practice sphere at the given world position.</summary>
    public static PinchClickPracticeTarget Create(Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "TutorialPinchTarget";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * BaseScale;
        return go.AddComponent<PinchClickPracticeTarget>();
    }

    protected override void Awake()
    {
        base.Awake();
        interactionLayers = ~0; // accept every interactor (hand rays, pokes, controllers)

        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            mat = TutorialGestureGizmo.MakeRuntimeMaterial(baseColor, false);
            rend.material = mat;
        }
        idlePhase = UnityEngine.Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        // Idle breathing pulse so the target reads as "touch me", stronger while hovered.
        float pulse = 1f + Mathf.Sin(Time.time * 3f + idlePhase) * 0.05f;
        float hoverBoost = hovered ? 1.15f : 1f;

        // Short pop after a successful click.
        float pop = 1f;
        if (popTimer > 0f)
        {
            popTimer -= Time.deltaTime;
            pop = 1f + Mathf.Sin(Mathf.Clamp01(1f - popTimer / 0.25f) * Mathf.PI) * 0.35f;
        }

        transform.localScale = Vector3.one * BaseScale * pulse * hoverBoost * pop;

        if (mat != null)
        {
            Color target = hovered ? hoverColor : baseColor;
            mat.color = Color.Lerp(mat.color, target, Time.deltaTime * 8f);
        }
    }

    protected override void OnHoverEntered(HoverEnterEventArgs args)
    {
        base.OnHoverEntered(args);
        hovered = true;
    }

    protected override void OnHoverExited(HoverExitEventArgs args)
    {
        base.OnHoverExited(args);
        hovered = false;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        popTimer = 0.25f;
        Clicked?.Invoke();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (mat != null) Destroy(mat);
    }
}
