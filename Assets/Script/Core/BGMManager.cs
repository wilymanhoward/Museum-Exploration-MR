using System.Collections;
using UnityEngine;

/// <summary>
/// Manages ambient background music (BGM) across the museum application.
/// Plays authentic Terengganu Gamelan ambient music at a gentle low volume.
/// Automatically ducks music volume when voice narrations are playing.
/// </summary>
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("Audio Settings")]
    [Tooltip("Ambient background music clip (auto-loaded from Resources/Audio/BGM_Gamelan_Terengganu if null).")]
    public AudioClip bgmClip;

    [Range(0f, 1f)]
    [Tooltip("Default relaxing ambient volume level.")]
    public float defaultVolume = 0.15f;

    [Range(0f, 1f)]
    [Tooltip("Ducked volume level when voice narration is active.")]
    public float duckedVolume = 0.04f;

    [Tooltip("Duration in seconds for volume fade transitions.")]
    public float fadeDuration = 1.2f;

    private AudioSource audioSource;
    private Coroutine fadeCoroutine;
    private int duckRequests = 0;
    private bool isMuted = false;
    private float currentTargetVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            GameObject bgmObj = new GameObject("BGMManager");
            bgmObj.AddComponent<BGMManager>();
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudio();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f; // 2D Stereo sound
        audioSource.ignoreListenerPause = false;

        // Load saved preferences
        defaultVolume = PlayerPrefs.GetFloat("BGM_Volume", defaultVolume);
        isMuted = PlayerPrefs.GetInt("BGM_Muted", 0) == 1;

        // Auto-load Terengganu Gamelan BGM clip if not assigned
        if (bgmClip == null)
        {
            bgmClip = Resources.Load<AudioClip>("Audio/BGM_Gamelan_Terengganu");
        }

        if (bgmClip != null)
        {
            audioSource.clip = bgmClip;
            currentTargetVolume = isMuted ? 0f : defaultVolume;
            audioSource.volume = 0f;
            audioSource.Play();
            FadeToVolume(currentTargetVolume, 2.5f);
        }
        else
        {
            Debug.LogWarning("[BGMManager] BGM_Gamelan_Terengganu audio clip could not be found in Resources/Audio/!");
        }
    }

    /// <summary>
    /// Smoothly transitions the BGM volume to target value.
    /// </summary>
    public void FadeToVolume(float targetVolume, float duration = -1f)
    {
        if (duration < 0f) duration = fadeDuration;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeVolumeCoroutine(targetVolume, duration));
    }

    private IEnumerator FadeVolumeCoroutine(float target, float duration)
    {
        if (audioSource == null) yield break;

        float start = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            audioSource.volume = Mathf.Lerp(start, target, t);
            yield return null;
        }

        audioSource.volume = target;
        fadeCoroutine = null;
    }

    /// <summary>
    /// Ducks the BGM volume down while voice narration is active, and restores when narration ends.
    /// Supports overlapping duck requests safely.
    /// </summary>
    public void SetDucked(bool duck)
    {
        if (duck)
        {
            duckRequests++;
        }
        else
        {
            duckRequests = Mathf.Max(0, duckRequests - 1);
        }

        if (isMuted) return;

        float target = (duckRequests > 0) ? duckedVolume : defaultVolume;
        currentTargetVolume = target;
        FadeToVolume(target, fadeDuration);
    }

    /// <summary>
    /// Adjusts master BGM volume and saves setting.
    /// </summary>
    public void SetVolume(float volume)
    {
        defaultVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("BGM_Volume", defaultVolume);
        PlayerPrefs.Save();

        if (!isMuted && duckRequests == 0)
        {
            FadeToVolume(defaultVolume, 0.5f);
        }
    }

    /// <summary>
    /// Toggles BGM mute state.
    /// </summary>
    public void ToggleMute()
    {
        isMuted = !isMuted;
        PlayerPrefs.SetInt("BGM_Muted", isMuted ? 1 : 0);
        PlayerPrefs.Save();

        if (isMuted)
        {
            FadeToVolume(0f, 0.5f);
        }
        else
        {
            float target = (duckRequests > 0) ? duckedVolume : defaultVolume;
            FadeToVolume(target, 0.8f);
        }
    }

    public bool IsMuted => isMuted;
    public float CurrentVolume => defaultVolume;
}
