using UnityEngine;

/// <summary>
/// Tutorial step 2: "Pinch &amp; Hold to Drag / Rotate".
/// Spawns a practice gem the player must pinch-HOLD and drag to spin.
/// Completes once the player has (a) held a pinch continuously for at least
/// minHoldSeconds and (b) rotated the gem by requiredRotationDegrees in total.
/// Milestone audio ticks rise in pitch as the rotation goal approaches.
/// </summary>
public class PinchDragRotateStep : TutorialStep
{
    [Header("Success Condition")]
    [Tooltip("Total unsigned rotation (degrees) the player must apply to the gem.")]
    public float requiredRotationDegrees = 120f;

    [Tooltip("Minimum single continuous pinch-hold duration, in seconds, so a quick pinch-click cannot pass the step.")]
    public float minHoldSeconds = 0.6f;

    [Tooltip("An audio tick plays every time this many degrees of progress are added.")]
    public float audioTickEveryDegrees = 30f;

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
            "CUBIT dan TAHAN permata, kemudian gerakkan tangan anda untuk memutarnya. Terus putar sehingga bar penuh!\n" +
            "<size=80%><i>PINCH and HOLD the gem, then move your hand to rotate it. Keep spinning until the bar is full!</i></size>";
    }

    protected override void OnStepBegin()
    {
        if (string.IsNullOrEmpty(instructionText)) Reset();

        Vector3 pos;
        Quaternion rot;
        Manager.GetPlacementPose(Manager.practiceDistance, Manager.practiceHeightOffset, out pos, out rot);

        target = PinchDragRotatePracticeTarget.Create(pos);
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
        // player satisfies the two conditions in doesn't matter - e.g. spinning the gem
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
