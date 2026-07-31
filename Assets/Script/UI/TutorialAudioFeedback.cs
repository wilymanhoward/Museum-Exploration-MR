using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spatial audio cues for the gesture tutorial. Every cue is played at the world
/// position of the object the player interacted with, so the sound reinforces
/// WHERE the gesture landed.
///
/// Assign real AudioClips in the Inspector for production polish; any clip left
/// empty falls back to a small procedurally generated chime (sine tones with a
/// soft envelope), so the tutorial is fully audible with zero audio assets.
/// </summary>
public class TutorialAudioFeedback : MonoBehaviour
{
    [Header("Clips (optional - procedural chimes used when empty)")]
    [Tooltip("Played when a required gesture is performed correctly (click registered / rotation goal reached).")]
    public AudioClip gesturePerformedClip;
    [Tooltip("Played when the player first pinch-grabs the rotation practice object.")]
    public AudioClip grabStartedClip;
    [Tooltip("Short tick played at rotation progress milestones; pitch rises with progress.")]
    public AudioClip progressTickClip;
    [Tooltip("Played when a tutorial step is completed.")]
    public AudioClip stepCompleteClip;
    [Tooltip("Played when the whole tutorial is finished.")]
    public AudioClip tutorialCompleteClip;

    [Header("Mix")]
    [Range(0f, 1f)] public float volume = 0.85f;
    [Tooltip("1 = fully spatialized 3D audio at the interaction point.")]
    [Range(0f, 1f)] public float spatialBlend = 1f;

    private AudioSource source;
    private readonly Dictionary<string, AudioClip> generatedClips = new Dictionary<string, AudioClip>();

    private void Awake()
    {
        source = gameObject.GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 0.4f;
        source.maxDistance = 10f;
    }

    public void PlayGesturePerformed(Vector3 position)
    {
        // Bright ascending two-note confirmation.
        PlayAt(position, gesturePerformedClip, new float[] { 880f, 1318.5f }, 0.09f, 1f);
    }

    public void PlayGrabStarted(Vector3 position)
    {
        // Single soft low note: "the object is in your grip".
        PlayAt(position, grabStartedClip, new float[] { 523.3f }, 0.08f, 0.8f);
    }

    public void PlayProgressTick(Vector3 position, float progress01)
    {
        // Tick pitch rises as the player nears the goal.
        if (progressTickClip != null)
        {
            source.transform.position = position;
            source.pitch = Mathf.Lerp(0.9f, 1.5f, Mathf.Clamp01(progress01));
            source.PlayOneShot(progressTickClip, volume * 0.6f);
            source.pitch = 1f;
            return;
        }
        float freq = Mathf.Lerp(600f, 1200f, Mathf.Clamp01(progress01));
        PlayAt(position, null, new float[] { freq }, 0.05f, 0.55f);
    }

    public void PlayStepComplete(Vector3 position)
    {
        // Major triad arpeggio.
        PlayAt(position, stepCompleteClip, new float[] { 523.3f, 659.3f, 784f }, 0.11f, 1f);
    }

    public void PlayTutorialComplete(Vector3 position)
    {
        // Triad plus octave: the "fanfare".
        PlayAt(position, tutorialCompleteClip, new float[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.14f, 1f);
    }

    private void PlayAt(Vector3 position, AudioClip clip, float[] fallbackFreqs, float noteDuration, float volumeScale)
    {
        if (source == null) return;
        source.transform.position = position;

        if (clip == null)
        {
            clip = GetOrGenerateSequence(fallbackFreqs, noteDuration);
        }
        if (clip != null)
        {
            source.PlayOneShot(clip, volume * volumeScale);
        }
    }

    #region Procedural chime generation

    private AudioClip GetOrGenerateSequence(float[] frequencies, float noteDuration)
    {
        if (frequencies == null || frequencies.Length == 0) return null;

        string key = noteDuration + ":" + string.Join(",", frequencies);
        AudioClip cached;
        if (generatedClips.TryGetValue(key, out cached)) return cached;

        int sampleRate = AudioSettings.outputSampleRate;
        if (sampleRate <= 0) sampleRate = 48000;

        // Notes overlap slightly (ring-out) for a softer, chime-like result.
        float ringOut = noteDuration * 2.2f;
        float totalSeconds = noteDuration * frequencies.Length + ringOut;
        int totalSamples = Mathf.CeilToInt(totalSeconds * sampleRate);
        float[] data = new float[totalSamples];

        for (int n = 0; n < frequencies.Length; n++)
        {
            float freq = frequencies[n];
            int startSample = Mathf.FloorToInt(n * noteDuration * sampleRate);
            int noteSamples = Mathf.CeilToInt((noteDuration + ringOut) * sampleRate);

            for (int i = 0; i < noteSamples && startSample + i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Fast attack, exponential decay envelope.
                float attack = Mathf.Clamp01(t / 0.008f);
                float decay = Mathf.Exp(-t * (3.5f / (noteDuration + ringOut)) * 4f);
                float sample = Mathf.Sin(2f * Mathf.PI * freq * t) * attack * decay * 0.5f;
                // A touch of the second harmonic makes it glassier than a pure sine.
                sample += Mathf.Sin(4f * Mathf.PI * freq * t) * attack * decay * 0.12f;
                data[startSample + i] += sample;
            }
        }

        // Guard against clipping where notes overlap.
        for (int i = 0; i < totalSamples; i++)
        {
            data[i] = Mathf.Clamp(data[i], -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create("TutorialChime_" + key, totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        generatedClips[key] = clip;
        return clip;
    }

    #endregion
}
