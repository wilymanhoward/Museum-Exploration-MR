using System;
using UnityEngine;

/// <summary>
/// Base class for a single interactive tutorial step.
/// A step owns its practice object(s): it spawns them in OnStepBegin and must
/// clean them up in OnStepEnd. The step decides for itself when the player has
/// successfully performed the required gesture and then calls ReportCompleted();
/// the TutorialManager listens to Completed and advances the sequence.
/// </summary>
public abstract class TutorialStep : MonoBehaviour
{
    [Header("Instruction Copy")]
    [Tooltip("Short title shown at the top of the tutorial panel.")]
    public string stepTitle = "Tutorial";

    [TextArea(3, 8)]
    [Tooltip("Main instruction body text shown on the tutorial panel.")]
    public string instructionText = "";

    [Tooltip("Optional voice-over clip played once when this step begins, narrating the gesture in Bahasa Melayu. Leave empty for no narration.")]
    public AudioClip narrationClip;

    public bool IsRunning { get; private set; }
    public bool IsCompleted { get; private set; }

    /// <summary>Fired once, when the player has performed the step's gesture successfully.</summary>
    public event Action<TutorialStep> Completed;

    protected TutorialManager Manager { get; private set; }

    /// <summary>Which gesture animation the ghost-hand gizmo should loop while this step is active.</summary>
    public abstract TutorialGestureGizmo.GestureMode GizmoMode { get; }

    public void Begin(TutorialManager manager)
    {
        Manager = manager;
        IsRunning = true;
        IsCompleted = false;
        OnStepBegin();
    }

    public void End()
    {
        if (!IsRunning) return;
        IsRunning = false;
        OnStepEnd();
    }

    protected void ReportCompleted()
    {
        if (!IsRunning || IsCompleted) return;
        IsCompleted = true;
        Completed?.Invoke(this);
    }

    /// <summary>
    /// Forces this step to report completion immediately, without the player actually
    /// performing the gesture - wired to the tutorial panel's Skip button so a player who
    /// wants to move on isn't stuck repeating a step. Goes through the exact same
    /// Completed event as a genuine pass, so TutorialManager's celebrate-then-advance flow
    /// (and cleanup) behaves identically either way.
    /// </summary>
    public void SkipStep()
    {
        ReportCompleted();
    }

    /// <summary>Spawn practice objects and subscribe to their events here.</summary>
    protected abstract void OnStepBegin();

    /// <summary>Destroy practice objects and unsubscribe here. Called on completion and on abort.</summary>
    protected abstract void OnStepEnd();

    /// <summary>Optional live progress line under the instructions (e.g. "1 / 2"). Null hides it.</summary>
    public virtual string GetProgressLabel() { return null; }

    /// <summary>0..1 for the progress fill bar. Return a negative value to hide the bar.</summary>
    public virtual float GetProgressNormalized() { return -1f; }
}
