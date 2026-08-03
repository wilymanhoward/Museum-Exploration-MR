using UnityEngine;

/// <summary>
/// Tutorial step 1: "Pinch to Click".
/// Spawns a floating practice button (app-styled, see PinchButtonPracticeTarget) in front
/// of the player. The step completes after the player pinch-presses it the required number
/// of times; between presses the button hops to a nearby position so the player also
/// practices re-aiming the hand ray.
/// </summary>
public class PinchClickStep : TutorialStep
{
    [Header("Success Condition")]
    [Tooltip("How many successful pinch-presses are needed before the step completes.")]
    public int requiredClicks = 2;

    [Tooltip("How far the button hops sideways/vertically after each successful press, in meters.")]
    public float repositionRadius = 0.18f;

    private PinchButtonPracticeTarget target;
    private int clickCount;

    public override TutorialGestureGizmo.GestureMode GizmoMode
    {
        get { return TutorialGestureGizmo.GestureMode.PinchClick; }
    }

    private void Reset()
    {
        stepTitle = "Langkah 1: Cubit untuk Klik";
        instructionText =
            "Halakan sinar tangan anda ke butang, kemudian CUBIT ibu jari dan jari telunjuk untuk menekannya. Tekan sebanyak 2 kali!\n" +
            "<size=80%><i>Point your hand ray at the button, then PINCH your thumb and index finger together to press it. Press it 2 times!</i></size>";
    }

    protected override void OnStepBegin()
    {
        // Reset() only runs in the Editor when the component is added manually;
        // make sure runtime-added instances still get the default copy.
        if (string.IsNullOrEmpty(instructionText)) Reset();

        clickCount = 0;
        SpawnTarget(Vector3.zero);
    }

    protected override void OnStepEnd()
    {
        DestroyTarget();
    }

    private void SpawnTarget(Vector3 localOffset)
    {
        DestroyTarget();

        Vector3 pos;
        Quaternion rot;
        Manager.GetPlacementPose(Manager.practiceDistance, Manager.practiceHeightOffset, out pos, out rot);
        pos += rot * localOffset;

        target = PinchButtonPracticeTarget.Create(pos, "TEKAN / PRESS");
        target.Pressed += OnTargetClicked;
    }

    private void OnTargetClicked()
    {
        clickCount++;

        if (Manager != null && Manager.Audio != null && target != null)
        {
            Manager.Audio.PlayGesturePerformed(target.transform.position);
        }

        if (clickCount >= requiredClicks)
        {
            ReportCompleted();
            return;
        }

        // Hop to a nearby spot (biased sideways so it stays comfortably reachable).
        Vector3 offset = new Vector3(
            Random.Range(-repositionRadius, repositionRadius),
            Random.Range(-repositionRadius * 0.4f, repositionRadius * 0.4f),
            0f);
        if (Mathf.Abs(offset.x) < repositionRadius * 0.4f)
        {
            offset.x = Mathf.Sign(offset.x == 0f ? 1f : offset.x) * repositionRadius * 0.6f;
        }
        SpawnTarget(offset);
    }

    private void DestroyTarget()
    {
        if (target != null)
        {
            target.Pressed -= OnTargetClicked;
            Destroy(target.gameObject);
            target = null;
        }
    }

    public override string GetProgressLabel()
    {
        return requiredClicks > 1 ? $"{clickCount} / {requiredClicks}" : null;
    }

    public override float GetProgressNormalized()
    {
        return requiredClicks > 1 ? (float)clickCount / requiredClicks : -1f;
    }
}
