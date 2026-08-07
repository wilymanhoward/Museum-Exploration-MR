using UnityEngine;

/// <summary>
/// Tutorial step 2: "Pinch &amp; Hold to Rotate".
/// Spawns a real museum artifact (from Resources/Models, same prefabs the exhibit's
/// 3D View uses) the player must pinch-HOLD and drag to spin. Completes once the player
/// has (a) held a pinch continuously for at least minHoldSeconds and (b) rotated the
/// artifact by requiredRotationDegrees in total - a generous target so they get a real
/// feel for the gesture, not just a nudge. Milestone audio ticks rise in pitch as the
/// rotation goal approaches.
/// </summary>
public class PinchDragRotateStep : TutorialStep
{
    [Header("Artifact Model")]
    [Tooltip("Resources path (under Assets/Resources) of the real artifact prefab to spin. Falls back to a placeholder gem if the path doesn't resolve to a model with renderers.")]
    public string artifactResourcePath = "Models/model_artifact_keris";

    [Header("Success Condition")]
    [Tooltip("Total unsigned rotation (degrees) the player must apply to the artifact. 540 = one and a half full turns of accumulated spinning.")]
    public float requiredRotationDegrees = 540f;

    [Tooltip("Minimum single continuous pinch-hold duration, in seconds, so a quick pinch-click cannot pass the step.")]
    public float minHoldSeconds = 0.6f;

    [Tooltip("An audio tick plays every time this many degrees of progress are added.")]
    public float audioTickEveryDegrees = 90f;

    private PinchDragRotatePracticeTarget target;
    private float nextTickAtDegrees;

    public override TutorialGestureGizmo.GestureMode GizmoMode
    {
        get { return TutorialGestureGizmo.GestureMode.PinchHoldDrag; }
    }

    private void Reset()
    {
        stepTitle = "Langkah 2: Cubit & Tahan untuk Putar";
        instructionText =
            "CUBIT dan TAHAN artifak, kemudian gerakkan tangan anda untuk memutarkannya. Terus putar sehingga bar penuh!";
    }

    protected override void OnStepBegin()
    {
        if (string.IsNullOrEmpty(instructionText)) Reset();

        Vector3 pos;
        Quaternion rot;
        Manager.GetPlacementPose(Manager.practiceDistance, Manager.practiceHeightOffset, out pos, out rot);

        GameObject artifactPrefab = string.IsNullOrEmpty(artifactResourcePath)
            ? null
            : Resources.Load<GameObject>(artifactResourcePath);

        target = PinchDragRotatePracticeTarget.Create(pos, artifactPrefab);
        target.GrabStarted += OnGrabStarted;
        target.Rotated += OnRotated;
        nextTickAtDegrees = audioTickEveryDegrees;
    }

    protected override void OnStepEnd()
    {
        if (target != null)
        {
            target.GrabStarted -= OnGrabStarted;
            target.Rotated -= OnRotated;
            Destroy(target.gameObject);
            target = null;
        }
    }

    private void OnGrabStarted()
    {
        if (Manager != null && Manager.Audio != null && target != null)
        {
            Manager.Audio.PlayGrabStarted(target.transform.position);
        }
    }

    private void OnRotated(float degreesThisFrame)
    {
        if (target == null) return;

        // Rising milestone ticks: audible confirmation that the hold-drag is registering.
        while (target.TotalRotationDegrees >= nextTickAtDegrees && nextTickAtDegrees < requiredRotationDegrees)
        {
            if (Manager != null && Manager.Audio != null)
            {
                float progress = Mathf.Clamp01(nextTickAtDegrees / requiredRotationDegrees);
                Manager.Audio.PlayProgressTick(target.transform.position, progress);
            }
            nextTickAtDegrees += audioTickEveryDegrees;
        }
    }

    private void Update()
    {
        // Success is polled here (not only inside the Rotated event) so the order the
        // player satisfies the two conditions in doesn't matter - e.g. spinning the artifact
        // with several quick pinches first, then doing one long hold afterwards.
        if (!IsRunning || IsCompleted || target == null) return;

        if (target.TotalRotationDegrees >= requiredRotationDegrees &&
            target.LongestHoldSeconds >= minHoldSeconds)
        {
            if (Manager != null && Manager.Audio != null)
            {
                Manager.Audio.PlayGesturePerformed(target.transform.position);
            }
            ReportCompleted();
        }
    }

    public override string GetProgressLabel()
    {
        if (target == null) return null;
        float shown = Mathf.Min(target.TotalRotationDegrees, requiredRotationDegrees);
        return $"{Mathf.RoundToInt(shown)}° / {Mathf.RoundToInt(requiredRotationDegrees)}°";
    }

    public override float GetProgressNormalized()
    {
        if (target == null) return 0f;
        return Mathf.Clamp01(target.TotalRotationDegrees / requiredRotationDegrees);
    }
}
